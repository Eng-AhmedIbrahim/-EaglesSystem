using EaglesResort.Services;
using EaglesResort.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClosedXML.Excel;
using EaglesResort.Data;
using EaglesResort.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using Microsoft.EntityFrameworkCore;

namespace EaglesResort.ViewModels
{
    public partial class GuestsViewModel : ObservableObject
    {
        [ObservableProperty] private string _searchQuery = string.Empty;
        [ObservableProperty] private ObservableCollection<BookingRow> _activeGuests = new();
        [ObservableProperty] private ObservableCollection<BookingRow> _departedGuests = new();
        [ObservableProperty] private string _statusMessage = string.Empty;

        public GuestsViewModel()
        {
            LoadGuests();
        }

        public void LoadGuests()
        {
            using var db = new AppDbContext();
            var all = db.Bookings.Include(b => b.Invoice).Include(b => b.Chalet).ToList();
            var chaletMap = db.Chalets.ToDictionary(c => c.Id, c => c.Number);
            var invoiceMap = db.Invoices.AsNoTracking().ToDictionary(i => i.Id);
            var lower = SearchQuery?.ToLower().Trim() ?? "";

            // Group count for multi-chalet reservations
            var groupCounts = all
                .Where(b => !string.IsNullOrEmpty(b.GroupBookingCode) || b.InvoiceId.HasValue)
                .GroupBy(b => !string.IsNullOrEmpty(b.GroupBookingCode) ? b.GroupBookingCode : b.InvoiceId!.Value.ToString())
                .ToDictionary(g => g.Key, g => g.Count());

            int getGroupCount(Booking b)
            {
                if (!string.IsNullOrEmpty(b.GroupBookingCode) && groupCounts.TryGetValue(b.GroupBookingCode, out var c1))
                    return c1;
                if (b.InvoiceId.HasValue && groupCounts.TryGetValue(b.InvoiceId.Value.ToString(), out var c2))
                    return c2;
                return 1;
            }

            // التأمين بيتحسب مرة واحدة على الفاتورة، وبيتوزّع بالتساوي على الشاليهات.
            // مثال: عميل دافع 2000 تأمين على شاليهين => 1000 لكل شاليه (مش 2000 لكل واحد).
            decimal getInsuranceShare(Booking b)
            {
                if (b.InvoiceId.HasValue && invoiceMap.TryGetValue(b.InvoiceId.Value, out var invoice))
                {
                    int count = Math.Max(1, getGroupCount(b));
                    return count == 1 ? invoice.Insurance : Math.Round(invoice.Insurance / count, 2);
                }

                return b.Invoice?.Insurance ?? 0m;
            }

            decimal getInsuranceTotal(Booking b)
            {
                if (b.InvoiceId.HasValue && invoiceMap.TryGetValue(b.InvoiceId.Value, out var invoice))
                    return invoice.Insurance;
                return b.Invoice?.Insurance ?? 0m;
            }

            bool matches(Booking b) =>
                string.IsNullOrEmpty(lower) ||
                b.GuestName.ToLower().Contains(lower) ||
                (!string.IsNullOrEmpty(b.GuestRank) && b.GuestRank.ToLower().Contains(lower)) ||
                b.PhoneNumber.Contains(lower) ||
                (!string.IsNullOrEmpty(b.SecondaryPhoneNumber) && b.SecondaryPhoneNumber.Contains(lower)) ||
                b.ChaletId.ToString().Contains(lower) ||
                (chaletMap.TryGetValue(b.ChaletId, out var num) && num.ToLower().Contains(lower));

            var active = all
                .Where(b => b.Status == BookingStatus.Active || b.Status == BookingStatus.Upcoming)
                .Where(matches)
                .OrderBy(b => b.CheckInDate)
                .Select((b, i) => new BookingRow(
                    b,
                    i + 1,
                    chaletMap.TryGetValue(b.ChaletId, out var n) ? n : b.ChaletId.ToString(),
                    getGroupCount(b),
                    getInsuranceShare(b),
                    getInsuranceTotal(b)))
                .ToList();

            var departed = all
                .Where(b => b.Status == BookingStatus.CheckedOut || b.Status == BookingStatus.Cancelled)
                .Where(matches)
                .OrderByDescending(b => b.CheckOutDate)
                .Select((b, i) => new BookingRow(
                    b,
                    i + 1,
                    chaletMap.TryGetValue(b.ChaletId, out var n) ? n : b.ChaletId.ToString(),
                    getGroupCount(b),
                    getInsuranceShare(b),
                    getInsuranceTotal(b)))
                .ToList();

            ActiveGuests = new ObservableCollection<BookingRow>(active);
            DepartedGuests = new ObservableCollection<BookingRow>(departed);
        }

        partial void OnSearchQueryChanged(string value) => LoadGuests();

        [RelayCommand]
        private void Checkout(BookingRow? row)
        {
            if (row == null) return;
            var result = MessageBox.Show(
                $"هل تريد تسجيل مغادرة النزيل: {row.Booking.GuestName}؟",
                "تأكيد المغادرة",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No,
                MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

            if (result != MessageBoxResult.Yes) return;

            using var db = new AppDbContext();
            var booking = db.Bookings.Find(row.Booking.Id);
            if (booking == null) return;

            booking.Status = BookingStatus.CheckedOut;
            booking.CheckOutDate = DateTime.Today;

            // Update chalet status
            var chalet = db.Chalets.Find(booking.ChaletId);
            if (chalet != null) chalet.Status = ChaletStatus.Available;

            db.SaveChanges();
            LoadGuests();
            StatusMessage = $"✅ تم تسجيل مغادرة {row.Booking.GuestName} بنجاح";

            var invoiceId = booking.InvoiceId
                ?? db.Invoices.FirstOrDefault(i => i.BookingId == booking.Id)?.Id;
            if (invoiceId.HasValue)
                InvoiceService.LogAction(invoiceId.Value, booking.Id, "CheckedOut", "تسجيل مغادرة",
                    $"تم تسجيل مغادرة النزيل {booking.GuestName} من الشاليه {row.ChaletNumber}");
        }

        public Action<Booking>? ExtendRequested { get; set; }

        [RelayCommand]
        private void ExtendStay(BookingRow? row)
        {
            if (row == null) return;
            ExtendRequested?.Invoke(row.Booking);
        }

        [RelayCommand]
        private void ViewPaymentDetails(BookingRow? row)
        {
            if (row == null) return;
            var invoice = LoadInvoice(row);
            if (invoice == null)
            {
                ShowInfo("لا توجد تفاصيل دفع أو فاتورة مسجلة لهذا الحجز.");
                return;
            }

            var dialog = new PaymentDetailsDialog(invoice) { Owner = GetOwner() };
            dialog.ShowDialog();
            LoadGuests();
        }

        /// <summary>تعديل الفاتورة (للأدمن فقط) ثم طباعتها تلقائياً.</summary>
        [RelayCommand]
        private void EditInvoice(BookingRow? row)
        {
            if (row == null) return;

            if (!SessionService.Instance.IsAdmin)
            {
                ShowInfo("تعديل الفاتورة متاح لحساب مدير النظام فقط.");
                return;
            }

            var invoice = LoadInvoice(row);
            if (invoice == null)
            {
                ShowInfo("لا توجد فاتورة مسجلة لهذا الحجز.");
                return;
            }

            if (invoice.IsCancelled)
            {
                ShowInfo("لا يمكن تعديل فاتورة ملغية.");
                return;
            }

            var editor = new InvoiceEditWindow(invoice) { Owner = GetOwner() };
            if (editor.ShowDialog() != true) return;

            var result = InvoiceService.UpdateInvoice(editor.EditedInvoice, editor.EditorName);
            StatusMessage = result.Message;

            if (!result.Success)
            {
                ShowInfo(result.Message);
                return;
            }

            LoadGuests();

            // طباعة الفاتورة بعد التعديل (مع بانر "فاتورة معدلة" أعلى الفاتورة)
            var refreshed = InvoiceService.GetById(invoice.Id);
            if (refreshed != null) TryPrint(refreshed);
        }

        /// <summary>إلغاء الفاتورة وكل حجوزاتها ثم طباعتها تلقائياً وهي مختومة "ملغية".</summary>
        [RelayCommand]
        private void CancelInvoice(BookingRow? row)
        {
            if (row == null) return;

            if (!SessionService.Instance.IsAdmin)
            {
                ShowInfo("إلغاء الفاتورة متاح لحساب مدير النظام فقط.");
                return;
            }

            var invoice = LoadInvoice(row);
            if (invoice == null)
            {
                ShowInfo("لا توجد فاتورة مسجلة لهذا الحجز.");
                return;
            }

            if (invoice.IsCancelled)
            {
                ShowInfo("هذه الفاتورة ملغية بالفعل.");
                return;
            }

            var dialog = new CancelInvoiceDialog(invoice.InvoiceNumberDisplay, invoice.Booking?.GuestName ?? row.Booking.GuestName)
            {
                Owner = GetOwner()
            };
            if (dialog.ShowDialog() != true) return;

            var result = InvoiceService.CancelInvoice(invoice.Id, dialog.Reason, dialog.CancelledBy);
            StatusMessage = result.Message;

            if (!result.Success)
            {
                ShowInfo(result.Message);
                return;
            }

            LoadGuests();

            // طباعة نسخة "ملغية" للتوثيق
            var refreshed = InvoiceService.GetById(invoice.Id);
            if (refreshed != null) TryPrint(refreshed);
        }

        /// <summary>إعادة طباعة الفاتورة.</summary>
        [RelayCommand]
        private void PrintInvoice(BookingRow? row)
        {
            if (row == null) return;

            var invoice = LoadInvoice(row);
            if (invoice == null)
            {
                ShowInfo("لا توجد فاتورة مسجلة لهذا الحجز.");
                return;
            }

            PrintService.PrintInvoice(invoice);
        }

        /// <summary>طباعة آمنة: تعرض رسالة واضحة بدل ما يقع التطبيق لو الطباعة فشلت.</summary>
        private void TryPrint(Invoice invoice)
        {
            try
            {
                PrintService.PrintInvoice(invoice);
            }
            catch (Exception ex)
            {
                StatusMessage = $"تعذر طباعة الفاتورة: {ex.Message}";
                ShowInfo(StatusMessage);
            }
        }

        private static Invoice? LoadInvoice(BookingRow row) => InvoiceService.GetByBookingId(row.Booking.Id);

        private static Window? GetOwner() => System.Windows.Application.Current?.MainWindow;

        private static void ShowInfo(string message) =>
            MessageBox.Show(message, "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);

        [RelayCommand]
        public void SaveRow(BookingRow? row)
        {
            if (row == null) return;
            try
            {
                using var db = new AppDbContext();
                var booking = db.Bookings.Include(b => b.Invoice).FirstOrDefault(b => b.Id == row.Booking.Id);
                if (booking == null) return;

                if (row.Booking.CheckOutDate <= row.Booking.CheckInDate)
                {
                    MessageBox.Show("تاريخ المغادرة يجب أن يكون بعد تاريخ الوصول!", "خطأ في التواريخ", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                    LoadGuests();
                    return;
                }

                var conflict = BookingService.CheckConflict(booking.ChaletId, row.Booking.CheckInDate, row.Booking.CheckOutDate, booking.Id, null);
                if (conflict.HasConflict)
                {
                    MessageBox.Show(conflict.ErrorMessage + "\n\nيرجى اختيار تواريخ أخرى لتجنب التضارب.", "⚠️ تعارض في مواعيد الحجز", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                    LoadGuests();
                    return;
                }

                booking.GuestName = row.Booking.GuestName;
                booking.PhoneNumber = row.Booking.PhoneNumber;
                booking.SecondaryPhoneNumber = row.Booking.SecondaryPhoneNumber;
                booking.GuestRank = row.Booking.GuestRank;
                booking.AdultCount = row.Booking.AdultCount;
                booking.ChildCount = row.Booking.ChildCount;
                booking.CheckInDate = row.Booking.CheckInDate;
                booking.CheckOutDate = row.Booking.CheckOutDate;
                booking.Deposit = row.Booking.Deposit;
                booking.Notes = row.Booking.Notes;
                booking.InsurancePaid = row.Booking.InsurancePaid;

                if (booking.Invoice != null && row.Booking.Invoice != null)
                {
                    booking.Invoice.Insurance = row.Booking.Invoice.Insurance;
                }

                db.SaveChanges();
                StatusMessage = "✅ تم حفظ التعديل";
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ خطأ: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ToggleInsurancePaid(BookingRow? row)
        {
            if (row == null) return;
            try
            {
                using var db = new AppDbContext();
                var booking = db.Bookings.Find(row.Booking.Id);
                if (booking != null)
                {
                    booking.InsurancePaid = row.Booking.InsurancePaid;
                    db.SaveChanges();
                    StatusMessage = row.Booking.InsurancePaid ? "✅ تم تأكيد دفع التأمين" : "ℹ️ تم إلغاء دفع التأمين";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ خطأ: {ex.Message}";
            }
        }

        [RelayCommand]
        private void SaveNotes(BookingRow? row) => SaveRow(row);

        [RelayCommand]
        private void ExportToExcel()
        {
            try
            {
                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("سجل العملاء");
                ws.RightToLeft = true;

                ws.Cell(1, 1).Value = "#";
                ws.Cell(1, 2).Value = "اسم النزيل";
                ws.Cell(1, 3).Value = "الرتبة";
                ws.Cell(1, 4).Value = "رقم الهاتف";
                ws.Cell(1, 5).Value = "الشاليه";
                ws.Cell(1, 6).Value = "تاريخ الوصول";
                ws.Cell(1, 7).Value = "تاريخ المغادرة";
                ws.Cell(1, 8).Value = "الأيام";
                ws.Cell(1, 9).Value = "ملاحظات";

                var headerRange = ws.Range(1, 1, 1, 9);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1A2533");
                headerRange.Style.Font.FontColor = XLColor.White;

                int row = 2;
                foreach (var g in ActiveGuests)
                {
                    ws.Cell(row, 1).Value = g.RowNumber;
                    ws.Cell(row, 2).Value = g.Booking.GuestName;
                    ws.Cell(row, 3).Value = g.Booking.GuestRank;
                    ws.Cell(row, 4).Value = g.Booking.PhoneNumber;
                    ws.Cell(row, 5).Value = g.Booking.ChaletId;
                    ws.Cell(row, 6).Value = g.Booking.CheckInDate.ToString("yyyy/MM/dd");
                    ws.Cell(row, 7).Value = g.Booking.CheckOutDate.ToString("yyyy/MM/dd");
                    ws.Cell(row, 8).Value = g.Booking.NumberOfDays;
                    ws.Cell(row, 9).Value = g.Booking.Notes;
                    row++;
                }

                ws.Columns().AdjustToContents();

                var path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"Guests_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

                workbook.SaveAs(path);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في التصدير: {ex.Message}", "خطأ");
            }
        }
    }

    /// <summary>Wraps a Booking with a display row number</summary>
    public class BookingRow : ObservableObject
    {
        public int RowNumber { get; }
        public Booking Booking { get; }
        public string ChaletNumber { get; }
        public bool IsMultiChalet { get; }
        public string MultiBadgeText { get; }
        public bool HasSecondaryPhone => !string.IsNullOrWhiteSpace(Booking.SecondaryPhoneNumber);

        /// <summary>نصيب هذا الشاليه من تأمين الفاتورة (بيتوزّع بالتساوي على الشاليهات).</summary>
        public decimal InsuranceShare { get; }

        /// <summary>إجمالي التأمين المسجل على الفاتورة كلها.</summary>
        public decimal InsuranceTotal { get; }

        public string InsuranceTooltip => IsMultiChalet
            ? $"إجمالي تأمين الفاتورة {InsuranceTotal:N0} ج.م مقسوم على {MultiChaletCount} شاليهات — نصيب هذا الشاليه {InsuranceShare:N0} ج.م"
            : $"قيمة التأمين {InsuranceShare:N0} ج.م";

        private int MultiChaletCount { get; }

        public string DisplayName => !string.IsNullOrWhiteSpace(Booking.GuestRank)
            ? $"{Booking.GuestRank} / {Booking.GuestName}"
            : Booking.GuestName;

        public BookingRow(Booking booking, int rowNumber, string chaletNumber = "", int groupCount = 1,
                          decimal insuranceShare = 0m, decimal insuranceTotal = 0m)
        {
            Booking = booking;
            RowNumber = rowNumber;
            ChaletNumber = chaletNumber;
            IsMultiChalet = groupCount > 1;
            MultiChaletCount = Math.Max(1, groupCount);
            MultiBadgeText = IsMultiChalet ? $"ضمن {groupCount} شاليهات" : "";
            InsuranceShare = insuranceShare;
            InsuranceTotal = insuranceTotal;
        }
    }
}

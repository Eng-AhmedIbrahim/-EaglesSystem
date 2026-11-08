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
                    getGroupCount(b)))
                .ToList();

            var departed = all
                .Where(b => b.Status == BookingStatus.CheckedOut || b.Status == BookingStatus.Cancelled)
                .Where(matches)
                .OrderByDescending(b => b.CheckOutDate)
                .Select((b, i) => new BookingRow(
                    b,
                    i + 1,
                    chaletMap.TryGetValue(b.ChaletId, out var n) ? n : b.ChaletId.ToString(),
                    getGroupCount(b)))
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
            using var db = new AppDbContext();
            var invoice = db.Invoices
                .Include(i => i.Bookings)
                    .ThenInclude(b => b.Chalet)
                .Include(i => i.Booking)
                    .ThenInclude(b => b!.Chalet)
                .FirstOrDefault(i => (row.Booking.InvoiceId.HasValue && i.Id == row.Booking.InvoiceId.Value) || i.BookingId == row.Booking.Id);

            if (invoice != null)
            {
                var dialog = new Views.Dialogs.PaymentDetailsDialog(invoice);
                dialog.ShowDialog();
            }
            else
            {
                System.Windows.MessageBox.Show("لا توجد تفاصيل دفع أو فاتورة مسجلة لهذا الحجز.", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        public void SaveRow(BookingRow? row)
        {
            if (row == null) return;
            try
            {
                using var db = new AppDbContext();
                var booking = db.Bookings.Include(b => b.Invoice).FirstOrDefault(b => b.Id == row.Booking.Id);
                if (booking == null) return;

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
        public string DisplayName => !string.IsNullOrWhiteSpace(Booking.GuestRank)
            ? $"{Booking.GuestRank} / {Booking.GuestName}"
            : Booking.GuestName;

        public BookingRow(Booking booking, int rowNumber, string chaletNumber = "", int groupCount = 1)
        {
            Booking = booking;
            RowNumber = rowNumber;
            ChaletNumber = chaletNumber;
            IsMultiChalet = groupCount > 1;
            MultiBadgeText = IsMultiChalet ? $"ضمن {groupCount} شاليهات" : "";
        }
    }
}

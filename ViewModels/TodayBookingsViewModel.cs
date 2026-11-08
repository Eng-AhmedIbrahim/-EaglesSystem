using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EaglesResort.Data;
using EaglesResort.Models;
using EaglesResort.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace EaglesResort.ViewModels
{
    public partial class TodayBookingsViewModel : ObservableObject
    {
        private List<Reservation> _allReservations = new();

        [ObservableProperty] private ObservableCollection<Reservation> _todayBookings = new();
        [ObservableProperty] private ObservableCollection<Chalet> _availableChalets = new();
        [ObservableProperty] private Reservation? _selectedReservation;
        [ObservableProperty] private Chalet? _selectedNewChalet;

        // Filter Tabs: 0 = وصول اليوم فقط, 1 = الحجوزات القادمة, 2 = تم التسكين, 3 = الكل
        [ObservableProperty] private int _activeFilterTab = 0;
        [ObservableProperty] private int _todayArrivalsCount;
        [ObservableProperty] private int _upcomingCount;
        [ObservableProperty] private int _checkedInCount;
        [ObservableProperty] private int _allActiveCount;

        // Change Chalet Dialog
        [ObservableProperty] private bool _isChangeChaletOpen;

        // Edit Reservation Dialog
        [ObservableProperty] private bool _isEditOpen;
        [ObservableProperty] private string _editGuestName = string.Empty;
        [ObservableProperty] private string _editPhoneNumber = string.Empty;
        [ObservableProperty] private string _editSecondaryPhoneNumber = string.Empty;
        [ObservableProperty] private DateTime _editCheckInDate = DateTime.Today;
        [ObservableProperty] private DateTime _editCheckOutDate = DateTime.Today.AddDays(1);
        [ObservableProperty] private string _editDepositText = "0";
        [ObservableProperty] private decimal _editDeposit;
        [ObservableProperty] private string _editNotes = string.Empty;

        [ObservableProperty] private string _statusMessage = string.Empty;

        public event Action? DataChanged;

        public TodayBookingsViewModel()
        {
            LoadData();
        }

        partial void OnEditDepositTextChanged(string value)
        {
            if (decimal.TryParse(value, out var d))
                EditDeposit = d;
            else if (string.IsNullOrWhiteSpace(value))
                EditDeposit = 0;
        }

        public void LoadData()
        {
            try
            {
                using var db = new AppDbContext();
                var today = DateTime.Today;

                // Load all chalets
                var chalets = db.Chalets.OrderBy(c => c.BuildingArea).ThenBy(c => c.Number).ToList();
                AvailableChalets = new ObservableCollection<Chalet>(chalets);

                // Load all reservations (Upcoming + CheckedIn) with Chalet
                _allReservations = db.Reservations
                    .Include(r => r.Chalet)
                    .Where(r => r.Status != ReservationStatus.Cancelled)
                    .OrderBy(r => r.CheckInDate)
                    .ThenBy(r => r.Chalet != null ? r.Chalet.Number : "")
                    .ToList();

                // Calculate counts
                TodayArrivalsCount = _allReservations.Count(r => r.CheckInDate.Date == today && r.Status == ReservationStatus.Upcoming);
                UpcomingCount = _allReservations.Count(r => r.CheckInDate.Date > today && r.Status == ReservationStatus.Upcoming);
                CheckedInCount = _allReservations.Count(r => r.Status == ReservationStatus.CheckedIn);
                AllActiveCount = _allReservations.Count;

                ApplyFilter();
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ في تحميل الحجوزات: {ex.Message}";
            }
        }

        [RelayCommand]
        public void SetFilterTab(string? tabStr)
        {
            if (int.TryParse(tabStr, out var tab))
            {
                ActiveFilterTab = tab;
                ApplyFilter();
            }
        }

        private void ApplyFilter()
        {
            var today = DateTime.Today;
            IEnumerable<Reservation> filtered = _allReservations;

            switch (ActiveFilterTab)
            {
                case 0: // وصول اليوم فقط (الافتراضي)
                    filtered = _allReservations.Where(r => r.CheckInDate.Date == today && r.Status == ReservationStatus.Upcoming);
                    break;
                case 1: // الحجوزات القادمة (مستقبلية)
                    filtered = _allReservations.Where(r => r.CheckInDate.Date > today && r.Status == ReservationStatus.Upcoming);
                    break;
                case 2: // تم التسكين
                    filtered = _allReservations.Where(r => r.Status == ReservationStatus.CheckedIn);
                    break;
                case 3: // الكل
                default:
                    filtered = _allReservations;
                    break;
            }

            TodayBookings = new ObservableCollection<Reservation>(filtered);
        }

        // ===== 1. تسكين وإصدار فاتورة (Check-In & Invoice) =====
        [RelayCommand]
        private void CheckInAndInvoice(Reservation? reservation)
        {
            if (reservation == null) return;

            var confirm = MessageBox.Show(
                $"هل تريد تسكين النزيل: {reservation.GuestName}\nفي الشاليه ({reservation.Chalet?.Number}) الآن وإصدار فاتورة له؟",
                "تأكيد التسكين وإصدار الفاتورة",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.Yes,
                MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                // Navigate to BookingPage with this reservation
                MainWindow.Instance?.NavigateToBookingForReservation(reservation);
                DataChanged?.Invoke();

                // Close this window
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is Views.Windows.TodayBookingsWindow tbw)
                    {
                        tbw.Close();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ أثناء التسكين: {ex.Message}";
            }
        }

        // ===== 2. تغيير الشاليه =====
        [RelayCommand]
        private void OpenChangeChalet(Reservation? reservation)
        {
            if (reservation == null) return;
            SelectedReservation = reservation;
            SelectedNewChalet = AvailableChalets.FirstOrDefault(c => c.Id == reservation.ChaletId);
            IsChangeChaletOpen = true;
        }

        [RelayCommand]
        private void ConfirmChangeChalet()
        {
            if (SelectedReservation == null || SelectedNewChalet == null) return;

            if (SelectedNewChalet.Id == SelectedReservation.ChaletId)
            {
                IsChangeChaletOpen = false;
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var r = db.Reservations.Find(SelectedReservation.Id);
                if (r != null)
                {
                    r.ChaletId = SelectedNewChalet.Id;
                    // Recalculate total with new chalet rate
                    var chalet = db.Chalets.Find(SelectedNewChalet.Id);
                    if (chalet != null)
                    {
                        // Keep existing daily rate or fetch from chalet
                        r.ChaletTotal = r.NumberOfDays * r.DailyRate;
                    }
                    db.SaveChanges();
                    StatusMessage = $"✅ تم نقل الحجز إلى الشاليه {SelectedNewChalet.Number} بنجاح";
                }

                IsChangeChaletOpen = false;
                LoadData();
                DataChanged?.Invoke();
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ أثناء تغيير الشاليه: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CancelChangeChalet()
        {
            IsChangeChaletOpen = false;
        }

        // ===== 3. تعديل الحجز =====
        [RelayCommand]
        private void OpenEditBooking(Reservation? reservation)
        {
            if (reservation == null) return;
            SelectedReservation = reservation;
            EditGuestName = reservation.GuestName;
            EditPhoneNumber = reservation.PhoneNumber;
            EditSecondaryPhoneNumber = reservation.SecondaryPhoneNumber;
            EditCheckInDate = reservation.CheckInDate;
            EditCheckOutDate = reservation.CheckOutDate;
            EditDepositText = reservation.Deposit.ToString("0");
            EditDeposit = reservation.Deposit;
            EditNotes = reservation.Notes;
            IsEditOpen = true;
        }

        [RelayCommand]
        private void SaveEditBooking()
        {
            if (SelectedReservation == null) return;

            if (string.IsNullOrWhiteSpace(EditGuestName))
            {
                MessageBox.Show("اسم النزيل مطلوب!", "بيانات ناقصة", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            if (EditCheckOutDate <= EditCheckInDate)
            {
                MessageBox.Show("تاريخ المغادرة يجب أن يكون بعد تاريخ الوصول!", "خطأ في التواريخ", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var r = db.Reservations.Find(SelectedReservation.Id);
                if (r != null)
                {
                    r.GuestName = EditGuestName.Trim();
                    r.PhoneNumber = EditPhoneNumber.Trim();
                    r.SecondaryPhoneNumber = EditSecondaryPhoneNumber.Trim();
                    r.CheckInDate = EditCheckInDate;
                    r.CheckOutDate = EditCheckOutDate;
                    r.NumberOfDays = (int)Math.Max(1, (EditCheckOutDate - EditCheckInDate).TotalDays);
                    r.Deposit = EditDeposit;
                    r.ChaletTotal = r.NumberOfDays * r.DailyRate;
                    r.Notes = EditNotes.Trim();
                    db.SaveChanges();
                    StatusMessage = "✅ تم حفظ التعديلات بنجاح";
                }

                IsEditOpen = false;
                LoadData();
                DataChanged?.Invoke();
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ أثناء حفظ التعديل: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CancelEditBooking()
        {
            IsEditOpen = false;
        }

        // ===== 4. إلغاء الحجز =====
        [RelayCommand]
        private void CancelReservation(Reservation? reservation)
        {
            if (reservation == null) return;

            var confirm = MessageBox.Show(
                $"هل تريد إلغاء حجز النزيل: {reservation.GuestName}؟\nلا يمكن التراجع عن هذا الإجراء.",
                "تأكيد الإلغاء",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No,
                MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                using var db = new AppDbContext();
                var r = db.Reservations.Find(reservation.Id);
                if (r != null)
                {
                    r.Status = ReservationStatus.Cancelled;
                    db.SaveChanges();
                    StatusMessage = $"🚫 تم إلغاء حجز النزيل {reservation.GuestName}";
                }

                LoadData();
                DataChanged?.Invoke();
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ أثناء الإلغاء: {ex.Message}";
            }
        }

        // ===== 5. صورة إيصال الديبوزت =====
        [RelayCommand]
        private void ViewDepositReceipt(Reservation? reservation)
        {
            if (reservation == null || string.IsNullOrEmpty(reservation.DepositReceiptImagePath) || !File.Exists(reservation.DepositReceiptImagePath))
            {
                MessageBox.Show("لا توجد صورة إيصال ديبوزت مرفقة لهذا الحجز.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(reservation.DepositReceiptImagePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في فتح الصورة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void UploadDepositReceipt(Reservation? reservation)
        {
            if (reservation == null) return;

            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "اختر صورة إيصال التحويل / العربون",
                Filter = "ملفات الصور (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp|جميع الملفات (*.*)|*.*"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    var dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "EaglesResort", "DepositReceipts");
                    Directory.CreateDirectory(dir);

                    var ext = Path.GetExtension(ofd.FileName);
                    var destName = $"receipt_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}{ext}";
                    var destPath = Path.Combine(dir, destName);

                    File.Copy(ofd.FileName, destPath, true);

                    using var db = new AppDbContext();
                    var r = db.Reservations.Find(reservation.Id);
                    if (r != null)
                    {
                        r.DepositReceiptImagePath = destPath;
                        db.SaveChanges();
                    }

                    StatusMessage = "✅ تم حفظ صورة إيصال الديبوزت بنجاح";
                    LoadData();
                }
                catch (Exception ex)
                {
                    StatusMessage = $"خطأ أثناء رفع الصورة: {ex.Message}";
                }
            }
        }
    }
}

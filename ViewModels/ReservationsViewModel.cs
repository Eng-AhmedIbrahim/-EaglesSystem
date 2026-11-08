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
    public partial class ReservationsViewModel : ObservableObject
    {
        private List<Reservation> _allReservations = new();

        [ObservableProperty] private ObservableCollection<Reservation> _reservations = new();
        [ObservableProperty] private ObservableCollection<Chalet> _availableChalets = new();
        [ObservableProperty] private string _searchQuery = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;

        // Filter: 0 = الحجوزات القادمة (الافتراضي), 1 = وصول اليوم, 2 = تم تسكينهم, 3 = الكل
        [ObservableProperty] private int _activeFilter = 0;
        [ObservableProperty] private int _upcomingCount;
        [ObservableProperty] private int _todayCount;
        [ObservableProperty] private int _checkedInCount;
                [ObservableProperty] private int _totalCount;

        // Date Filter
        [ObservableProperty] private DateTime? _filterDate;

        // Real-Time Smart Conflict & Suggestions
        [ObservableProperty] private string _conflictNotice = string.Empty;
        [ObservableProperty] private ObservableCollection<Chalet> _suggestedChalets = new();

        // ===== NEW RESERVATION MODAL =====
        [ObservableProperty] private bool _isNewReservationOpen;
        [ObservableProperty] private string _newGuestName = string.Empty;
        [ObservableProperty] private string _newPhoneNumber = string.Empty;
        [ObservableProperty] private string _newSecondaryPhoneNumber = string.Empty;
        [ObservableProperty] private Chalet? _newSelectedChalet;
        [ObservableProperty] private DateTime _newCheckInDate = DateTime.Today.AddDays(1);
        [ObservableProperty] private DateTime _newCheckOutDate = DateTime.Today.AddDays(2);
        [ObservableProperty] private int _newNumberOfDays = 1;
        [ObservableProperty] private decimal _newDailyRate;
        [ObservableProperty] private decimal _newChaletTotal;
        [ObservableProperty] private decimal _newDeposit;
        [ObservableProperty] private string _newDepositText = "0";
        [ObservableProperty] private DateTime? _newDepositDate = DateTime.Today;
        [ObservableProperty] private string _newDepositReceiptImagePath = string.Empty;
        [ObservableProperty] private string _newDepositReceiptFileName = string.Empty;
        [ObservableProperty] private bool _newHasDepositReceipt;
        [ObservableProperty] private string _newNotes = string.Empty;

        // ===== EDIT RESERVATION MODAL =====
        [ObservableProperty] private bool _isEditReservationOpen;
        [ObservableProperty] private Reservation? _selectedReservation;
        [ObservableProperty] private string _editGuestName = string.Empty;
        [ObservableProperty] private string _editPhoneNumber = string.Empty;
        [ObservableProperty] private string _editSecondaryPhoneNumber = string.Empty;
        [ObservableProperty] private DateTime _editCheckInDate = DateTime.Today;
        [ObservableProperty] private DateTime _editCheckOutDate = DateTime.Today.AddDays(1);
        [ObservableProperty] private decimal _editDeposit;
        [ObservableProperty] private string _editDepositText = "0";
        [ObservableProperty] private string _editNotes = string.Empty;

        // ===== CHANGE CHALET MODAL =====
        [ObservableProperty] private bool _isChangeChaletOpen;
        [ObservableProperty] private Chalet? _selectedNewChalet;

        public ReservationsViewModel()
        {
            LoadData();
        }

        partial void OnNewDepositTextChanged(string value)
        {
            if (decimal.TryParse(value, out var d))
            {
                NewDeposit = d;
            }
            else if (string.IsNullOrWhiteSpace(value))
            {
                NewDeposit = 0;
            }
        }

        partial void OnEditDepositTextChanged(string value)
        {
            if (decimal.TryParse(value, out var d))
            {
                EditDeposit = d;
            }
            else if (string.IsNullOrWhiteSpace(value))
            {
                EditDeposit = 0;
            }
        }

        public void LoadData()
        {
            try
            {
                using var db = new AppDbContext();
                var chalets = db.Chalets.OrderBy(c => c.BuildingArea).ThenBy(c => c.Number).ToList();
                AvailableChalets = new ObservableCollection<Chalet>(chalets);

                _allReservations = db.Reservations
                    .Include(r => r.Chalet)
                    .Include(r => r.Invoice)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();

                TotalCount = _allReservations.Count;
                UpcomingCount = _allReservations.Count(r => r.Status == ReservationStatus.Upcoming);
                TodayCount = _allReservations.Count(r => r.Status == ReservationStatus.Upcoming && r.CheckInDate.Date == DateTime.Today);
                CheckedInCount = _allReservations.Count(r => r.Status == ReservationStatus.CheckedIn);

                ApplyFilter();
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ في تحميل الحجوزات المسبقة: {ex.Message}";
            }
        }

                partial void OnSearchQueryChanged(string value) => ApplyFilter();
        partial void OnFilterDateChanged(DateTime? value) => ApplyFilter();

        [RelayCommand]
        public void ClearDateFilter()
        {
            FilterDate = null;
        }

        [RelayCommand]
        public void SelectSuggestedChalet(Chalet? chalet)
        {
            if (chalet == null) return;
            NewSelectedChalet = chalet;
        }

        [RelayCommand]
        public void SetFilter(object? filterParam)
        {
            if (filterParam != null && int.TryParse(filterParam.ToString(), out var f))
            {
                ActiveFilter = f;
                ApplyFilter();
            }
        }

        private void ApplyFilter()
        {
            IEnumerable<Reservation> query = _allReservations;

            switch (ActiveFilter)
            {
                case 0: // الحجوزات القادمة (حجز مسبق قائم لم يتم تسكينه بعد)
                    query = query.Where(r => r.Status == ReservationStatus.Upcoming);
                    break;
                case 1: // وصول اليوم (حجوزات مسبقة منتظر وصولها اليوم)
                    query = query.Where(r => r.Status == ReservationStatus.Upcoming && r.CheckInDate.Date == DateTime.Today);
                    break;
                case 2: // تم تسكينهم (حجوزات مسبقة تحولت إلى فواتير وسكنوا بالفعل)
                    query = query.Where(r => r.Status == ReservationStatus.CheckedIn);
                    break;
                case 3: // الكل (أرشيف كامل يشمل الملغي والمسكن)
                    break;
            }

            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.Trim().ToLower();
                query = query.Where(r =>
                    (r.GuestName != null && r.GuestName.ToLower().Contains(q)) ||
                    (r.PhoneNumber != null && r.PhoneNumber.Contains(q)) ||
                    (r.SecondaryPhoneNumber != null && r.SecondaryPhoneNumber.Contains(q)) ||
                    (r.Chalet != null && r.Chalet.Number.ToLower().Contains(q)));
            }

                        if (FilterDate.HasValue)
            {
                var d = FilterDate.Value.Date;
                query = query.Where(r => r.CheckInDate.Date == d || (r.CheckInDate.Date <= d && r.CheckOutDate.Date > d));
            }

            Reservations = new ObservableCollection<Reservation>(query);
        }

        // ===== NEW RESERVATION FLOW =====
        [RelayCommand]
        private void OpenNewReservation()
        {
            ResetNewReservationForm();
            if (AvailableChalets.Any())
            {
                NewSelectedChalet = AvailableChalets.FirstOrDefault(c => c.Status == ChaletStatus.Available) ?? AvailableChalets[0];
            }
            RecalculateNewBookingPrices();
            IsNewReservationOpen = true;
        }

        // يُستدعى من التايملاين - يفتح المودال بشاليه وتاريخ مملوء مسبقاً
        public void OpenNewReservationPreFilled(Chalet chalet, DateTime checkInDate)
        {
            ResetNewReservationForm();
            NewSelectedChalet = AvailableChalets.FirstOrDefault(c => c.Id == chalet.Id)
                                 ?? AvailableChalets.FirstOrDefault(c => c.Status == ChaletStatus.Available)
                                 ?? AvailableChalets.FirstOrDefault();
            NewCheckInDate = checkInDate;
            NewCheckOutDate = checkInDate.AddDays(1);
            RecalculateNewBookingPrices();
            IsNewReservationOpen = true;
        }

        private void ResetNewReservationForm()
        {
            NewGuestName = string.Empty;
            NewPhoneNumber = string.Empty;
            NewSecondaryPhoneNumber = string.Empty;
            NewCheckInDate = DateTime.Today.AddDays(1);
            NewCheckOutDate = DateTime.Today.AddDays(2);
            NewNumberOfDays = 1;
            NewDeposit = 0;
            NewDepositText = "0";
            NewDepositDate = DateTime.Today;
            NewDepositReceiptImagePath = string.Empty;
            NewDepositReceiptFileName = string.Empty;
            NewHasDepositReceipt = false;
            NewNotes = string.Empty;
        }

        partial void OnNewSelectedChaletChanged(Chalet? value) => RecalculateNewBookingPrices();
        partial void OnNewCheckInDateChanged(DateTime value) => RecalculateNewBookingPrices();
        partial void OnNewCheckOutDateChanged(DateTime value) => RecalculateNewBookingPrices();

        private void RecalculateNewBookingPrices()
        {
            if (NewCheckOutDate <= NewCheckInDate)
            {
                NewNumberOfDays = 1;
            }
            else
            {
                NewNumberOfDays = (int)(NewCheckOutDate.Date - NewCheckInDate.Date).TotalDays;
                if (NewNumberOfDays <= 0) NewNumberOfDays = 1;
            }

            if (NewSelectedChalet != null)
            {
                var rateTuple = BookingService.GetRate(NewSelectedChalet.ChaletType, NewCheckInDate.Month, "مدني");
                NewDailyRate = rateTuple.Daily > 0 ? rateTuple.Daily : 1000;
                NewChaletTotal = NewNumberOfDays * NewDailyRate;

                if (NewCheckOutDate > NewCheckInDate)
                {
                    var conflict = BookingService.CheckReservationConflict(NewSelectedChalet.Id, NewCheckInDate, NewCheckOutDate);
                    if (conflict.HasConflict)
                    {
                        ConflictNotice = conflict.ErrorMessage;
                        SuggestedChalets = new ObservableCollection<Chalet>(conflict.SuggestedChalets);
                    }
                    else
                    {
                        ConflictNotice = string.Empty;
                        SuggestedChalets.Clear();
                    }
                }
            }
            else
            {
                NewDailyRate = 0;
                NewChaletTotal = 0;
                ConflictNotice = string.Empty;
                SuggestedChalets.Clear();
            }
        }

        [RelayCommand]
        private void UploadNewDepositReceipt()
        {
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
                    NewDepositReceiptImagePath = destPath;
                    NewDepositReceiptFileName = Path.GetFileName(ofd.FileName);
                    NewHasDepositReceipt = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ أثناء رفع الصورة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ViewNewDepositReceipt()
        {
            if (!string.IsNullOrEmpty(NewDepositReceiptImagePath) && File.Exists(NewDepositReceiptImagePath))
            {
                try { Process.Start(new ProcessStartInfo(NewDepositReceiptImagePath) { UseShellExecute = true }); } catch { }
            }
        }

        [RelayCommand]
        private void RemoveNewDepositReceipt()
        {
            NewDepositReceiptImagePath = string.Empty;
            NewDepositReceiptFileName = string.Empty;
            NewHasDepositReceipt = false;
        }

        [RelayCommand]
        private void SaveNewReservation()
        {
            if (string.IsNullOrWhiteSpace(NewGuestName))
            {
                MessageBox.Show("يرجى إدخال اسم العميل أولاً!", "حقل مطلوب", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            if (string.IsNullOrWhiteSpace(NewPhoneNumber))
            {
                MessageBox.Show("يرجى إدخال رقم الهاتف الأساسي للعميل!", "حقل مطلوب", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            if (NewSelectedChalet == null)
            {
                MessageBox.Show("يرجى اختيار الشاليه المراد حجزه!", "حقل مطلوب", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            if (NewCheckOutDate <= NewCheckInDate)
            {
                MessageBox.Show("تاريخ المغادرة يجب أن يكون بعد تاريخ الوصول!", "خطأ في التواريخ", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            // Conflict Check
            var conflict = BookingService.CheckReservationConflict(NewSelectedChalet.Id, NewCheckInDate, NewCheckOutDate);
            if (conflict.HasConflict)
            {
                MessageBox.Show(conflict.ErrorMessage, "⚠️ تعارض في مواعيد الحجز", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            try
            {
                using var db = new AppDbContext();

                var reservation = new Reservation
                {
                    ChaletId = NewSelectedChalet.Id,
                    GuestName = NewGuestName.Trim(),
                    PhoneNumber = NewPhoneNumber.Trim(),
                    SecondaryPhoneNumber = NewSecondaryPhoneNumber.Trim(),
                    CheckInDate = NewCheckInDate,
                    CheckOutDate = NewCheckOutDate,
                    NumberOfDays = NewNumberOfDays,
                    DailyRate = NewDailyRate,
                    ChaletTotal = NewChaletTotal,
                    Deposit = NewDeposit,
                    DepositDate = NewDepositDate,
                    DepositReceiptImagePath = NewDepositReceiptImagePath,
                    CreatedAt = DateTime.Now, // يسحب التاريخ والوقت تلقائياً باليوم والساعة
                    Notes = NewNotes.Trim(),
                    Status = ReservationStatus.Upcoming
                };

                db.Reservations.Add(reservation);
                db.SaveChanges();

                StatusMessage = $"✅ تم تسجيل الحجز المسبق للشاليه {NewSelectedChalet.Number} للنزيل {NewGuestName} بنجاح!";
                IsNewReservationOpen = false;
                
                ActiveFilter = 0;
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء حفظ الحجز المسبق: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void CancelNewReservation()
        {
            IsNewReservationOpen = false;
        }

        // ===== EDIT RESERVATION =====
        [RelayCommand]
        private void OpenEditReservation(Reservation? reservation)
        {
            if (reservation == null) return;
            SelectedReservation = reservation;
            EditGuestName = reservation.GuestName;
            EditPhoneNumber = reservation.PhoneNumber;
            EditSecondaryPhoneNumber = reservation.SecondaryPhoneNumber;
            EditCheckInDate = reservation.CheckInDate;
            EditCheckOutDate = reservation.CheckOutDate;
            EditDeposit = reservation.Deposit;
            EditDepositText = reservation.Deposit.ToString("0.##");
            EditNotes = reservation.Notes;
            IsEditReservationOpen = true;
        }

        [RelayCommand]
        private void SaveEditReservation()
        {
            if (SelectedReservation == null) return;

            if (EditCheckOutDate <= EditCheckInDate)
            {
                MessageBox.Show("تاريخ المغادرة يجب أن يكون بعد تاريخ الوصول!", "خطأ في التواريخ", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            var conflict = BookingService.CheckReservationConflict(SelectedReservation.ChaletId, EditCheckInDate, EditCheckOutDate, SelectedReservation.Id);
            if (conflict.HasConflict)
            {
                MessageBox.Show(conflict.ErrorMessage, "⚠️ تعارض في مواعيد الحجز", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
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
                    r.NumberOfDays = (int)(EditCheckOutDate.Date - EditCheckInDate.Date).TotalDays;
                    if (r.NumberOfDays <= 0) r.NumberOfDays = 1;
                    r.Deposit = EditDeposit;
                    r.Notes = EditNotes.Trim();
                    r.ChaletTotal = r.NumberOfDays * r.DailyRate;
                    db.SaveChanges();
                }

                StatusMessage = "✅ تم حفظ تعديلات الحجز المسبق بنجاح";
                IsEditReservationOpen = false;
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء حفظ التعديل: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void CancelEditReservation()
        {
            IsEditReservationOpen = false;
        }

        // ===== CHANGE CHALET =====
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

            var conflict = BookingService.CheckReservationConflict(SelectedNewChalet.Id, SelectedReservation.CheckInDate, SelectedReservation.CheckOutDate, SelectedReservation.Id);
            if (conflict.HasConflict)
            {
                MessageBox.Show(conflict.ErrorMessage, "تعارض في التواريخ", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var r = db.Reservations.Find(SelectedReservation.Id);
                if (r != null)
                {
                    r.ChaletId = SelectedNewChalet.Id;
                    var rateTuple = BookingService.GetRate(SelectedNewChalet.ChaletType, r.CheckInDate.Month, "مدني");
                    r.DailyRate = rateTuple.Daily > 0 ? rateTuple.Daily : r.DailyRate;
                    r.ChaletTotal = r.NumberOfDays * r.DailyRate;
                    db.SaveChanges();
                }

                StatusMessage = $"✅ تم نقل الحجز المسبق إلى الشاليه {SelectedNewChalet.Number} بنجاح";
                IsChangeChaletOpen = false;
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء تغيير الشاليه: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void CancelChangeChalet()
        {
            IsChangeChaletOpen = false;
        }

        // ===== CHECK-IN & CREATE INVOICE =====
        [RelayCommand]
        private void CheckInAndInvoice(Reservation? reservation)
        {
            if (reservation == null) return;
            MainWindow.Instance?.NavigateToBookingForReservation(reservation);
        }

        // ===== CANCEL RESERVATION =====
        [RelayCommand]
        private void CancelReservation(Reservation? reservation)
        {
            if (reservation == null) return;

            var confirm = MessageBox.Show(
                $"هل أنت متأكد من إلغاء الحجز المسبق للنزيل: {reservation.GuestName} في الشاليه ({reservation.Chalet?.Number})؟",
                "تأكيد إلغاء الحجز المسبق",
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
                }
                StatusMessage = "تم إلغاء الحجز المسبق بنجاح";
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء الإلغاء: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===== VIEW RECEIPT FROM ROW =====
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
    }
}

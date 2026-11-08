using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EaglesResort.Data;
using EaglesResort.Models;
using EaglesResort.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;

namespace EaglesResort.ViewModels
{
    public partial class BookingViewModel : ObservableObject
    {
        // --- Guest Info ---
        [ObservableProperty] private string _guestName = string.Empty;
        [ObservableProperty] private string _phoneNumber = string.Empty;
        [ObservableProperty] private string _secondaryPhoneNumber = string.Empty;
        [ObservableProperty] private string _guestRank = string.Empty;
        [ObservableProperty] private bool _isMilitary;

        // Permissions
        public bool IsAdmin => SessionService.Instance.IsAdmin;
        [ObservableProperty] private int _adultCount = 1;
        [ObservableProperty] private int _childCount = 0;
        [ObservableProperty] private bool _hasResortMembership;
        [ObservableProperty] private string _membershipNumber = string.Empty;

        // --- Category & Systems ---
        [ObservableProperty] private ObservableCollection<string> _guestCategories = new();
        [ObservableProperty] private string _selectedGuestCategory = "مدني";

        [ObservableProperty] private ObservableCollection<string> _militaryRanks = new();

        [ObservableProperty] private ObservableCollection<string> _paymentSystems = new();
        [ObservableProperty] private string _selectedPaymentSystem = "دفع إدارة";

        [ObservableProperty] private ObservableCollection<string> _mealPlans = new();
        [ObservableProperty] private string _selectedMealPlan = "وجبات طول الحجز";

        [ObservableProperty] private string _notes = string.Empty;
        [ObservableProperty] private int? _existingBookingId;
        [ObservableProperty] private int? _linkedReservationId;
        [ObservableProperty] private string _depositReceiptImagePath = string.Empty;
        [ObservableProperty] private bool _hasDepositReceipt;

        partial void OnDepositReceiptImagePathChanged(string value)
        {
            HasDepositReceipt = !string.IsNullOrEmpty(value) && System.IO.File.Exists(value);
        }

        [RelayCommand]
        private void UploadDepositReceipt()
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
                    var dir = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "EaglesResort", "DepositReceipts");
                    System.IO.Directory.CreateDirectory(dir);

                    var ext = System.IO.Path.GetExtension(ofd.FileName);
                    var destName = $"receipt_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}{ext}";
                    var destPath = System.IO.Path.Combine(dir, destName);

                    System.IO.File.Copy(ofd.FileName, destPath, true);
                    DepositReceiptImagePath = destPath;
                    StatusMessage = "✅ تم إرفاق صورة إيصال الديبوزت بنجاح";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"خطأ أثناء حفظ الصورة: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void ViewDepositReceipt()
        {
            if (string.IsNullOrEmpty(DepositReceiptImagePath) || !System.IO.File.Exists(DepositReceiptImagePath))
            {
                StatusMessage = "لا توجد صورة إيصال مرفقة للعرض";
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(DepositReceiptImagePath)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ في فتح الصورة: {ex.Message}";
            }
        }

        [RelayCommand]
        private void RemoveDepositReceipt()
        {
            DepositReceiptImagePath = string.Empty;
            StatusMessage = "تم حذف صورة الإيصال";
        }

        // --- Multi-Chalet Bookings ---
        [ObservableProperty] private ObservableCollection<Chalet> _availableChalets = new();
        [ObservableProperty] private ObservableCollection<ChaletBookingItem> _bookedChalets = new();

        // --- Per-Chalet Guests Mode ---
        // When true: each chalet row shows its own AdultCount/ChildCount
        // When false: one shared AdultCount/ChildCount used for all chalets
        [ObservableProperty] private bool _usePerChaletGuests = false;
        public bool UseSharedGuests => !UsePerChaletGuests;

        // --- Meals Rates ---
        [ObservableProperty] private decimal _adultMealRate = 650;
        [ObservableProperty] private decimal _childMealRate = 450;

        // --- Meals Count & Totals ---
        [ObservableProperty] private int _adultMealsCount;
        [ObservableProperty] private decimal _adultMealsTotal;
        [ObservableProperty] private int _childMealsCount;
        [ObservableProperty] private decimal _childMealsTotal;

        // --- Extras & Insurance ---
        [ObservableProperty] private decimal _extraServices;
        [ObservableProperty] private string _extraServicesNote = string.Empty;

        [ObservableProperty] private bool _hasInsurance = true;
        [ObservableProperty] private decimal _insurance = 500;
        private decimal _defaultInsuranceValue = 500;

        // --- Staff Names ---
        [ObservableProperty] private string _receptionistName = string.Empty;
        [ObservableProperty] private ObservableCollection<string> _receptionistList = new();
        [ObservableProperty] private string _accountantName = string.Empty;
        [ObservableProperty] private string _resortManagerName = string.Empty;

        // --- Summary Totals ---
        [ObservableProperty] private int _numberOfDays;
        [ObservableProperty] private decimal _chaletTotal;
        [ObservableProperty] private decimal _deposit;
        [ObservableProperty] private DateTime? _depositDate;
        [ObservableProperty] private decimal _mealsTotal;
        [ObservableProperty] private decimal _grandTotal;
        [ObservableProperty] private decimal _netHousing;
        [ObservableProperty] private decimal _housingAfterDeposit;
        [ObservableProperty] private decimal _totalAfterDeposit;
        [ObservableProperty] private decimal _totalAfterDepositWithoutInsurance;

        // --- Payment Breakdown (Housing) ---
        [ObservableProperty] private string _housingPaymentMethod = "كاش";
        [ObservableProperty] private string _mealsPaymentMethod = "كاش";
        [ObservableProperty] private ObservableCollection<string> _paymentMethods = new();

        // Housing payment split
        [ObservableProperty] private decimal _cashPaid;
        [ObservableProperty] private decimal _instaPayPaid;
        [ObservableProperty] private decimal _visaPaid;
        [ObservableProperty] private decimal _bankTransferPaid;
        [ObservableProperty] private decimal _totalPaidAmount;
        [ObservableProperty] private decimal _remainingPaymentAmount;
        [ObservableProperty] private string _paymentBreakdownSummary = string.Empty;

        // Meals payment split
        [ObservableProperty] private decimal _mealsCashPaid;
        [ObservableProperty] private decimal _mealsInstaPayPaid;
        [ObservableProperty] private decimal _mealsVisaPaid;
        [ObservableProperty] private decimal _mealsBankTransferPaid;
        [ObservableProperty] private decimal _mealsTotalPaid;
        [ObservableProperty] private decimal _mealsRemainingAmount;
        [ObservableProperty] private string _mealsPaymentBreakdownSummary = string.Empty;

        // --- Status ---
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _isSaved;

        public event Action<Invoice>? BookingSaved;

        public BookingViewModel()
        {
            LoadData();
            InitializeDefaultChalet();
        }

        public BookingViewModel(Chalet selectedChalet)
        {
            LoadData();
            var item = CreateChaletItem();
            item.SelectedChalet = AvailableChalets.FirstOrDefault(c => c.Id == selectedChalet.Id);
            item.Recalculate(SelectedGuestCategory);
            BookedChalets.Add(item);
            UpdateCanDelete();
            RecalculateTotals();
        }

        public BookingViewModel(Booking existingBooking)
        {
            LoadData();
            GuestName = existingBooking.GuestName;
            PhoneNumber = existingBooking.PhoneNumber;
            SecondaryPhoneNumber = existingBooking.SecondaryPhoneNumber;
            SelectedGuestCategory = existingBooking.GuestCategory;
            GuestRank = existingBooking.GuestRank;
            SelectedPaymentSystem = existingBooking.PaymentSystem;
            SelectedMealPlan = existingBooking.MealPlan;
            AdultCount = existingBooking.AdultCount;
            ChildCount = existingBooking.ChildCount;
            HasResortMembership = existingBooking.HasResortMembership;
            MembershipNumber = existingBooking.MembershipNumber;
            Notes = $"تمديد للإقامة السابقة بشاليه رقم {existingBooking.ChaletId}";

            var item = CreateChaletItem();
            item.SelectedChalet = AvailableChalets.FirstOrDefault(c => c.Id == existingBooking.ChaletId);
            item.CheckInDate = existingBooking.CheckOutDate;
            item.CheckOutDate = existingBooking.CheckOutDate.AddDays(1);
            item.Recalculate(SelectedGuestCategory);
            BookedChalets.Add(item);
            UpdateCanDelete();
            RecalculateTotals();
        }

        public BookingViewModel(Reservation reservation)
        {
            LoadData();
            LinkedReservationId = reservation.Id;
            GuestName = reservation.GuestName;
            PhoneNumber = reservation.PhoneNumber;
            SecondaryPhoneNumber = reservation.SecondaryPhoneNumber;
            SelectedGuestCategory = "مدني";
            SelectedPaymentSystem = "دفع إدارة";
            SelectedMealPlan = "وجبات طول الحجز";
            AdultCount = 1;
            ChildCount = 0;
            Notes = reservation.Notes;
            DepositReceiptImagePath = reservation.DepositReceiptImagePath;
            Deposit = reservation.Deposit;
            DepositDate = reservation.DepositDate ?? DateTime.Today;

            var item = CreateChaletItem();
            item.SelectedChalet = AvailableChalets.FirstOrDefault(c => c.Id == reservation.ChaletId);
            item.CheckInDate = reservation.CheckInDate;
            item.CheckOutDate = reservation.CheckOutDate;
            item.Deposit = reservation.Deposit;
            item.DepositDate = reservation.DepositDate;
            item.Recalculate(SelectedGuestCategory);
            BookedChalets.Add(item);
            UpdateCanDelete();
            RecalculateTotals();
        }

        private void InitializeDefaultChalet()
        {
            BookedChalets.Clear();
            var item = CreateChaletItem();
            if (AvailableChalets.Any())
            {
                item.SelectedChalet = AvailableChalets.First();
                item.Recalculate(SelectedGuestCategory);
            }
            BookedChalets.Add(item);
            UpdateCanDelete();
            RecalculateTotals();
        }

        private ChaletBookingItem CreateChaletItem()
        {
            var item = new ChaletBookingItem
            {
                ItemIndex = BookedChalets.Count + 1,
                CheckInDate = BookedChalets.LastOrDefault()?.CheckInDate ?? DateTime.Today,
                CheckOutDate = BookedChalets.LastOrDefault()?.CheckOutDate ?? DateTime.Today.AddDays(3),
                CanDelete = false
            };
            item.Changed += () =>
            {
                item.Recalculate(SelectedGuestCategory);
                RecalculateTotals();
            };
            return item;
        }

        private void LoadData()
        {
            GuestCategories = new ObservableCollection<string>
            {
                "مدني",
                "أجانب (غير مصريين)",
                "ضباط قوات جوية (عضوية)",
                "ضباط قوات جوية (بدون عضوية)",
                "ضباط ق.جوية (مرافقين)",
                "ضباط قوات مسلحة وشرطة"
            };

            PaymentSystems = new ObservableCollection<string> { "دفع إدارة", "دفع منظومة" };
            MealPlans = new ObservableCollection<string> { "وجبات طول الحجز", "أول يوم بدون وجبات", "بدون وجبات" };

            MilitaryRanks = new ObservableCollection<string>
            {
                "مشير",
                "فريق أول",
                "فريق",
                "لواء",
                "عميد",
                "عقيد",
                "مقدم",
                "رائد",
                "نقيب",
                "ملازم أول",
                "ملازم"
            };

            using var db = new AppDbContext();
                        // Smart Filter: Only chalets that are currently Available (strictly exclude Occupied and Maintenance)
            var chalets = db.Chalets
                .Where(c => c.Status == ChaletStatus.Available)
                .OrderBy(c => c.BuildingArea)
                .ThenBy(c => c.Number)
                .ToList();
            AvailableChalets = new ObservableCollection<Chalet>(chalets);

            // Load defaults from settings
            var settings = db.AppSettings.ToList();
            _defaultInsuranceValue = decimal.TryParse(settings.FirstOrDefault(s => s.Key == "DefaultInsurance")?.Value, out var ins) ? ins : 1000;
            Insurance = HasInsurance ? _defaultInsuranceValue : 0;

                        try
            {
                var recNames = db.Receptionists.Where(r => r.IsActive).OrderBy(r => r.Name).Select(r => r.Name).ToList();
                if (!recNames.Any())
                {
                    recNames = new List<string> { "أحمد إبراهيم", "محمد علي" };
                }
                ReceptionistList = new ObservableCollection<string>(recNames);
                ReceptionistName = ReceptionistList.FirstOrDefault() ?? settings.FirstOrDefault(s => s.Key == "ReceptionistName")?.Value ?? string.Empty;
            }
            catch
            {
                ReceptionistName = settings.FirstOrDefault(s => s.Key == "ReceptionistName")?.Value ?? string.Empty;
            }
            AccountantName = settings.FirstOrDefault(s => s.Key == "AccountantName")?.Value ?? string.Empty;
            ResortManagerName = settings.FirstOrDefault(s => s.Key == "ResortManagerName")?.Value ?? string.Empty;

            var pmStr = settings.FirstOrDefault(s => s.Key == "PaymentMethods")?.Value ?? "كاش,فيزا,انستا باي,تحويل بنكي";
            var pmList = pmStr.Split(',').Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
            PaymentMethods = new ObservableCollection<string>(pmList);
            if (PaymentMethods.Any())
            {
                if (string.IsNullOrEmpty(HousingPaymentMethod)) HousingPaymentMethod = PaymentMethods[0];
                if (string.IsNullOrEmpty(MealsPaymentMethod)) MealsPaymentMethod = PaymentMethods[0];
            }

            IsMilitary = SelectedGuestCategory != "مدني" && SelectedGuestCategory != "أجانب (غير مصريين)";
            SelectedPaymentSystem = "دفع إدارة";
        }

        [RelayCommand]
        private void AddChalet()
        {
            var item = CreateChaletItem();
            BookedChalets.Add(item);
            UpdateCanDelete();
            RecalculateTotals();
        }

        [RelayCommand]
        private void RemoveChalet(ChaletBookingItem? item)
        {
            if (item == null || BookedChalets.Count <= 1) return;
            BookedChalets.Remove(item);
            for (int i = 0; i < BookedChalets.Count; i++)
            {
                BookedChalets[i].ItemIndex = i + 1;
            }
            UpdateCanDelete();
            RecalculateTotals();
        }

        private void UpdateCanDelete()
        {
            bool canDelete = BookedChalets.Count > 1;
            foreach (var ch in BookedChalets)
            {
                ch.CanDelete = canDelete;
            }
        }

        partial void OnSelectedGuestCategoryChanged(string value)
        {
            IsMilitary = value != "مدني" && value != "أجانب (غير مصريين)";
            if (IsMilitary)
            {
                if (string.IsNullOrWhiteSpace(GuestRank))
                    GuestRank = MilitaryRanks.FirstOrDefault() ?? "نقيب";
            }
            else
            {
                GuestRank = string.Empty;
            SelectedPaymentSystem = "دفع إدارة";
            }

            foreach (var item in BookedChalets)
            {
                if (item.SelectedChalet != null)
                {
                    item.DailyRate = 0;
                    item.Recalculate(value);
                }
            }
            RecalculateTotals();
        }

        partial void OnSelectedPaymentSystemChanged(string value) => RecalculateTotals();
        partial void OnSelectedMealPlanChanged(string value) => RecalculateTotals();
        partial void OnAdultCountChanged(int value) => RecalculateTotals();
        partial void OnChildCountChanged(int value) => RecalculateTotals();
        partial void OnAdultMealRateChanged(decimal value) => RecalculateTotals();
        partial void OnChildMealRateChanged(decimal value) => RecalculateTotals();
        partial void OnExtraServicesChanged(decimal value) => RecalculateTotals();
        partial void OnInsuranceChanged(decimal value) => RecalculateTotals();

        partial void OnHasInsuranceChanged(bool value)
        {
            Insurance = value ? _defaultInsuranceValue : 0;
        }

        // --- Split Payment Watchers (Housing) ---
        partial void OnCashPaidChanged(decimal value) => UpdateHousingPaymentCalc();
        partial void OnInstaPayPaidChanged(decimal value) => UpdateHousingPaymentCalc();
        partial void OnVisaPaidChanged(decimal value) => UpdateHousingPaymentCalc();
        partial void OnBankTransferPaidChanged(decimal value) => UpdateHousingPaymentCalc();

        // --- Split Payment Watchers (Meals) ---
        partial void OnMealsCashPaidChanged(decimal value) => UpdateMealsPaymentCalc();
        partial void OnMealsInstaPayPaidChanged(decimal value) => UpdateMealsPaymentCalc();
        partial void OnMealsVisaPaidChanged(decimal value) => UpdateMealsPaymentCalc();
        partial void OnMealsBankTransferPaidChanged(decimal value) => UpdateMealsPaymentCalc();

        // Alias kept for compatibility
        private void UpdatePaymentCalculations() { UpdateHousingPaymentCalc(); UpdateMealsPaymentCalc(); }

        private void UpdateHousingPaymentCalc()
        {
            TotalPaidAmount = CashPaid + InstaPayPaid + VisaPaid + BankTransferPaid;
            RemainingPaymentAmount = Math.Max(0, HousingAfterDeposit - TotalPaidAmount);

            var parts = new List<string>();
            if (CashPaid > 0) parts.Add($"كاش: {CashPaid:N0} ج.م");
            if (InstaPayPaid > 0) parts.Add($"انستا باي: {InstaPayPaid:N0} ج.م");
            if (VisaPaid > 0) parts.Add($"فيزا: {VisaPaid:N0} ج.م");
            if (BankTransferPaid > 0) parts.Add($"تحويل بنكي: {BankTransferPaid:N0} ج.م");

            PaymentBreakdownSummary = parts.Any() ? string.Join(" | ", parts) : HousingPaymentMethod;
        }

        private void UpdateMealsPaymentCalc()
        {
            MealsTotalPaid = MealsCashPaid + MealsInstaPayPaid + MealsVisaPaid + MealsBankTransferPaid;
            MealsRemainingAmount = Math.Max(0, MealsTotal - MealsTotalPaid);

            var parts = new List<string>();
            if (MealsCashPaid > 0) parts.Add($"كاش: {MealsCashPaid:N0} ج.م");
            if (MealsInstaPayPaid > 0) parts.Add($"انستا باي: {MealsInstaPayPaid:N0} ج.م");
            if (MealsVisaPaid > 0) parts.Add($"فيزا: {MealsVisaPaid:N0} ج.م");
            if (MealsBankTransferPaid > 0) parts.Add($"تحويل بنكي: {MealsBankTransferPaid:N0} ج.م");

            MealsPaymentBreakdownSummary = parts.Any() ? string.Join(" | ", parts) : MealsPaymentMethod;
        }

        [RelayCommand]
        private void PayAllCash()
        {
            CashPaid = HousingAfterDeposit;
            InstaPayPaid = 0;
            VisaPaid = 0;
            BankTransferPaid = 0;
        }

        [RelayCommand]
        private void PayAllInstaPay()
        {
            CashPaid = 0;
            InstaPayPaid = HousingAfterDeposit;
            VisaPaid = 0;
            BankTransferPaid = 0;
        }

        [RelayCommand]
        private void PayAllVisa()
        {
            CashPaid = 0;
            InstaPayPaid = 0;
            VisaPaid = HousingAfterDeposit;
            BankTransferPaid = 0;
        }

        [RelayCommand]
        private void ClearPayments()
        {
            CashPaid = 0;
            InstaPayPaid = 0;
            VisaPaid = 0;
            BankTransferPaid = 0;
        }

        // --- Meals Quick-Pay Commands ---
        [RelayCommand]
        private void MealsPayAllCash()
        {
            MealsCashPaid = MealsTotal;
            MealsInstaPayPaid = 0;
            MealsVisaPaid = 0;
            MealsBankTransferPaid = 0;
        }

        [RelayCommand]
        private void MealsPayAllInstaPay()
        {
            MealsCashPaid = 0;
            MealsInstaPayPaid = MealsTotal;
            MealsVisaPaid = 0;
            MealsBankTransferPaid = 0;
        }

        [RelayCommand]
        private void MealsPayAllVisa()
        {
            MealsCashPaid = 0;
            MealsInstaPayPaid = 0;
            MealsVisaPaid = MealsTotal;
            MealsBankTransferPaid = 0;
        }

        [RelayCommand]
        private void ClearMealsPayments()
        {
            MealsCashPaid = 0;
            MealsInstaPayPaid = 0;
            MealsVisaPaid = 0;
            MealsBankTransferPaid = 0;
        }

        // Toggle per-chalet guests mode
        [RelayCommand]
        private void TogglePerChaletGuests()
        {
            UsePerChaletGuests = !UsePerChaletGuests;
            OnPropertyChanged(nameof(UseSharedGuests));
            RecalculateTotals();
        }

        private void RecalculateTotals()
        {
            foreach (var item in BookedChalets)
            {
                item.Recalculate(SelectedGuestCategory);
            }

            NumberOfDays = BookedChalets.Any() ? BookedChalets.Max(c => c.NumberOfDays) : 0;
            Deposit = BookedChalets.Sum(c => c.Deposit);
            DepositDate = BookedChalets.FirstOrDefault(c => c.DepositDate != null)?.DepositDate;

            // Meals Logic: calculate per chalet accurately if per-chalet guests is enabled
            if (UsePerChaletGuests)
            {
                int totalAdultMeals = 0;
                int totalChildMeals = 0;
                foreach (var c in BookedChalets)
                {
                    int chaletMealDays = c.NumberOfDays;
                    if (SelectedMealPlan == "أول يوم بدون وجبات") chaletMealDays = Math.Max(0, c.NumberOfDays - 1);
                    else if (SelectedMealPlan == "بدون وجبات") chaletMealDays = 0;

                    totalAdultMeals += c.AdultCount * chaletMealDays;
                    totalChildMeals += c.ChildCount * chaletMealDays;
                }
                AdultMealsCount = totalAdultMeals;
                ChildMealsCount = totalChildMeals;
            }
            else
            {
                int mealDays = NumberOfDays;
                if (SelectedMealPlan == "أول يوم بدون وجبات") mealDays = Math.Max(0, NumberOfDays - 1);
                else if (SelectedMealPlan == "بدون وجبات") mealDays = 0;

                AdultMealsCount = AdultCount * mealDays;
                ChildMealsCount = ChildCount * mealDays;
            }

            // Housing Logic (Payment System)
            if (SelectedPaymentSystem == "دفع منظومة")
            {
                ChaletTotal = 0;
            }
            else
            {
                ChaletTotal = BookedChalets.Sum(c => c.ChaletTotal);
            }

            AdultMealsTotal = AdultMealsCount * AdultMealRate;
            ChildMealsTotal = ChildMealsCount * ChildMealRate;
            MealsTotal = AdultMealsTotal + ChildMealsTotal;

            // Housing after deposit
            HousingAfterDeposit = Math.Max(0, ChaletTotal - Deposit);
            NetHousing = HousingAfterDeposit;

            // Grand Total
            GrandTotal = ChaletTotal + MealsTotal + ExtraServices + Insurance;
            TotalAfterDeposit = HousingAfterDeposit + MealsTotal + ExtraServices + Insurance;
            TotalAfterDepositWithoutInsurance = HousingAfterDeposit + MealsTotal + ExtraServices;

            UpdatePaymentCalculations();
        }

        [RelayCommand]
        private void SaveBooking()
        {
            if (string.IsNullOrWhiteSpace(GuestName)) { StatusMessage = "الرجاء إدخال اسم النزيل"; return; }
            if (string.IsNullOrWhiteSpace(PhoneNumber)) { StatusMessage = "الرجاء إدخال رقم هاتف النزيل"; return; }
            if (!BookedChalets.Any()) { StatusMessage = "الرجاء إضافة شاليه واحد على الأقل"; return; }

            // Validate each chalet
            for (int i = 0; i < BookedChalets.Count; i++)
            {
                var ch = BookedChalets[i];
                if (ch.SelectedChalet == null)
                {
                    StatusMessage = $"الرجاء اختيار الشاليه في البند رقم {i + 1}";
                    return;
                }
                if (ch.CheckOutDate <= ch.CheckInDate)
                {
                    StatusMessage = $"تاريخ المغادرة يجب أن يكون بعد تاريخ الوصول للشاليه {ch.SelectedChalet.Number}";
                    return;
                }
            }

            // Check internal duplicates with overlapping dates
            for (int i = 0; i < BookedChalets.Count; i++)
            {
                for (int j = i + 1; j < BookedChalets.Count; j++)
                {
                    var a = BookedChalets[i];
                    var b = BookedChalets[j];
                    if (a.SelectedChalet!.Id == b.SelectedChalet!.Id)
                    {
                        if (a.CheckInDate < b.CheckOutDate && a.CheckOutDate > b.CheckInDate)
                        {
                            StatusMessage = $"الشاليه رقم {a.SelectedChalet.Number} تم تكراره بفترات متداخلة!";
                            return;
                        }
                    }
                }
            }

            // Check database overlap for all chalets with detailed conflict warning
            using (var db = new AppDbContext())
            {
                foreach (var item in BookedChalets)
                {
                    var conflicting = db.Bookings
                        .Include(b => b.Chalet)
                        .FirstOrDefault(b => b.ChaletId == item.SelectedChalet!.Id
                                          && b.Status != BookingStatus.CheckedOut
                                          && b.Status != BookingStatus.Cancelled
                                          && b.CheckInDate < item.CheckOutDate
                                          && b.CheckOutDate > item.CheckInDate);
                    if (conflicting != null)
                    {
                        var msg = $"⚠️ لا يمكن إتمام الحجز بسبب تعارض في التواريخ!\n\n" +
                                  $"الشاليه رقم ({item.SelectedChalet!.Number}) محجوز بالفعل للنزيل:\n" +
                                  $"«{conflicting.GuestName}» (هاتف: {conflicting.PhoneNumber})\n" +
                                  $"في الفترة من {conflicting.CheckInDate:dd/MM/yyyy} إلى {conflicting.CheckOutDate:dd/MM/yyyy}.\n\n" +
                                  $"يرجى اختيار شاليه آخر أو تعديل تواريخ الحجز لتجنب التضارب.";

                        StatusMessage = $"تعارض: الشاليه {item.SelectedChalet.Number} محجوز للنزيل {conflicting.GuestName} ({conflicting.CheckInDate:dd/MM} - {conflicting.CheckOutDate:dd/MM})";
                        MessageBox.Show(msg, "تعارض في مواعيد الحجز", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                        return;
                    }
                }
            }

            // Build Chalets Summary
            var chaletsSummary = string.Join(" | ", BookedChalets.Select(c =>
                $"شاليه {c.SelectedChalet!.Number} ({c.SelectedChalet.ChaletType}) من {c.CheckInDate:yyyy/MM/dd} إلى {c.CheckOutDate:yyyy/MM/dd} [مقدم: {c.Deposit:N0}]"));

            var invoice = new Invoice
            {
                DailyRate = BookedChalets.Count == 1 ? BookedChalets[0].DailyRate : 0,
                ChaletTotal = ChaletTotal,
                AdultMealsCount = AdultMealsCount,
                AdultMealRate = AdultMealRate,
                ChildMealsCount = ChildMealsCount,
                ChildMealRate = ChildMealRate,
                MealsTotal = MealsTotal,
                ExtraServices = ExtraServices,
                ExtraServicesNote = ExtraServicesNote,
                Insurance = Insurance,
                DepositReceiptImagePath = DepositReceiptImagePath,
                Deposit = Deposit,
                DepositDate = DepositDate,
                HousingPaymentMethod = HousingPaymentMethod,
                MealsPaymentMethod = MealsPaymentMethod,
                PaymentDetails = PaymentBreakdownSummary,
                CashAmount = CashPaid,
                InstaPayAmount = InstaPayPaid,
                VisaAmount = VisaPaid,
                BankTransferAmount = BankTransferPaid,
                MealsPaymentDetails = MealsPaymentBreakdownSummary,
                MealsCashAmount = MealsCashPaid,
                MealsInstaPayAmount = MealsInstaPayPaid,
                MealsVisaAmount = MealsVisaPaid,
                MealsBankTransferAmount = MealsBankTransferPaid,
                NetHousing = NetHousing,
                GrandTotal = GrandTotal,
                TotalAfterDeposit = TotalAfterDeposit,
                ReceptionistName = ReceptionistName,
                AccountantName = AccountantName,
                ResortManagerName = ResortManagerName,
                ChaletsSummary = chaletsSummary,
                InvoiceDate = DateTime.Now
            };

            var bookingsToSave = new List<Booking>();
            foreach (var item in BookedChalets)
            {
                var booking = new Booking
                {
                    ChaletId = item.SelectedChalet!.Id,
                    // Do NOT set Chalet nav property — EF will try to re-insert it causing UNIQUE constraint error
                    GuestName = GuestName,
                    PhoneNumber = PhoneNumber,
                    SecondaryPhoneNumber = SecondaryPhoneNumber,
                    GuestRank = GuestRank,
                    GuestCategory = SelectedGuestCategory,
                    PaymentSystem = SelectedPaymentSystem,
                    MealPlan = SelectedMealPlan,
                    AdultCount = UsePerChaletGuests ? item.AdultCount : AdultCount,
                    ChildCount = UsePerChaletGuests ? item.ChildCount : ChildCount,
                    HasResortMembership = HasResortMembership,
                    MembershipNumber = MembershipNumber,
                    CheckInDate = item.CheckInDate,
                    CheckOutDate = item.CheckOutDate,
                    DailyRate = item.DailyRate,
                    ChaletTotal = item.ChaletTotal,
                    Deposit = item.Deposit,
                    DepositDate = item.DepositDate,
                    Notes = Notes,
                    InsurancePaid = HasInsurance && Insurance > 0,
                    DepositReceiptImagePath = DepositReceiptImagePath
                };
                bookingsToSave.Add(booking);
            }

            if (ExistingBookingId.HasValue && bookingsToSave.Count > 0)
            {
                bookingsToSave[0].Id = ExistingBookingId.Value;
            }

            var savedBookings = BookingService.SaveMultiBooking(bookingsToSave, invoice);
            if (LinkedReservationId.HasValue)
            {
                using var db = new AppDbContext();
                var res = db.Reservations.Find(LinkedReservationId.Value);
                if (res != null)
                {
                    res.Status = ReservationStatus.CheckedIn;
                    res.InvoiceId = invoice.Id;
                    db.SaveChanges();
                }
            }

            IsSaved = true;
            StatusMessage = $"✅ تم حفظ الحجز بنجاح! تم حجز {savedBookings.Count} شاليه برقم فاتورة: {invoice.Id}";

            // Link for printing
            invoice.Bookings = savedBookings;
            invoice.Booking = savedBookings.FirstOrDefault();

            BookingSaved?.Invoke(invoice);
        }

        [RelayCommand]
        private void PrintInvoice()
        {
            if (!IsSaved) { StatusMessage = "الرجاء حفظ الحجز أولاً قبل الطباعة"; return; }
        }

        [RelayCommand]
        private void ClearForm()
        {
            GuestName = string.Empty;
            DepositReceiptImagePath = string.Empty;
            PhoneNumber = string.Empty;
            SecondaryPhoneNumber = string.Empty;
            GuestRank = string.Empty;
            AdultCount = 1;
            ChildCount = 0;
            HasResortMembership = false;
            MembershipNumber = string.Empty;
            Notes = string.Empty;
            SelectedGuestCategory = "مدني";
            SelectedPaymentSystem = "دفع إدارة";
            SelectedMealPlan = "وجبات طول الحجز";
            AdultMealsCount = 0;
            ChildMealsCount = 0;
            ExtraServices = 0;
            Deposit = 0;
            DepositDate = null;
            CashPaid = 0;
            InstaPayPaid = 0;
            VisaPaid = 0;
            BankTransferPaid = 0;
            MealsCashPaid = 0;
            MealsInstaPayPaid = 0;
            MealsVisaPaid = 0;
            MealsBankTransferPaid = 0;
            UsePerChaletGuests = false;
            StatusMessage = string.Empty;
            IsSaved = false;

            InitializeDefaultChalet();
        }
    }
}

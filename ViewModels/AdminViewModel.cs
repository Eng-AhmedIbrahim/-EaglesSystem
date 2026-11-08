using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EaglesResort.Data;
using EaglesResort.Models;
using EaglesResort.Services;
using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;

namespace EaglesResort.ViewModels
{
    public partial class AdminViewModel : ObservableObject
    {
        // --- Rates Tab ---
        [ObservableProperty] private ObservableCollection<string> _chaletTypes = new();
        [ObservableProperty] private string _selectedChaletType = string.Empty;
        [ObservableProperty] private ObservableCollection<Rate> _monthlyRates = new();

        // --- Special Rates Tab ---
        [ObservableProperty] private ObservableCollection<string> _guestCategories = new() { "ضباط قوات جوية (عضوية)", "ضباط قوات جوية (بدون عضوية)", "ضباط ق.جوية (مرافقين)", "ضباط قوات مسلحة وشرطة" };
        [ObservableProperty] private string _selectedGuestCategory = string.Empty;
        [ObservableProperty] private ObservableCollection<SpecialRate> _specialRates = new();

        // --- Settings Tab ---
        [ObservableProperty] private decimal _defaultInsurance;
        [ObservableProperty] private string _receptionistName = string.Empty;
        [ObservableProperty] private string _accountantName = string.Empty;
        [ObservableProperty] private string _resortManagerName = string.Empty;
        [ObservableProperty] private string _restaurantTerms = "الفطار من الساعة 9ص حتي 11ص و العشاء من الساعة 6م حتي 8م والمطعم غير مسئول في حالة التأخير عن المواعيد المحددة";
        [ObservableProperty] private string _paymentMethodsConfig = string.Empty;
        
        [ObservableProperty] private ObservableCollection<string> _configuredChaletTypes = new();
        [ObservableProperty] private string _newChaletType = string.Empty;
        [ObservableProperty] private string? _editingChaletType = null; // currently being edited

        [ObservableProperty] private ObservableCollection<string> _configuredBuildingAreas = new();
        [ObservableProperty] private string _newBuildingArea = string.Empty;
        [ObservableProperty] private string? _editingBuildingArea = null; // currently being edited

        // --- Printer ---
        [ObservableProperty] private ObservableCollection<string> _availablePrinters = new();
        [ObservableProperty] private string _selectedPrinterName = string.Empty;

        // --- Archive Tab ---
        [ObservableProperty] private string _searchQuery = string.Empty;
                [ObservableProperty] private ObservableCollection<Booking> _archiveResults = new();

        // --- Receptionists Tab ---
        [ObservableProperty] private ObservableCollection<Receptionist> _receptionists = new();
        [ObservableProperty] private string _newReceptionistName = string.Empty;
        [ObservableProperty] private Receptionist? _editingReceptionist;
        [ObservableProperty] private Receptionist? _selectedReceptionist;

        // --- Status ---
        [ObservableProperty] private string _statusMessage = string.Empty;

        public event Action<Invoice>? PrintRequested;

        public AdminViewModel()
        {
            LoadData();
        }

        public void LoadData()
        {
            using var db = new AppDbContext();
            
            var settings = db.AppSettings.ToList();
            DefaultInsurance = decimal.TryParse(settings.FirstOrDefault(s => s.Key == "DefaultInsurance")?.Value, out var ins) ? ins : 1000;
            ReceptionistName = settings.FirstOrDefault(s => s.Key == "ReceptionistName")?.Value ?? string.Empty;
            AccountantName = settings.FirstOrDefault(s => s.Key == "AccountantName")?.Value ?? string.Empty;
            ResortManagerName = settings.FirstOrDefault(s => s.Key == "ResortManagerName")?.Value ?? string.Empty;
            RestaurantTerms = settings.FirstOrDefault(s => s.Key == "RestaurantTerms")?.Value ?? "الفطار من الساعة 9ص حتي 11ص و العشاء من الساعة 6م حتي 8م والمطعم غير مسئول في حالة التأخير عن المواعيد المحددة";
            PaymentMethodsConfig = settings.FirstOrDefault(s => s.Key == "PaymentMethods")?.Value ?? "كاش,فيزا,انستا باي,تحويل بنكي";
            SelectedPrinterName = settings.FirstOrDefault(s => s.Key == "DefaultPrinter")?.Value ?? string.Empty;
            LoadReceptionists();

            // Load available printers using WPF System.Printing
            try
            {
                var printServer = new System.Printing.LocalPrintServer();
                var queues = printServer.GetPrintQueues();
                AvailablePrinters = new System.Collections.ObjectModel.ObservableCollection<string>(
                    queues.Select(q => q.Name).ToList()
                );
            }
            catch { }
            
            // Load Chalet Types
            var savedTypesStr = settings.FirstOrDefault(s => s.Key == "ChaletTypesConfig")?.Value;
            var typesList = string.IsNullOrEmpty(savedTypesStr)
                ? new List<string> { "VIP", "Eagle1", "Eagle2", "Studio", "Single" }
                : savedTypesStr.Split(',').Where(x => !string.IsNullOrEmpty(x.Trim())).Select(x => x.Trim()).ToList();
            ConfiguredChaletTypes = new ObservableCollection<string>(typesList);
            ChaletTypes = new ObservableCollection<string>(typesList);
            if (ChaletTypes.Any()) SelectedChaletType = ChaletTypes[0];
            if (GuestCategories.Any()) SelectedGuestCategory = GuestCategories[0];

            // Load Building Areas
            var savedBuildingsStr = settings.FirstOrDefault(s => s.Key == "BuildingAreasConfig")?.Value;
            var buildingsList = string.IsNullOrEmpty(savedBuildingsStr)
                ? new List<string> { "A", "B", "C", "D", "E" }
                : savedBuildingsStr.Split(',').Where(x => !string.IsNullOrEmpty(x.Trim())).Select(x => x.Trim()).ToList();
            ConfiguredBuildingAreas = new ObservableCollection<string>(buildingsList);

            LoadRatesForType();
            LoadSpecialRates();
            SearchArchive();
        }

        partial void OnSelectedChaletTypeChanged(string value) 
        {
            LoadRatesForType();
            LoadSpecialRates();
            SearchArchive();
        }
        
        partial void OnSelectedGuestCategoryChanged(string value) => LoadSpecialRates();

        private void LoadRatesForType()
        {
            if (string.IsNullOrEmpty(SelectedChaletType)) return;
            using var db = new AppDbContext();
            var rates = db.Rates.Where(r => r.ChaletType == SelectedChaletType).OrderBy(r => r.Month).ToList();

            // Ensure all 12 months exist
            for (int m = 1; m <= 12; m++)
            {
                if (!rates.Any(r => r.Month == m))
                {
                    var newRate = new Rate { ChaletType = SelectedChaletType, Month = m, DailyRate = 0, AdultMealRate = 650, ChildMealRate = 450 };
                    db.Rates.Add(newRate);
                    rates.Add(newRate);
                }
            }
            db.SaveChanges();

            MonthlyRates = new ObservableCollection<Rate>(rates.OrderBy(r => r.Month));
        }

        private void LoadSpecialRates()
        {
            if (string.IsNullOrEmpty(SelectedChaletType) || string.IsNullOrEmpty(SelectedGuestCategory)) return;
            using var db = new AppDbContext();
            var specialRate = db.SpecialRates.FirstOrDefault(r => r.ChaletType == SelectedChaletType && r.GuestCategory == SelectedGuestCategory);
            
            if (specialRate == null)
            {
                specialRate = new SpecialRate { ChaletType = SelectedChaletType, GuestCategory = SelectedGuestCategory, DailyRate = 0, AdultMealRate = 650, ChildMealRate = 450 };
                db.SpecialRates.Add(specialRate);
                db.SaveChanges();
            }
            
            SpecialRates = new ObservableCollection<SpecialRate> { specialRate };
        }

        [RelayCommand]
        private void AddChaletType()
        {
            if (!string.IsNullOrWhiteSpace(NewChaletType) && !ConfiguredChaletTypes.Contains(NewChaletType.Trim()))
            {
                ConfiguredChaletTypes.Add(NewChaletType.Trim());
                ChaletTypes.Add(NewChaletType.Trim());
                NewChaletType = string.Empty;
                SaveSettings(); // Auto save
            }
        }

        [RelayCommand]
        private void RemoveChaletType(string typeToRemove)
        {
            if (typeToRemove != null && ConfiguredChaletTypes.Contains(typeToRemove))
            {
                ConfiguredChaletTypes.Remove(typeToRemove);
                ChaletTypes.Remove(typeToRemove);
                SaveSettings(); // Auto save
            }
        }

        [RelayCommand]
        private void SaveRates()
        {
            using var db = new AppDbContext();
            // Save Monthly Rates
            foreach (var rate in MonthlyRates)
            {
                var dbRate = db.Rates.Find(rate.Id);
                if (dbRate != null)
                {
                    dbRate.DailyRate = rate.DailyRate;
                    dbRate.AdultMealRate = rate.AdultMealRate;
                    dbRate.ChildMealRate = rate.ChildMealRate;
                }
            }
            
            // Save Special Rates
            foreach (var sRate in SpecialRates)
            {
                var dbSRate = db.SpecialRates.Find(sRate.Id);
                if (dbSRate != null)
                {
                    dbSRate.DailyRate = sRate.DailyRate;
                    dbSRate.AdultMealRate = sRate.AdultMealRate;
                    dbSRate.ChildMealRate = sRate.ChildMealRate;
                }
            }
            
            db.SaveChanges();
            StatusMessage = "تم حفظ الأسعار بنجاح!";
        }

        [RelayCommand]
        private void SaveSettings()
        {
            using var db = new AppDbContext();
            var settings = db.AppSettings.ToList();

            void UpdateSetting(string key, string value)
            {
                var s = settings.FirstOrDefault(x => x.Key == key);
                if (s != null) s.Value = value;
                else db.AppSettings.Add(new AppSettings { Key = key, Value = value });
            }

            UpdateSetting("DefaultInsurance", DefaultInsurance.ToString());
            UpdateSetting("ReceptionistName", ReceptionistName);
            UpdateSetting("AccountantName", AccountantName);
            UpdateSetting("ResortManagerName", ResortManagerName);
            UpdateSetting("RestaurantTerms", RestaurantTerms);
            UpdateSetting("PaymentMethods", PaymentMethodsConfig);
            UpdateSetting("DefaultPrinter", SelectedPrinterName ?? string.Empty);
            UpdateSetting("ChaletTypesConfig", string.Join(",", ConfiguredChaletTypes));
            UpdateSetting("BuildingAreasConfig", string.Join(",", ConfiguredBuildingAreas));

            db.SaveChanges();
            StatusMessage = "تم حفظ الإعدادات العامة بنجاح!";
        }

        [RelayCommand]
        private void AddBuildingArea()
        {
            if (!string.IsNullOrWhiteSpace(NewBuildingArea) && !ConfiguredBuildingAreas.Contains(NewBuildingArea.Trim()))
            {
                ConfiguredBuildingAreas.Add(NewBuildingArea.Trim());
                NewBuildingArea = string.Empty;
                SaveSettings(); // Auto save
            }
        }

        [RelayCommand]
        private void RemoveBuildingArea(string areaToRemove)
        {
            if (areaToRemove != null && ConfiguredBuildingAreas.Contains(areaToRemove))
            {
                ConfiguredBuildingAreas.Remove(areaToRemove);
                SaveSettings(); // Auto save
            }
        }

        [RelayCommand]
        private void EditChaletType(string typeToEdit)
        {
            if (string.IsNullOrWhiteSpace(typeToEdit)) return;
            // Put value in textbox for editing, mark as editing
            EditingChaletType = typeToEdit;
            NewChaletType = typeToEdit;
        }

        [RelayCommand]
        private void SaveChaletTypeEdit()
        {
            if (string.IsNullOrWhiteSpace(NewChaletType) || EditingChaletType == null) return;
            var newVal = NewChaletType.Trim();
            var oldVal = EditingChaletType.Trim();

            if (!string.Equals(newVal, oldVal, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using (var db = new AppDbContext())
                    {
                        // 1. Update in Chalets
                        var affectedChalets = db.Chalets.Where(c => c.ChaletType == oldVal).ToList();
                        foreach (var c in affectedChalets) c.ChaletType = newVal;

                        // 2. Update in Rates
                        var affectedRates = db.Rates.Where(r => r.ChaletType == oldVal).ToList();
                        foreach (var r in affectedRates) r.ChaletType = newVal;

                        // 3. Update in SpecialRates
                        var affectedSpecials = db.SpecialRates.Where(sr => sr.ChaletType == oldVal).ToList();
                        foreach (var sr in affectedSpecials) sr.ChaletType = newVal;

                        db.SaveChanges();
                    }

                    // Update local in-memory collections
                    var idx = ConfiguredChaletTypes.IndexOf(EditingChaletType);
                    if (idx >= 0) ConfiguredChaletTypes[idx] = newVal;
                    var ci = ChaletTypes.IndexOf(EditingChaletType);
                    if (ci >= 0) ChaletTypes[ci] = newVal;

                    SaveSettings();
                    StatusMessage = $"تم حفظ تعديل نوع الشاليه إلى '{newVal}' بنجاح";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"خطأ أثناء حفظ التعديل: {ex.Message}";
                }
            }

            EditingChaletType = null;
            NewChaletType = string.Empty;
        }

        [RelayCommand]
        private void CancelChaletTypeEdit()
        {
            EditingChaletType = null;
            NewChaletType = string.Empty;
        }

        [RelayCommand]
        private void EditBuildingArea(string areaToEdit)
        {
            if (string.IsNullOrWhiteSpace(areaToEdit)) return;
            EditingBuildingArea = areaToEdit;
            NewBuildingArea = areaToEdit;
        }

        [RelayCommand]
        private void SaveBuildingAreaEdit()
        {
            if (string.IsNullOrWhiteSpace(NewBuildingArea) || EditingBuildingArea == null) return;
            var newVal = NewBuildingArea.Trim();
            if (!ConfiguredBuildingAreas.Contains(newVal))
            {
                var idx = ConfiguredBuildingAreas.IndexOf(EditingBuildingArea);
                if (idx >= 0) ConfiguredBuildingAreas[idx] = newVal;
                SaveSettings();
            }
            EditingBuildingArea = null;
            NewBuildingArea = string.Empty;
        }

        [RelayCommand]
        private void CancelBuildingAreaEdit()
        {
            EditingBuildingArea = null;
            NewBuildingArea = string.Empty;
        }

        [RelayCommand]
        private void SearchArchive()
        {
            // Allow empty query to load recent bookings
            var results = BookingService.SearchBookings(SearchQuery);
            ArchiveResults = new ObservableCollection<Booking>(results);
        }

        [RelayCommand]
        private void ReprintInvoice(Booking? booking)
        {
            if (booking == null) return;
            var invoice = BookingService.GetInvoiceByBookingId(booking.Id);
            if (invoice != null) PrintRequested?.Invoke(invoice);
            else StatusMessage = "لا يوجد فاتورة محفوظة لهذا الحجز";
        }

        public void LoadReceptionists()
        {
            try
            {
                using var db = new AppDbContext();
                

                var list = db.Receptionists.OrderBy(r => r.Name).ToList();
                if (!list.Any())
                {
                    var initial = new[]
                    {
                        new Receptionist { Name = "أحمد إبراهيم", IsActive = true },
                        new Receptionist { Name = "محمد علي", IsActive = true }
                    };
                    db.Receptionists.AddRange(initial);
                    db.SaveChanges();
                    list = initial.ToList();
                }
                Receptionists = new ObservableCollection<Receptionist>(list);
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ في تحميل مسؤولي الاستقبال: {ex.Message}";
            }
        }

        [RelayCommand]
        private void AddReceptionist()
        {
            if (string.IsNullOrWhiteSpace(NewReceptionistName))
            {
                StatusMessage = "يرجى كتابة اسم مسؤول الاستقبال أولاً";
                return;
            }

            try
            {
                using var db = new AppDbContext();
                

                var receptionist = new Receptionist
                {
                    Name = NewReceptionistName.Trim(),
                    IsActive = true
                };
                db.Receptionists.Add(receptionist);
                db.SaveChanges();

                NewReceptionistName = string.Empty;
                LoadReceptionists();
                StatusMessage = "تم إضافة مسؤول الاستقبال بنجاح";
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ أثناء إضافة المسؤول: {ex.Message}";
            }
        }
        [RelayCommand]
        private void DeleteReceptionist(Receptionist? item)
        {
            if (item == null) return;
            using var db = new AppDbContext();
            var existing = db.Receptionists.Find(item.Id);
            if (existing != null)
            {
                db.Receptionists.Remove(existing);
                db.SaveChanges();
            }
            LoadReceptionists();
            StatusMessage = "تم حذف مسؤول الاستقبال";
        }

        [RelayCommand]
        private void EditReceptionist(Receptionist? item)
        {
            if (item == null) return;
            EditingReceptionist = item;
            NewReceptionistName = item.Name;
        }

        [RelayCommand]
        private void CancelReceptionistEdit()
        {
            EditingReceptionist = null;
            NewReceptionistName = string.Empty;
        }

        [RelayCommand]
        private void SaveReceptionistEdit()
        {
            if (EditingReceptionist == null) return;
            if (string.IsNullOrWhiteSpace(NewReceptionistName))
            {
                StatusMessage = "يرجى كتابة اسم مسؤول الاستقبال";
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var existing = db.Receptionists.Find(EditingReceptionist.Id);
                if (existing != null)
                {
                    existing.Name = NewReceptionistName.Trim();
                    db.SaveChanges();
                    StatusMessage = "✅ تم تعديل اسم مسؤول الاستقبال بنجاح";
                }
                EditingReceptionist = null;
                NewReceptionistName = string.Empty;
                LoadReceptionists();
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ أثناء حفظ التعديل: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ToggleReceptionistActive(Receptionist? item)
        {
            if (item == null) return;
            using var db = new AppDbContext();
            var existing = db.Receptionists.Find(item.Id);
            if (existing != null)
            {
                existing.IsActive = !existing.IsActive;
                db.SaveChanges();
            }
            LoadReceptionists();
        }
    }
}

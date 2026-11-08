using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EaglesResort.Data;
using EaglesResort.Models;
using EaglesResort.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EaglesResort.ViewModels
{
    public partial class MonthItem : ObservableObject
    {
        public int MonthNumber { get; set; }
        public string MonthName { get; set; } = string.Empty;
        [ObservableProperty] private bool _isSelected;
    }

    public partial class TimelineViewModel : ObservableObject
    {
        private List<Chalet> _allChalets = new();

        // Loading indicator for smooth async UI & Spinner
        [ObservableProperty] private bool _isLoading;

        // Stats
        [ObservableProperty] private int _totalChalets;
        [ObservableProperty] private int _availableCount;
        [ObservableProperty] private int _occupiedCount;
        [ObservableProperty] private int _maintenanceCount;
        [ObservableProperty] private int _cleaningCount;
        [ObservableProperty] private string _occupancyPercentage = "0%";
        [ObservableProperty] private ObservableCollection<Booking> _todayCheckouts = new();
        [ObservableProperty] private ObservableCollection<Booking> _activeBookings = new();
        [ObservableProperty] private ObservableCollection<Reservation> _todayArrivals = new();

        // 1. Chalet Types (STANDARD, PREMIUM, PLUS, DOUBLE, DELUXE)
        [ObservableProperty] private ObservableCollection<string> _chaletTypes = new();
        [ObservableProperty] private string _selectedChaletType = "STANDARD";

        // 2. Building Areas (Eagle1, Eagle2, Golden_Eagle, D) - بدون زر كل المباني
        [ObservableProperty] private ObservableCollection<string> _buildingAreas = new();
        [ObservableProperty] private string _selectedBuildingArea = string.Empty;

        // 3. Floor Filters (GROUND, FIRST, SECOND) - مخصصة بذكاء ديناميكياً بدون زر الكل
        [ObservableProperty] private ObservableCollection<string> _floorFilters = new();
        [ObservableProperty] private string _selectedFloorFilter = string.Empty;

        // Month & Year Filter
        [ObservableProperty] private int _selectedYear = DateTime.Today.Year;
        [ObservableProperty] private int _selectedMonth = DateTime.Today.Month;
        [ObservableProperty] private string _currentMonthDisplay = string.Empty;
        [ObservableProperty] private ObservableCollection<MonthItem> _monthsList = new();

        // Monthly Grid Data
        [ObservableProperty] private ObservableCollection<DateTime> _monthDates = new();
        [ObservableProperty] private ObservableCollection<MonthlyTimelineRow> _monthlyRows = new();

        public event Action<Chalet, DateTime>? NewReservationRequested;

        private static readonly string[] ArabicMonths = {
            "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
            "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"
        };

        private static readonly Dictionary<DayOfWeek, string> DayNameLetters = new()
        {
            { DayOfWeek.Saturday, "س" },
            { DayOfWeek.Sunday, "ح" },
            { DayOfWeek.Monday, "ن" },
            { DayOfWeek.Tuesday, "ث" },
            { DayOfWeek.Wednesday, "ر" },
            { DayOfWeek.Thursday, "خ" },
            { DayOfWeek.Friday, "ج" }
        };

        public TimelineViewModel()
        {
            InitializeMonthsList();
            _ = LoadDataAsync();
        }

        private void InitializeMonthsList()
        {
            var list = new List<MonthItem>();
            for (int i = 1; i <= 12; i++)
            {
                list.Add(new MonthItem
                {
                    MonthNumber = i,
                    MonthName = $"{ArabicMonths[i - 1]} ({i})",
                    IsSelected = (i == SelectedMonth)
                });
            }
            MonthsList = new ObservableCollection<MonthItem>(list);
        }

        public void LoadData() => _ = LoadDataAsync();
        public void BuildMonthlyGrid() => _ = BuildMonthlyGridAsync();

        public async Task LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        BookingService.UpdateBookingStatus();
                    }
                    catch { }

                    using var db = new AppDbContext();

                    // Ensure Seed & Missing Chalets like B-21 are inserted and B-11 is locked
                    try
                    {
                        db.SeedData();
                    }
                    catch { }

                    // 1. Load Chalets with Smart Numeric Sorting
                    _allChalets = db.Chalets.ToList()
                        .OrderBy(c => c.BuildingArea)
                        .ThenBy(c => ExtractNumericPart(c.Number))
                        .ThenBy(c => c.Number)
                        .ToList();

                    // Ensure B-11 in memory is marked as Occupied
                    var b11 = _allChalets.FirstOrDefault(c => c.Number.Trim() == "B-11");
                    if (b11 != null)
                    {
                        b11.Status = ChaletStatus.Occupied;
                        b11.Notes = "إقامة دائمة - السيد العميد";
                    }

                    var d8 = _allChalets.FirstOrDefault(c => c.Number.Trim() == "D-8");
                    if (d8 != null)
                    {
                        d8.Status = ChaletStatus.Occupied;
                        d8.Notes = "إقامة دائمة - النقيب احمد";
                    }
                });

                UpdateChaletStats();

                // 2. Load Chalet Types
                var distinctTypes = _allChalets
                    .Select(c => c.ChaletType?.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Distinct()
                    .Cast<string>()
                    .ToList();

                var order = new[] { "STANDARD", "PREMIUM", "PLUS", "DOUBLE", "DELUXE" };
                var sortedTypes = distinctTypes
                    .OrderBy(t => {
                        int idx = Array.IndexOf(order, t.ToUpperInvariant());
                        return idx >= 0 ? idx : 99;
                    })
                    .ToList();

                ChaletTypes = new ObservableCollection<string>(sortedTypes);

                if (string.IsNullOrEmpty(SelectedChaletType) || !ChaletTypes.Contains(SelectedChaletType))
                {
                    SelectedChaletType = ChaletTypes.FirstOrDefault() ?? "STANDARD";
                }

                // 3. Smart update of Building and Floor filters
                UpdateSmartFilters();

                // 4. Load Stats collections asynchronously
                await LoadStatsDataAsync();

                // 5. Build Monthly Grid
                await BuildMonthlyGridAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading Timeline: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadStatsDataAsync()
        {
            try
            {
                var today = DateTime.Today;
                var (checkouts, active, arrivals) = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    var todayCheckouts = BookingService.GetCheckoutsToday();
                    foreach (var b in todayCheckouts)
                    {
                        b.Chalet ??= _allChalets.FirstOrDefault(c => c.Id == b.ChaletId);
                    }

                    var activeList = db.Bookings
                        .Where(b => b.Status == BookingStatus.Active)
                        .OrderBy(b => b.CheckOutDate)
                        .ToList();
                    foreach (var b in activeList)
                    {
                        b.Chalet ??= _allChalets.FirstOrDefault(c => c.Id == b.ChaletId);
                    }

                    var arrList = db.Reservations
                        .Include(r => r.Chalet)
                        .Where(r => r.Status == ReservationStatus.Upcoming && r.CheckInDate.Date == today)
                        .OrderBy(r => r.CheckInDate)
                        .ToList();

                    return (todayCheckouts, activeList, arrList);
                });

                TodayCheckouts = new ObservableCollection<Booking>(checkouts);
                ActiveBookings = new ObservableCollection<Booking>(active);
                TodayArrivals = new ObservableCollection<Reservation>(arrivals);
            }
            catch { }
        }

        private void UpdateChaletStats()
        {
            TotalChalets = _allChalets.Count;
            AvailableCount = _allChalets.Count(c => c.Status == ChaletStatus.Available && c.Number.Trim() != "B-11" && c.Number.Trim() != "D-8");
            OccupiedCount = _allChalets.Count(c => c.Status == ChaletStatus.Occupied) - 2;
            MaintenanceCount = _allChalets.Count(c => c.Status == ChaletStatus.Maintenance);
            CleaningCount = _allChalets.Count(c => c.Status == ChaletStatus.Cleaning);
            OccupancyPercentage = TotalChalets > 0 ? $"{OccupiedCount * 100.0 / TotalChalets:F1}%" : "0%";
        }

        private void UpdateSmartFilters()
        {
            var chaletsOfType = _allChalets
                .Where(c => c.ChaletType != null && c.ChaletType.Trim().Equals(SelectedChaletType.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            var distinctBuildings = chaletsOfType
                .Select(c => c.BuildingArea?.Trim())
                .Where(a => !string.IsNullOrEmpty(a))
                .Distinct()
                .OrderBy(a => a)
                .Cast<string>()
                .ToList();

            BuildingAreas = new ObservableCollection<string>(distinctBuildings);

            if (string.IsNullOrEmpty(SelectedBuildingArea) || !BuildingAreas.Contains(SelectedBuildingArea))
            {
                if (BuildingAreas.Contains("Eagle1"))
                    SelectedBuildingArea = "Eagle1";
                else
                    SelectedBuildingArea = BuildingAreas.FirstOrDefault() ?? string.Empty;
            }

            UpdateFloorsForSelectedBuilding();
        }

        private void UpdateFloorsForSelectedBuilding()
        {
            var chaletsInBuilding = _allChalets
                .Where(c => c.ChaletType != null && c.ChaletType.Trim().Equals(SelectedChaletType.Trim(), StringComparison.OrdinalIgnoreCase) &&
                            c.BuildingArea != null && c.BuildingArea.Trim().Equals(SelectedBuildingArea.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            var distinctFloors = chaletsInBuilding
                .Select(c => GetFloorName(c.Number, c.ChaletType, c.BuildingArea))
                .Distinct()
                .ToList();

            var orderedFloors = new List<string>();
            if (distinctFloors.Contains("GROUND")) orderedFloors.Add("GROUND");
            if (distinctFloors.Contains("FIRST")) orderedFloors.Add("FIRST");
            if (distinctFloors.Contains("SECOND")) orderedFloors.Add("SECOND");

            FloorFilters = new ObservableCollection<string>(orderedFloors);

            if (string.IsNullOrEmpty(SelectedFloorFilter) || !FloorFilters.Contains(SelectedFloorFilter))
            {
                if (FloorFilters.Contains("GROUND"))
                    SelectedFloorFilter = "GROUND";
                else
                    SelectedFloorFilter = FloorFilters.FirstOrDefault() ?? string.Empty;
            }
        }

        private List<Chalet> GetFilteredChaletsList()
        {
            var targetChalets = _allChalets.AsEnumerable();

            if (!string.IsNullOrEmpty(SelectedChaletType))
            {
                targetChalets = targetChalets.Where(c => c.ChaletType != null && c.ChaletType.Trim().Equals(SelectedChaletType.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(SelectedBuildingArea))
            {
                targetChalets = targetChalets.Where(c => c.BuildingArea != null && c.BuildingArea.Trim().Equals(SelectedBuildingArea.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(SelectedFloorFilter))
            {
                targetChalets = targetChalets.Where(c => GetFloorName(c.Number, c.ChaletType, c.BuildingArea).Equals(SelectedFloorFilter, StringComparison.OrdinalIgnoreCase));
            }

            return targetChalets
                .OrderBy(c => c.BuildingArea)
                .ThenBy(c => ExtractNumericPart(c.Number))
                .ThenBy(c => c.Number)
                .ToList();
        }

        public static int ExtractNumericPart(string? chaletNumber)
        {
            if (string.IsNullOrEmpty(chaletNumber)) return 0;
            var match = Regex.Match(chaletNumber, @"\d+");
            return match.Success && int.TryParse(match.Value, out int result) ? result : 0;
        }

        public static string GetFloorName(string? chaletNumber, string? chaletType, string? buildingArea)
        {
            if (string.IsNullOrWhiteSpace(chaletNumber)) return "GROUND";
            string num = chaletNumber.Trim();
            string type = chaletType?.Trim().ToUpperInvariant() ?? "";

            // 1. DOUBLE: D-1..D-8 => FIRST, D-9..D-16 => SECOND
            if (type == "DOUBLE" || num.StartsWith("D-", StringComparison.OrdinalIgnoreCase) || buildingArea == "D")
            {
                int dNum = ExtractNumericPart(num);
                return dNum <= 8 ? "FIRST" : "SECOND";
            }

            // 2. PREMIUM: B-1..B-8 => GROUND
            if (type == "PREMIUM")
            {
                return "GROUND";
            }

            // 3. DELUXE: B-11..B-18 => FIRST, B-21..B-28 => SECOND
            if (type == "DELUXE")
            {
                int bNum = ExtractNumericPart(num);
                return bNum <= 18 ? "FIRST" : "SECOND";
            }

            // Fallback for B- numbers
            if (num.StartsWith("B-", StringComparison.OrdinalIgnoreCase))
            {
                int bNum = ExtractNumericPart(num);
                if (bNum <= 8) return "GROUND";
                if (bNum <= 18) return "FIRST";
                return "SECOND";
            }

            // 4. Eagle1 and Eagle2 numeric chalets
            if (int.TryParse(num, out int nVal))
            {
                int tens = (nVal % 100) / 10;
                if (tens == 0) return "GROUND";
                if (tens == 1) return "FIRST";
                if (tens == 2) return "SECOND";
            }

            return "GROUND";
        }

        public async Task BuildMonthlyGridAsync()
        {
            IsLoading = true;
            try
            {
                var year = SelectedYear;
                var month = SelectedMonth;
                var filteredList = GetFilteredChaletsList();

                var (dates, rows, display) = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    var daysCount = DateTime.DaysInMonth(year, month);
                    var startDate = new DateTime(year, month, 1);
                    var endDate = new DateTime(year, month, daysCount);
                    var monthTitle = $"{ArabicMonths[month - 1]} {year}";

                    var datesList = new List<DateTime>(daysCount);
                    for (int d = 1; d <= daysCount; d++)
                    {
                        datesList.Add(new DateTime(year, month, d));
                    }

                    var bookings = db.Bookings
                        .Where(b => b.Status != BookingStatus.Cancelled &&
                                    b.CheckInDate.Date <= endDate &&
                                    b.CheckOutDate.Date > startDate)
                        .ToList();

                    var reservations = db.Reservations
                        .Where(r => r.Status == ReservationStatus.Upcoming &&
                                    r.CheckInDate.Date <= endDate &&
                                    r.CheckOutDate.Date > startDate)
                        .ToList();

                    var bookingsLookup = bookings.ToLookup(b => b.ChaletId);
                    var reservationsLookup = reservations.ToLookup(r => r.ChaletId);

                    var today = DateTime.Today;
                    var generatedRows = new List<MonthlyTimelineRow>(filteredList.Count);

                    foreach (var chalet in filteredList)
                    {
                        var row = new MonthlyTimelineRow { Chalet = chalet };
                        var chaletBookings = bookingsLookup[chalet.Id].ToList();
                        var chaletReservations = reservationsLookup[chalet.Id].ToList();

                        bool isB11 = chalet.Number.Trim().Equals("B-11", StringComparison.OrdinalIgnoreCase);
                        bool isD8 = chalet.Number.Trim().Equals("D-8", StringComparison.OrdinalIgnoreCase);

                        var cells = new List<MonthlyTimelineCell>(daysCount);

                        for (int d = 1; d <= daysCount; d++)
                        {
                            var cellDate = datesList[d - 1];
                            var isToday = cellDate.Date == today;
                            string dayLetter = DayNameLetters.TryGetValue(cellDate.DayOfWeek, out var dl) ? dl : "";

                            // B-11: مغلق دائماً (إقامة دائمة - السيد العميد)
                            if (isB11)
                            {
                                cells.Add(new MonthlyTimelineCell
                                {
                                    Date = cellDate,
                                    DayNameArabic = dayLetter,
                                    Chalet = chalet,
                                    Booking = null,
                                    Reservation = null,
                                    IsPermanentResidence = true,
                                    IsToday = isToday,
                                    TooltipText = "🔒 شاليه (B-11) - DELUXE\nإقامة دائمة (السيد العميد)\nمحجوز دائماً ومغلق للحجز"
                                });
                                continue;
                            }

                            if (isD8)
                            {
                                cells.Add(new MonthlyTimelineCell
                                {
                                    Date = cellDate,
                                    DayNameArabic = dayLetter,
                                    Chalet = chalet,
                                    Booking = null,
                                    Reservation = null,
                                    IsPermanentResidence = true,
                                    IsToday = isToday,
                                    TooltipText = "🔒 شاليه (D-8) - DELUXE\nإقامة دائمة (النقيب احمد صلاح )\nمحجوز دائماً ومغلق للحجز"
                                });
                                continue;
                            }

                            var activeBooking = chaletBookings.FirstOrDefault(b => b.CheckInDate.Date <= cellDate && b.CheckOutDate.Date > cellDate);
                            var preRes = chaletReservations.FirstOrDefault(r => r.CheckInDate.Date <= cellDate && r.CheckOutDate.Date > cellDate);

                            string tooltip = activeBooking != null
                                ? $"🏨 إقامة نشطة (مسكن حالياً)\nالنزيل: {activeBooking.GuestName}\nالهاتف: {activeBooking.PhoneNumber}\nالشاليه: ({chalet.Number}) - {chalet.ChaletType}\nالوصول: {activeBooking.CheckInDate:dd/MM/yyyy}\nالمغادرة: {activeBooking.CheckOutDate:dd/MM/yyyy}\nرقم الفاتورة: #{activeBooking.InvoiceId}"
                                : preRes != null
                                ? $"⏳ حجز مسبق (عربون: {preRes.Deposit:N0} ج.م)\nالعميل: {preRes.GuestName}\nالهاتف: {preRes.PhoneNumber}\nالشاليه: ({chalet.Number}) - {chalet.ChaletType}\nالوصول: {preRes.CheckInDate:dd/MM/yyyy}\nالمغادرة: {preRes.CheckOutDate:dd/MM/yyyy}\nحجز بتاريخ: {preRes.CreatedAt:dd/MM/yyyy hh:mm tt}"
                                : $"🟢 متاح للحجز\nالشاليه: ({chalet.Number}) - {chalet.ChaletType}\nالتاريخ: {cellDate:dd/MM/yyyy}\nاضغط لإضافة حجز مسبق جديد";

                            cells.Add(new MonthlyTimelineCell
                            {
                                Date = cellDate,
                                DayNameArabic = dayLetter,
                                Chalet = chalet,
                                Booking = activeBooking,
                                Reservation = preRes,
                                IsToday = isToday,
                                TooltipText = tooltip
                            });
                        }

                        row.Cells = new ObservableCollection<MonthlyTimelineCell>(cells);
                        generatedRows.Add(row);
                    }

                    return (datesList, generatedRows, monthTitle);
                });

                MonthDates = new ObservableCollection<DateTime>(dates);
                MonthlyRows = new ObservableCollection<MonthlyTimelineRow>(rows);
                CurrentMonthDisplay = display;

                // تحديث تحديد الشهر فورياً ليعكس الشهر المختار
                UpdateMonthSelection();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error building monthly grid: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateMonthSelection()
        {
            foreach (var m in MonthsList)
            {
                m.IsSelected = (m.MonthNumber == SelectedMonth);
            }
        }

        // ===== COMMANDS =====
        [RelayCommand]
        public async Task SelectTypeFilter(string? type)
        {
            if (!string.IsNullOrEmpty(type) && SelectedChaletType != type)
            {
                SelectedChaletType = type;
                UpdateSmartFilters();
                await BuildMonthlyGridAsync();
            }
        }

        [RelayCommand]
        public async Task SelectBuildingArea(string? area)
        {
            if (!string.IsNullOrEmpty(area) && SelectedBuildingArea != area)
            {
                SelectedBuildingArea = area;
                UpdateFloorsForSelectedBuilding();
                await BuildMonthlyGridAsync();
            }
        }

        [RelayCommand]
        public async Task SelectFloorFilter(string? floor)
        {
            if (!string.IsNullOrEmpty(floor) && SelectedFloorFilter != floor)
            {
                SelectedFloorFilter = floor;
                await BuildMonthlyGridAsync();
            }
        }

        [RelayCommand]
        public async Task SelectMonth(object? monthObj)
        {
            int m = 0;
            if (monthObj is MonthItem mi)
            {
                m = mi.MonthNumber;
            }
            else if (monthObj != null && int.TryParse(monthObj.ToString(), out var parsedMonth))
            {
                m = parsedMonth;
            }

            if (m >= 1 && m <= 12 && m != SelectedMonth)
            {
                SelectedMonth = m;
                UpdateMonthSelection();
                await BuildMonthlyGridAsync();
            }
        }

        [RelayCommand]
        public async Task PreviousMonth()
        {
            if (SelectedMonth > 1)
            {
                SelectedMonth--;
            }
            else
            {
                SelectedMonth = 12;
                SelectedYear--;
            }
            UpdateMonthSelection();
            await BuildMonthlyGridAsync();
        }

        [RelayCommand]
        public async Task NextMonth()
        {
            if (SelectedMonth < 12)
            {
                SelectedMonth++;
            }
            else
            {
                SelectedMonth = 1;
                SelectedYear++;
            }
            UpdateMonthSelection();
            await BuildMonthlyGridAsync();
        }

        [RelayCommand]
        public async Task GoToCurrentMonth()
        {
            var today = DateTime.Today;
            if (SelectedYear != today.Year || SelectedMonth != today.Month)
            {
                SelectedYear = today.Year;
                SelectedMonth = today.Month;
                UpdateMonthSelection();
                await BuildMonthlyGridAsync();
            }
        }

        [RelayCommand]
        public void CellClick(MonthlyTimelineCell? cell)
        {
            if (cell == null || cell.IsPermanentResidence || !cell.IsAvailable) return;

            NewReservationRequested?.Invoke(cell.Chalet, cell.Date);
        }
    }

    public class MonthlyTimelineRow : ObservableObject
    {
        public Chalet Chalet { get; set; } = null!;
        public string ChaletDisplay => $"شاليه ({Chalet.Number})";
        public ObservableCollection<MonthlyTimelineCell> Cells { get; set; } = new();
    }

    public class MonthlyTimelineCell : ObservableObject
    {
        public DateTime Date { get; set; }
        public int DayNumber => Date.Day;
        public string DayNameArabic { get; set; } = string.Empty;
        public Chalet Chalet { get; set; } = null!;

        public Booking? Booking { get; set; }
        public Reservation? Reservation { get; set; }
        public bool IsPermanentResidence { get; set; }

        public bool IsOccupied => IsPermanentResidence || Booking != null;
        public bool IsReserved => !IsPermanentResidence && Reservation != null && Booking == null;
        public bool IsAvailable => !IsPermanentResidence && !IsOccupied && !IsReserved;
        public bool IsToday { get; set; }

        public string GuestName =>
            (IsPermanentResidence && Chalet.Number.Trim() == "B-11") ? "العميد" :
            IsPermanentResidence ? "النقيب" :
            Booking?.GuestName ?? Reservation?.GuestName ?? string.Empty; 
        
        public string StatusBadge =>
            (IsPermanentResidence && Chalet.Number.Trim() == "B-11") ? "العميد" :
            IsOccupied ? "مسكن" :
            IsReserved ? "حجز" :
            "النقيب"; 

        public string TooltipText { get; set; } = string.Empty;
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EaglesResort.Data;
using EaglesResort.Models;
using EaglesResort.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace EaglesResort.ViewModels
{
    public partial class ChaletsDirectoryViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<Chalet> _chalets = new();
        [ObservableProperty] private bool _isDialogOpen;
        [ObservableProperty] private string _dialogTitle = string.Empty;
        [ObservableProperty] private Chalet _editingChalet = new();
        [ObservableProperty] private ObservableCollection<string> _allChaletTypes = new();

        [ObservableProperty]
        private ObservableCollection<Chalet> _filteredChalets = new();

        [ObservableProperty]
        private Chalet? _selectedChalet;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _filterBuilding = "الكل";

        [ObservableProperty]
        private string _filterStatus = "الكل";

        [ObservableProperty]
        private string _filterType = "الكل";

        public ObservableCollection<string> Buildings { get; } = new() { "الكل" };
        public ObservableCollection<string> ChaletTypes { get; } = new() { "الكل" };
        public ObservableCollection<string> Statuses { get; } = new() { "الكل", "متاح", "مشغول", "قيد الصيانة", "تنظيف" };

        public event Action<Chalet>? NewBookingRequested;
        public event Action<Chalet>? ViewBookingsRequested;

        public ChaletsDirectoryViewModel()
        {
            LoadChalets();
        }

        public void LoadChalets()
        {
            using var db = new AppDbContext();
            var chalets = db.Chalets.OrderBy(c => c.BuildingArea).ThenBy(c => c.Number).ToList();
            Chalets = new ObservableCollection<Chalet>(chalets);

            Buildings.Clear(); Buildings.Add("الكل");
            ChaletTypes.Clear(); ChaletTypes.Add("الكل");
            AllChaletTypes.Clear();
            // Optional: AllBuildingAreas if you want it
            
            var settings = db.AppSettings.ToList();
            var savedTypesStr = settings.FirstOrDefault(s => s.Key == "ChaletTypesConfig")?.Value;
            var configuredTypes = string.IsNullOrEmpty(savedTypesStr) 
                ? new List<string>() 
                : savedTypesStr.Split(',').Where(x => !string.IsNullOrEmpty(x.Trim())).Select(x => x.Trim()).ToList();

            foreach (var b in chalets.Select(c => c.BuildingArea).Distinct().Where(x => !string.IsNullOrEmpty(x)))
                if (!Buildings.Contains(b)) Buildings.Add(b);
            
            foreach (var t in chalets.Select(c => c.ChaletType).Distinct().Where(x => !string.IsNullOrEmpty(x)))
                if (!ChaletTypes.Contains(t)) ChaletTypes.Add(t);

            // Merge DB types with configured types for the DropDowns
            var finalTypes = configuredTypes.Union(ChaletTypes.Where(t => t != "الكل")).ToList();
            if (!finalTypes.Any()) finalTypes = new List<string> { "زوجي مميز", "استوديو", "فردي" };
            
            foreach (var t in finalTypes)
                AllChaletTypes.Add(t);

            ApplyFilter();
        }

        [RelayCommand]
        private void AddChalet()
        {
            EditingChalet = new Chalet { ChaletType = AllChaletTypes.FirstOrDefault() ?? "عادي" };
            DialogTitle = "إضافة شاليه جديد";
            IsDialogOpen = true;
        }

        [RelayCommand]
        private void EditChalet(Chalet chalet)
        {
            if (chalet == null) return;
            EditingChalet = new Chalet
            {
                Id = chalet.Id,
                Number = chalet.Number,
                BuildingArea = chalet.BuildingArea,
                ChaletType = chalet.ChaletType,
                RoomsCount = chalet.RoomsCount,
                MaxCapacity = chalet.MaxCapacity,
                Notes = chalet.Notes,
                Status = chalet.Status
            };
            DialogTitle = "تعديل الشاليه";
            IsDialogOpen = true;
        }

        [RelayCommand]
        private void SaveChalet()
        {
            if (string.IsNullOrWhiteSpace(EditingChalet.Number))
                return;

            using var db = new AppDbContext();
            if (EditingChalet.Id == 0)
            {
                // New
                db.Chalets.Add(EditingChalet);
            }
            else
            {
                // Update
                var existing = db.Chalets.Find(EditingChalet.Id);
                if (existing != null)
                {
                    existing.Number = EditingChalet.Number;
                    existing.BuildingArea = EditingChalet.BuildingArea;
                    existing.ChaletType = EditingChalet.ChaletType;
                    existing.RoomsCount = EditingChalet.RoomsCount;
                    existing.MaxCapacity = EditingChalet.MaxCapacity;
                    existing.Notes = EditingChalet.Notes;
                }
            }

            db.SaveChanges();
            IsDialogOpen = false;
            LoadChalets();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnFilterBuildingChanged(string value) => ApplyFilter();
        partial void OnFilterStatusChanged(string value) => ApplyFilter();
        partial void OnFilterTypeChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            var filtered = Chalets.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
                filtered = filtered.Where(c =>
                    c.Number.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    c.ChaletType.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

            if (FilterBuilding != "الكل")
                filtered = filtered.Where(c => c.BuildingArea == FilterBuilding);

            if (FilterStatus != "الكل")
            {
                var status = FilterStatus switch
                {
                    "متاح" => ChaletStatus.Available,
                    "مشغول" => ChaletStatus.Occupied,
                    "قيد الصيانة" => ChaletStatus.Maintenance,
                    "تنظيف" => ChaletStatus.Cleaning,
                    _ => (ChaletStatus?)null
                };
                if (status.HasValue) filtered = filtered.Where(c => c.Status == status.Value);
            }

            if (FilterType != "الكل")
                filtered = filtered.Where(c => c.ChaletType == FilterType);

            FilteredChalets = new ObservableCollection<Chalet>(filtered);
        }

        [RelayCommand]
        private void ChangeStatus(Chalet? chalet)
        {
            if (chalet == null) return;
            // Cycle through statuses
            chalet.Status = chalet.Status switch
            {
                ChaletStatus.Available => ChaletStatus.Maintenance,
                ChaletStatus.Maintenance => ChaletStatus.Cleaning,
                ChaletStatus.Cleaning => ChaletStatus.Available,
                ChaletStatus.Occupied => ChaletStatus.Cleaning,
                _ => ChaletStatus.Available
            };

            using var db = new AppDbContext();
            var dbChalet = db.Chalets.Find(chalet.Id);
            if (dbChalet != null) { dbChalet.Status = chalet.Status; db.SaveChanges(); }
            OnPropertyChanged(nameof(FilteredChalets));
            ApplyFilter();
        }

        [RelayCommand]
        private void NewBooking(Chalet? chalet)
        {
            if (chalet != null) NewBookingRequested?.Invoke(chalet);
        }

        [RelayCommand]
        private void ViewBookings(Chalet? chalet)
        {
            if (chalet != null) ViewBookingsRequested?.Invoke(chalet);
        }

        [RelayCommand]
        private void SaveChaletNotes(Chalet? chalet)
        {
            if (chalet == null) return;
            using var db = new AppDbContext();
            var dbChalet = db.Chalets.Find(chalet.Id);
            if (dbChalet != null) { dbChalet.Notes = chalet.Notes; db.SaveChanges(); }
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using EaglesResort.Models;
using EaglesResort.Services;

namespace EaglesResort.ViewModels
{
    public partial class ChaletBookingItem : ObservableObject
    {
        [ObservableProperty] private int _itemIndex = 1;
        [ObservableProperty] private Chalet? _selectedChalet;
        [ObservableProperty] private DateTime _checkInDate = DateTime.Today;
        [ObservableProperty] private DateTime _checkOutDate = DateTime.Today.AddDays(3);
        [ObservableProperty] private decimal _dailyRate;
        [ObservableProperty] private decimal _deposit;
        [ObservableProperty] private DateTime? _depositDate;
        [ObservableProperty] private int _numberOfDays;
        [ObservableProperty] private decimal _chaletTotal;
        [ObservableProperty] private bool _canDelete = false;

        // Per-chalet guests — used when UsePerChaletGuests = true on parent ViewModel
        [ObservableProperty] private int _adultCount = 1;
        [ObservableProperty] private int _childCount = 0;

        public event Action? Changed;

        public void Recalculate(string guestCategory)
        {
            NumberOfDays = CheckOutDate > CheckInDate ? (CheckOutDate - CheckInDate).Days : 0;
            if (SelectedChalet != null && DailyRate <= 0)
            {
                var rates = BookingService.GetRate(SelectedChalet.ChaletType, CheckInDate.Month, guestCategory);
                DailyRate = rates.Daily;
            }
            ChaletTotal = NumberOfDays * DailyRate;
        }

        partial void OnSelectedChaletChanged(Chalet? value)
        {
            DailyRate = 0; // Trigger reload for the new chalet type
            Changed?.Invoke();
        }

        partial void OnCheckInDateChanged(DateTime value) => Changed?.Invoke();
        partial void OnCheckOutDateChanged(DateTime value) => Changed?.Invoke();
        partial void OnDailyRateChanged(decimal value)
        {
            ChaletTotal = NumberOfDays * DailyRate;
            Changed?.Invoke();
        }
        partial void OnDepositChanged(decimal value) => Changed?.Invoke();
        partial void OnDepositDateChanged(DateTime? value) => Changed?.Invoke();
        partial void OnAdultCountChanged(int value) => Changed?.Invoke();
        partial void OnChildCountChanged(int value) => Changed?.Invoke();
    }
}

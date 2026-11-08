using EaglesResort.Models;
using EaglesResort.ViewModels;
using System;
using System.Windows.Controls;

namespace EaglesResort.Views.Pages
{
    public partial class TimelinePage : Page
    {
        private TimelineViewModel _vm;
        public event Action<Chalet, DateTime>? NewReservationRequested;

        public TimelinePage()
        {
            InitializeComponent();
            _vm = new TimelineViewModel();
            _vm.NewReservationRequested += (chalet, date) => NewReservationRequested?.Invoke(chalet, date);
            DataContext = _vm;
        }

        private void ShowActiveBookings_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var win = new Windows.ActiveBookingsWindow(_vm);
            win.Owner = System.Windows.Window.GetWindow(this);
            win.ShowDialog();
        }

                private void ShowTodayBookings_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var win = new Windows.TodayBookingsWindow();
            win.Owner = System.Windows.Window.GetWindow(this);
            win.ViewModel.DataChanged += () => _vm.LoadData();
            win.ShowDialog();
            _vm.LoadData();
        }

        private void ShowTodayCheckouts_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var win = new Windows.TodayCheckoutsWindow(_vm);
            win.Owner = System.Windows.Window.GetWindow(this);
            win.ShowDialog();
        }
    }
}

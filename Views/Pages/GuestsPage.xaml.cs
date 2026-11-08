using System.Windows.Controls;
using EaglesResort.ViewModels;
using EaglesResort.Data;

namespace EaglesResort.Views.Pages
{
    public partial class GuestsPage : Page
    {
        private GuestsViewModel _vm;

        public event Action<Models.Booking>? ExtendBookingRequested;

        public GuestsPage()
        {
            InitializeComponent();
            _vm = new GuestsViewModel();
            _vm.ExtendRequested = booking => ExtendBookingRequested?.Invoke(booking);
            DataContext = _vm;
        }

        // Called when user finishes editing any cell — saves to DB
        private void ActiveGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            if (e.Row.Item is not BookingRow row) return;

            // Small delay so binding updates first
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
            {
                _vm.SaveRowCommand.Execute(row);
            });
        }
    }
}

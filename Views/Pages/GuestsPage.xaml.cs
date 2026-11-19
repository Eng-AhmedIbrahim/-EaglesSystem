using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using EaglesResort.Data;
using EaglesResort.ViewModels;

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

        /// <summary>
        /// قائمة الإجراءات المنسدلة لكل صف: عرض التفاصيل، تعديل الفاتورة،
        /// طباعة، تمديد الإقامة، تسجيل مغادرة، وإلغاء الفاتورة.
        /// </summary>
        private void RowActions_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.DataContext is not BookingRow row) return;

            var menu = new ContextMenu
            {
                Style = (Style)FindResource("RowActionsContextMenu"),
                FlowDirection = FlowDirection.RightToLeft,
                PlacementTarget = fe,
                Placement = PlacementMode.Bottom
            };

            menu.Items.Add(MakeItem("👁   عرض تفاصيل الدفع", _vm.ViewPaymentDetailsCommand, row));
            menu.Items.Add(MakeItem("✏   تعديل الفاتورة", _vm.EditInvoiceCommand, row));
            menu.Items.Add(MakeItem("🖨   طباعة الفاتورة", _vm.PrintInvoiceCommand, row));

            menu.Items.Add(MakeSeparator());

            var extend = MakeItem("⏰   تمديد الإقامة", _vm.ExtendStayCommand, row);
            var checkout = MakeItem("🚪   تسجيل مغادرة", _vm.CheckoutCommand, row);

            var isActive = row.Booking.Status == Models.BookingStatus.Active
                        || row.Booking.Status == Models.BookingStatus.Upcoming;
            extend.IsEnabled = isActive;
            checkout.IsEnabled = isActive;

            menu.Items.Add(extend);
            menu.Items.Add(checkout);

            menu.Items.Add(MakeSeparator());
            menu.Items.Add(MakeItem("✖   إلغاء الفاتورة", _vm.CancelInvoiceCommand, row, "#F87171"));

            menu.IsOpen = true;
        }

        private static MenuItem MakeItem(string header, ICommand command, object parameter,
                                         string? dangerForeground = null)
        {
            var item = new MenuItem
            {
                Header = header,
                Command = command,
                CommandParameter = parameter
            };

            if (!string.IsNullOrEmpty(dangerForeground))
                item.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dangerForeground));

            return item;
        }

        private Separator MakeSeparator() =>
            new() { Style = (Style)FindResource("RowActionsMenuSeparator") };
    }
}

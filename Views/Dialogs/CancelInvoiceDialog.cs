using System.Windows;
using System.Windows.Input;
using EaglesResort.Services;

namespace EaglesResort.Views.Dialogs
{
    public partial class CancelInvoiceDialog : Window
    {
        public string Reason { get; private set; } = string.Empty;

        /// <summary>اسم من قام بالإلغاء (إلزامي) — يُسجَّل في سجل الحركات وعلى الفاتورة.</summary>
        public string CancelledBy { get; private set; } = string.Empty;

        public CancelInvoiceDialog(string invoiceNumber, string guestName)
        {
            InitializeComponent();
            SubtitleText.Text = $"فاتورة رقم {invoiceNumber} — النزيل: {guestName}";
            CancelledByBox.Text = InvoiceService.CurrentUserName;
            ReasonBox.Focus();
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CancelledByBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (ErrorText != null && ErrorText.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(CancelledByBox.Text))
                ErrorText.Visibility = Visibility.Collapsed;
        }

        private void ReasonBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (ErrorText != null && ErrorText.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(ReasonBox.Text))
                ErrorText.Visibility = Visibility.Collapsed;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            var cancelledBy = CancelledByBox.Text?.Trim() ?? string.Empty;
            if (cancelledBy.Length < 3)
            {
                ErrorText.Text = "من فضلك اكتب اسم من قام بالإلغاء (3 أحرف على الأقل) — إلزامي ليتم تسجيله في سجل الحركات.";
                ErrorText.Visibility = Visibility.Visible;
                CancelledByBox.Focus();
                return;
            }

            var reason = ReasonBox.Text?.Trim() ?? string.Empty;
            if (reason.Length < 3)
            {
                ErrorText.Text = "من فضلك اكتب سبب الإلغاء (3 أحرف على الأقل) ليتم تسجيله في سجل الحركات.";
                ErrorText.Visibility = Visibility.Visible;
                ReasonBox.Focus();
                return;
            }

            CancelledBy = cancelledBy;
            Reason = reason;
            DialogResult = true;
        }

        private void Back_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}

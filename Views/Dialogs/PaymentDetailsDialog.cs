using System;
using System.Windows;
using System.Windows.Input;
using EaglesResort.Models;
using EaglesResort.Services;

namespace EaglesResort.Views.Dialogs
{
    public partial class PaymentDetailsDialog : Window
    {
        private Invoice _invoice;

        public PaymentDetailsDialog(Invoice invoice)
        {
            _invoice = invoice ?? throw new ArgumentNullException(nameof(invoice));
            InitializeComponent();
            DataContext = _invoice;

            // تعديل الفاتورة متاح لمدير النظام فقط
            EditInvoiceButton.Visibility = SessionService.Instance.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PrintService.PrintInvoice(_invoice);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"تعذر طباعة الفاتورة: {ex.Message}";
            }
        }

        private void EditInvoice_Click(object sender, RoutedEventArgs e)
        {
            var editor = new InvoiceEditWindow(_invoice) { Owner = this };
            if (editor.ShowDialog() != true) return;

            var result = InvoiceService.UpdateInvoice(editor.EditedInvoice, editor.EditorName);
            if (!result.Success)
            {
                StatusText.Text = result.Message;
                return;
            }

            var refreshed = InvoiceService.GetById(_invoice.Id);
            if (refreshed != null) _invoice = refreshed;
            DataContext = _invoice;
            StatusText.Text = result.Message;

            // طباعة الفاتورة بعد التعديل وهي مختومة "فاتورة معدلة"
            try
            {
                PrintService.PrintInvoice(_invoice);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"{result.Message} — تعذر طباعة الفاتورة: {ex.Message}";
            }
        }
    }
}

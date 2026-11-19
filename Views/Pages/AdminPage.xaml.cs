using EaglesResort.Services;
using EaglesResort.ViewModels;
using System.Windows.Controls;

namespace EaglesResort.Views.Pages
{
    public partial class AdminPage : Page
    {
        private AdminViewModel _vm;

        public AdminPage()
        {
            InitializeComponent();
            _vm = new AdminViewModel();
            _vm.PrintRequested += invoice =>
            {
                try
                {
                    PrintService.PrintInvoice(invoice);
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show($"تعذر طباعة الفاتورة: {ex.Message}", "تنبيه",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            };
            DataContext = _vm;
        }
    }
}

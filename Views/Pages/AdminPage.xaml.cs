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
            _vm.PrintRequested += invoice => PrintService.PrintInvoice(invoice);
            DataContext = _vm;
        }
    }
}

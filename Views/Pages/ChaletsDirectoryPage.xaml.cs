using EaglesResort.Models;
using EaglesResort.ViewModels;
using System.Windows.Controls;

namespace EaglesResort.Views.Pages
{
    public partial class ChaletsDirectoryPage : Page
    {
        private ChaletsDirectoryViewModel _vm;
        public event Action<Chalet>? NewBookingRequested;

        public ChaletsDirectoryPage()
        {
            InitializeComponent();
            _vm = new ChaletsDirectoryViewModel();
            _vm.NewBookingRequested += chalet => NewBookingRequested?.Invoke(chalet);
            DataContext = _vm;
        }
    }
}

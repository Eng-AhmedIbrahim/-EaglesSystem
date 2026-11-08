using System.Windows.Controls;

namespace EaglesResort.Views.Pages
{
    public partial class UsersPage : Page
    {
        public UsersPage()
        {
            InitializeComponent();
            DataContext = new EaglesResort.ViewModels.UsersViewModel();
        }
    }
}

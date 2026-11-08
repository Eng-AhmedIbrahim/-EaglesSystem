using EaglesResort.Models;
using EaglesResort.ViewModels;
using System.Windows.Controls;

namespace EaglesResort.Views.Pages
{
    public partial class ReservationsPage : Page
    {
        public ReservationsViewModel ViewModel { get; }

        public ReservationsPage()
        {
            InitializeComponent();
            ViewModel = new ReservationsViewModel();
            DataContext = ViewModel;

            Loaded += (s, e) => ViewModel.LoadData();
        }

        // Constructor يُفتح مباشرة على مودال حجز جديد مع شاليه وتاريخ مملوء
        public ReservationsPage(Chalet preselectedChalet, System.DateTime checkInDate)
        {
            InitializeComponent();
            ViewModel = new ReservationsViewModel();
            DataContext = ViewModel;

            Loaded += (s, e) =>
            {
                ViewModel.LoadData();
                ViewModel.OpenNewReservationPreFilled(preselectedChalet, checkInDate);
            };
        }
    }
}

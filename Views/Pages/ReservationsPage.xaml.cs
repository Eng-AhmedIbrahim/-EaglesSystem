using EaglesResort.Models;
using EaglesResort.ViewModels;
using System.Windows.Controls;

namespace EaglesResort.Views.Pages
{
    public partial class ReservationsPage : Page
    {
        public ReservationsViewModel ViewModel { get; }

        // يُطلق عند إلغاء مودال الحجز الجديد (للرجوع للوحة التحكم)
        public event EventHandler? CancelNewReservationRequested;

        public ReservationsPage()
        {
            InitializeComponent();
            ViewModel = new ReservationsViewModel();
            DataContext = ViewModel;
            ViewModel.NewReservationCancelled += (s, e) => CancelNewReservationRequested?.Invoke(this, EventArgs.Empty);

            Loaded += (s, e) => ViewModel.LoadData();
        }

        // Constructor يُفتح مباشرة على مودال حجز جديد مع شاليه وتاريخ مملوء
        public ReservationsPage(Chalet preselectedChalet, System.DateTime checkInDate)
        {
            InitializeComponent();
            ViewModel = new ReservationsViewModel();
            DataContext = ViewModel;
            ViewModel.NewReservationCancelled += (s, e) => CancelNewReservationRequested?.Invoke(this, EventArgs.Empty);

            Loaded += (s, e) =>
            {
                ViewModel.LoadData();
                ViewModel.OpenNewReservationPreFilled(preselectedChalet, checkInDate);
            };
        }
    }
}

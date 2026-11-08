using EaglesResort.Models;
using EaglesResort.Services;
using EaglesResort.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace EaglesResort.Views.Pages
{
    public partial class BookingPage : Page
    {
        private BookingViewModel _vm;
        private Invoice? _lastSavedInvoice;

        public BookingPage(Chalet? preselectedChalet = null)
        {
            InitializeComponent();
            _vm = preselectedChalet != null
                ? new BookingViewModel(preselectedChalet)
                : new BookingViewModel();

            SetupViewModel();
        }

        public BookingPage(Booking existingBooking)
        {
            InitializeComponent();
            _vm = new BookingViewModel(existingBooking);
            SetupViewModel();
        }

        public BookingPage(Reservation reservation)
        {
            InitializeComponent();
            _vm = new BookingViewModel(reservation);
            SetupViewModel();
        }

        private void SetupViewModel()
        {
            _vm.BookingSaved += invoice =>
            {
                _lastSavedInvoice = invoice;
                
                string? savedPrinter = null;
                using (var db = new Data.AppDbContext())
                {
                    savedPrinter = db.AppSettings.FirstOrDefault(s => s.Key == "DefaultPrinter")?.Value;
                }

                PrintService.PrintInvoice(_lastSavedInvoice, savedPrinter);
            };
            DataContext = _vm;
        }

        private void PrintBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_lastSavedInvoice == null)
            {
                MessageBox.Show("الرجاء حفظ الحجز أولاً قبل الطباعة", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Load saved printer
            string? savedPrinter = null;
            using (var db = new Data.AppDbContext())
            {
                savedPrinter = db.AppSettings.FirstOrDefault(s => s.Key == "DefaultPrinter")?.Value;
            }

            PrintService.PrintInvoice(_lastSavedInvoice, savedPrinter);
        }
    }
}

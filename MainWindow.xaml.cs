using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EaglesResort.Models;
using EaglesResort.Services;
using EaglesResort.Views.Pages;

namespace EaglesResort
{
    public partial class MainWindow : Window
    {
        public static MainWindow? Instance { get; private set; }

        private TimelinePage? _timelinePage;
        private ChaletsDirectoryPage? _chaletsPage;
        private BookingPage? _bookingPage;
        private ReservationsPage? _reservationsPage;
        private AdminPage? _adminPage;
        private GuestsPage? _guestsPage;
        private UsersPage? _usersPage;

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Display user info
            var user = SessionService.Instance.CurrentUser;
            if (user != null)
            {
                UserNameText.Text = user.FullName;
                UserRoleText.Text = user.Role == "Admin" ? "مدير النظام" : "موظف استقبال";

                if (user.Role != "Admin")
                {
                    NavAdmin.Visibility = Visibility.Collapsed;
                    NavUsers.Visibility = Visibility.Collapsed;
                }
            }

            CurrentDateText.Text = DateTime.Now.ToString("dddd، d MMMM yyyy", new System.Globalization.CultureInfo("ar"));

            // Navigate to timeline by default
            NavigateToTimeline();
        }

        private void NavigateToTimeline()
        {
            PageTitle.Text = "لوحة التحكم والجدول الزمني";
            _timelinePage = new TimelinePage();
            _timelinePage.NewReservationRequested += (chalet, date) => NavigateToReservationsWithChalet(chalet, date);
            MainFrame.Navigate(_timelinePage);
            SetActiveNav(NavTimeline);
        }

        private void NavigateToChalets()
        {
            PageTitle.Text = "دليل جميع الشاليهات";
            _chaletsPage = new ChaletsDirectoryPage();
            _chaletsPage.NewBookingRequested += chalet => NavigateToBooking(chalet);
            MainFrame.Navigate(_chaletsPage);
            SetActiveNav(NavChalets);
        }

        public void NavigateToBookingForExisting(Booking booking)
        {
            PageTitle.Text = "الفاتورة وإصدار الحسابات";
            _bookingPage = new BookingPage(booking);
            MainFrame.Navigate(_bookingPage);
            SetActiveNav(NavBooking);
        }

        public void NavigateToBookingForReservation(Reservation reservation)
        {
            PageTitle.Text = "الفاتورة وإصدار الحسابات";
            _bookingPage = new BookingPage(reservation);
            MainFrame.Navigate(_bookingPage);
            SetActiveNav(NavBooking);
        }

        private void NavigateToBooking(Chalet? preselectedChalet = null)
        {
            PageTitle.Text = "الفاتورة وإصدار الحسابات";
            _bookingPage = new BookingPage(preselectedChalet);
            MainFrame.Navigate(_bookingPage);
            SetActiveNav(NavBooking);
        }

        private void NavigateToReservations()
        {
            PageTitle.Text = "سجل وإدارة الحجوزات";
            _reservationsPage = new ReservationsPage();
            MainFrame.Navigate(_reservationsPage);
            SetActiveNav(NavReservations);
        }

        public void NavigateToReservationsWithChalet(Chalet chalet, DateTime checkInDate)
        {
            PageTitle.Text = "سجل وإدارة الحجوزات";
            _reservationsPage = new ReservationsPage(chalet, checkInDate);
            MainFrame.Navigate(_reservationsPage);
            SetActiveNav(NavReservations);
        }

        private void NavigateToAdmin()
        {
            PageTitle.Text = "الإعدادات والإدارة";
            _adminPage = new AdminPage();
            MainFrame.Navigate(_adminPage);
            SetActiveNav(NavAdmin);
        }

        private void NavigateToGuests()
        {
            PageTitle.Text = "سجل العملاء والمستأجرين";
            _guestsPage = new GuestsPage();
            _guestsPage.ExtendBookingRequested += booking => NavigateToExtendBooking(booking);
            MainFrame.Navigate(_guestsPage);
            SetActiveNav(NavGuests);
        }

        private void NavigateToExtendBooking(Booking existingBooking)
        {
            PageTitle.Text = "تمديد حجز النزيل وإصدار فاتورة جديدة";
            _bookingPage = new BookingPage(existingBooking);
            MainFrame.Navigate(_bookingPage);
            SetActiveNav(NavBooking);
        }

        private void NavigateToUsers()
        {
            PageTitle.Text = "إدارة المستخدمين والصلاحيات";
            _usersPage = new UsersPage();
            MainFrame.Navigate(_usersPage);
            SetActiveNav(NavUsers);
        }

        private void SetActiveNav(System.Windows.Controls.Button activeBtn)
        {
            foreach (var btn in new[] { NavTimeline, NavChalets, NavBooking, NavReservations, NavGuests, NavUsers, NavAdmin })
            {
                if (btn == null) continue;
                btn.Background = btn == activeBtn
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 37, 51))
                    : System.Windows.Media.Brushes.Transparent;
                btn.Foreground = btn == activeBtn
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(212, 160, 23))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 244, 248));
            }
        }

        private void NavTimeline_Click(object sender, RoutedEventArgs e) => NavigateToTimeline();
        private void NavChalets_Click(object sender, RoutedEventArgs e) => NavigateToChalets();
        private void NavBooking_Click(object sender, RoutedEventArgs e) => NavigateToBooking();
        private void NavReservations_Click(object sender, RoutedEventArgs e) => NavigateToReservations();
        private void NavGuests_Click(object sender, RoutedEventArgs e) => NavigateToGuests();
        private void NavUsers_Click(object sender, RoutedEventArgs e) => NavigateToUsers();
        private void NavAdmin_Click(object sender, RoutedEventArgs e) => NavigateToAdmin();

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            SessionService.Instance.Logout();
            var login = new Views.LoginView();
            login.Show();
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void TopBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && WindowState != WindowState.Maximized) DragMove();
        }

        private void MainFrame_Navigating(object sender, System.Windows.Navigation.NavigatingCancelEventArgs e)
        {
            if (NavigationLoadingOverlay != null)
                NavigationLoadingOverlay.Visibility = Visibility.Visible;
        }

        private void MainFrame_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            if (NavigationLoadingOverlay != null)
                NavigationLoadingOverlay.Visibility = Visibility.Collapsed;

            // Smooth modern Fade-In & Slide-Up transition animation
            if (e.Content is UIElement pageElement)
            {
                pageElement.Opacity = 0;
                var transform = new System.Windows.Media.TranslateTransform(0, 14);
                pageElement.RenderTransform = transform;

                var fadeAnim = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240))
                {
                    EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };

                var slideAnim = new System.Windows.Media.Animation.DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(240))
                {
                    EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };

                pageElement.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
                transform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideAnim);
            }
        }
    
        private bool _isSidebarExpanded = true;
        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            _isSidebarExpanded = !_isSidebarExpanded;
            if (SidebarColumn != null)
            {
                SidebarColumn.Width = _isSidebarExpanded ? new GridLength(220) : new GridLength(0);
            }
            if (SidebarBorder != null)
            {
                SidebarBorder.Visibility = _isSidebarExpanded ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}

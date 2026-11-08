using EaglesResort.Data;
using EaglesResort.Models;
using System.Windows;
using System.Windows.Controls;

namespace EaglesResort.Views.Dialogs
{
    public partial class ExtendStayDialog : Window
    {
        private readonly Booking _booking;
        private int _extraDays = 1;

        public ExtendStayDialog(Booking booking)
        {
            _booking = booking;
            FlowDirection = FlowDirection.RightToLeft;
            Title = "تمديد الإقامة";
            Width = 460;
            Height = 340;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Owner = Application.Current.MainWindow;
            ResizeMode = ResizeMode.NoResize;
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#111E2B"));

            BuildUI();
        }

        private TextBlock _availabilityBlock = new();
        private TextBlock _newDateBlock = new();
        private Slider _daysSlider = new();
        private TextBlock _daysLabel = new();

        private void BuildUI()
        {
            var stack = new StackPanel { Margin = new Thickness(24) };

            // Header
            stack.Children.Add(new TextBlock
            {
                Text = $"تمديد إقامة: {_booking.GuestName}",
                FontSize = 16, FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(0, 0, 0, 4)
            });
            stack.Children.Add(new TextBlock
            {
                Text = $"تاريخ المغادرة الحالي: {_booking.CheckOutDate:dd/MM/yyyy}",
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8")),
                FontSize = 13, Margin = new Thickness(0, 0, 0, 16)
            });

            // Days selector
            stack.Children.Add(new TextBlock { Text = "عدد أيام التمديد:", Foreground = System.Windows.Media.Brushes.White, FontSize = 13 });

            _daysSlider = new Slider { Minimum = 1, Maximum = 30, Value = 1, TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new Thickness(0, 6, 0, 0) };
            _daysSlider.ValueChanged += OnDaysChanged;
            stack.Children.Add(_daysSlider);

            _daysLabel = new TextBlock
            {
                Text = "1 يوم",
                FontSize = 18, FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D4A017")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 12)
            };
            stack.Children.Add(_daysLabel);

            _newDateBlock = new TextBlock
            {
                FontSize = 13, Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(0, 0, 0, 4)
            };
            stack.Children.Add(_newDateBlock);

            _availabilityBlock = new TextBlock { FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 16) };
            stack.Children.Add(_availabilityBlock);

            UpdateAvailability();

            // Buttons
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

            var confirmBtn = new Button
            {
                Content = "✅ تأكيد التمديد",
                Padding = new Thickness(20, 8, 20, 8),
                Margin = new Thickness(0, 0, 12, 0),
                Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#22C55E")),
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14, FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand
            };
            confirmBtn.Click += Confirm_Click;

            var cancelBtn = new Button
            {
                Content = "إلغاء",
                Padding = new Thickness(20, 8, 20, 8),
                Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#374151")),
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14, BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };

            btnPanel.Children.Add(confirmBtn);
            btnPanel.Children.Add(cancelBtn);
            stack.Children.Add(btnPanel);

            Content = stack;
        }

        private void OnDaysChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _extraDays = (int)_daysSlider.Value;
            _daysLabel.Text = $"{_extraDays} يوم";
            UpdateAvailability();
        }

        private void UpdateAvailability()
        {
            var newCheckOut = _booking.CheckOutDate.AddDays(_extraDays);
            _newDateBlock.Text = $"تاريخ المغادرة الجديد: {newCheckOut:dd/MM/yyyy}";

            // Check if chalet is available in the extended period
            using var db = new AppDbContext();
            bool conflict = db.Bookings.Any(b =>
                b.Id != _booking.Id &&
                b.ChaletId == _booking.ChaletId &&
                b.Status != BookingStatus.CheckedOut &&
                b.Status != BookingStatus.Cancelled &&
                b.CheckInDate < newCheckOut &&
                b.CheckOutDate > _booking.CheckOutDate);

            if (conflict)
            {
                _availabilityBlock.Text = "⚠️ الشاليه محجوز في هذه الفترة — اختر عدد أيام أقل";
                _availabilityBlock.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
            else
            {
                _availabilityBlock.Text = "✅ الشاليه متاح في الفترة المحددة";
                _availabilityBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            var newCheckOut = _booking.CheckOutDate.AddDays(_extraDays);

            // Final conflict check
            using var db = new AppDbContext();
            bool conflict = db.Bookings.Any(b =>
                b.Id != _booking.Id &&
                b.ChaletId == _booking.ChaletId &&
                b.Status != BookingStatus.CheckedOut &&
                b.Status != BookingStatus.Cancelled &&
                b.CheckInDate < newCheckOut &&
                b.CheckOutDate > _booking.CheckOutDate);

            if (conflict)
            {
                MessageBox.Show("لا يمكن التمديد — الشاليه محجوز في هذه الفترة", "تعارض حجوزات",
                    MessageBoxButton.OK, MessageBoxImage.Warning,
                    MessageBoxResult.OK, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
                return;
            }

            var booking = db.Bookings.Find(_booking.Id);
            if (booking != null)
            {
                booking.CheckOutDate = newCheckOut;
                db.SaveChanges();
            }

            DialogResult = true;
            Close();
        }
    }
}

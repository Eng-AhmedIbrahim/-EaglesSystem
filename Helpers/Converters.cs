using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using EaglesResort.Models;
using EaglesResort.ViewModels;

namespace EaglesResort.Helpers
{
    public class ChaletStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChaletStatus status)
            {
                return status switch
                {
                    ChaletStatus.Available => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                    ChaletStatus.Occupied => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                    ChaletStatus.Maintenance => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                    ChaletStatus.Cleaning => new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ChaletStatusToArabicConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChaletStatus status)
            {
                return status switch
                {
                    ChaletStatus.Available => "متاح",
                    ChaletStatus.Occupied => "مشغول",
                    ChaletStatus.Maintenance => "صيانة",
                    ChaletStatus.Cleaning => "تنظيف",
                    _ => "غير معروف"
                };
            }
            return "غير معروف";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BookingStatusToArabicConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is BookingStatus status)
            {
                return status switch
                {
                    BookingStatus.Active => "نشط",
                    BookingStatus.Upcoming => "قادم",
                    BookingStatus.CheckedOut => "غادر",
                    BookingStatus.Cancelled => "ملغي",
                    _ => "غير معروف"
                };
            }
            return value?.ToString() ?? string.Empty;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BookingStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is BookingStatus status)
            {
                return status switch
                {
                    BookingStatus.Active => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                    BookingStatus.Upcoming => new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                    BookingStatus.CheckedOut => new SolidColorBrush(Colors.Gray),
                    BookingStatus.Cancelled => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class MonthNumberToNameConverter : IValueConverter
    {
        private static readonly string[] Months = { "", "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر" };
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int m && m >= 1 && m <= 12) return Months[m];
            return value?.ToString() ?? string.Empty;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class TimelineCellColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EaglesResort.ViewModels.MonthlyTimelineCell cell)
            {
                if (cell.IsToday && !cell.IsOccupied && !cell.IsReserved) return new SolidColorBrush(Color.FromRgb(254, 243, 199));
                if (cell.IsOccupied) return new SolidColorBrush(Color.FromRgb(239, 68, 68));
                if (cell.IsReserved) return new SolidColorBrush(Color.FromRgb(245, 158, 11));
                return new SolidColorBrush(Colors.Transparent);
            }
            return new SolidColorBrush(Colors.Transparent);
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value == null ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // null → Collapsed, non-null → Visible  (show Save/Cancel when editing)
    public class NullToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value == null ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // null → Visible, non-null → Collapsed  (show Add button when NOT editing)
    public class NullToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value == null ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ReservationStatusToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ReservationStatus status)
            {
                return status == ReservationStatus.Upcoming ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class StringEqualsConverter : IMultiValueConverter
    {
        public static readonly StringEqualsConverter Instance = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values != null && values.Length >= 2 && values[0] != null && values[1] != null)
            {
                return string.Equals(values[0].ToString()?.Trim(), values[1].ToString()?.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class DateEqualsConverter : IMultiValueConverter
    {
        public static readonly DateEqualsConverter Instance = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values != null && values.Length >= 1 && values[0] is DateTime dt)
            {
                return dt.Date == DateTime.Today;
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
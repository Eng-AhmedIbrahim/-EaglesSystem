using EaglesResort.Models;
using EaglesResort.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace EaglesResort.Views.Pages
{
    public partial class TimelinePage : Page
    {
        // عرض عمود اليوم في الجدول (ثابت في XAML = 46)
        private const double DayColumnWidth = 46;

        private TimelineViewModel _vm;
        private bool _pendingScrollToToday;
        public event Action<Chalet, DateTime>? NewReservationRequested;

        public TimelinePage()
        {
            InitializeComponent();
            _vm = new TimelineViewModel();
            _vm.NewReservationRequested += (chalet, date) => NewReservationRequested?.Invoke(chalet, date);
            DataContext = _vm;

            // رأس الأيام لازم يكون بنفس عرض منطقة الأيام بالظبط (بعد خصم عرض شريط التمرير الرأسي)
            // عشان التمرير الأفقي يفضل متطابق مع الخلايا، واليوم الأخير مايتقطعش عند الشمال.

            // كل ما الجدول يتبني من جديد نرجّع الأسكرول لأول يوم = اليوم الحالي
            _vm.PropertyChanged += ViewModel_PropertyChanged;
            Loaded += (s, e) =>
            {
                UpdateHeaderSpacer();
                _pendingScrollToToday = true;
                ScrollToToday();
            };
            TimelineScroll.SizeChanged += (s, e) =>
            {
                UpdateHeaderSpacer();
                if (_pendingScrollToToday) ScrollToToday();
            };
            Unloaded += (s, e) => _vm.PropertyChanged -= ViewModel_PropertyChanged;
        }

        /// <summary>
        /// يخلي عرض رأس الأيام مساوي لعرض منطقة العرض في الجدول (Viewport)
        /// عن طريق حجز مساحة شريط التمرير الرأسي في نفس الجهة، وبكده
        /// التمرير الأفقي للرأس والخلايا يفضل متطابق مهما ظهر/اختفى الشريط.
        /// </summary>
        private void UpdateHeaderSpacer()
        {
            if (TimelineScroll == null || HeaderScrollbarSpacer == null) return;

            double scrollBar = Math.Max(0, TimelineScroll.ActualWidth - TimelineScroll.ViewportWidth);

            if (Math.Abs(HeaderScrollbarSpacer.Width.Value - scrollBar) > 0.1)
                HeaderScrollbarSpacer.Width = new GridLength(scrollBar);
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TimelineViewModel.MonthDates))
            {
                _pendingScrollToToday = true;
                // بعد ما التخطيط يخلص عشان ScrollableWidth يبقى محسوب
                Dispatcher.BeginInvoke(new Action(ScrollToToday), DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// يزامن عمود أسماء الشاليهات (الثابت على اليمين) مع التمرير الرأسي،
        /// ويزامن رأس الأيام مع التمرير الأفقي.
        /// </summary>
        private void TimelineScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (ChaletNamesTranslate != null)
                ChaletNamesTranslate.Y = -TimelineScroll.VerticalOffset;

            if (e.ExtentWidthChange != 0 || e.ViewportWidthChange != 0)
                UpdateHeaderSpacer();

            SyncHeaderToBody();
        }

        private void DaysHeaderScroll_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            SyncHeaderToBody();
        }

        private void DaysHeaderScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // لو امتداد المحتوى اتغير بعد بناء الشهر، نرجع نطابق الرأس مع الجدول
            if (e.ExtentWidthChange != 0 || e.ViewportWidthChange != 0)
                SyncHeaderToBody();
        }

        private bool _syncingHeader;

        /// <summary>يحرّك رأس الأيام لنفس إزاحة الجدول الأفقية.</summary>
        private void SyncHeaderToBody()
        {
            if (DaysHeaderScroll == null || TimelineScroll == null || _syncingHeader) return;

            if (Math.Abs(DaysHeaderScroll.HorizontalOffset - TimelineScroll.HorizontalOffset) > 0.1)
            {
                _syncingHeader = true;
                DaysHeaderScroll.ScrollToHorizontalOffset(TimelineScroll.HorizontalOffset);
                _syncingHeader = false;
            }
        }

        // عجلة الماوس فوق عمود الأسماء بتتمرر الجدول نفسه
        private void ChaletNames_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            TimelineScroll.ScrollToVerticalOffset(TimelineScroll.VerticalOffset - e.Delta);
        }

        /// <summary>
        /// يخلي أول يوم ظاهر في التقويم هو اليوم الحالي (لو الشهر المعروض هو الشهر الحالي).
        /// </summary>
        private void ScrollToToday()
        {
            if (TimelineScroll == null) return;

            TimelineScroll.UpdateLayout();
            UpdateHeaderSpacer();

            // لسه التخطيط ما خلصش: نستنى حدث SizeChanged
            if (TimelineScroll.ScrollableWidth <= 0.5) return;

            _pendingScrollToToday = false;

            var today = DateTime.Today;
            if (_vm.SelectedYear != today.Year || _vm.SelectedMonth != today.Month)
            {
                // شهر آخر غير الشهر الحالي: نبدأ من أول يوم في الشهر
                TimelineScroll.ScrollToHorizontalOffset(0);
                SyncHeaderToBody();
                return;
            }

            // في RTL: offset = 0 يعني أقصى اليمين (يوم 1)، فبنزحف بمقدار الأيام السابقة
            double offset = (today.Day - 1) * DayColumnWidth;
            if (offset > TimelineScroll.ScrollableWidth)
                offset = TimelineScroll.ScrollableWidth;
            if (offset < 0)
                offset = 0;

            TimelineScroll.ScrollToHorizontalOffset(offset);
            SyncHeaderToBody();
        }

        private void ShowActiveBookings_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var win = new Windows.ActiveBookingsWindow(_vm);
            win.Owner = System.Windows.Window.GetWindow(this);
            win.ShowDialog();
        }

                private void ShowTodayBookings_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var win = new Windows.TodayBookingsWindow();
            win.Owner = System.Windows.Window.GetWindow(this);
            win.ViewModel.DataChanged += () => _vm.LoadData();
            win.ShowDialog();
            _vm.LoadData();
        }

        private void ShowTodayCheckouts_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var win = new Windows.TodayCheckoutsWindow(_vm);
            win.Owner = System.Windows.Window.GetWindow(this);
            win.ShowDialog();
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EaglesResort.Helpers
{
    public static class ScrollViewerExtensions
    {
        private static readonly DependencyProperty TargetVerticalOffsetProperty =
            DependencyProperty.RegisterAttached(
                "TargetVerticalOffset",
                typeof(double),
                typeof(ScrollViewerExtensions),
                new PropertyMetadata(0.0));

        public static readonly DependencyProperty EnableSmoothScrollProperty =
            DependencyProperty.RegisterAttached(
                "EnableSmoothScroll",
                typeof(bool),
                typeof(ScrollViewerExtensions),
                new PropertyMetadata(false, OnEnableSmoothScrollChanged));

        public static void SetEnableSmoothScroll(FrameworkElement target, bool value) =>
            target.SetValue(EnableSmoothScrollProperty, value);

        public static bool GetEnableSmoothScroll(FrameworkElement target) =>
            (bool)target.GetValue(EnableSmoothScrollProperty);

        private static void OnEnableSmoothScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer scv)
            {
                if ((bool)e.NewValue)
                {
                    scv.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
                }
                else
                {
                    scv.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
                    // إيقاف التحديث عند إيقاف الخاصية
                    CompositionTarget.Rendering -= (s, args) => RenderUpdate(scv);
                }
            }
        }

        private static void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scv) return;

            e.Handled = true;

            double currentTarget = (double)scv.GetValue(TargetVerticalOffsetProperty);
            if (double.IsNaN(currentTarget) || Math.Abs(currentTarget - scv.VerticalOffset) > 50)
            {
                currentTarget = scv.VerticalOffset;
            }

            double offsetChange = e.Delta * 1.2;
            double newTarget = currentTarget - offsetChange;
            newTarget = Math.Max(0, Math.Min(scv.ScrollableHeight, newTarget));

            scv.SetValue(TargetVerticalOffsetProperty, newTarget);

            // ربط حدث التحديث لكل إطار (Frame)
            CompositionTarget.Rendering -= RenderingHandler!;
            CompositionTarget.Rendering += RenderingHandler!;
        }

        private static void RenderingHandler(object sender, EventArgs e)
        {
            // يمكنك تخصيص أو تتبع التحديث هنا إذا لزم الأمر
        }

        // دالة التحديث السلس التي كانت ناقصة
        private static void RenderUpdate(ScrollViewer scv)
        {
            if (scv == null) return;

            double current = scv.VerticalOffset;
            double target = (double)scv.GetValue(TargetVerticalOffsetProperty);

            // استخدام طريقة Lerp لتنعيم الحركة تدريجياً بكل إطار
            double delta = target - current;
            if (Math.Abs(delta) > 0.1)
            {
                scv.ScrollToVerticalOffset(current + (delta * 0.2)); // كلما زاد الرقم زادت سرعة اللحاق بالحركة
            }
            else
            {
                scv.ScrollToVerticalOffset(target);
                CompositionTarget.Rendering -= RenderingHandler!;
            }
        }
    }
}
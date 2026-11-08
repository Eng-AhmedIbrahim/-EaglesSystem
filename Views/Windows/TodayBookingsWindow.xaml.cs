using System;
using System.Windows;
using System.Windows.Input;
using EaglesResort.ViewModels;

namespace EaglesResort.Views.Windows
{
    public partial class TodayBookingsWindow : Window
    {
        public TodayBookingsViewModel ViewModel { get; }

        public TodayBookingsWindow()
        {
            InitializeComponent();
            ViewModel = new TodayBookingsViewModel();
            DataContext = ViewModel;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
            }
            else
            {
                DragMove();
            }
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

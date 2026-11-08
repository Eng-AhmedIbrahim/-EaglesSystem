using System.Windows;
using EaglesResort.ViewModels;

namespace EaglesResort.Views.Windows
{
    public partial class ActiveBookingsWindow : Window
    {
        public ActiveBookingsWindow(TimelineViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

using EaglesResort.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace EaglesResort.Views
{
    public partial class LoginView : Window
    {
        public LoginView()
        {
            InitializeComponent();
            var vm = (LoginViewModel)DataContext;
            vm.LoginSucceeded += () =>
            {
                var main = new MainWindow();
                main.Show();
                this.Close();
            };
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel vm)
                vm.Password = PasswordBox.Password;
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (DataContext is LoginViewModel vm)
                    vm.LoginCommand.Execute(null);
            }
        }
    }
}

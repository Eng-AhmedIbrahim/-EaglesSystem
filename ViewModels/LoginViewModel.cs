using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EaglesResort.Data;
using EaglesResort.Services;

namespace EaglesResort.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotLoading))]
        private bool _isLoading;

        public bool IsNotLoading => !IsLoading;

        public event Action? LoginSucceeded;

        [RelayCommand]
        private async Task LoginAsync()
        {
            ErrorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "الرجاء إدخال اسم المستخدم وكلمة المرور";
                return;
            }

            IsLoading = true;
            await Task.Run(() =>
            {
                using var db = new AppDbContext();
                db.SeedData();
                if (db.ValidateUser(Username, Password, out var user) && user != null)
                {
                    SessionService.Instance.Login(user);
                    App.Current.Dispatcher.Invoke(() => LoginSucceeded?.Invoke());
                }
                else
                {
                    App.Current.Dispatcher.Invoke(() =>
                        ErrorMessage = "اسم المستخدم أو كلمة المرور غير صحيحة");
                }
            });
            IsLoading = false;
        }
    }
}

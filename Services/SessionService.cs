using CommunityToolkit.Mvvm.ComponentModel;
using EaglesResort.Models;

namespace EaglesResort.Services
{
    public class SessionService : ObservableObject
    {
        private static SessionService? _instance;
        public static SessionService Instance => _instance ??= new SessionService();

        private User? _currentUser;
        public User? CurrentUser
        {
            get => _currentUser;
            set => SetProperty(ref _currentUser, value);
        }

        public bool IsLoggedIn => CurrentUser != null;
        public bool IsAdmin => CurrentUser?.Role == "Admin";

        public void Login(User user)
        {
            CurrentUser = user;
        }

        public void Logout()
        {
            CurrentUser = null;
        }
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EaglesResort.Data;
using EaglesResort.Models;
using System.Collections.ObjectModel;

namespace EaglesResort.ViewModels
{
    public partial class UsersViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<User> _users = new();

        [ObservableProperty]
        private User _editingUser = new();

        [ObservableProperty]
        private bool _isDialogOpen;

        [ObservableProperty]
        private string _dialogTitle = string.Empty;

        [ObservableProperty]
        private string _selectedRole = "User";

        public List<string> Roles { get; } = new() { "Admin", "User" };

        public UsersViewModel()
        {
            LoadUsers();
        }

        private void LoadUsers()
        {
            using var db = new AppDbContext();
            Users = new ObservableCollection<User>(db.Users.ToList());
        }

        [RelayCommand]
        private void AddUser()
        {
            EditingUser = new User { Role = "User" };
            SelectedRole = "User";
            DialogTitle = "إضافة مستخدم جديد";
            IsDialogOpen = true;
        }

        [RelayCommand]
        private void EditUser(User user)
        {
            EditingUser = new User
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                PasswordHash = user.PasswordHash,
                Role = user.Role
            };
            SelectedRole = user.Role;
            DialogTitle = "تعديل مستخدم";
            IsDialogOpen = true;
        }

        [RelayCommand]
        private void DeleteUser(User user)
        {
            if (user.Username == "admin") return;
            using var db = new AppDbContext();
            var u = db.Users.Find(user.Id);
            if (u != null)
            {
                db.Users.Remove(u);
                db.SaveChanges();
                LoadUsers();
            }
        }

        [RelayCommand]
        private void SaveUser()
        {
            if (string.IsNullOrWhiteSpace(EditingUser.Username) ||
                string.IsNullOrWhiteSpace(EditingUser.PasswordHash))
                return;

            EditingUser.Role = SelectedRole;

            // Hash the password before saving
            var hashedPw = AppDbContext.HashPassword(EditingUser.PasswordHash);

            using var db = new AppDbContext();
            if (EditingUser.Id == 0)
            {
                if (db.Users.Any(u => u.Username == EditingUser.Username))
                {
                    System.Windows.MessageBox.Show("اسم المستخدم موجود مسبقاً", "خطأ");
                    return;
                }
                db.Users.Add(new User
                {
                    Username = EditingUser.Username,
                    FullName = EditingUser.FullName,
                    PasswordHash = hashedPw,
                    Role = SelectedRole
                });
            }
            else
            {
                var u = db.Users.Find(EditingUser.Id);
                if (u != null)
                {
                    u.Username = EditingUser.Username;
                    u.FullName = EditingUser.FullName;
                    u.PasswordHash = hashedPw;
                    u.Role = SelectedRole;
                }
            }

            db.SaveChanges();
            IsDialogOpen = false;
            LoadUsers();
        }

        [RelayCommand]
        private void CancelDialog()
        {
            IsDialogOpen = false;
        }
    }
}

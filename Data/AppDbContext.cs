using EaglesResort.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EaglesResort.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Chalet> Chalets { get; set; }
        public DbSet<Rate> Rates { get; set; }
        public DbSet<SpecialRate> SpecialRates { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<AppSettings> AppSettings { get; set; }
        public DbSet<Receptionist> Receptionists { get; set; }
        public DbSet<Reservation> Reservations { get; set; }

        public static string DefaultConnectionString { get; set; } =
            @"Server=.\SQLEXPRESS;Database=EaglesResort;Trusted_Connection=True;TrustServerCertificate=True;";

        public static string GetConnectionString()
        {
            try
            {
                // 1. Try next to exe (BaseDirectory)
                var baseDirFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(baseDirFile))
                {
                    var conn = ReadConnectionStringFromFile(baseDirFile);
                    if (!string.IsNullOrWhiteSpace(conn)) return conn;
                }

                // 2. Try current working directory
                var currentDirFile = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                if (File.Exists(currentDirFile))
                {
                    var conn = ReadConnectionStringFromFile(currentDirFile);
                    if (!string.IsNullOrWhiteSpace(conn)) return conn;
                }
            }
            catch { }

            return DefaultConnectionString;
        }

        private static string? ReadConnectionStringFromFile(string path)
        {
            try
            {
                var json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("ConnectionStrings", out var csSection) &&
                    csSection.TryGetProperty("DefaultConnection", out var connStr))
                {
                    return connStr.GetString();
                }
            }
            catch { }
            return null;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            string connectionString = GetConnectionString();
            options.UseSqlServer(connectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Unique constraints
            modelBuilder.Entity<Rate>()
                .HasIndex(r => new { r.ChaletType, r.Month })
                .IsUnique();

            modelBuilder.Entity<AppSettings>()
                .HasIndex(s => s.Key)
                .IsUnique();

            // Configure relationships with DeleteBehavior.NoAction to eliminate multiple cascade paths in SQL Server
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Booking)
                .WithOne()
                .HasForeignKey<Invoice>(i => i.BookingId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Invoice)
                .WithMany(i => i.Bookings)
                .HasForeignKey(b => b.InvoiceId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Chalet)
                .WithMany()
                .HasForeignKey(b => b.ChaletId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Chalet)
                .WithMany()
                .HasForeignKey(r => r.ChaletId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Invoice)
                .WithMany()
                .HasForeignKey(r => r.InvoiceId)
                .OnDelete(DeleteBehavior.SetNull);
        }

        public void SeedData()
        {
            // Create database and all tables automatically in SQL Server
            try
            {
                Database.EnsureCreated();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnsureCreated notice: {ex.Message}");
            }

            // Ensure newly added tables and columns exist in existing database schema
            try
            {
                Database.ExecuteSqlRaw(@"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Reservations')
                    BEGIN
                        CREATE TABLE [dbo].[Reservations] (
                            [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            [GuestName] NVARCHAR(MAX) NOT NULL,
                            [PhoneNumber] NVARCHAR(MAX) NOT NULL,
                            [SecondaryPhoneNumber] NVARCHAR(MAX) NOT NULL DEFAULT '',
                            [ChaletId] INT NOT NULL,
                            [CheckInDate] DATETIME2 NOT NULL,
                            [CheckOutDate] DATETIME2 NOT NULL,
                            [NumberOfDays] INT NOT NULL DEFAULT 1,
                            [DailyRate] DECIMAL(18,2) NOT NULL DEFAULT 0,
                            [ChaletTotal] DECIMAL(18,2) NOT NULL DEFAULT 0,
                            [Deposit] DECIMAL(18,2) NOT NULL DEFAULT 0,
                            [DepositDate] DATETIME2 NULL,
                            [DepositReceiptImagePath] NVARCHAR(MAX) NOT NULL DEFAULT '',
                            [Notes] NVARCHAR(MAX) NOT NULL DEFAULT '',
                            [Status] INT NOT NULL DEFAULT 0,
                            [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
                            [InvoiceId] INT NULL,
                            CONSTRAINT [FK_Reservations_Chalets_ChaletId] FOREIGN KEY ([ChaletId]) REFERENCES [dbo].[Chalets] ([Id]) ON DELETE NO ACTION,
                            CONSTRAINT [FK_Reservations_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [dbo].[Invoices] ([Id]) ON DELETE SET NULL
                        );
                    END

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Bookings') AND name = 'DepositReceiptImagePath')
                    BEGIN
                        ALTER TABLE [dbo].[Bookings] ADD [DepositReceiptImagePath] NVARCHAR(MAX) NOT NULL DEFAULT '';
                    END

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Invoices') AND name = 'DepositReceiptImagePath')
                    BEGIN
                        ALTER TABLE [dbo].[Invoices] ADD [DepositReceiptImagePath] NVARCHAR(MAX) NOT NULL DEFAULT '';
                    END

                    IF NOT EXISTS (SELECT 1 FROM [dbo].[Chalets] WHERE [Number] = 'B-21')
                    BEGIN
                        INSERT INTO [dbo].[Chalets] ([Number], [BuildingArea], [ChaletType], [RoomsCount], [MaxCapacity], [Status], [Notes])
                        VALUES ('B-21', 'Golden_Eagle', 'DELUXE', 2, 6, 0, '');
                    END

                    -- Lock B-11 permanently for the Brigadier General (السيد العميد)
                    UPDATE [dbo].[Chalets]
                    SET [Status] = 1, [Notes] = N'إقامة دائمة - السيد العميد'
                    WHERE [Number] = 'B-11';
                    UPDATE [dbo].[Chalets]
                    SET [Status] = 1, [Notes] = N'إقامة دائمة -  النقيب احمد'
                    WHERE [Number] = 'D-8';
                ");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Schema migration notice: {ex.Message}");
            }

            // Seed Receptionists
            try
            {
                if (!Receptionists.Any())
                {
                    Receptionists.AddRange(
                        new Receptionist { Name = "أحمد إبراهيم", IsActive = true },
                        new Receptionist { Name = "محمود ابراهيم", IsActive = true },
                        new Receptionist { Name = "مصطفى عبدالعظيم", IsActive = true }
                    );
                    SaveChanges();
                }
            }
            catch { }

            // Seed admin user if not exists
            if (!Users.Any())
            {
                Users.Add(new User
                {
                    Username = "admin",
                    PasswordHash = HashPassword("admin123"),
                    Role = "Admin",
                    FullName = "مدير النظام"
                });
                SaveChanges();
            }

            // Seed default settings
            if (!AppSettings.Any())
            {
                var defaultChaletTypes = "PREMIUM,STANDARD,PLUS,DELUXE,DOUBLE";
                var defaultBuildings = "Golden_Eagle,Eagle1,Eagle2,D";

                AppSettings.AddRange(
                    new AppSettings { Key = "DefaultInsurance", Value = "1000" },
                    new AppSettings { Key = "ResortManagerName", Value = "قائد المنتجع" },
                    new AppSettings { Key = "AccountantName", Value = "مسؤول الحسابات" },
                    new AppSettings { Key = "ReceptionistName", Value = "مسؤول الاستقبال" },
                    new AppSettings { Key = "PaymentMethods", Value = "كاش,فيزا,انستا باي,تحويل بنكي" },
                    new AppSettings { Key = "ChaletTypesConfig", Value = defaultChaletTypes },
                    new AppSettings { Key = "BuildingAreasConfig", Value = defaultBuildings }
                );
                SaveChanges();
            }
            else
            {
                if (!AppSettings.Any(s => s.Key == "ChaletTypesConfig"))
                {
                    AppSettings.Add(new AppSettings { Key = "ChaletTypesConfig", Value = "PREMIUM,STANDARD,PLUS,DELUXE,DOUBLE" });
                    SaveChanges();
                }
                if (!AppSettings.Any(s => s.Key == "BuildingAreasConfig"))
                {
                    AppSettings.Add(new AppSettings { Key = "BuildingAreasConfig", Value = "Golden_Eagle,Eagle1,Eagle2,D" });
                    SaveChanges();
                }
                if (!AppSettings.Any(s => s.Key == "PaymentMethods"))
                {
                    AppSettings.Add(new AppSettings { Key = "PaymentMethods", Value = "كاش,فيزا,انستا باي,تحويل بنكي" });
                    SaveChanges();
                }
            }

            // Fix old rates if needed
            var oldRates = Rates.Where(r => r.AdultMealRate == 65 || r.ChildMealRate == 40).ToList();
            if (oldRates.Any())
            {
                foreach (var r in oldRates)
                {
                    r.AdultMealRate = 650;
                    r.ChildMealRate = 450;
                }
                SaveChanges();
            }

            // Fix old default insurance
            var oldIns = AppSettings.FirstOrDefault(s => s.Key == "DefaultInsurance" && s.Value == "500");
            if (oldIns != null)
            {
                oldIns.Value = "1000";
                SaveChanges();
            }

            // Seed chalets if not exists
            if (!Chalets.Any())
            {
                var chaletsToSeed = new List<Chalet>();

                // Golden_Eagle Building - B-1 to B-8: PREMIUM
                foreach (var num in Enumerable.Range(1, 8).Select(i => $"B-{i}"))
                {
                    chaletsToSeed.Add(new Chalet { Number = num, BuildingArea = "Golden_Eagle", ChaletType = "PREMIUM", RoomsCount = 3, MaxCapacity = 8, Status = ChaletStatus.Available });
                }

                // Golden_Eagle Building - B-11 to B-18 and B-22 to B-28: DELUXE
                var Golden_EagleMumayaz = Enumerable.Range(11, 8).Select(i => $"B-{i}")
                    .Concat(Enumerable.Range(21, 8).Select(i => $"B-{i}"));
                foreach (var num in Golden_EagleMumayaz)
                {
                    chaletsToSeed.Add(new Chalet { Number = num, BuildingArea = "Golden_Eagle", ChaletType = "DELUXE", RoomsCount = 2, MaxCapacity = 6, Status = ChaletStatus.Available });
                }

                // Eagle1 Building
                var eagle1Groups = new[] { 100, 110, 120 };
                foreach (var grp in eagle1Groups)
                {
                    chaletsToSeed.Add(new Chalet { Number = $"{grp + 1}", BuildingArea = "Eagle1", ChaletType = "PLUS", RoomsCount = 2, MaxCapacity = 5, Status = ChaletStatus.Available });
                    for (int i = 2; i <= 8; i++)
                    {
                        chaletsToSeed.Add(new Chalet { Number = $"{grp + i}", BuildingArea = "Eagle1", ChaletType = "STANDARD", RoomsCount = 2, MaxCapacity = 4, Status = ChaletStatus.Available });
                    }
                }

                // Eagle2 Building
                var eagle2Groups = new[] { 200, 210, 220 };
                foreach (var grp in eagle2Groups)
                {
                    for (int i = 1; i <= 3; i++)
                    {
                        chaletsToSeed.Add(new Chalet { Number = $"{grp + i}", BuildingArea = "Eagle2", ChaletType = "STANDARD", RoomsCount = 2, MaxCapacity = 4, Status = ChaletStatus.Available });
                    }
                    chaletsToSeed.Add(new Chalet { Number = $"{grp + 4}", BuildingArea = "Eagle2", ChaletType = "PLUS", RoomsCount = 2, MaxCapacity = 5, Status = ChaletStatus.Available });
                }

                // Building D
                for (int i = 1; i <= 16; i++)
                {
                    chaletsToSeed.Add(new Chalet { Number = $"D-{i}", BuildingArea = "D", ChaletType = "DOUBLE", RoomsCount = 1, MaxCapacity = 1, Status = ChaletStatus.Available });
                }

                Chalets.AddRange(chaletsToSeed);
                SaveChanges();
            }

            // Seed monthly rates if not exists
            if (!Rates.Any())
            {
                var defaultTypes = new[] { "PREMIUM", "STANDARD", "PLUS", "DELUXE", "DOUBLE" };
                var baseDailyRates = new Dictionary<string, decimal>
                {
                    { "PREMIUM", 6000 },
                    { "DELUXE", 5000 },
                    { "PLUS", 5400 },
                    { "STANDARD", 4500 },
                    { "DOUBLE", 3000 }
                };

                foreach (var cType in defaultTypes)
                {
                    var baseRate = baseDailyRates[cType];
                    for (int month = 1; month <= 12; month++)
                    {
                        decimal multiplier = (month >= 6 && month <= 9) ? 1.5m : 1.0m;
                        Rates.Add(new Rate
                        {
                            ChaletType = cType,
                            Month = month,
                            DailyRate = baseRate * multiplier,
                            AdultMealRate = 650,
                            ChildMealRate = 450
                        });
                    }
                }
                SaveChanges();
            }

            if (!SpecialRates.Any())
            {
                var specialRatesToSeed = new List<SpecialRate>();

                // Mumayaz (Golden_Eagle)
                var mumayazTypes = new[] { "PREMIUM", "DELUXE" };
                foreach (var t in mumayazTypes)
                {
                    specialRatesToSeed.Add(new SpecialRate { ChaletType = t, GuestCategory = "ضباط قوات جوية (عضوية)", DailyRate = 950, AdultMealRate = 450, ChildMealRate = 350 });
                    specialRatesToSeed.Add(new SpecialRate { ChaletType = t, GuestCategory = "ضباط قوات جوية (بدون عضوية)", DailyRate = 1700, AdultMealRate = 450, ChildMealRate = 350 });
                    specialRatesToSeed.Add(new SpecialRate { ChaletType = t, GuestCategory = "ضباط ق.جوية (مرافقين)", DailyRate = 1900, AdultMealRate = 450, ChildMealRate = 350 });
                    specialRatesToSeed.Add(new SpecialRate { ChaletType = t, GuestCategory = "ضباط قوات مسلحة وشرطة", DailyRate = 2200, AdultMealRate = 500, ChildMealRate = 400 });
                }

                // Aadi
                var aadiTypes = new[] { "PLUS", "STANDARD" };
                foreach (var t in aadiTypes)
                {
                    specialRatesToSeed.Add(new SpecialRate { ChaletType = t, GuestCategory = "ضباط قوات جوية (عضوية)", DailyRate = 600, AdultMealRate = 450, ChildMealRate = 350 });
                    specialRatesToSeed.Add(new SpecialRate { ChaletType = t, GuestCategory = "ضباط قوات جوية (بدون عضوية)", DailyRate = 1400, AdultMealRate = 450, ChildMealRate = 350 });
                    specialRatesToSeed.Add(new SpecialRate { ChaletType = t, GuestCategory = "ضباط ق.جوية (مرافقين)", DailyRate = 1600, AdultMealRate = 450, ChildMealRate = 350 });
                    specialRatesToSeed.Add(new SpecialRate { ChaletType = t, GuestCategory = "ضباط قوات مسلحة وشرطة", DailyRate = 2000, AdultMealRate = 500, ChildMealRate = 400 });
                }

                // Fardy
                var fardyTypes = new[] { "DOUBLE" };
                foreach (var t in fardyTypes)
                {
                    specialRatesToSeed.Add(new SpecialRate { ChaletType = t, GuestCategory = "ضباط قوات جوية (عضوية)", DailyRate = 480, AdultMealRate = 450, ChildMealRate = 350 });
                    specialRatesToSeed.Add(new SpecialRate { ChaletType = t, GuestCategory = "ضباط قوات جوية (بدون عضوية)", DailyRate = 800, AdultMealRate = 450, ChildMealRate = 350 });
                    specialRatesToSeed.Add(new SpecialRate { ChaletType = t, GuestCategory = "ضباط ق.جوية (مرافقين)", DailyRate = 1000, AdultMealRate = 450, ChildMealRate = 350 });
                    specialRatesToSeed.Add(new SpecialRate { ChaletType = t, GuestCategory = "ضباط قوات مسلحة وشرطة", DailyRate = 1300, AdultMealRate = 500, ChildMealRate = 400 });
                }

                SpecialRates.AddRange(specialRatesToSeed);
                SaveChanges();
            }
        }

        public static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hash);
        }

        public bool ValidateUser(string username, string password, out User? user)
        {
            var hash = HashPassword(password);
            user = Users.FirstOrDefault(u => u.Username == username && u.PasswordHash == hash);
            return user != null;
        }
    }
}

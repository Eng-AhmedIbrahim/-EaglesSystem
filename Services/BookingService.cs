using EaglesResort.Data;
using EaglesResort.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EaglesResort.Services
{
    public static class BookingService
    {
        private static AppDbContext CreateContext() => new();

        public static List<Chalet> GetAllChalets()
        {
            using var db = CreateContext();
            return db.Chalets.OrderBy(c => c.BuildingArea).ThenBy(c => c.Number).ToList();
        }

        public static List<Chalet> GetAvailableChalets(DateTime checkIn, DateTime checkOut, int? excludeBookingId = null)
        {
            using var db = CreateContext();
            var busyFromBookings = db.Bookings
                .Where(b => b.Status != BookingStatus.Cancelled &&
                            b.Status != BookingStatus.CheckedOut &&
                            (!excludeBookingId.HasValue || b.Id != excludeBookingId.Value) &&
                            b.CheckInDate.Date < checkOut.Date &&
                            b.CheckOutDate.Date > checkIn.Date)
                .Select(b => b.ChaletId)
                .Distinct()
                .ToList();

            var busyFromReservations = db.Reservations
                .Where(r => r.Status == ReservationStatus.Upcoming &&
                            r.CheckInDate.Date < checkOut.Date &&
                            r.CheckOutDate.Date > checkIn.Date)
                .Select(r => r.ChaletId)
                .Distinct()
                .ToList();

            var allBusy = new HashSet<int>(busyFromBookings.Concat(busyFromReservations));

            return db.Chalets
                .Where(c => c.Status != ChaletStatus.Maintenance && !allBusy.Contains(c.Id))
                .OrderBy(c => c.BuildingArea)
                .ThenBy(c => c.Number)
                .ToList();
        }

        public static decimal GetNightlyRate(string chaletType, DateTime date, bool isMilitary = false)
        {
            using var db = CreateContext();
            var month = date.Month;

            if (isMilitary)
            {
                var specialRate = db.SpecialRates.FirstOrDefault(r => r.ChaletType == chaletType && r.GuestCategory.Contains("عسكري"));
                if (specialRate != null && specialRate.DailyRate > 0)
                    return specialRate.DailyRate;
            }

            var rate = db.Rates.FirstOrDefault(r => r.ChaletType == chaletType && r.Month == month);
            if (rate != null && rate.DailyRate > 0)
                return rate.DailyRate;

            return chaletType switch
            {
                "DOUBLE" => 1500,
                "DELUXE" => 2500,
                "PLUS" => 3500,
                "VIP" => 5000,
                _ => 1500
            };
        }

        public static (decimal Daily, decimal Meal) GetRate(string chaletType, int month, string category)
        {
            using var db = CreateContext();
            if (!string.IsNullOrEmpty(category) && category.Contains("عسكري"))
            {
                var sp = db.SpecialRates.FirstOrDefault(r => r.ChaletType == chaletType && r.GuestCategory.Contains("عسكري"));
                if (sp != null && sp.DailyRate > 0) return (sp.DailyRate, sp.AdultMealRate);
            }
            var rate = db.Rates.FirstOrDefault(r => r.ChaletType == chaletType && r.Month == month);
            if (rate != null && rate.DailyRate > 0) return (rate.DailyRate, rate.AdultMealRate);
            var def = chaletType switch
            {
                "DOUBLE" => 1500m,
                "DELUXE" => 2500m,
                "PLUS" => 3500m,
                "VIP" => 5000m,
                _ => 1500m
            };
            return (def, 0);
        }

        public static decimal CalculateStayTotal(string chaletType, DateTime checkIn, DateTime checkOut, bool isMilitary = false)
        {
            decimal total = 0;
            var current = checkIn.Date;
            while (current < checkOut.Date)
            {
                total += GetNightlyRate(chaletType, current, isMilitary);
                current = current.AddDays(1);
            }
            return total;
        }

        public static Booking SaveBooking(Booking booking, Invoice invoice)
        {
            var list = SaveMultiBooking(new List<Booking> { booking }, invoice);
            return list.First();
        }

        public static List<Booking> SaveMultiBooking(List<Booking> bookings, Invoice invoice)
        {
            using var db = CreateContext();
            var groupCode = Guid.NewGuid().ToString("N")[..8].ToUpper();

            var firstBooking = bookings[0];
            if (firstBooking.Id > 0)
            {
                var existing = db.Bookings.Find(firstBooking.Id);
                if (existing != null)
                {
                    existing.GuestName = firstBooking.GuestName;
                                        existing.PhoneNumber = firstBooking.PhoneNumber;
                    existing.SecondaryPhoneNumber = firstBooking.SecondaryPhoneNumber;
                    existing.ChaletId = firstBooking.ChaletId;
                    existing.CheckInDate = firstBooking.CheckInDate;
                    existing.CheckOutDate = firstBooking.CheckOutDate;
                                        existing.DailyRate = firstBooking.DailyRate;
                    existing.ChaletTotal = firstBooking.ChaletTotal;
                    existing.AdultCount = firstBooking.AdultCount;
                    existing.ChildCount = firstBooking.ChildCount;
                    existing.GuestRank = firstBooking.GuestRank;
                    existing.GuestCategory = firstBooking.GuestCategory;
                    existing.PaymentSystem = firstBooking.PaymentSystem;
                    existing.MealPlan = firstBooking.MealPlan;
                    existing.Notes = firstBooking.Notes;
                    existing.Deposit = firstBooking.Deposit;
                    existing.DepositReceiptImagePath = firstBooking.DepositReceiptImagePath;
                    firstBooking = existing;
                }
            }
            else
            {
                firstBooking.GroupBookingCode = groupCode;
                firstBooking.Status = firstBooking.CheckInDate.Date <= DateTime.Today ? BookingStatus.Active : BookingStatus.Upcoming;
                db.Bookings.Add(firstBooking);
            }
            db.SaveChanges();

            invoice.BookingId = firstBooking.Id;
            db.Invoices.Add(invoice);
            db.SaveChanges();

            firstBooking.InvoiceId = invoice.Id;

            for (int i = 1; i < bookings.Count; i++)
            {
                var b = bookings[i];
                if (b.Id > 0)
                {
                    var existing = db.Bookings.Find(b.Id);
                    if (existing != null)
                    {
                        existing.InvoiceId = invoice.Id;
                        existing.GroupBookingCode = groupCode;
                        existing.Status = b.CheckInDate.Date <= DateTime.Today ? BookingStatus.Active : BookingStatus.Upcoming;
                        existing.Deposit = b.Deposit;
                        existing.DepositReceiptImagePath = b.DepositReceiptImagePath;
                    }
                }
                else
                {
                    b.InvoiceId = invoice.Id;
                    b.GroupBookingCode = groupCode;
                    b.Status = b.CheckInDate.Date <= DateTime.Today ? BookingStatus.Active : BookingStatus.Upcoming;
                    db.Bookings.Add(b);
                }
            }
            db.SaveChanges();

            var today = DateTime.Today;
            foreach (var b in bookings)
            {
                if (b.CheckInDate.Date <= today && b.CheckOutDate.Date > today)
                {
                    var chalet = db.Chalets.Find(b.ChaletId);
                    if (chalet != null)
                        chalet.Status = ChaletStatus.Occupied;
                }
            }
            db.SaveChanges();

            invoice.Booking = firstBooking;
            firstBooking.Invoice = invoice;
            return bookings;
        }

        public static void CheckoutBooking(int bookingId)
        {
            using var db = CreateContext();
            var booking = db.Bookings.Include(b => b.Chalet).FirstOrDefault(b => b.Id == bookingId);
            if (booking != null)
            {
                booking.Status = BookingStatus.CheckedOut;
                if (booking.Chalet != null)
                    booking.Chalet.Status = ChaletStatus.Cleaning;
                db.SaveChanges();
            }
        }

        public static List<Booking> SearchBookings(string query)
        {
            using var db = CreateContext();
            if (string.IsNullOrWhiteSpace(query))
            {
                return db.Bookings
                    .Include(b => b.Chalet)
                    .Include(b => b.Invoice)
                    .OrderByDescending(b => b.CreatedAt)
                    .Take(100)
                    .ToList();
            }

            query = query.Trim().ToLower();
            return db.Bookings
                .Include(b => b.Chalet)
                .Include(b => b.Invoice)
                .Where(b => b.GuestName.ToLower().Contains(query) ||
                            (b.PhoneNumber != null && b.PhoneNumber.Contains(query)) ||
                            (b.Chalet != null && b.Chalet.Number.ToLower().Contains(query)))
                .OrderByDescending(b => b.CheckInDate)
                .ToList();
        }

        public static Invoice? GetInvoiceByBookingId(int bookingId)
        {
            using var db = CreateContext();
            var invoice = db.Invoices
                .Include(i => i.Booking)
                    .ThenInclude(b => b!.Chalet)
                .Include(i => i.Bookings)
                    .ThenInclude(b => b.Chalet)
                .FirstOrDefault(i => i.BookingId == bookingId || i.Bookings.Any(b => b.Id == bookingId));

            if (invoice != null && invoice.Booking == null && invoice.Bookings.Any())
            {
                invoice.Booking = invoice.Bookings.First();
            }

            return invoice;
        }

        public static void UpdateBookingStatus()
        {
            using var db = CreateContext();
            var today = DateTime.Today;
            var bookings = db.Bookings.Include(b => b.Chalet)
                .Where(b => b.Status == BookingStatus.Upcoming || b.Status == BookingStatus.Active)
                .ToList();

            foreach (var booking in bookings)
            {
                if (booking.Status == BookingStatus.Upcoming && booking.CheckInDate.Date <= today)
                {
                    booking.Status = BookingStatus.Active;
                    if (booking.Chalet != null) booking.Chalet.Status = ChaletStatus.Occupied;
                }
            }

            db.SaveChanges();
        }

        public static List<Booking> GetBookingsForTimeline(DateTime from, DateTime to)
        {
            using var db = CreateContext();
            return db.Bookings
                .Include(b => b.Chalet)
                .Where(b => b.Status != BookingStatus.Cancelled &&
                            b.CheckInDate < to && b.CheckOutDate > from)
                .OrderBy(b => b.ChaletId)
                .ThenBy(b => b.CheckInDate)
                .ToList();
        }

        public static List<Reservation> GetReservationsForTimeline(DateTime from, DateTime to)
        {
            using var db = CreateContext();
            return db.Reservations
                .Include(r => r.Chalet)
                .Where(r => r.Status == ReservationStatus.Upcoming &&
                            r.CheckInDate < to && r.CheckOutDate > from)
                .OrderBy(r => r.ChaletId)
                .ThenBy(r => r.CheckInDate)
                .ToList();
        }

        public static List<Booking> GetCheckoutsToday()
        {
            using var db = CreateContext();
            var today = DateTime.Today;
            return db.Bookings
                .Include(b => b.Chalet)
                .Where(b => b.Status == BookingStatus.Active && b.CheckOutDate.Date == today)
                .ToList();
        }

        public static BookingConflictResult CheckConflict(int chaletId, DateTime checkIn, DateTime checkOut, int? ignoreBookingId = null)
        {
            using var db = CreateContext();

            // 1. Conflict with Bookings
            var conflict = db.Bookings
                .Include(b => b.Chalet)
                .Where(b => b.ChaletId == chaletId &&
                            b.Status != BookingStatus.Cancelled &&
                            b.Status != BookingStatus.CheckedOut &&
                            (!ignoreBookingId.HasValue || b.Id != ignoreBookingId.Value) &&
                            b.CheckInDate.Date < checkOut.Date &&
                            b.CheckOutDate.Date > checkIn.Date)
                .FirstOrDefault();

            if (conflict != null)
            {
                var chaletNum = conflict.Chalet?.Number ?? chaletId.ToString();
                var suggestions = GetAvailableAlternativeChalets(db, chaletId, checkIn, checkOut, ignoreBookingId, null);
                return new BookingConflictResult
                {
                    HasConflict = true,
                    ConflictingBooking = conflict,
                    ConflictingGuestName = conflict.GuestName,
                    ConflictingDates = $"{conflict.CheckInDate:dd/MM/yyyy} إلى {conflict.CheckOutDate:dd/MM/yyyy}",
                    ErrorMessage = $"⚠️ الشاليه ({chaletNum}) غير متاح في هذه الفترة!\nمسجل له إقامة حالية للنزيل: «{conflict.GuestName}» من {conflict.CheckInDate:dd/MM/yyyy} إلى {conflict.CheckOutDate:dd/MM/yyyy}.",
                    SuggestedChalets = suggestions
                };
            }

            // 2. Conflict with Reservations
            var resConflict = db.Reservations
                .Include(r => r.Chalet)
                .Where(r => r.ChaletId == chaletId &&
                            r.Status == ReservationStatus.Upcoming &&
                            r.CheckInDate.Date < checkOut.Date &&
                            r.CheckOutDate.Date > checkIn.Date)
                .FirstOrDefault();

            if (resConflict != null)
            {
                var chaletNum = resConflict.Chalet?.Number ?? chaletId.ToString();
                var suggestions = GetAvailableAlternativeChalets(db, chaletId, checkIn, checkOut, ignoreBookingId, null);
                return new BookingConflictResult
                {
                    HasConflict = true,
                    ConflictingGuestName = resConflict.GuestName,
                    ConflictingDates = $"{resConflict.CheckInDate:dd/MM/yyyy} إلى {resConflict.CheckOutDate:dd/MM/yyyy}",
                    ErrorMessage = $"⚠️ الشاليه ({chaletNum}) غير متاح في هذه الفترة!\nمسجل له حجز مسبق باسم: «{resConflict.GuestName}» من {resConflict.CheckInDate:dd/MM/yyyy} إلى {resConflict.CheckOutDate:dd/MM/yyyy}.",
                    SuggestedChalets = suggestions
                };
            }

            return new BookingConflictResult { HasConflict = false };
        }

        public static BookingConflictResult CheckReservationConflict(int chaletId, DateTime checkIn, DateTime checkOut, int? ignoreReservationId = null)
        {
            using var db = CreateContext();

            // 1. Check against active bookings
            var bookingConflict = db.Bookings
                .Include(b => b.Chalet)
                .Where(b => b.ChaletId == chaletId &&
                            b.Status != BookingStatus.Cancelled &&
                            b.Status != BookingStatus.CheckedOut &&
                            b.CheckInDate.Date < checkOut.Date &&
                            b.CheckOutDate.Date > checkIn.Date)
                .FirstOrDefault();

            if (bookingConflict != null)
            {
                var chaletNum = bookingConflict.Chalet?.Number ?? chaletId.ToString();
                var suggestions = GetAvailableAlternativeChalets(db, chaletId, checkIn, checkOut, null, ignoreReservationId);
                return new BookingConflictResult
                {
                    HasConflict = true,
                    ConflictingBooking = bookingConflict,
                    ConflictingGuestName = bookingConflict.GuestName,
                    ConflictingDates = $"{bookingConflict.CheckInDate:dd/MM/yyyy} إلى {bookingConflict.CheckOutDate:dd/MM/yyyy}",
                    ErrorMessage = $"⚠️ الشاليه ({chaletNum}) غير متاح!\nمسجل بإقامة حالية للنزيل: «{bookingConflict.GuestName}» (هاتف: {bookingConflict.PhoneNumber}) في الفترة من {bookingConflict.CheckInDate:dd/MM/yyyy} إلى {bookingConflict.CheckOutDate:dd/MM/yyyy}.",
                    SuggestedChalets = suggestions
                };
            }

            // 2. Check against other pre-reservations
            var resConflict = db.Reservations
                .Include(r => r.Chalet)
                .Where(r => r.ChaletId == chaletId &&
                            r.Status == ReservationStatus.Upcoming &&
                            (!ignoreReservationId.HasValue || r.Id != ignoreReservationId.Value) &&
                            r.CheckInDate.Date < checkOut.Date &&
                            r.CheckOutDate.Date > checkIn.Date)
                .FirstOrDefault();

            if (resConflict != null)
            {
                var chaletNum = resConflict.Chalet?.Number ?? chaletId.ToString();
                var suggestions = GetAvailableAlternativeChalets(db, chaletId, checkIn, checkOut, null, ignoreReservationId);
                return new BookingConflictResult
                {
                    HasConflict = true,
                    ConflictingGuestName = resConflict.GuestName,
                    ConflictingDates = $"{resConflict.CheckInDate:dd/MM/yyyy} إلى {resConflict.CheckOutDate:dd/MM/yyyy}",
                    ErrorMessage = $"⚠️ الشاليه ({chaletNum}) غير متاح!\nمسجل له حجز مسبق بالفعل للنزيل: «{resConflict.GuestName}» (هاتف: {resConflict.PhoneNumber}) في الفترة من {resConflict.CheckInDate:dd/MM/yyyy} إلى {resConflict.CheckOutDate:dd/MM/yyyy}.",
                    SuggestedChalets = suggestions
                };
            }

            return new BookingConflictResult { HasConflict = false };
        }

        public static List<Chalet> GetAvailableAlternativeChalets(AppDbContext db, int requestedChaletId, DateTime checkIn, DateTime checkOut, int? ignoreBookingId = null, int? ignoreReservationId = null)
        {
            var targetChalet = db.Chalets.Find(requestedChaletId);
            if (targetChalet == null) return new List<Chalet>();

            var busyFromBookings = db.Bookings
                .Where(b => b.Status != BookingStatus.Cancelled &&
                            b.Status != BookingStatus.CheckedOut &&
                            (!ignoreBookingId.HasValue || b.Id != ignoreBookingId.Value) &&
                            b.CheckInDate.Date < checkOut.Date &&
                            b.CheckOutDate.Date > checkIn.Date)
                .Select(b => b.ChaletId)
                .Distinct()
                .ToList();

            var busyFromReservations = db.Reservations
                .Where(r => r.Status == ReservationStatus.Upcoming &&
                            (!ignoreReservationId.HasValue || r.Id != ignoreReservationId.Value) &&
                            r.CheckInDate.Date < checkOut.Date &&
                            r.CheckOutDate.Date > checkIn.Date)
                .Select(r => r.ChaletId)
                .Distinct()
                .ToList();

            var busyIds = new HashSet<int>(busyFromBookings.Concat(busyFromReservations));
            busyIds.Add(requestedChaletId);

            var available = db.Chalets
                .Where(c => c.Status != ChaletStatus.Maintenance && c.Number != "B-11" && !busyIds.Contains(c.Id))
                .ToList();

            // Intelligent sorting:
            // 1. Same ChaletType AND same BuildingArea (نفس الفئة في نفس المبنى)
            // 2. Same ChaletType in other BuildingArea (نفس الفئة في مبنى آخر)
            // 3. Other available chalets
            return available
                .OrderByDescending(c => c.ChaletType == targetChalet.ChaletType && c.BuildingArea == targetChalet.BuildingArea)
                .ThenByDescending(c => c.ChaletType == targetChalet.ChaletType)
                .ThenBy(c => c.BuildingArea)
                .ThenBy(c => c.Number)
                .Take(6)
                .ToList();
        }

        public static void CheckinBooking(int bookingId)
        {
            using var db = CreateContext();
            var booking = db.Bookings.Include(b => b.Chalet).FirstOrDefault(b => b.Id == bookingId);
            if (booking != null)
            {
                booking.Status = BookingStatus.Active;
                if (booking.Chalet != null)
                {
                    booking.Chalet.Status = ChaletStatus.Occupied;
                }
                db.SaveChanges();
            }
        }

        public static (bool Success, string Message) ChangeChalet(int bookingId, int newChaletId)
        {
            using var db = CreateContext();
            var booking = db.Bookings.Include(b => b.Chalet).FirstOrDefault(b => b.Id == bookingId);
            if (booking == null) return (false, "الحجز غير موجود");

            var conflict = CheckConflict(newChaletId, booking.CheckInDate, booking.CheckOutDate, booking.Id);
            if (conflict.HasConflict)
            {
                return (false, conflict.ErrorMessage);
            }

            var oldChaletId = booking.ChaletId;
            booking.ChaletId = newChaletId;

            var today = DateTime.Today;
            if (booking.CheckInDate.Date <= today && booking.CheckOutDate.Date > today && booking.Status == BookingStatus.Active)
            {
                var oldC = db.Chalets.Find(oldChaletId);
                if (oldC != null) oldC.Status = ChaletStatus.Available;

                var newC = db.Chalets.Find(newChaletId);
                if (newC != null) newC.Status = ChaletStatus.Occupied;
            }

            db.SaveChanges();
            return (true, "✅ تم تغيير الشاليه بنجاح");
        }
    }

    public class BookingConflictResult
    {
        public bool HasConflict { get; set; }
        public Booking? ConflictingBooking { get; set; }
        public string ConflictingGuestName { get; set; } = string.Empty;
        public string ConflictingDates { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public List<Chalet> SuggestedChalets { get; set; } = new();
    }
}

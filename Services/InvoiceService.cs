using EaglesResort.Data;
using EaglesResort.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EaglesResort.Services
{
    /// <summary>
    /// خدمة خاصة بفواتير الحجز: تحميل الفاتورة، تعديلها (للأدمن فقط)، إلغاؤها
    /// وتسجيل كل حركة في قاعدة البيانات.
    /// </summary>
    public static class InvoiceService
    {
        public static string CurrentUserName =>
            SessionService.Instance.CurrentUser?.FullName is { Length: > 0 } fullName
                ? fullName
                : (SessionService.Instance.CurrentUser?.Username ?? "غير معروف");

        /// <summary>
        /// يعيد حساب كل الإجماليات والملخصات بناءً على القيم المدخلة.
        /// </summary>
        public static void Recalculate(Invoice invoice)
        {
            if (invoice == null) return;

            invoice.MealsTotal = (invoice.AdultMealsCount * invoice.AdultMealRate)
                               + (invoice.ChildMealsCount * invoice.ChildMealRate);

            invoice.NetHousing = Math.Max(0, invoice.ChaletTotal - invoice.Deposit);

            invoice.GrandTotal = invoice.ChaletTotal + invoice.MealsTotal + invoice.ExtraServices + invoice.Insurance;

            invoice.TotalAfterDeposit = invoice.NetHousing + invoice.MealsTotal + invoice.ExtraServices + invoice.Insurance;

            invoice.PaymentDetails = BuildPaymentBreakdown(invoice.CashAmount, invoice.InstaPayAmount, invoice.VisaAmount, invoice.BankTransferAmount, invoice.HousingPaymentMethod);
            invoice.MealsPaymentDetails = BuildPaymentBreakdown(invoice.MealsCashAmount, invoice.MealsInstaPayAmount, invoice.MealsVisaAmount, invoice.MealsBankTransferAmount, invoice.MealsPaymentMethod);
        }

        private static string BuildPaymentBreakdown(decimal cash, decimal insta, decimal visa, decimal bank, string fallback)
        {
            var parts = new List<string>();
            if (cash > 0) parts.Add($"كاش: {cash:N0} ج.م");
            if (insta > 0) parts.Add($"انستا باي: {insta:N0} ج.م");
            if (visa > 0) parts.Add($"فيزا: {visa:N0} ج.م");
            if (bank > 0) parts.Add($"تحويل بنكي: {bank:N0} ج.م");

            return parts.Count > 0 ? string.Join(" | ", parts) : (fallback ?? string.Empty);
        }

        public static Invoice? GetById(int invoiceId)
        {
            using var db = new AppDbContext();
            var invoice = db.Invoices
                .Include(i => i.Booking)!.ThenInclude(b => b!.Chalet)
                .Include(i => i.Bookings).ThenInclude(b => b.Chalet)
                .FirstOrDefault(i => i.Id == invoiceId);

            if (invoice != null)
                invoice.ActionLogs = GetActionLogs(invoice.Id);

            return invoice;
        }

        public static Invoice? GetByBookingId(int bookingId)
        {
            using var db = new AppDbContext();
            var invoice = db.Invoices
                .Include(i => i.Booking)!.ThenInclude(b => b!.Chalet)
                .Include(i => i.Bookings).ThenInclude(b => b.Chalet)
                .FirstOrDefault(i => i.BookingId == bookingId || i.Bookings.Any(b => b.Id == bookingId));

            if (invoice != null)
                invoice.ActionLogs = GetActionLogs(invoice.Id);

            return invoice;
        }

        public static List<InvoiceActionLog> GetActionLogs(int invoiceId)
        {
            try
            {
                using var db = new AppDbContext();
                return db.InvoiceActionLogs
                    .Where(l => l.InvoiceId == invoiceId)
                    .OrderByDescending(l => l.PerformedAt)
                    .ThenByDescending(l => l.Id)
                    .ToList();
            }
            catch
            {
                return new List<InvoiceActionLog>();
            }
        }

        /// <summary>
        /// تسجيل حركة على الفاتورة (تعديل/إلغاء/طباعة/مغادرة) في قاعدة البيانات.
        /// </summary>
        public static void LogAction(int invoiceId, int? bookingId, string actionType, string actionTitle, string details)
        {
            if (invoiceId <= 0) return;

            try
            {
                using var db = new AppDbContext();
                db.InvoiceActionLogs.Add(new InvoiceActionLog
                {
                    InvoiceId = invoiceId,
                    BookingId = bookingId,
                    ActionType = actionType,
                    ActionTitle = actionTitle,
                    PerformedBy = CurrentUserName,
                    PerformedAt = DateTime.Now,
                    Details = details ?? string.Empty
                });
                db.SaveChanges();
            }
            catch
            {
                // تسجيل الحركة لا يجب أن يمنع العملية الأساسية
            }
        }

        /// <summary>
        /// يحفظ تعديلات الفاتورة ويسجل التغييرات. مسموح للأدمن فقط.
        /// </summary>
        public static (bool Success, string Message) UpdateInvoice(Invoice edited, string performedBy)
        {
            if (!SessionService.Instance.IsAdmin)
                return (false, "غير مصرح: تعديل الفاتورة متاح لحساب مدير النظام فقط.");

            if (edited == null || edited.Id <= 0)
                return (false, "الفاتورة غير موجودة.");

            var editorName = string.IsNullOrWhiteSpace(performedBy) ? CurrentUserName : performedBy.Trim();

            try
            {
                using var db = new AppDbContext();
                var invoice = db.Invoices.FirstOrDefault(i => i.Id == edited.Id);
                if (invoice == null) return (false, "الفاتورة غير موجودة في قاعدة البيانات.");

                if (invoice.IsCancelled)
                    return (false, "لا يمكن تعديل فاتورة ملغية.");

                // نحتفظ بالقيم القديمة لعمل ملخص التغييرات
                var before = Snapshot(invoice);

                invoice.AdultMealsCount = edited.AdultMealsCount;
                invoice.AdultMealRate = edited.AdultMealRate;
                invoice.ChildMealsCount = edited.ChildMealsCount;
                invoice.ChildMealRate = edited.ChildMealRate;
                invoice.ExtraServices = edited.ExtraServices;
                invoice.ExtraServicesNote = edited.ExtraServicesNote ?? string.Empty;
                invoice.Insurance = edited.Insurance;
                invoice.Deposit = edited.Deposit;
                invoice.DepositDate = edited.DepositDate;

                invoice.CashAmount = edited.CashAmount;
                invoice.InstaPayAmount = edited.InstaPayAmount;
                invoice.VisaAmount = edited.VisaAmount;
                invoice.BankTransferAmount = edited.BankTransferAmount;

                invoice.MealsCashAmount = edited.MealsCashAmount;
                invoice.MealsInstaPayAmount = edited.MealsInstaPayAmount;
                invoice.MealsVisaAmount = edited.MealsVisaAmount;
                invoice.MealsBankTransferAmount = edited.MealsBankTransferAmount;

                Recalculate(invoice);

                var changes = BuildChangesSummary(before, invoice);

                invoice.LastModifiedAt = DateTime.Now;
                invoice.LastModifiedBy = editorName;

                db.SaveChanges();

                db.InvoiceActionLogs.Add(new InvoiceActionLog
                {
                    InvoiceId = invoice.Id,
                    BookingId = invoice.BookingId,
                    ActionType = "Edited",
                    ActionTitle = "تعديل الفاتورة",
                    PerformedBy = editorName,
                    PerformedAt = DateTime.Now,
                    Details = changes
                });
                db.SaveChanges();

                // نرجع نفس الكائن محدث للاستخدام في العرض/الطباعة
                CopyValues(invoice, edited);
                edited.ActionLogs = GetActionLogs(invoice.Id);
                return (true, "✅ تم حفظ تعديلات الفاتورة بنجاح");
            }
            catch (Exception ex)
            {
                return (false, $"خطأ أثناء حفظ الفاتورة: {ex.Message}");
            }
        }

        /// <summary>
        /// إلغاء الفاتورة وكل الحجوزات المرتبطة بها مع تسجيل سبب الإلغاء.
        /// </summary>
        public static (bool Success, string Message) CancelInvoice(int invoiceId, string reason, string performedBy)
        {
            if (!SessionService.Instance.IsAdmin)
                return (false, "غير مصرح: إلغاء الفاتورة متاح لحساب مدير النظام فقط.");

            var cancelledBy = string.IsNullOrWhiteSpace(performedBy) ? CurrentUserName : performedBy.Trim();

            try
            {
                using var db = new AppDbContext();
                var invoice = db.Invoices.FirstOrDefault(i => i.Id == invoiceId);
                if (invoice == null) return (false, "الفاتورة غير موجودة.");

                if (invoice.IsCancelled)
                    return (false, "هذه الفاتورة ملغية بالفعل.");

                var bookings = db.Bookings
                    .Include(b => b.Chalet)
                    .Where(b => b.InvoiceId == invoice.Id || b.Id == invoice.BookingId)
                    .ToList();

                invoice.IsCancelled = true;
                invoice.CancelledAt = DateTime.Now;
                invoice.CancelledBy = cancelledBy;
                invoice.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "بدون سبب مسجل" : reason.Trim();

                foreach (var booking in bookings)
                {
                    booking.Status = BookingStatus.Cancelled;
                    if (booking.Chalet != null && booking.Chalet.Status == ChaletStatus.Occupied)
                        booking.Chalet.Status = ChaletStatus.Available;
                }

                db.SaveChanges();

                var chaletNumbers = string.Join("، ", bookings
                    .Select(b => b.Chalet?.Number)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct());

                db.InvoiceActionLogs.Add(new InvoiceActionLog
                {
                    InvoiceId = invoice.Id,
                    BookingId = invoice.BookingId,
                    ActionType = "Cancelled",
                    ActionTitle = "إلغاء الفاتورة",
                    PerformedBy = cancelledBy,
                    PerformedAt = DateTime.Now,
                    Details = $"السبب: {invoice.CancellationReason}" +
                              (string.IsNullOrWhiteSpace(chaletNumbers) ? "" : $" | الشاليهات: {chaletNumbers}") +
                              $" | عدد الحجوزات الملغية: {bookings.Count}"
                });
                db.SaveChanges();

                return (true, "✅ تم إلغاء الفاتورة وتسجيل الحركة بنجاح");
            }
            catch (Exception ex)
            {
                return (false, $"خطأ أثناء إلغاء الفاتورة: {ex.Message}");
            }
        }

        private static Invoice Snapshot(Invoice source) => new()
        {
            AdultMealsCount = source.AdultMealsCount,
            AdultMealRate = source.AdultMealRate,
            ChildMealsCount = source.ChildMealsCount,
            ChildMealRate = source.ChildMealRate,
            ExtraServices = source.ExtraServices,
            ExtraServicesNote = source.ExtraServicesNote,
            Insurance = source.Insurance,
            Deposit = source.Deposit,
            DepositDate = source.DepositDate,
            CashAmount = source.CashAmount,
            InstaPayAmount = source.InstaPayAmount,
            VisaAmount = source.VisaAmount,
            BankTransferAmount = source.BankTransferAmount,
            MealsCashAmount = source.MealsCashAmount,
            MealsInstaPayAmount = source.MealsInstaPayAmount,
            MealsVisaAmount = source.MealsVisaAmount,
            MealsBankTransferAmount = source.MealsBankTransferAmount
        };

        private static string BuildChangesSummary(Invoice before, Invoice after)
        {
            var parts = new List<string>();

            void Add(string label, decimal oldValue, decimal newValue)
            {
                if (oldValue != newValue)
                    parts.Add($"{label}: {oldValue:N0} ← {newValue:N0}");
            }

            Add("وجبات البالغين (عدد)", before.AdultMealsCount, after.AdultMealsCount);
            Add("سعر وجبة البالغ", before.AdultMealRate, after.AdultMealRate);
            Add("وجبات الأطفال (عدد)", before.ChildMealsCount, after.ChildMealsCount);
            Add("سعر وجبة الطفل", before.ChildMealRate, after.ChildMealRate);
            Add("خدمات إضافية", before.ExtraServices, after.ExtraServices);
            Add("التأمين", before.Insurance, after.Insurance);
            Add("العربون", before.Deposit, after.Deposit);

            Add("كاش السكن", before.CashAmount, after.CashAmount);
            Add("فيزا السكن", before.VisaAmount, after.VisaAmount);
            Add("انستا باي السكن", before.InstaPayAmount, after.InstaPayAmount);
            Add("تحويل بنكي للسكن", before.BankTransferAmount, after.BankTransferAmount);

            Add("كاش الوجبات", before.MealsCashAmount, after.MealsCashAmount);
            Add("فيزا الوجبات", before.MealsVisaAmount, after.MealsVisaAmount);
            Add("انستا باي الوجبات", before.MealsInstaPayAmount, after.MealsInstaPayAmount);
            Add("تحويل بنكي للوجبات", before.MealsBankTransferAmount, after.MealsBankTransferAmount);

            if ((before.ExtraServicesNote ?? "") != (after.ExtraServicesNote ?? ""))
                parts.Add("ملاحظات الخدمات الإضافية تم تغييرها");

            if (before.DepositDate != after.DepositDate)
                parts.Add($"تاريخ العربون: {before.DepositDate:dd/MM/yyyy} ← {after.DepositDate:dd/MM/yyyy}");

            return parts.Count == 0
                ? "تم حفظ الفاتورة بدون تغيير في القيم."
                : string.Join(" • ", parts);
        }

        private static void CopyValues(Invoice source, Invoice target)
        {
            target.MealsTotal = source.MealsTotal;
            target.NetHousing = source.NetHousing;
            target.GrandTotal = source.GrandTotal;
            target.TotalAfterDeposit = source.TotalAfterDeposit;
            target.PaymentDetails = source.PaymentDetails;
            target.MealsPaymentDetails = source.MealsPaymentDetails;
            target.Deposit = source.Deposit;
            target.Insurance = source.Insurance;
            target.ExtraServices = source.ExtraServices;
            target.AdultMealsCount = source.AdultMealsCount;
            target.AdultMealRate = source.AdultMealRate;
            target.ChildMealsCount = source.ChildMealsCount;
            target.ChildMealRate = source.ChildMealRate;

            target.LastModifiedAt = source.LastModifiedAt;
            target.LastModifiedBy = source.LastModifiedBy;
            target.IsCancelled = source.IsCancelled;
            target.CancelledAt = source.CancelledAt;
            target.CancelledBy = source.CancelledBy;
            target.CancellationReason = source.CancellationReason;
        }
    }
}

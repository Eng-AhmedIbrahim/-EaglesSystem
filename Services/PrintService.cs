using EaglesResort.Models;
using EaglesResort.Data;
using System.Windows.Controls;
using EaglesResort.Views;
using Microsoft.EntityFrameworkCore;

namespace EaglesResort.Services
{
    public static class PrintService
    {
        public static void PrintInvoice(Invoice invoice, string? targetPrinter = null)
        {
            if (invoice?.Booking == null) return;
            var booking = invoice.Booking;
            var chalet = booking.Chalet;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // Ensure all bookings for this invoice are loaded
                if (invoice.Bookings == null || !invoice.Bookings.Any())
                {
                    using var db = new AppDbContext();
                    invoice.Bookings = db.Bookings
                        .Include(b => b.Chalet)
                        .Where(b => (invoice.Id > 0 && b.InvoiceId == invoice.Id) || b.Id == invoice.BookingId)
                        .ToList();
                }

                var primaryBooking = invoice.Bookings.FirstOrDefault() ?? booking;
                var chaletList = invoice.Bookings.Any() ? invoice.Bookings.ToList() : new List<Booking> { booking };

                // Load dynamic settings from DB
                string restaurantTerms, resortManager;
                using (var db = new AppDbContext())
                {
                    restaurantTerms = db.AppSettings.FirstOrDefault(s => s.Key == "RestaurantTerms")?.Value
                        ?? "الفطار من الساعة 9ص حتي 11ص و العشاء من الساعة 6م حتي 8م والمطعم غير مسئول في حالة التأخير عن المواعيد المحددة";
                    resortManager = db.AppSettings.FirstOrDefault(s => s.Key == "ResortManagerName")?.Value
                        ?? invoice.ResortManagerName;
                }

                // Show print dialog once
                var printDlg = new PrintDialog();
                bool doPrint = false;
                if (!string.IsNullOrEmpty(targetPrinter))
                {
                    try
                    {
                        var queue = new System.Printing.LocalPrintServer().GetPrintQueue(targetPrinter);
                        printDlg.PrintQueue = queue;
                        doPrint = true;
                    }
                    catch { }
                }
                if (!doPrint)
                {
                    if (printDlg.ShowDialog() != true) return;
                }

                double pageWidth = printDlg.PrintableAreaWidth;
                double pageHeight = printDlg.PrintableAreaHeight;

                if (chaletList.Count <= 1)
                {
                    // --- Single chalet: print one invoice ---
                    var dc = BuildDataContext(invoice, chaletList, primaryBooking, restaurantTerms, resortManager, isSingle: true);
                    PrintOnePage(printDlg, pageWidth, pageHeight, dc, "Invoice " + invoice.Id);
                }
                else
                {
                    // --- Multi-chalet: print one invoice per chalet ---
                    // Format guest name with military rank if present
                    var formattedGuestName = !string.IsNullOrWhiteSpace(primaryBooking.GuestRank)
                        ? $"{primaryBooking.GuestRank} / {primaryBooking.GuestName}"
                        : primaryBooking.GuestName;

                    for (int i = 0; i < chaletList.Count; i++)
                    {
                        var cb = chaletList[i];

                        // Each chalet gets its own totals
                        decimal thisChaletTotal = cb.NumberOfDays * invoice.DailyRate;
                        // Use actual ChaletTotal from invoice only on the first chalet if single
                        // For multi, calculate per-chalet housing total from daily rate
                        // If the invoice stores a per-chalet daily rate, use it; otherwise use invoice.DailyRate
                        decimal dailyRate = invoice.DailyRate;

                        // Adult & child meals: same across all chalets (whole stay meals)
                        decimal adultMealsTotal = invoice.AdultMealsCount * invoice.AdultMealRate;
                        decimal childMealsTotal = invoice.ChildMealsCount * invoice.ChildMealRate;
                        decimal mealsTotal = adultMealsTotal + childMealsTotal;

                        // Deposit only on first page, rest = 0
                        decimal depositForThisPage = (i == 0) ? invoice.Deposit : 0m;

                        // Extra services only on first page
                        decimal extraForThisPage = (i == 0) ? invoice.ExtraServices : 0m;

                        // Insurance only on first page
                        decimal insuranceForThisPage = (i == 0) ? invoice.Insurance : 0m;

                        // InvoiceTotal = chalet housing + meals + extra (before deposit/insurance)
                        decimal invoiceTotal = thisChaletTotal + mealsTotal + extraForThisPage;

                        // TotalAfterDeposit (shown as "إجمالي المتبقي") = invoiceTotal + insurance - deposit
                        decimal totalAfterDeposit = invoiceTotal + insuranceForThisPage - depositForThisPage;

                        // ChaletRemaining = chalet housing - deposit (remaining to pay for housing)
                        decimal chaletRemaining = thisChaletTotal - depositForThisPage;

                        // MealsTotal for payment table
                        decimal mealsPaidDisplay = mealsTotal;

                        string extraNote = (i == 0) ? invoice.ExtraServicesNote ?? "" : "";

                        string depositDateStr = (i == 0 && invoice.DepositDate.HasValue)
                            ? invoice.DepositDate.Value.ToString("yyyy/MM/dd")
                            : "-";

                        // DepositFormatted shown as negative (deduction)
                        string depositFormatted = depositForThisPage == 0 ? "." : $"({depositForThisPage:N0})";

                        // MealDays = number of days for this chalet booking
                        int mealDays = cb.NumberOfDays;

                        // Housing payment details (only show on first page)
                        string housingPaymentText = (i == 0 && !string.IsNullOrWhiteSpace(invoice.PaymentDetails))
                            ? invoice.PaymentDetails
                            : (i == 0 ? invoice.HousingPaymentMethod : "");

                        string mealsPaymentText = string.IsNullOrWhiteSpace(invoice.MealsPaymentDetails)
                            ? (invoice.MealsTotal == 0 ? "بدون وجبات" : invoice.MealsPaymentMethod)
                            : invoice.MealsPaymentDetails;

                        var dc = new
                        {
                            GuestName = formattedGuestName,
                            CheckInDate = cb.CheckInDate.ToString("yyyy/MM/dd"),
                            CheckOutDate = cb.CheckOutDate.ToString("yyyy/MM/dd"),
                            PersonsCount = $"{primaryBooking.AdultCount + primaryBooking.ChildCount} ({primaryBooking.AdultCount} بالغ / {primaryBooking.ChildCount} طفل)",
                            ChaletInfo = cb.Chalet?.Number ?? "",
                            MembershipNumber = string.IsNullOrEmpty(primaryBooking.MembershipNumber) ? "لا يوجد" : primaryBooking.MembershipNumber,
                            Days = cb.NumberOfDays.ToString(),
                            DailyRate = dailyRate.ToString("N0"),
                            ChaletTotal = thisChaletTotal.ToString("N0"),
                            ChaletRemaining = chaletRemaining.ToString("N0"),

                            AdultMealsCount = invoice.AdultMealsCount.ToString(),
                            AdultMealRate = invoice.AdultMealRate.ToString("N0"),
                            AdultMealsTotal = adultMealsTotal.ToString("N0"),

                            ChildMealsCount = invoice.ChildMealsCount.ToString(),
                            ChildMealRate = invoice.ChildMealRate.ToString("N0"),
                            ChildMealsTotal = childMealsTotal.ToString("N0"),

                            ExtraServicesNote = extraNote,
                            ExtraServices = extraForThisPage == 0 ? "." : extraForThisPage.ToString("N0"),
                            ExtraServicesFormatted = extraForThisPage == 0 ? "." : extraForThisPage.ToString("N0"),
                            HasExtraServices = extraForThisPage > 0,

                            Insurance = insuranceForThisPage.ToString("N0"),
                            InsuranceFormatted = insuranceForThisPage == 0 ? "." : insuranceForThisPage.ToString("N0"),

                            DepositDate = depositDateStr,
                            DepositFormatted = depositFormatted,

                            InvoiceTotal = invoiceTotal.ToString("N0"),
                            TotalAfterDeposit = totalAfterDeposit.ToString("N0"),
                            GrandTotal = (invoiceTotal + insuranceForThisPage - depositForThisPage).ToString("N0"),
                            GrandTotalWithoutInsurance = invoiceTotal.ToString("N0"),

                            MealDays = mealDays.ToString(),
                            MealsTotalFormatted = mealsPaidDisplay.ToString("N0"),

                            InvoiceDate = invoice.InvoiceDate.ToString("yyyy/MM/dd"),
                            FullInvoiceDate = "تاريخ إصدار الفاتورة: " + invoice.InvoiceDate.ToString("yyyy/MM/dd h:mm tt"),
                            ReceptionistName = invoice.ReceptionistName,
                            AccountantName = invoice.AccountantName,
                            PhoneNumber = primaryBooking.PhoneNumber,
                            SecondaryPhoneNumber = primaryBooking.SecondaryPhoneNumber,
                            ResortManagerName = resortManager,
                            RestaurantTerms = restaurantTerms,

                            HousingPaymentMethod = housingPaymentText,
                            NetHousingFormatted = thisChaletTotal.ToString("N0"),
                            MealsPaymentMethod = mealsPaymentText,

                            HousingPaymentText = housingPaymentText,
                            MealsPaymentText = mealsPaymentText,
                            HousingPaidFormatted = thisChaletTotal.ToString("N0"),
                            MealsPaidFormatted = mealsPaidDisplay.ToString("N0")
                        };

                        PrintOnePage(printDlg, pageWidth, pageHeight, dc, $"Invoice {invoice.Id} - Chalet {cb.Chalet?.Number}");
                    }
                }
            });
        }

        /// <summary>
        /// Builds DataContext for single-chalet printing.
        /// </summary>
        private static object BuildDataContext(
            Invoice invoice,
            List<Booking> chaletList,
            Booking primaryBooking,
            string restaurantTerms,
            string resortManager,
            bool isSingle)
        {
            // Format guest name with military rank if present
            var formattedGuestName = !string.IsNullOrWhiteSpace(primaryBooking.GuestRank)
                ? $"{primaryBooking.GuestRank} / {primaryBooking.GuestName}"
                : primaryBooking.GuestName;

            // Format chalet info
            var chaletNumbers = chaletList
                .Select(b => b.Chalet?.Number ?? "")
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .ToList();

            string chaletInfoText = chaletNumbers.Any()
                ? string.Join(" , ", chaletNumbers)
                : (primaryBooking.Chalet?.Number ?? "");

            var minCheckIn = chaletList.Min(b => b.CheckInDate);
            var maxCheckOut = chaletList.Max(b => b.CheckOutDate);
            var totalDays = chaletList.Sum(b => b.NumberOfDays);

            // Housing payment details
            string housingPaymentText = !string.IsNullOrWhiteSpace(invoice.PaymentDetails)
                ? invoice.PaymentDetails
                : invoice.HousingPaymentMethod;

            decimal housingPaid = invoice.CashAmount + invoice.InstaPayAmount + invoice.VisaAmount + invoice.BankTransferAmount;
            string housingPaidDisplay;
            if (housingPaid > 0 && housingPaid < invoice.NetHousing)
            {
                decimal rem = invoice.NetHousing - housingPaid;
                housingPaidDisplay = $"مسدد: {housingPaid:N0} (متبقي: {rem:N0})";
            }
            else if (housingPaid > 0)
                housingPaidDisplay = housingPaid.ToString("N0");
            else
                housingPaidDisplay = invoice.NetHousing.ToString("N0");

            // Meals payment details
            string mealsPaymentText;
            if (!string.IsNullOrWhiteSpace(invoice.MealsPaymentDetails))
                mealsPaymentText = invoice.MealsPaymentDetails;
            else if (invoice.MealsTotal == 0)
                mealsPaymentText = "بدون وجبات";
            else
                mealsPaymentText = invoice.MealsPaymentMethod;

            decimal mealsPaid = invoice.MealsCashAmount + invoice.MealsInstaPayAmount + invoice.MealsVisaAmount + invoice.MealsBankTransferAmount;
            string mealsPaidDisplay;
            if (mealsPaid > 0 && mealsPaid < invoice.MealsTotal)
            {
                decimal remMeals = invoice.MealsTotal - mealsPaid;
                mealsPaidDisplay = $"مسدد: {mealsPaid:N0} (متبقي: {remMeals:N0})";
            }
            else if (mealsPaid > 0)
                mealsPaidDisplay = mealsPaid.ToString("N0");
            else
                mealsPaidDisplay = invoice.MealsTotal.ToString("N0");

            // Extra note
            var extraNote = invoice.ExtraServicesNote;
            if (chaletList.Count > 1 && !string.IsNullOrWhiteSpace(invoice.ChaletsSummary))
            {
                extraNote = string.IsNullOrWhiteSpace(extraNote)
                    ? invoice.ChaletsSummary
                    : $"{extraNote} | {invoice.ChaletsSummary}";
            }

            // InvoiceTotal = ChaletTotal + MealsTotal + ExtraServices (before deposit & insurance)
            decimal invoiceTotal = invoice.ChaletTotal + invoice.MealsTotal + invoice.ExtraServices;
            // TotalAfterDeposit = invoiceTotal + insurance - deposit
            decimal totalAfterDeposit = invoiceTotal + invoice.Insurance - invoice.Deposit;
            // ChaletRemaining = ChaletTotal - Deposit
            decimal chaletRemaining = invoice.ChaletTotal - invoice.Deposit;
            // MealDays for single chalet
            int mealDays = isSingle ? chaletList[0].NumberOfDays : totalDays;

            return new
            {
                GuestName = formattedGuestName,
                CheckInDate = minCheckIn.ToString("yyyy/MM/dd"),
                CheckOutDate = maxCheckOut.ToString("yyyy/MM/dd"),
                PersonsCount = $"{primaryBooking.AdultCount + primaryBooking.ChildCount} ({primaryBooking.AdultCount} بالغ / {primaryBooking.ChildCount} طفل)",
                ChaletInfo = chaletInfoText,
                MembershipNumber = string.IsNullOrEmpty(primaryBooking.MembershipNumber) ? "لا يوجد" : primaryBooking.MembershipNumber,
                Days = totalDays.ToString(),
                DailyRate = isSingle ? invoice.DailyRate.ToString("N0") : "-",
                ChaletTotal = invoice.ChaletTotal.ToString("N0"),
                ChaletRemaining = chaletRemaining.ToString("N0"),

                AdultMealsCount = invoice.AdultMealsCount.ToString(),
                AdultMealRate = invoice.AdultMealRate.ToString("N0"),
                AdultMealsTotal = (invoice.AdultMealsCount * invoice.AdultMealRate).ToString("N0"),

                ChildMealsCount = invoice.ChildMealsCount.ToString(),
                ChildMealRate = invoice.ChildMealRate.ToString("N0"),
                ChildMealsTotal = (invoice.ChildMealsCount * invoice.ChildMealRate).ToString("N0"),

                ExtraServicesNote = extraNote,
                ExtraServices = invoice.ExtraServices == 0 ? "." : invoice.ExtraServices.ToString("N0"),
                ExtraServicesFormatted = invoice.ExtraServices == 0 ? "." : invoice.ExtraServices.ToString("N0"),
                HasExtraServices = invoice.ExtraServices > 0,

                Insurance = invoice.Insurance.ToString("N0"),
                InsuranceFormatted = invoice.Insurance == 0 ? "." : invoice.Insurance.ToString("N0"),

                DepositDate = invoice.DepositDate?.ToString("yyyy/MM/dd") ?? "-",
                DepositFormatted = invoice.Deposit == 0 ? "." : $"({invoice.Deposit:N0})",

                InvoiceTotal = invoiceTotal.ToString("N0"),
                TotalAfterDeposit = totalAfterDeposit.ToString("N0"),
                GrandTotal = invoice.GrandTotal.ToString("N0"),
                GrandTotalWithoutInsurance = invoiceTotal.ToString("N0"),

                MealDays = mealDays.ToString(),
                MealsTotalFormatted = mealsPaidDisplay,

                InvoiceDate = invoice.InvoiceDate.ToString("yyyy/MM/dd"),
                FullInvoiceDate = "تاريخ إصدار الفاتورة: " + invoice.InvoiceDate.ToString("yyyy/MM/dd h:mm tt"),
                ReceptionistName = invoice.ReceptionistName,
                AccountantName = invoice.AccountantName,
                PhoneNumber = primaryBooking.PhoneNumber,
                SecondaryPhoneNumber = primaryBooking.SecondaryPhoneNumber,
                ResortManagerName = resortManager,
                RestaurantTerms = restaurantTerms,

                HousingPaymentMethod = housingPaymentText,
                NetHousingFormatted = housingPaidDisplay,
                MealsPaymentMethod = mealsPaymentText,

                HousingPaymentText = housingPaymentText,
                MealsPaymentText = mealsPaymentText,
                HousingPaidFormatted = housingPaidDisplay,
                MealsPaidFormatted = mealsPaidDisplay
            };
        }

        /// <summary>
        /// Renders and prints one InvoicePrintViewNew page with the given DataContext.
        /// </summary>
        private static void PrintOnePage(PrintDialog printDlg, double pageWidth, double pageHeight, object dataContext, string jobTitle)
        {
            var printView = new InvoicePrintViewNew();
            printView.DataContext = dataContext;

            printView.Measure(new System.Windows.Size(pageWidth, pageHeight));
            printView.Arrange(new System.Windows.Rect(new System.Windows.Size(pageWidth, pageHeight)));
            printView.UpdateLayout();

            printDlg.PrintVisual(printView, jobTitle);
        }
    }
}

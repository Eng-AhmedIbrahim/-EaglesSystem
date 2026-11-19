using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EaglesResort.Models;
using EaglesResort.Services;

namespace EaglesResort.Views.Dialogs
{
    /// <summary>
    /// نافذة تعديل الفاتورة (للأدمن فقط) - تُستخدم من نافذة تفاصيل الفاتورة.
    /// </summary>
    public partial class InvoiceEditWindow : Window
    {
        private readonly Invoice _source;
        private readonly Dictionary<string, TextBox> _boxes = new();

        /// <summary>اسم من قام بالتعديل (إلزامي) — يُسجَّل في سجل الحركات وعلى الفاتورة.</summary>
        public string EditorName { get; private set; } = string.Empty;

        public Invoice EditedInvoice { get; private set; }

        public InvoiceEditWindow(Invoice invoice)
        {
            InitializeComponent();

            _source = invoice ?? throw new ArgumentNullException(nameof(invoice));
            EditedInvoice = new Invoice { Id = invoice.Id };

            InvoiceNumberText.Text = invoice.InvoiceNumberDisplay;

            // ربط الحقول بالكائنات
            _boxes[nameof(Invoice.AdultMealsCount)] = AdultMealsCountBox;
            _boxes[nameof(Invoice.AdultMealRate)] = AdultMealRateBox;
            _boxes[nameof(Invoice.ChildMealsCount)] = ChildMealsCountBox;
            _boxes[nameof(Invoice.ChildMealRate)] = ChildMealRateBox;
            _boxes[nameof(Invoice.ExtraServices)] = ExtraServicesBox;
            _boxes[nameof(Invoice.ExtraServicesNote)] = ExtraServicesNoteBox;
            _boxes[nameof(Invoice.Insurance)] = InsuranceBox;
            _boxes[nameof(Invoice.Deposit)] = DepositBox;
            _boxes[nameof(Invoice.DepositDate)] = DepositDateBox;
            _boxes[nameof(Invoice.CashAmount)] = CashAmountBox;
            _boxes[nameof(Invoice.VisaAmount)] = VisaAmountBox;
            _boxes[nameof(Invoice.InstaPayAmount)] = InstaPayAmountBox;
            _boxes[nameof(Invoice.BankTransferAmount)] = BankTransferAmountBox;
            _boxes[nameof(Invoice.MealsCashAmount)] = MealsCashAmountBox;
            _boxes[nameof(Invoice.MealsVisaAmount)] = MealsVisaAmountBox;
            _boxes[nameof(Invoice.MealsInstaPayAmount)] = MealsInstaPayAmountBox;
            _boxes[nameof(Invoice.MealsBankTransferAmount)] = MealsBankTransferAmountBox;

            // القيم الحالية
            AdultMealsCountBox.Text = invoice.AdultMealsCount.ToString();
            AdultMealRateBox.Text = FormatValue(invoice.AdultMealRate);
            ChildMealsCountBox.Text = invoice.ChildMealsCount.ToString();
            ChildMealRateBox.Text = FormatValue(invoice.ChildMealRate);
            ExtraServicesBox.Text = FormatValue(invoice.ExtraServices);
            ExtraServicesNoteBox.Text = invoice.ExtraServicesNote ?? string.Empty;
            InsuranceBox.Text = FormatValue(invoice.Insurance);
            DepositBox.Text = FormatValue(invoice.Deposit);
            DepositDateBox.Text = invoice.DepositDate?.ToString("yyyy/MM/dd") ?? string.Empty;
            CashAmountBox.Text = FormatValue(invoice.CashAmount);
            VisaAmountBox.Text = FormatValue(invoice.VisaAmount);
            InstaPayAmountBox.Text = FormatValue(invoice.InstaPayAmount);
            BankTransferAmountBox.Text = FormatValue(invoice.BankTransferAmount);
            MealsCashAmountBox.Text = FormatValue(invoice.MealsCashAmount);
            MealsVisaAmountBox.Text = FormatValue(invoice.MealsVisaAmount);
            MealsInstaPayAmountBox.Text = FormatValue(invoice.MealsInstaPayAmount);
            MealsBankTransferAmountBox.Text = FormatValue(invoice.MealsBankTransferAmount);

            // اسم المعدّل مبدئياً هو المستخدم الحالي (قابل للتعديل والإلزامية عند الحفظ)
            EditorNameBox.Text = InvoiceService.CurrentUserName;

            // حساب مباشر أثناء الكتابة
            foreach (var key in new[]
                     {
                         nameof(Invoice.AdultMealsCount), nameof(Invoice.AdultMealRate),
                         nameof(Invoice.ChildMealsCount), nameof(Invoice.ChildMealRate),
                         nameof(Invoice.ExtraServices), nameof(Invoice.Insurance),
                         nameof(Invoice.Deposit)
                     })
            {
                _boxes[key].TextChanged += (_, _) => UpdatePreview();
            }

            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var meals = TryRead(nameof(Invoice.AdultMealsCount)) * TryRead(nameof(Invoice.AdultMealRate))
                      + TryRead(nameof(Invoice.ChildMealsCount)) * TryRead(nameof(Invoice.ChildMealRate));
            var extra = TryRead(nameof(Invoice.ExtraServices));
            var insurance = TryRead(nameof(Invoice.Insurance));
            var deposit = TryRead(nameof(Invoice.Deposit));

            var chalet = _source.ChaletTotal;
            var netHousing = Math.Max(0, chalet - deposit);
            var grand = chalet + meals + extra + insurance;
            var remaining = netHousing + meals + extra + insurance;

            ChaletTotalText.Text = $"{chalet:N0} ج.م";
            GrandTotalText.Text = $"{grand:N0} ج.م";
            InsuranceTotalText.Text = $"{insurance:N0} ج.م";
            RemainingTotalText.Text = $"{remaining:N0} ج.م";
            MealsTotalPreview.Text = $"إجمالي الوجبات: {meals:N0} ج.م";
        }

        private decimal TryRead(string key)
        {
            if (!_boxes.TryGetValue(key, out var box)) return 0m;
            var text = box.Text?.Trim();
            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var value) && value >= 0
                ? value
                : 0m;
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var editorName = EditorNameBox.Text?.Trim() ?? string.Empty;
            if (editorName.Length < 3)
            {
                StatusText.Text = "من فضلك اكتب اسم من قام بالتعديل (3 أحرف على الأقل) — إلزامي ليتم تسجيله في سجل الحركات.";
                EditorNameBox.Focus();
                return;
            }

            try
            {
                var edited = new Invoice
                {
                    Id = _source.Id,
                    AdultMealsCount = ReadInt(nameof(Invoice.AdultMealsCount)),
                    AdultMealRate = ReadDecimal(nameof(Invoice.AdultMealRate)),
                    ChildMealsCount = ReadInt(nameof(Invoice.ChildMealsCount)),
                    ChildMealRate = ReadDecimal(nameof(Invoice.ChildMealRate)),

                    ExtraServices = ReadDecimal(nameof(Invoice.ExtraServices)),
                    ExtraServicesNote = ExtraServicesNoteBox.Text?.Trim() ?? string.Empty,
                    Insurance = ReadDecimal(nameof(Invoice.Insurance)),

                    Deposit = ReadDecimal(nameof(Invoice.Deposit)),
                    DepositDate = ReadDate(nameof(Invoice.DepositDate)),

                    CashAmount = ReadDecimal(nameof(Invoice.CashAmount)),
                    VisaAmount = ReadDecimal(nameof(Invoice.VisaAmount)),
                    InstaPayAmount = ReadDecimal(nameof(Invoice.InstaPayAmount)),
                    BankTransferAmount = ReadDecimal(nameof(Invoice.BankTransferAmount)),

                    MealsCashAmount = ReadDecimal(nameof(Invoice.MealsCashAmount)),
                    MealsVisaAmount = ReadDecimal(nameof(Invoice.MealsVisaAmount)),
                    MealsInstaPayAmount = ReadDecimal(nameof(Invoice.MealsInstaPayAmount)),
                    MealsBankTransferAmount = ReadDecimal(nameof(Invoice.MealsBankTransferAmount))
                };

                EditorName = editorName;
                EditedInvoice = edited;
                DialogResult = true;
            }
            catch (FormatException ex)
            {
                StatusText.Text = ex.Message;
            }
        }

        private decimal ReadDecimal(string key)
        {
            var text = _boxes[key].Text?.Trim();
            if (string.IsNullOrEmpty(text)) return 0m;
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var value) || value < 0)
                throw new FormatException("قيمة غير صحيحة، يرجى إدخال رقم صحيح غير سالب.");
            return value;
        }

        private int ReadInt(string key)
        {
            var text = _boxes[key].Text?.Trim();
            if (string.IsNullOrEmpty(text)) return 0;
            if (!int.TryParse(text, out var value) || value < 0)
                throw new FormatException("قيمة غير صحيحة، يرجى إدخال عدد صحيح غير سالب.");
            return value;
        }

        private DateTime? ReadDate(string key)
        {
            var text = _boxes[key].Text?.Trim();
            if (string.IsNullOrEmpty(text)) return null;

            var formats = new[] { "yyyy/MM/dd", "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy" };
            if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
                || DateTime.TryParse(text, out value))
            {
                return value.Date;
            }

            throw new FormatException("تاريخ غير صحيح، استخدم الصيغة yyyy/MM/dd.");
        }

        private static string FormatValue(object value) => value switch
        {
            decimal d => d.ToString("0.##"),
            int i => i.ToString(),
            _ => value?.ToString() ?? string.Empty
        };
    }
}

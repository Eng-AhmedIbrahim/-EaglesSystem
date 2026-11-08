using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EaglesResort
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Set global Arabic culture with dd/MM/yyyy date format and Gregorian calendar
            try
            {
                var culture = new System.Globalization.CultureInfo("ar-EG");
                culture.DateTimeFormat.ShortDatePattern = "dd/MM/yyyy";
                culture.DateTimeFormat.LongDatePattern = "dd MMMM yyyy";
                culture.DateTimeFormat.Calendar = new System.Globalization.GregorianCalendar();
                culture.NumberFormat.DigitSubstitution = System.Globalization.DigitShapes.None;

                System.Threading.Thread.CurrentThread.CurrentCulture = culture;
                System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
                System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
                System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;

                FrameworkElement.LanguageProperty.OverrideMetadata(
                    typeof(FrameworkElement),
                    new FrameworkPropertyMetadata(
                        System.Windows.Markup.XmlLanguage.GetLanguage("ar-EG")));
            }
            catch { }

            this.DispatcherUnhandledException += (s, args) =>
            {
                try { System.IO.File.WriteAllText("crash_log.txt", args.Exception.ToString()); } catch { }
                MessageBox.Show(args.Exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            // Safe global text select-all on focus (using Dispatcher to prevent any mouse recursion or StackOverflow)
            EventManager.RegisterClassHandler(
                typeof(TextBox),
                UIElement.GotKeyboardFocusEvent,
                new KeyboardFocusChangedEventHandler(TextBox_GotKeyboardFocus));

            // Initialize database
            using var db = new Data.AppDbContext();
            db.SeedData();
        }

        private static void TextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                // Run after mouse click finishes so it does not deselect or loop
                tb.Dispatcher.BeginInvoke(new Action(() => tb.SelectAll()));
            }
        }
    }
}

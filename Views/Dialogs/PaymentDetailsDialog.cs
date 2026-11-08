using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EaglesResort.Models;

namespace EaglesResort.Views.Dialogs
{
    public class PaymentDetailsDialog : Window
    {
        public PaymentDetailsDialog(Invoice invoice)
        {
            Title = "تفاصيل الفاتورة والدفع";
            Width = 520;
            Height = 540;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(17, 30, 43)); // #111E2B
            Foreground = Brushes.White;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Calibri");
            FontSize = 15;
            ResizeMode = ResizeMode.NoResize;

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var mainPanel = new StackPanel { Margin = new Thickness(20) };

            // Title
            mainPanel.Children.Add(new TextBlock
            {
                Text = "تفاصيل الفاتورة والدفع للنزيل",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(212, 160, 23)), // Gold
                Margin = new Thickness(0, 0, 0, 16),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            // Chalets Summary if multi-chalet
            if (!string.IsNullOrWhiteSpace(invoice.ChaletsSummary))
            {
                var summaryBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(22, 34, 50)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 12),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(45, 63, 85)),
                    BorderThickness = new Thickness(1)
                };
                var summaryStack = new StackPanel();
                summaryStack.Children.Add(new TextBlock
                {
                    Text = "🏡 الوحدات المحجوزة:",
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250)),
                    Margin = new Thickness(0, 0, 0, 4)
                });
                summaryStack.Children.Add(new TextBlock
                {
                    Text = invoice.ChaletsSummary,
                    Foreground = Brushes.WhiteSmoke,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13
                });
                summaryBorder.Child = summaryStack;
                mainPanel.Children.Add(summaryBorder);
            }

            // Payment method details
            string paymentMethodText = !string.IsNullOrWhiteSpace(invoice.PaymentDetails)
                ? invoice.PaymentDetails
                : invoice.HousingPaymentMethod;

            // Add Detail Rows
            mainPanel.Children.Add(CreateDetailRow("إجمالي الإقامة (السكن):", $"{invoice.ChaletTotal:N2} ج.م", ""));
            mainPanel.Children.Add(CreateDetailRow("إجمالي الوجبات:", $"{invoice.MealsTotal:N2} ج.م", $"طريقة الدفع: {invoice.MealsPaymentMethod}"));
            mainPanel.Children.Add(CreateDetailRow("خدمات إضافية:", $"{invoice.ExtraServices:N2} ج.م", invoice.ExtraServicesNote));
            mainPanel.Children.Add(CreateDetailRow("التأمين المسترد:", $"{invoice.Insurance:N2} ج.م", ""));
            
            mainPanel.Children.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(45, 63, 85)), Margin = new Thickness(0, 8, 0, 8) });

            mainPanel.Children.Add(CreateDetailRow("الإجمالي الكلي:", $"{invoice.GrandTotal:N2} ج.م", ""));
            mainPanel.Children.Add(CreateDetailRow("عربون مدفوع (ديبوزت):", $"{invoice.Deposit:N2} ج.م", ""));
            
            mainPanel.Children.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(45, 63, 85)), Margin = new Thickness(0, 8, 0, 8) });

            mainPanel.Children.Add(CreateDetailRow("المتبقي بعد العربون:", $"{invoice.TotalAfterDeposit:N2} ج.م", "", true));

            // Split Payment Details Box
            var payBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(15, 25, 35)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 12, 0, 0),
                BorderBrush = new SolidColorBrush(Color.FromRgb(212, 160, 23)),
                BorderThickness = new Thickness(1)
            };
            var payStack = new StackPanel();
            payStack.Children.Add(new TextBlock
            {
                Text = "💳 تفصيل طرق الدفع والسداد:",
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(212, 160, 23)),
                Margin = new Thickness(0, 0, 0, 4)
            });
            payStack.Children.Add(new TextBlock
            {
                Text = paymentMethodText,
                Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            });
            payBorder.Child = payStack;
            mainPanel.Children.Add(payBorder);

            var closeBtn = new Button
            {
                Content = "إغلاق",
                Width = 120,
                Height = 35,
                Margin = new Thickness(0, 16, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(36, 52, 71)), // #243447
                Foreground = Brushes.White,
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            closeBtn.Click += (s, e) => Close();

            mainPanel.Children.Add(closeBtn);

            scroll.Content = mainPanel;
            Content = scroll;
        }

        private UIElement CreateDetailRow(string label, string value, string note, bool isHighlight = false)
        {
            var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lbl = new TextBlock
            {
                Text = label,
                FontWeight = isHighlight ? FontWeights.Bold : FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), // #94A3B8
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            var valStack = new StackPanel { Orientation = Orientation.Horizontal };
            
            var val = new TextBlock
            {
                Text = value,
                FontWeight = FontWeights.Bold,
                Foreground = isHighlight ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) : Brushes.White,
                Margin = new Thickness(0, 0, 15, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            valStack.Children.Add(val);

            if (!string.IsNullOrWhiteSpace(note))
            {
                var noteLbl = new TextBlock
                {
                    Text = $"({note})",
                    Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center
                };
                valStack.Children.Add(noteLbl);
            }

            Grid.SetColumn(valStack, 1);
            grid.Children.Add(valStack);

            return grid;
        }
    }
}

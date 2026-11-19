using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ClosedXML.Excel;
using EaglesResort.Data;
using EaglesResort.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace EaglesResort.Views.Pages
{
    public partial class ReportsPage : Page
    {
        public ReportsPage()
        {
            InitializeComponent();

            var today = DateTime.Today;
            InvoicesFromPicker.SelectedDate = new DateTime(today.Year, today.Month, 1);
            InvoicesToPicker.SelectedDate = today;
            LogsFromPicker.SelectedDate = new DateTime(today.Year, today.Month, 1);
            LogsToPicker.SelectedDate = today;

            Loaded += (_, _) =>
            {
                LoadInvoices();
                LoadOccupancy();
                LoadLogs();
            };
        }

        // ===================== تقرير الفواتير =====================

        private void LoadInvoices_Click(object sender, RoutedEventArgs e) => LoadInvoices();

        private void LoadInvoices()
        {
            try
            {
                var (from, to) = GetRange(InvoicesFromPicker, InvoicesToPicker);

                using var db = new AppDbContext();
                var invoices = db.Invoices
                    .Include(i => i.Booking)!.ThenInclude(b => b!.Chalet)
                    .Include(i => i.Bookings).ThenInclude(b => b.Chalet)
                    .Where(i => i.InvoiceDate >= from && i.InvoiceDate < to.AddDays(1))
                    .OrderByDescending(i => i.InvoiceDate)
                    .ThenByDescending(i => i.Id)
                    .ToList();

                var rows = invoices.Select(i =>
                {
                    var guest = i.Booking?.GuestName
                                ?? i.Bookings.FirstOrDefault()?.GuestName
                                ?? "—";

                    var chalets = !string.IsNullOrWhiteSpace(i.ChaletsSummary)
                        ? i.ChaletsSummary
                        : string.Join("، ", i.Bookings
                            .Select(b => b.Chalet?.Number)
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .Distinct());

                    return new InvoiceReportRow
                    {
                        InvoiceNumber = i.InvoiceNumberDisplay,
                        InvoiceDate = i.InvoiceDate,
                        GuestName = guest,
                        Chalets = chalets,
                        GrandTotal = i.GrandTotal,
                        Paid = i.TotalPaid,
                        Remaining = i.RemainingAmount,
                        Status = i.IsCancelled ? "ملغية" : (i.IsModified ? "معدلة" : "سارية")
                    };
                }).ToList();

                InvoicesGrid.ItemsSource = rows;

                var active = rows.Where(r => r.Status != "ملغية").ToList();
                InvCountText.Text = rows.Count.ToString();
                InvTotalText.Text = $"{active.Sum(r => r.GrandTotal):N0} ج.م";
                InvPaidText.Text = $"{active.Sum(r => r.Paid):N0} ج.م";
                InvRemainingText.Text = $"{active.Sum(r => r.Remaining):N0} ج.م";
                InvCancelledText.Text = rows.Count(r => r.Status == "ملغية").ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"تعذر تحميل تقرير الفواتير: {ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportInvoices_Click(object sender, RoutedEventArgs e)
            => ExportGrid(InvoicesGrid, "تقرير-الفواتير", "الفواتير");

        // ===================== تقرير الإشغال =====================

        private void LoadOccupancy_Click(object sender, RoutedEventArgs e) => LoadOccupancy();

        private void LoadOccupancy()
        {
            try
            {
                using var db = new AppDbContext();
                var today = DateTime.Today;

                var chalets = db.Chalets
                    .OrderBy(c => c.BuildingArea)
                    .ThenBy(c => c.Number)
                    .ToList();

                var activeBookings = db.Bookings
                    .Include(b => b.Chalet)
                    .Where(b => (b.Status == BookingStatus.Active || b.Status == BookingStatus.Upcoming)
                                && b.CheckInDate <= today && b.CheckOutDate > today)
                    .ToList()
                    .GroupBy(b => b.ChaletId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.CheckInDate).First());

                var rows = chalets.Select(c =>
                {
                    activeBookings.TryGetValue(c.Id, out var booking);
                    var isPermanent = (c.Notes ?? string.Empty).Contains("إقامة دائمة");

                    var status = booking != null || isPermanent
                        ? "مسكون"
                        : c.Status switch
                        {
                            ChaletStatus.Available => "متاح",
                            ChaletStatus.Occupied => "مسكون",
                            ChaletStatus.Maintenance => "تحت الصيانة",
                            ChaletStatus.Cleaning => "تحت النظافة",
                            _ => "غير معروف"
                        };

                    return new OccupancyReportRow
                    {
                        Number = c.Number,
                        Building = c.BuildingArea,
                        ChaletType = c.ChaletType,
                        Status = status,
                        GuestName = booking?.GuestName ?? (isPermanent ? "إقامة دائمة" : ""),
                        CheckIn = booking?.CheckInDate.ToString("dd/MM/yyyy") ?? "",
                        CheckOut = booking?.CheckOutDate.ToString("dd/MM/yyyy") ?? ""
                    };
                }).ToList();

                OccupancyGrid.ItemsSource = rows;

                OccTotalText.Text = rows.Count.ToString();
                OccAvailableText.Text = rows.Count(r => r.Status == "متاح").ToString();
                OccOccupiedText.Text = rows.Count(r => r.Status == "مسكون").ToString();
                OccMaintenanceText.Text = rows.Count(r => r.Status == "تحت الصيانة").ToString();
                OccCleaningText.Text = rows.Count(r => r.Status == "تحت النظافة").ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"تعذر تحميل تقرير الإشغال: {ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportOccupancy_Click(object sender, RoutedEventArgs e)
            => ExportGrid(OccupancyGrid, "تقرير-الإشغال", "الإشغال");

        // ===================== سجل الحركات =====================

        private void LoadLogs_Click(object sender, RoutedEventArgs e) => LoadLogs();

        private void LoadLogs()
        {
            try
            {
                var (from, to) = GetRange(LogsFromPicker, LogsToPicker);

                using var db = new AppDbContext();
                var invoiceNumbers = db.Invoices
                    .Select(i => new { i.Id, i.InvoiceNumber })
                    .ToDictionary(i => i.Id, i => i.InvoiceNumber);

                var logs = db.InvoiceActionLogs
                    .Where(l => l.PerformedAt >= from && l.PerformedAt < to.AddDays(1))
                    .OrderByDescending(l => l.PerformedAt)
                    .ThenByDescending(l => l.Id)
                    .ToList();

                var rows = logs.Select(l => new ActionLogReportRow
                {
                    PerformedAt = l.PerformedAt,
                    InvoiceNumber = invoiceNumbers.TryGetValue(l.InvoiceId, out var number) && !string.IsNullOrWhiteSpace(number)
                        ? number
                        : $"#{l.InvoiceId}",
                    ActionTitle = l.ActionTitle,
                    PerformedBy = l.PerformedBy,
                    Details = l.Details
                }).ToList();

                LogsGrid.ItemsSource = rows;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"تعذر تحميل سجل الحركات: {ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportLogs_Click(object sender, RoutedEventArgs e)
            => ExportGrid(LogsGrid, "سجل-حركات-الفواتير", "سجل الحركات");

        // ===================== أدوات مشتركة =====================

        private static (DateTime From, DateTime To) GetRange(DatePicker fromPicker, DatePicker toPicker)
        {
            var today = DateTime.Today;
            var from = (fromPicker.SelectedDate ?? new DateTime(today.Year, today.Month, 1)).Date;
            var to = (toPicker.SelectedDate ?? today).Date;

            if (to < from) (from, to) = (to, from);
            return (from, to);
        }

        private static void ExportGrid(DataGrid grid, string defaultFileName, string sheetName)
        {
            try
            {
                if (grid.ItemsSource == null || grid.Items.Count == 0)
                {
                    MessageBox.Show("لا توجد بيانات للتصدير.", "تنبيه",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    FileName = $"{defaultFileName}-{DateTime.Now:yyyyMMdd-HHmm}.xlsx"
                };
                if (dialog.ShowDialog() != true) return;

                using var workbook = new XLWorkbook();
                var sheet = workbook.Worksheets.Add(sheetName);

                var columns = new List<(int Column, string Path)>();
                var colIndex = 1;

                foreach (var column in grid.Columns)
                {
                    sheet.Cell(1, colIndex).Value = column.Header?.ToString() ?? string.Empty;
                    sheet.Cell(1, colIndex).Style.Font.Bold = true;
                    sheet.Cell(1, colIndex).Style.Fill.BackgroundColor = XLColor.FromHtml("#243447");
                    sheet.Cell(1, colIndex).Style.Font.FontColor = XLColor.White;

                    var path = (column as DataGridBoundColumn)?.Binding is Binding binding
                        ? binding.Path?.Path
                        : null;

                    if (!string.IsNullOrWhiteSpace(path))
                        columns.Add((colIndex, path));

                    colIndex++;
                }

                var rowIndex = 2;
                foreach (var item in grid.Items)
                {
                    foreach (var (column, path) in columns)
                    {
                        var value = item?.GetType().GetProperty(path)?.GetValue(item);
                        sheet.Cell(rowIndex, column).Value = value switch
                        {
                            null => string.Empty,
                            DateTime date => date,
                            decimal number => Convert.ToDouble(number),
                            int number => number,
                            bool flag => flag ? "نعم" : "لا",
                            _ => value.ToString() ?? string.Empty
                        };
                    }
                    rowIndex++;
                }

                sheet.Columns().AdjustToContents();
                sheet.RightToLeft = true;
                workbook.SaveAs(dialog.FileName);

                MessageBox.Show("تم تصدير التقرير بنجاح.", "تم",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"تعذر تصدير التقرير: {ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class InvoiceReportRow
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public string GuestName { get; set; } = string.Empty;
        public string Chalets { get; set; } = string.Empty;
        public decimal GrandTotal { get; set; }
        public decimal Paid { get; set; }
        public decimal Remaining { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class OccupancyReportRow
    {
        public string Number { get; set; } = string.Empty;
        public string Building { get; set; } = string.Empty;
        public string ChaletType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string GuestName { get; set; } = string.Empty;
        public string CheckIn { get; set; } = string.Empty;
        public string CheckOut { get; set; } = string.Empty;
    }

    public class ActionLogReportRow
    {
        public DateTime PerformedAt { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string ActionTitle { get; set; } = string.Empty;
        public string PerformedBy { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}

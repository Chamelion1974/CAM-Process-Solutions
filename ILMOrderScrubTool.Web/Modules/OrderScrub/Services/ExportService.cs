using ILMOperationsPlatform.Web.Modules.OrderScrub.Models;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace ILMOperationsPlatform.Web.Modules.OrderScrub.Services;

public class ExportService : IExportService
{
    private readonly ILogger<ExportService> _logger;

    public ExportService(ILogger<ExportService> logger)
    {
        _logger = logger;
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    public async Task<byte[]> ExportReportToExcelAsync(ScrubReport report)
    {
        try
        {
            using var package = new ExcelPackage();

            // Summary Sheet
            var summarySheet = package.Workbook.Worksheets.Add("Summary");
            CreateSummarySheet(summarySheet, report);

            // Discrepancies Sheet
            var discrepanciesSheet = package.Workbook.Worksheets.Add("Discrepancies");
            CreateDiscrepanciesSheet(discrepanciesSheet, report);

            // Missing Orders Sheet
            var missingSheet = package.Workbook.Worksheets.Add("Missing Orders");
            CreateMissingOrdersSheet(missingSheet, report);

            _logger.LogInformation("Exported report {ReportId} to Excel", report.ReportId);
            return await Task.FromResult(package.GetAsByteArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report {ReportId} to Excel", report.ReportId);
            throw;
        }
    }

    private void CreateSummarySheet(ExcelWorksheet worksheet, ScrubReport report)
    {
        worksheet.Cells["A1"].Value = "Order Scrub Report Summary";
        worksheet.Cells["A1:B1"].Merge = true;
        worksheet.Cells["A1"].Style.Font.Size = 16;
        worksheet.Cells["A1"].Style.Font.Bold = true;

        int row = 3;
        worksheet.Cells[$"A{row}"].Value = "Report ID:";
        worksheet.Cells[$"B{row}"].Value = report.ReportId.ToString();
        row++;

        worksheet.Cells[$"A{row}"].Value = "Created Date:";
        worksheet.Cells[$"B{row}"].Value = report.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss");
        row++;

        worksheet.Cells[$"A{row}"].Value = "Customer:";
        worksheet.Cells[$"B{row}"].Value = report.CustomerName;
        row++;

        worksheet.Cells[$"A{row}"].Value = "JobBoss File:";
        worksheet.Cells[$"B{row}"].Value = report.JobBossFileName;
        row++;

        worksheet.Cells[$"A{row}"].Value = "Customer File:";
        worksheet.Cells[$"B{row}"].Value = report.CustomerFileName;
        row += 2;

        // Statistics
        worksheet.Cells[$"A{row}"].Value = "Statistics";
        worksheet.Cells[$"A{row}"].Style.Font.Bold = true;
        row++;

        worksheet.Cells[$"A{row}"].Value = "Total Orders:";
        worksheet.Cells[$"B{row}"].Value = report.Statistics.Total;
        row++;

        worksheet.Cells[$"A{row}"].Value = "Perfect Matches:";
        worksheet.Cells[$"B{row}"].Value = report.Statistics.Perfect;
        worksheet.Cells[$"B{row}"].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells[$"B{row}"].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
        row++;

        worksheet.Cells[$"A{row}"].Value = "Critical Issues:";
        worksheet.Cells[$"B{row}"].Value = report.Statistics.Critical;
        worksheet.Cells[$"B{row}"].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells[$"B{row}"].Style.Fill.BackgroundColor.SetColor(Color.Red);
        row++;

        worksheet.Cells[$"A{row}"].Value = "High Issues:";
        worksheet.Cells[$"B{row}"].Value = report.Statistics.High;
        worksheet.Cells[$"B{row}"].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells[$"B{row}"].Style.Fill.BackgroundColor.SetColor(Color.Orange);
        row++;

        worksheet.Cells[$"A{row}"].Value = "Medium Issues:";
        worksheet.Cells[$"B{row}"].Value = report.Statistics.Medium;
        worksheet.Cells[$"B{row}"].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells[$"B{row}"].Style.Fill.BackgroundColor.SetColor(Color.Yellow);
        row++;

        worksheet.Cells[$"A{row}"].Value = "Missing from Customer:";
        worksheet.Cells[$"B{row}"].Value = report.Statistics.MissingFromCustomer;
        row++;

        worksheet.Cells[$"A{row}"].Value = "Missing from JobBoss:";
        worksheet.Cells[$"B{row}"].Value = report.Statistics.MissingFromJobBoss;

        worksheet.Cells["A:B"].AutoFitColumns();
    }

    private void CreateDiscrepanciesSheet(ExcelWorksheet worksheet, ScrubReport report)
    {
        // Headers
        worksheet.Cells["A1"].Value = "Sales Order";
        worksheet.Cells["B1"].Value = "Line";
        worksheet.Cells["C1"].Value = "Customer PO";
        worksheet.Cells["D1"].Value = "Part Number";
        worksheet.Cells["E1"].Value = "Revision";
        worksheet.Cells["F1"].Value = "Field";
        worksheet.Cells["G1"].Value = "JobBoss Value";
        worksheet.Cells["H1"].Value = "Customer Value";
        worksheet.Cells["I1"].Value = "Severity";

        worksheet.Cells["A1:I1"].Style.Font.Bold = true;
        worksheet.Cells["A1:I1"].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells["A1:I1"].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);

        int row = 2;
        foreach (var match in report.Matches.Where(m => m.Discrepancies.Any()))
        {
            foreach (var discrepancy in match.Discrepancies)
            {
                worksheet.Cells[$"A{row}"].Value = match.JobBossOrder?.SalesOrder ?? "";
                worksheet.Cells[$"B{row}"].Value = match.JobBossOrder?.Line ?? "";
                worksheet.Cells[$"C{row}"].Value = match.JobBossOrder?.CustomerPO ?? match.CustomerOrder?.CustomerPO ?? "";
                worksheet.Cells[$"D{row}"].Value = match.JobBossOrder?.PartNumber ?? match.CustomerOrder?.PartNumber ?? "";
                worksheet.Cells[$"E{row}"].Value = match.JobBossOrder?.Revision ?? match.CustomerOrder?.Revision ?? "";
                worksheet.Cells[$"F{row}"].Value = discrepancy.Field;
                worksheet.Cells[$"G{row}"].Value = discrepancy.JobBossValue;
                worksheet.Cells[$"H{row}"].Value = discrepancy.CustomerValue;
                worksheet.Cells[$"I{row}"].Value = discrepancy.Severity.ToString();

                // Color code by severity
                var severityCell = worksheet.Cells[$"I{row}"];
                severityCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                severityCell.Style.Fill.BackgroundColor.SetColor(discrepancy.Severity switch
                {
                    DiscrepancySeverity.Critical => Color.Red,
                    DiscrepancySeverity.High => Color.Orange,
                    DiscrepancySeverity.Medium => Color.Yellow,
                    _ => Color.White
                });

                row++;
            }
        }

        worksheet.Cells["A:I"].AutoFitColumns();
    }

    private void CreateMissingOrdersSheet(ExcelWorksheet worksheet, ScrubReport report)
    {
        // Headers
        worksheet.Cells["A1"].Value = "Type";
        worksheet.Cells["B1"].Value = "Sales Order";
        worksheet.Cells["C1"].Value = "Customer PO";
        worksheet.Cells["D1"].Value = "Part Number";
        worksheet.Cells["E1"].Value = "Revision";
        worksheet.Cells["F1"].Value = "Order Qty";
        worksheet.Cells["G1"].Value = "Open Qty";
        worksheet.Cells["H1"].Value = "Unit Price";

        worksheet.Cells["A1:H1"].Style.Font.Bold = true;
        worksheet.Cells["A1:H1"].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells["A1:H1"].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);

        int row = 2;
        var missingOrders = report.Matches.Where(m =>
            m.MatchType == MatchType.MissingCustomer || m.MatchType == MatchType.MissingJobBoss);

        foreach (var match in missingOrders)
        {
            worksheet.Cells[$"A{row}"].Value = match.MatchType.ToString();
            
            if (match.MatchType == MatchType.MissingCustomer && match.JobBossOrder != null)
            {
                worksheet.Cells[$"B{row}"].Value = match.JobBossOrder.SalesOrder;
                worksheet.Cells[$"C{row}"].Value = match.JobBossOrder.CustomerPO;
                worksheet.Cells[$"D{row}"].Value = match.JobBossOrder.PartNumber;
                worksheet.Cells[$"E{row}"].Value = match.JobBossOrder.Revision;
                worksheet.Cells[$"F{row}"].Value = match.JobBossOrder.OrderQty;
                worksheet.Cells[$"G{row}"].Value = match.JobBossOrder.OpenQty;
                worksheet.Cells[$"H{row}"].Value = match.JobBossOrder.UnitPrice;
            }
            else if (match.MatchType == MatchType.MissingJobBoss && match.CustomerOrder != null)
            {
                worksheet.Cells[$"B{row}"].Value = "";
                worksheet.Cells[$"C{row}"].Value = match.CustomerOrder.CustomerPO;
                worksheet.Cells[$"D{row}"].Value = match.CustomerOrder.PartNumber;
                worksheet.Cells[$"E{row}"].Value = match.CustomerOrder.Revision;
                worksheet.Cells[$"F{row}"].Value = match.CustomerOrder.OrderQty;
                worksheet.Cells[$"G{row}"].Value = match.CustomerOrder.OpenQty;
                worksheet.Cells[$"H{row}"].Value = match.CustomerOrder.UnitPrice;
            }

            row++;
        }

        worksheet.Cells["A:H"].AutoFitColumns();
    }
}
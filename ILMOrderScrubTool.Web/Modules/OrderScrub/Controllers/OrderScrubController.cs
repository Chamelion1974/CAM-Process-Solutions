using ILMOperationsPlatform.Web.Modules.OrderScrub.Models;
using ILMOperationsPlatform.Web.Modules.OrderScrub.Services;
using ILMOperationsPlatform.Web.Shared.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ILMOperationsPlatform.Web.Modules.OrderScrub.Controllers;

/// <summary>
/// API controller for order scrubbing operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class OrderScrubController : ControllerBase
{
    private readonly IExcelParserService _parserService;
    private readonly IReconciliationService _reconciliationService;
    private readonly IExportService _exportService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OrderScrubController> _logger;

    private static readonly string[] AllowedExtensions = { ".xlsx", ".xls" };
    private const long MaxFileSize = 10 * 1024 * 1024; // 10MB

    public OrderScrubController(
        IExcelParserService parserService,
        IReconciliationService reconciliationService,
        IExportService exportService,
        ApplicationDbContext context,
        ILogger<OrderScrubController> logger)
    {
        _parserService = parserService;
        _reconciliationService = reconciliationService;
        _exportService = exportService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Upload and process JobBoss and customer order files
    /// </summary>
    /// <param name="jobBossFile">Excel file containing JobBoss orders</param>
    /// <param name="customerFile">Excel file containing customer orders</param>
    /// <returns>Report summary with statistics</returns>
    /// <response code="200">Returns the report summary</response>
    /// <response code="400">If files are invalid or processing fails</response>
    /// <response code="401">If user is not authenticated</response>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UploadResponse>> UploadFiles(
        IFormFile jobBossFile,
        IFormFile customerFile)
    {
        try
        {
            // Validate files
            var validationResult = ValidateFiles(jobBossFile, customerFile);
            if (!string.IsNullOrEmpty(validationResult))
            {
                return BadRequest(new { error = validationResult });
            }

            var userId = GetUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized(new { error = "User ID not found in token" });
            }

            _logger.LogInformation("Processing order scrub for user {UserId}", userId);

            // Parse JobBoss file
            List<JobBossOrder> jobBossOrders;
            using (var stream = jobBossFile.OpenReadStream())
            {
                jobBossOrders = await _parserService.ParseJobBossFileAsync(stream, jobBossFile.FileName);
            }

            // Parse Customer file
            List<CustomerOrder> customerOrders;
            using (var stream = customerFile.OpenReadStream())
            {
                customerOrders = await _parserService.ParseCustomerFileAsync(stream, customerFile.FileName);
            }

            // Reconcile orders
            var report = await _reconciliationService.ReconcileOrdersAsync(
                jobBossOrders,
                customerOrders,
                jobBossFile.FileName,
                customerFile.FileName,
                userId);

            _logger.LogInformation("Order scrub completed successfully. ReportId: {ReportId}", report.ReportId);

            return Ok(new UploadResponse
            {
                ReportId = report.ReportId,
                Summary = report.Statistics
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation during file upload");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order scrub files");
            return StatusCode(500, new { error = "An error occurred processing the files. Please try again." });
        }
    }

    /// <summary>
    /// Get a specific scrub report by ID
    /// </summary>
    /// <param name="reportId">The unique identifier of the report</param>
    /// <returns>Complete report with all discrepancies</returns>
    /// <response code="200">Returns the full report</response>
    /// <response code="404">If report is not found</response>
    /// <response code="401">If user is not authenticated</response>
    [HttpGet("report/{reportId:guid}")]
    [ProducesResponseType(typeof(ScrubReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ScrubReport>> GetReport(Guid reportId)
    {
        try
        {
            var reportEntity = await _context.ScrubReports
                .Include(r => r.Discrepancies)
                .Include(r => r.CreatedByUser)
                .FirstOrDefaultAsync(r => r.ReportId == reportId);

            if (reportEntity == null)
            {
                return NotFound(new { error = $"Report with ID {reportId} not found" });
            }

            // Convert entity to DTO
            var report = MapEntityToReport(reportEntity);

            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report {ReportId}", reportId);
            return StatusCode(500, new { error = "An error occurred retrieving the report" });
        }
    }

    /// <summary>
    /// Get a paginated list of scrub reports
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Items per page (default: 20, max: 100)</param>
    /// <param name="customerName">Optional filter by customer name</param>
    /// <returns>Paginated list of report summaries</returns>
    /// <response code="200">Returns the paginated report list</response>
    /// <response code="401">If user is not authenticated</response>
    [HttpGet("reports")]
    [ProducesResponseType(typeof(PagedReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedReportResponse>> GetReports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? customerName = null)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var query = _context.ScrubReports.AsQueryable();

            // Apply filter if provided
            if (!string.IsNullOrWhiteSpace(customerName))
            {
                query = query.Where(r => r.CustomerName.Contains(customerName));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var reports = await query
                .OrderByDescending(r => r.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new ReportSummary
                {
                    ReportId = r.ReportId,
                    CreatedDate = r.CreatedDate,
                    CustomerName = r.CustomerName,
                    JobBossFileName = r.JobBossFileName,
                    CustomerFileName = r.CustomerFileName,
                    TotalOrders = r.TotalOrders,
                    PerfectMatches = r.PerfectMatches,
                    CriticalIssues = r.CriticalIssues,
                    HighIssues = r.HighIssues,
                    MediumIssues = r.MediumIssues
                })
                .ToListAsync();

            return Ok(new PagedReportResponse
            {
                Reports = reports,
                TotalCount = totalCount,
                TotalPages = totalPages,
                CurrentPage = page,
                PageSize = pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reports list");
            return StatusCode(500, new { error = "An error occurred retrieving reports" });
        }
    }

    /// <summary>
    /// Export a report to Excel format
    /// </summary>
    /// <param name="reportId">The unique identifier of the report</param>
    /// <returns>Excel file download</returns>
    /// <response code="200">Returns the Excel file</response>
    /// <response code="404">If report is not found</response>
    /// <response code="401">If user is not authenticated</response>
    [HttpGet("export/{reportId:guid}")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ExportReport(Guid reportId)
    {
        try
        {
            var reportEntity = await _context.ScrubReports
                .Include(r => r.Discrepancies)
                .FirstOrDefaultAsync(r => r.ReportId == reportId);

            if (reportEntity == null)
            {
                return NotFound(new { error = $"Report with ID {reportId} not found" });
            }

            var report = MapEntityToReport(reportEntity);
            var excelBytes = await _exportService.ExportReportToExcelAsync(report);

            var fileName = $"OrderScrub_{reportEntity.CustomerName}_{reportEntity.CreatedDate:yyyyMMdd_HHmmss}.xlsx";

            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting report {ReportId}", reportId);
            return StatusCode(500, new { error = "An error occurred exporting the report" });
        }
    }

    /// <summary>
    /// Delete a scrub report (Admin only)
    /// </summary>
    /// <param name="reportId">The unique identifier of the report to delete</param>
    /// <returns>No content on success</returns>
    /// <response code="204">Report deleted successfully</response>
    /// <response code="403">If user is not an Admin</response>
    /// <response code="404">If report is not found</response>
    /// <response code="401">If user is not authenticated</response>
    [HttpDelete("report/{reportId:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteReport(Guid reportId)
    {
        try
        {
            var report = await _context.ScrubReports.FindAsync(reportId);

            if (report == null)
            {
                return NotFound(new { error = $"Report with ID {reportId} not found" });
            }

            var userId = GetUserId();

            // Remove report (cascade delete will handle discrepancies)
            _context.ScrubReports.Remove(report);

            // Create audit log
            _context.AuditLogs.Add(new Shared.Models.AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = "Deleted Scrub Report",
                EntityType = "ScrubReport",
                EntityId = reportId,
                Timestamp = DateTime.UtcNow,
                Details = System.Text.Json.JsonSerializer.Serialize(new
                {
                    CustomerName = report.CustomerName,
                    CreatedDate = report.CreatedDate,
                    TotalOrders = report.TotalOrders
                })
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Report {ReportId} deleted by user {UserId}", reportId, userId);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting report {ReportId}", reportId);
            return StatusCode(500, new { error = "An error occurred deleting the report" });
        }
    }

    #region Helper Methods

    private string ValidateFiles(IFormFile jobBossFile, IFormFile customerFile)
    {
        if (jobBossFile == null || jobBossFile.Length == 0)
        {
            return "JobBoss file is required";
        }

        if (customerFile == null || customerFile.Length == 0)
        {
            return "Customer file is required";
        }

        if (jobBossFile.Length > MaxFileSize)
        {
            return $"JobBoss file size exceeds maximum allowed size of {MaxFileSize / 1024 / 1024}MB";
        }

        if (customerFile.Length > MaxFileSize)
        {
            return $"Customer file size exceeds maximum allowed size of {MaxFileSize / 1024 / 1024}MB";
        }

        var jobBossExt = Path.GetExtension(jobBossFile.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(jobBossExt))
        {
            return $"JobBoss file must be an Excel file ({string.Join(", ", AllowedExtensions)})";
        }

        var customerExt = Path.GetExtension(customerFile.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(customerExt))
        {
            return $"Customer file must be an Excel file ({string.Join(", ", AllowedExtensions)})";
        }

        return string.Empty;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    private ScrubReport MapEntityToReport(ScrubReportEntity entity)
    {
        var report = new ScrubReport
        {
            ReportId = entity.ReportId,
            CreatedDate = entity.CreatedDate,
            JobBossFileName = entity.JobBossFileName,
            CustomerFileName = entity.CustomerFileName,
            CustomerName = entity.CustomerName,
            Statistics = new ScrubStatistics
            {
                Total = entity.TotalOrders,
                Perfect = entity.PerfectMatches,
                Critical = entity.CriticalIssues,
                High = entity.HighIssues,
                Medium = entity.MediumIssues,
                MissingFromCustomer = entity.MissingFromCustomer,
                MissingFromJobBoss = entity.MissingFromJobBoss
            },
            Matches = new List<OrderMatch>()
        };

        // Group discrepancies by order
        var discrepancyGroups = entity.Discrepancies
            .GroupBy(d => new { d.SalesOrder, d.CustomerPO, d.PartNumber });

        foreach (var group in discrepancyGroups)
        {
            var match = new OrderMatch
            {
                Discrepancies = group.Select(d => new Discrepancy
                {
                    Field = d.DiscrepancyType.ToString(),
                    JobBossValue = d.JobBossValue,
                    CustomerValue = d.CustomerValue,
                    Severity = MapSeverityFromEntity(d.Severity)
                }).ToList()
            };

            // Determine match type from worst severity
            if (match.Discrepancies.Any())
            {
                match.MatchType = DetermineMatchType(match.Discrepancies);
            }

            report.Matches.Add(match);
        }

        return report;
    }

    private DiscrepancySeverity MapSeverityFromEntity(Severity severity)
    {
        return severity switch
        {
            Severity.Critical => DiscrepancySeverity.Critical,
            Severity.High => DiscrepancySeverity.High,
            Severity.Medium => DiscrepancySeverity.Medium,
            Severity.Low => DiscrepancySeverity.Low,
            _ => DiscrepancySeverity.Medium
        };
    }

    private MatchType DetermineMatchType(List<Discrepancy> discrepancies)
    {
        if (discrepancies.Any(d => d.Severity == DiscrepancySeverity.Critical))
            return MatchType.Critical;
        if (discrepancies.Any(d => d.Severity == DiscrepancySeverity.High))
            return MatchType.High;
        return MatchType.Medium;
    }

    #endregion
}
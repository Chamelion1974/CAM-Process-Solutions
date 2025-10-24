namespace ILMOperationsPlatform.Web.Modules.OrderScrub.Models;

/// <summary>
/// Paginated response containing scrub reports
/// </summary>
public class PagedReportResponse
{
    /// <summary>
    /// List of report summaries for the current page
    /// </summary>
    public List<ReportSummary> Reports { get; set; } = new();

    /// <summary>
    /// Total number of reports matching the query
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Total number of pages based on page size
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Current page number
    /// </summary>
    public int CurrentPage { get; set; }

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; set; }
}

/// <summary>
/// Summary information for a scrub report
/// </summary>
public class ReportSummary
{
    public Guid ReportId { get; set; }
    public DateTime CreatedDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string JobBossFileName { get; set; } = string.Empty;
    public string CustomerFileName { get; set; } = string.Empty;
    public int TotalOrders { get; set; }
    public int PerfectMatches { get; set; }
    public int CriticalIssues { get; set; }
    public int HighIssues { get; set; }
    public int MediumIssues { get; set; }
}
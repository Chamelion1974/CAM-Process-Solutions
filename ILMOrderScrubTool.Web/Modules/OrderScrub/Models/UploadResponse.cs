namespace ILMOperationsPlatform.Web.Modules.OrderScrub.Models;

/// <summary>
/// Response model for order scrub upload operation
/// </summary>
public class UploadResponse
{
    /// <summary>
    /// Unique identifier for the generated report
    /// </summary>
    public Guid ReportId { get; set; }

    /// <summary>
    /// Summary statistics of the scrub results
    /// </summary>
    public ScrubStatistics Summary { get; set; } = new();
}
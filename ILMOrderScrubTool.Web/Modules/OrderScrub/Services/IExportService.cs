using ILMOperationsPlatform.Web.Modules.OrderScrub.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using OfficeOpenXml;

namespace ILMOperationsPlatform.Web.Modules.OrderScrub.Services;

public interface IExportService
{
    Task<byte[]> ExportReportToExcelAsync(ScrubReport report);
}
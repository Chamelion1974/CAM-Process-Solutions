using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using ILMOperationsPlatform.Web.Shared.Database;
using ILMOperationsPlatform.Web.Modules.OrderScrub.Services;
using ILMOperationsPlatform.Web.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure Entity Framework Core with SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null
        )
    )
);

// Register Order Scrub services
builder.Services.AddScoped<IReconciliationService, ReconciliationService>();
builder.Services.AddScoped<IExcelParserService, ExcelParserService>();
builder.Services.AddScoped<IExportService, ExportService>();

// Add SignalR
builder.Services.AddSignalR();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? "YourSuperSecretKeyMinimum32CharactersLong!";
var issuer = jwtSettings["Issuer"] ?? "ILMOperationsPlatform";
var audience = jwtSettings["Audience"] ?? "ILMOperationsPlatform";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddAuthorization();

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ILM Operations Platform API",
        Version = "v1",
        Description = "API for ILM Operations Platform including Order Scrub Module",
        Contact = new OpenApiContact
        {
            Name = "ILM Tool Inc.",
            Email = "support@ilmtool.com"
        }
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ProgressHub>("/progressHub");

app.Run();



# Navigate to repository root (if not already there)
cd C:\Users\adamg\source\repos\Chamelion1974\ILM-Operations-Platform

# Create and switch to feature branch
git checkout -b feature/phase2-order-scrub-module

# Stage all changes
git add .

# Commit with comprehensive message
git commit -m "Phase 2: Order Scrub Module implementation complete

Features implemented:
- Database infrastructure with EF Core
  * User model with role-based access (Admin, Programmer, Operations, Viewer)
  * AuditLog for compliance and activity tracking
  * ScrubReportEntity and DiscrepancyEntity models
  * ApplicationDbContext with indexes and relationships

- Order reconciliation engine
  * ReconciliationService with intelligent CustomerPO + PartNumber matching
  * Severity-based discrepancy detection (Critical, High, Medium, Low)
  * Support for missing orders from either system
  * Real-time processing metrics and statistics

- Excel file processing
  * ExcelParserService for JobBoss and Customer file parsing
  * ExportService with multi-sheet Excel reports
  * Color-coded severity indicators in exports
  * Support for .xlsx and .xls formats

- REST API endpoints
  * POST /api/orderscrub/upload - File upload and processing
  * GET /api/orderscrub/report/{id} - Retrieve full reports
  * GET /api/orderscrub/reports - Paginated list with filtering
  * GET /api/orderscrub/export/{id} - Excel export
  * DELETE /api/orderscrub/report/{id} - Admin-only deletion

- Security and authentication
  * JWT Bearer token authentication
  * Role-based authorization
  * Comprehensive audit logging with IP tracking
  * Secure password hashing support

- Progress tracking dashboard
  * Real-time metrics via SignalR ProgressHub
  * Module progress indicators
  * Development status dashboard (dashboard.html)
  * Platform health monitoring endpoints

- Developer experience
  * Swagger UI with JWT authentication support
  * XML documentation for all API endpoints
  * Comprehensive error handling and validation
  * Structured logging throughout

Technical stack: .NET 8, EF Core 8, SQL Server LocalDB, EPPlus 7.0, SignalR, JWT Bearer"

# Push to remote repository (creates the branch on GitHub)
git push -u origin feature/phase2-order-scrub-module

git checkout main
git merge feature/phase2-order-scrub-module
git push origin main

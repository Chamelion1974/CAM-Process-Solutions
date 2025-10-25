using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;

namespace ILMOperationsPlatform.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Generate a test JWT token (Development only)
    /// </summary>
    [HttpPost("token")]
    public IActionResult GenerateToken([FromBody] TokenRequest request)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? "YourSuperSecretKeyMinimum32CharactersLong!";
        var issuer = jwtSettings["Issuer"] ?? "ILMOperationsPlatform";
        var audience = jwtSettings["Audience"] ?? "ILMOperationsPlatform";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, request.UserId.ToString()),
            new Claim(ClaimTypes.Name, request.Username),
            new Claim(ClaimTypes.Role, request.Role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token),
            expires = token.ValidTo
        });
    }
}

public class TokenRequest
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = "testuser";
    public string Role { get; set; } = "Admin";
}
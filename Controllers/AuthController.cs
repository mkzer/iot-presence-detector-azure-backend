using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using IoTDetectorApi.Data;
using IoTDetectorApi.Models;

namespace IoTDetectorApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<ActionResult<object>> Login([FromBody] LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user == null)
        {
            return Unauthorized(new { message = "Nom d'utilisateur ou mot de passe incorrect" });
        }

        // Simple password check (in production, use hashed passwords)
        if (!VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Nom d'utilisateur ou mot de passe incorrect" });
        }

        var token = GenerateJwtToken(user);

        return Ok(new
        {
            token,
            user = new
            {
                id = user.Id,
                username = user.Username,
                role = user.Role
            }
        });
    }

    private string GenerateJwtToken(User user)
    {
        var jwtKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT Key not configured");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "IoTDetectorApi",
            audience: _configuration["Jwt:Audience"] ?? "IoTDetectorClient",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private bool VerifyPassword(string password, string passwordHash)
    {
        // Use BCrypt to verify password
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }

    [HttpPost("register")]
    public async Task<ActionResult<object>> Register([FromBody] RegisterRequest request)
    {
        // Validate request
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Validate username format (alphanumeric, 3-50 chars)
        if (!Regex.IsMatch(request.Username, @"^[a-zA-Z0-9_]{3,50}$"))
        {
            return BadRequest(new { message = "Le nom d'utilisateur doit contenir entre 3 et 50 caractères alphanumériques" });
        }

        // Check if username already exists
        if (await _context.Users.AnyAsync(u => u.Username == request.Username))
        {
            return BadRequest(new { message = "Ce nom d'utilisateur existe déjà" });
        }

        // Validate password strength (min 8 chars, 1 uppercase, 1 digit, 1 special)
        if (!IsPasswordStrong(request.Password, out string? passwordError))
        {
            return BadRequest(new { message = passwordError });
        }

        // Hash the password using BCrypt
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // SECURITY: Always create users with "user" role
        // Only admins can elevate privileges via database or admin panel
        var user = new User
        {
            Username = request.Username,
            PasswordHash = passwordHash,
            Role = "user", // Force user role for public registration
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Utilisateur créé avec succès",
            user = new
            {
                id = user.Id,
                username = user.Username,
                role = user.Role
            }
        });
    }

    private bool IsPasswordStrong(string password, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(password))
        {
            errorMessage = "Le mot de passe est requis";
            return false;
        }

        if (password.Length < 8)
        {
            errorMessage = "Le mot de passe doit contenir au moins 8 caractères";
            return false;
        }

        if (!password.Any(char.IsUpper))
        {
            errorMessage = "Le mot de passe doit contenir au moins une majuscule";
            return false;
        }

        if (!password.Any(char.IsDigit))
        {
            errorMessage = "Le mot de passe doit contenir au moins un chiffre";
            return false;
        }

        if (!password.Any(ch => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(ch)))
        {
            errorMessage = "Le mot de passe doit contenir au moins un caractère spécial (!@#$%^&*...)";
            return false;
        }

        return true;
    }
}

public class LoginRequest
{
    [Required(ErrorMessage = "Le nom d'utilisateur est requis")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le mot de passe est requis")]
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    [Required(ErrorMessage = "Le nom d'utilisateur est requis")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Le nom d'utilisateur doit contenir entre 3 et 50 caractères")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le mot de passe est requis")]
    [MinLength(8, ErrorMessage = "Le mot de passe doit contenir au moins 8 caractères")]
    public string Password { get; set; } = string.Empty;
}

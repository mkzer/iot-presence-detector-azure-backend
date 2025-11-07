using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace IoTDetectorApi.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [JsonIgnore] // Never expose password hash in API responses
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = "user"; // "admin" or "user"

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace IoTDetectorApi.Models;

public class Device
{
    [Required]
    [MaxLength(100)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty; // ESP32, Photon2

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "inactive"; // active, inactive

    public DateTime? LastSeen { get; set; }

    [MaxLength(100)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastSignal { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

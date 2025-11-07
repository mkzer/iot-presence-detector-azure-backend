using System.ComponentModel.DataAnnotations;

namespace IoTDetectorApi.Models;

public class SensorData
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string DeviceId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty; // motion, heartbeat, etc.

    public double? Value { get; set; }

    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [MaxLength(2000)]
    public string? RawData { get; set; }
}

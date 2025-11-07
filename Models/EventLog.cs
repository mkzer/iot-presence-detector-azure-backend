using System.ComponentModel.DataAnnotations;

namespace IoTDetectorApi.Models;

public class EventLog
{
    public int Id { get; set; }

    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Level { get; set; } = "info"; // info, warning, error

    [MaxLength(100)]
    public string? DeviceId { get; set; }
}

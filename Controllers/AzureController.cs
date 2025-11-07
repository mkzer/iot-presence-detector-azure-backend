using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Devices;
using Microsoft.EntityFrameworkCore;
using IoTDetectorApi.Data;
using IoTDetectorApi.Models;

namespace IoTDetectorApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "admin")]
public class AzureController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AzureController> _logger;

    public AzureController(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<AzureController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Maps physical device IDs from Azure IoT Hub to logical device IDs used in the system
    /// </summary>
    private static string MapDeviceId(string physicalDeviceId)
    {
        // Map Photon2 physical ID to logical ID
        if (physicalDeviceId == "0a10aced202194944a044df4")
        {
            return "photon2-pir-01";
        }

        // Add other mappings here if needed
        // Example: if (physicalDeviceId == "another-physical-id") return "logical-id";

        return physicalDeviceId;
    }

    [HttpPost("test-connection")]
    public async Task<ActionResult<object>> TestConnection()
    {
        try
        {
            var connectionString = _configuration.GetValue<string>("Azure:IoTHub:ConnectionString");

            if (string.IsNullOrEmpty(connectionString))
            {
                return BadRequest(new { success = false, message = "Connection string not configured" });
            }

            var registryManager = RegistryManager.CreateFromConnectionString(connectionString);

            // Try to get IoT Hub statistics as a connection test
            var stats = await registryManager.GetRegistryStatisticsAsync();

            await registryManager.CloseAsync();

            return Ok(new
            {
                success = true,
                message = "Connexion réussie à Azure IoT Hub",
                totalDeviceCount = stats.TotalDeviceCount,
                enabledDeviceCount = stats.EnabledDeviceCount,
                disabledDeviceCount = stats.DisabledDeviceCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test Azure IoT Hub connection");
            return Ok(new
            {
                success = false,
                message = $"Échec de connexion: {ex.Message}"
            });
        }
    }

    [HttpPost("sync-devices")]
    public async Task<ActionResult<object>> SyncDevices()
    {
        try
        {
            var connectionString = _configuration.GetValue<string>("Azure:IoTHub:ConnectionString");

            if (string.IsNullOrEmpty(connectionString))
            {
                return BadRequest(new { success = false, message = "Connection string not configured" });
            }

            var registryManager = RegistryManager.CreateFromConnectionString(connectionString);

            // Get all devices from Azure IoT Hub
            var query = registryManager.CreateQuery("SELECT * FROM devices", 100);
            var devicesAdded = 0;
            var devicesUpdated = 0;

            while (query.HasMoreResults)
            {
                var page = await query.GetNextAsTwinAsync();

                foreach (var twin in page)
                {
                    var physicalDeviceId = twin.DeviceId;
                    var deviceId = MapDeviceId(physicalDeviceId);

                    var existingDevice = await _context.Devices.FindAsync(deviceId);

                    if (existingDevice == null)
                    {
                        // Add new device
                        var device = new Models.Device
                        {
                            Id = deviceId,
                            Name = deviceId,
                            Type = deviceId.Contains("esp32") ? "ESP32" :
                                   deviceId.Contains("photon") ? "Photon2" : "Unknown",
                            Status = twin.ConnectionState.ToString() == "Connected" ? "active" : "inactive",
                            LastSeen = twin.LastActivityTime,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.Devices.Add(device);
                        devicesAdded++;

                        _logger.LogInformation("Added device from Azure: {DeviceId}", deviceId);
                    }
                    else
                    {
                        // Update existing device
                        existingDevice.Status = twin.ConnectionState.ToString() == "Connected" ? "active" : "inactive";
                        existingDevice.LastSeen = twin.LastActivityTime;
                        devicesUpdated++;

                        _logger.LogInformation("Updated device from Azure: {DeviceId}", deviceId);
                    }
                }
            }

            await _context.SaveChangesAsync();
            await registryManager.CloseAsync();

            // Log the sync event
            var eventLog = new EventLog
            {
                Message = $"Synchronisation Azure: {devicesAdded} ajoutés, {devicesUpdated} mis à jour",
                Level = "info",
                Timestamp = DateTime.UtcNow
            };
            _context.EventLogs.Add(eventLog);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Synchronisation terminée",
                devicesAdded,
                devicesUpdated
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync devices from Azure IoT Hub");
            return Ok(new
            {
                success = false,
                message = $"Échec de synchronisation: {ex.Message}"
            });
        }
    }

    [HttpGet("device/{deviceId}/twin")]
    public async Task<ActionResult<object>> GetDeviceTwin(string deviceId)
    {
        try
        {
            var connectionString = _configuration.GetValue<string>("Azure:IoTHub:ConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                return BadRequest(new { success = false, message = "Connection string not configured" });
            }

            var registryManager = RegistryManager.CreateFromConnectionString(connectionString);
            var twin = await registryManager.GetTwinAsync(deviceId);
            await registryManager.CloseAsync();

            return Ok(new
            {
                success = true,
                deviceId = twin.DeviceId,
                connectionState = twin.ConnectionState.ToString(),
                lastActivityTime = twin.LastActivityTime,
                properties = new
                {
                    desired = twin.Properties.Desired.ToJson(),
                    reported = twin.Properties.Reported.ToJson()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get device twin for {DeviceId}", deviceId);
            return Ok(new { success = false, message = $"Erreur: {ex.Message}" });
        }
    }

    [HttpPost("device/{deviceId}/simulate-motion")]
    public async Task<ActionResult<object>> SimulateMotion(string deviceId)
    {
        try
        {
            // Créer un événement de mouvement simulé
            var sensorData = new SensorData
            {
                DeviceId = deviceId,
                EventType = "motion_detected",
                Value = 1,
                Timestamp = DateTime.UtcNow,
                RawData = $"{{\"event\":\"motion_detected\",\"count\":1,\"simulated\":true}}"
            };

            _context.SensorData.Add(sensorData);

            var eventLog = new EventLog
            {
                DeviceId = deviceId,
                Message = $"Mouvement SIMULÉ détecté par {deviceId} (TEST)",
                Level = "info",
                Timestamp = DateTime.UtcNow
            };

            _context.EventLogs.Add(eventLog);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Simulated motion event for {DeviceId}", deviceId);

            return Ok(new
            {
                success = true,
                message = $"Événement de mouvement simulé créé pour {deviceId}",
                eventId = eventLog.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to simulate motion for {DeviceId}", deviceId);
            return Ok(new { success = false, message = $"Erreur: {ex.Message}" });
        }
    }

    [HttpPost("device/{deviceId}/enable-detection")]
    public async Task<ActionResult<object>> EnableDetection(string deviceId)
    {
        try
        {
            var connectionString = _configuration.GetValue<string>("Azure:IoTHub:ConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                return BadRequest(new { success = false, message = "Connection string not configured" });
            }

            var registryManager = RegistryManager.CreateFromConnectionString(connectionString);
            var twin = await registryManager.GetTwinAsync(deviceId);

            // Update desired properties to enable detection
            var patch = @"{""properties"":{""desired"":{""enabled"":true}}}";
            await registryManager.UpdateTwinAsync(deviceId, patch, twin.ETag);

            await registryManager.CloseAsync();

            // Log the event
            var eventLog = new EventLog
            {
                DeviceId = deviceId,
                Message = $"Détection activée pour {deviceId} via Device Twin",
                Level = "info",
                Timestamp = DateTime.UtcNow
            };
            _context.EventLogs.Add(eventLog);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Enabled detection for {DeviceId}", deviceId);

            return Ok(new
            {
                success = true,
                message = $"Détection activée pour {deviceId}. L'appareil appliquera le changement au prochain heartbeat."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable detection for {DeviceId}", deviceId);
            return Ok(new { success = false, message = $"Erreur: {ex.Message}" });
        }
    }

    /// <summary>
    /// Met à jour la configuration d'un appareil via Device Twin
    /// </summary>
    [HttpPost("device/{deviceId}/update-config")]
    public async Task<ActionResult<object>> UpdateDeviceConfig(string deviceId, [FromBody] DeviceConfigUpdate config)
    {
        try
        {
            var connectionString = _configuration.GetValue<string>("Azure:IoTHub:ConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                return BadRequest(new { success = false, message = "Connection string not configured" });
            }

            var registryManager = RegistryManager.CreateFromConnectionString(connectionString);
            var twin = await registryManager.GetTwinAsync(deviceId);

            // Construire le patch JSON pour Device Twin
            var patchJson = new System.Text.StringBuilder();
            patchJson.Append("{\"properties\":{\"desired\":{");

            var updates = new List<string>();

            if (config.DetectionEnabled.HasValue)
            {
                updates.Add($"\"detectionEnabled\":{config.DetectionEnabled.Value.ToString().ToLower()}");
            }

            if (config.CooldownMs.HasValue)
            {
                updates.Add($"\"cooldownMs\":{config.CooldownMs.Value}");
            }

            if (!updates.Any())
            {
                return BadRequest(new { success = false, message = "No configuration fields provided" });
            }

            patchJson.Append(string.Join(",", updates));
            patchJson.Append("}}}");

            // Apply the patch
            var patch = patchJson.ToString();
            await registryManager.UpdateTwinAsync(deviceId, patch, twin.ETag);

            await registryManager.CloseAsync();

            var eventLog = new EventLog
            {
                DeviceId = deviceId,
                Message = $"Configuration mise a jour pour {deviceId}: {patch}",
                Level = "info",
                Timestamp = DateTime.UtcNow
            };
            _context.EventLogs.Add(eventLog);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Configuration updated for device {DeviceId}: {Patch}", deviceId, patch);

            return Ok(new
            {
                success = true,
                message = $"Configuration mise à jour pour {deviceId}",
                patch = patch
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update config for {DeviceId}", deviceId);
            return Ok(new { success = false, message = $"Erreur: {ex.Message}" });
        }
    }
}

public class DeviceConfigUpdate
{
    public bool? DetectionEnabled { get; set; }
    public int? CooldownMs { get; set; }
}

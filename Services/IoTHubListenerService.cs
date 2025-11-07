using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using IoTDetectorApi.Data;
using IoTDetectorApi.Models;

namespace IoTDetectorApi.Services;

public class IoTHubListenerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IoTHubListenerService> _logger;
    private readonly IConfiguration _configuration;

    public IoTHubListenerService(
        IServiceProvider serviceProvider,
        ILogger<IoTHubListenerService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>("Azure:IoTHub:Enabled");
        if (!enabled)
        {
            _logger.LogInformation("Azure IoT Hub listener is disabled");
            return;
        }

        var eventHubConnectionString = _configuration.GetValue<string>("Azure:IoTHub:EventHubConnectionString");
        var consumerGroup = _configuration.GetValue<string>("Azure:IoTHub:ConsumerGroup") ?? EventHubConsumerClient.DefaultConsumerGroupName;

        if (string.IsNullOrEmpty(eventHubConnectionString))
        {
            _logger.LogWarning("Azure Event Hub connection string not configured");
            return;
        }

        _logger.LogInformation("Starting Azure IoT Hub listener...");

        // Retry loop with exponential backoff
        var retryCount = 0;
        var maxRetries = 10;
        var baseDelay = TimeSpan.FromSeconds(5);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Create Event Hub consumer client directly with Event Hub connection string
                await using var consumer = new EventHubConsumerClient(
                    consumerGroup,
                    eventHubConnectionString);

                _logger.LogInformation("Connected to Azure IoT Hub Event Hub. Consumer Group: {ConsumerGroup}", consumerGroup);

                // Create log entry
                await LogEventAsync("Azure IoT Hub", "Connexion établie avec Azure IoT Hub", "info");

                // Reset retry count on successful connection
                retryCount = 0;

                // Read events from all partitions
                await foreach (PartitionEvent partitionEvent in consumer.ReadEventsAsync(stoppingToken))
                {
                    if (partitionEvent.Data == null)
                        continue;

                    try
                    {
                        await ProcessEventAsync(partitionEvent.Data);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing IoT Hub event from device {DeviceId}",
                            partitionEvent.Data.SystemProperties.TryGetValue("iothub-connection-device-id", out var deviceId)
                                ? deviceId
                                : "unknown");
                        // Continue processing other events
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("IoT Hub listener stopped gracefully");
                break;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("IoT Hub listener stopped due to cancellation");
                break;
            }
            catch (Exception ex)
            {
                retryCount++;
                var delay = TimeSpan.FromSeconds(Math.Min(baseDelay.TotalSeconds * Math.Pow(2, retryCount - 1), 300)); // Max 5 minutes

                _logger.LogError(ex,
                    "Error in IoT Hub listener (attempt {RetryCount}/{MaxRetries}). Reconnecting in {Delay} seconds...",
                    retryCount, maxRetries, delay.TotalSeconds);

                await LogEventAsync("Azure IoT Hub",
                    $"Erreur de connexion (tentative {retryCount}/{maxRetries}): {ex.Message}",
                    retryCount >= maxRetries ? "error" : "warning");

                if (retryCount >= maxRetries)
                {
                    _logger.LogCritical("Max retry attempts reached. Stopping IoT Hub listener.");
                    await LogEventAsync("Azure IoT Hub",
                        "Nombre maximum de tentatives de reconnexion atteint. Service arrêté.",
                        "error");
                    break;
                }

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    _logger.LogInformation("IoT Hub listener stopped during reconnection delay");
                    break;
                }
            }
        }
    }

    private async Task ProcessEventAsync(EventData eventData)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Get device ID from system properties
        var systemDeviceId = eventData.SystemProperties.ContainsKey("iothub-connection-device-id")
            ? eventData.SystemProperties["iothub-connection-device-id"]?.ToString()
            : null;

        var deviceId = string.IsNullOrWhiteSpace(systemDeviceId) ? "unknown" : systemDeviceId;

        // Map physical device ID to logical device ID
        if (deviceId == "0a10aced202194944a044df4")
        {
            deviceId = "photon2-pir-01";
        }

        // Parse message body
        var messageBody = Encoding.UTF8.GetString(eventData.EventBody.ToArray());
        _logger.LogInformation("Received message from {DeviceId}: {Message}", deviceId, messageBody);

        JsonElement root;
        try
        {
            using var jsonDoc = JsonDocument.Parse(messageBody);
            root = jsonDoc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse message as JSON: {Message}", messageBody);
            return;
        }

        // Update device LastSeen
        var device = await context.Devices.FindAsync(deviceId);
        if (device != null)
        {
            device.LastSeen = DateTime.UtcNow;
            device.Status = "active";
            await context.SaveChangesAsync();
        }
        else
        {
            // Create device if it doesn't exist
            _logger.LogInformation("Creating new device: {DeviceId}", deviceId);
            device = new Device
            {
                Id = deviceId,
                Name = deviceId,
                Type = deviceId.Contains("esp32") ? "ESP32" : deviceId.Contains("photon") ? "Photon2" : "Unknown",
                Status = "active",
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            context.Devices.Add(device);
            await context.SaveChangesAsync();

            await LogEventAsync(deviceId, $"Nouveau device détecté: {deviceId}", "info");
        }

        // Determine event type - check if there's nested data with the real event type
        string? eventType = null;
        JsonElement dataElement = default;
        bool hasNestedData = false;

        // First check if there's a "data" property that contains stringified JSON
        if (root.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.String)
        {
            string? dataString = dataProp.GetString();
            if (!string.IsNullOrEmpty(dataString))
            {
                try
                {
                    using var innerDoc = JsonDocument.Parse(dataString);
                    dataElement = innerDoc.RootElement.Clone();
                    hasNestedData = true;

                    // Get event type from nested data
                    if (dataElement.TryGetProperty("event", out var innerEventProp))
                    {
                        var innerEvent = innerEventProp.GetString();
                        if (!string.IsNullOrEmpty(innerEvent))
                        {
                            eventType = innerEvent;
                        }
                    }
                }
                catch (JsonException)
                {
                    // If parsing fails, fall back to outer event property
                }
            }
        }

        // If no nested event found, check outer event property
        if (string.IsNullOrEmpty(eventType))
        {
            if (root.TryGetProperty("event", out var eventProp) && eventProp.ValueKind == JsonValueKind.String)
            {
                eventType = eventProp.GetString();
            }

            eventType ??= "unknown";
        }

        var sensorData = new SensorData
        {
            DeviceId = deviceId,
            EventType = eventType ?? "unknown",
            Timestamp = DateTime.UtcNow,
            RawData = messageBody
        };

        // Parse specific fields based on event type
        if (eventType == "motion" || eventType == "motion_detected")
        {
            sensorData.Value = 1;

            // Check for count in nested data first, then outer level
            if (hasNestedData && dataElement.TryGetProperty("count", out var nestedCountProp))
            {
                sensorData.Value = nestedCountProp.GetInt32();
            }
            else if (root.TryGetProperty("count", out var countProp))
            {
                sensorData.Value = countProp.GetInt32();
            }

            // Create event log for motion detection
            await LogEventAsync(deviceId, $"Mouvement détecté par {deviceId} (count: {sensorData.Value})", "info");
        }

        context.SensorData.Add(sensorData);
        await context.SaveChangesAsync();

        _logger.LogInformation("Saved sensor data: {DeviceId} - {EventType}", deviceId, eventType);
    }

    private async Task LogEventAsync(string? deviceId, string message, string level)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var eventLog = new EventLog
            {
                DeviceId = deviceId,
                Message = message,
                Level = level,
                Timestamp = DateTime.UtcNow
            };

            context.EventLogs.Add(eventLog);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log event");
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stopping Azure IoT Hub listener...");
        await base.StopAsync(stoppingToken);
    }
}

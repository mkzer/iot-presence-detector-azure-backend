# IoT Presence Detector — Backend API

REST API for a multi-sensor IoT presence/alarm system. It ingests telemetry from
ESP32 and Particle Photon 2 presence sensors (via **Azure IoT Hub**), persists it in
**PostgreSQL**, and serves it to the [dashboard frontend](https://github.com/mkzer/iot-presence-detector-azure-frontend).

![.NET](https://img.shields.io/badge/.NET_9-512BD4?style=flat&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=flat&logo=csharp&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?style=flat&logo=postgresql&logoColor=white)
![Azure IoT Hub](https://img.shields.io/badge/Azure_IoT_Hub-0078D4?style=flat&logo=microsoftazure&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=flat&logo=docker&logoColor=white)

## Features

- **Azure IoT Hub ingestion** — a hosted background service (`IoTHubListenerService`) consumes
  device messages from the IoT Hub Event Hub endpoint.
- **JWT authentication** with BCrypt-hashed passwords and role-based access (`admin` / `user`).
- **PostgreSQL + EF Core** (Npgsql) with migrations applied automatically on startup.
- **Hardening**: rate limiting (AspNetCoreRateLimit), security headers, HSTS, strict CORS,
  input-sanitization middleware, response compression (Brotli/Gzip) and response caching.
- **Observability**: structured logging with Serilog (console + daily rolling files),
  request logging, and health checks at `/health` and `/health/ready`.

## API controllers

| Controller | Responsibility |
|---|---|
| `AuthController` | Login / register, JWT issuance |
| `DevicesController` | Device registry (ESP32, Photon2…) |
| `TelemetryController` | Sensor telemetry ingest / query |
| `EventsController` | Presence / alarm events |
| `AzureController` | Azure IoT Hub device management |
| `ExportController` | Data export |

## Tech stack

ASP.NET Core Web API (.NET 9) · C# · Entity Framework Core · PostgreSQL (Npgsql) ·
Azure IoT Hub (`Azure.Messaging.EventHubs`, `Microsoft.Azure.Devices`) · JWT · Serilog · Docker

## Getting started

```bash
dotnet restore
dotnet run
```

Configuration is read from `appsettings.json` / environment variables. Minimum required:

| Key | Description |
|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `Jwt__Key` | JWT signing key (≥ 32 characters) |
| `Jwt__Issuer`, `Jwt__Audience` | JWT issuer / audience |
| `Azure__IoTHub__EventHubConnectionString` | Azure IoT Hub Event Hub connection string |

See `appsettings.Production.json.example` for the full list, and
[`README_DEPLOYMENT.md`](./README_DEPLOYMENT.md) for the Render deployment guide.

## Deployment

A `Dockerfile` is provided. The API also deploys to Render — see
[`README_DEPLOYMENT.md`](./README_DEPLOYMENT.md).

---

Part of the **IoT Presence Detector** project:
[overview](https://github.com/mkzer/iot-presence-detector-azure) ·
[frontend](https://github.com/mkzer/iot-presence-detector-azure-frontend)

# Déploiement Backend sur Render

## Variables d'environnement requises

Configurez ces variables d'environnement sur Render:

```bash
# Database Supabase
ConnectionStrings__DefaultConnection=Host=aws-1-eu-west-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.seppvhdembyrgbsxrmzs;Password=VOTRE_MOT_DE_PASSE_SUPABASE;SSL Mode=Require;Trust Server Certificate=true

# JWT (Générez une clé sécurisée de 64+ caractères)
Jwt__Key=VOTRE_CLE_JWT_SECURISEE_MINIMUM_64_CARACTERES
Jwt__Issuer=IoTDetectorApi
Jwt__Audience=IoTDetectorClient
Jwt__ExpiryMinutes=1440

# Azure IoT Hub
Azure__IoTHub__Enabled=true
Azure__IoTHub__EventHubConnectionString=VOTRE_AZURE_IOTHUB_EVENTHUB_CONNECTION_STRING
Azure__IoTHub__ConsumerGroup=$Default

# Azure IoT Hub Management
AzureIoTHub__ConnectionString=VOTRE_AZURE_IOTHUB_CONNECTION_STRING
AzureIoTHub__EventHubConnectionString=VOTRE_AZURE_IOTHUB_EVENTHUB_CONNECTION_STRING
AzureIoTHub__ConsumerGroup=$Default

# ASP.NET Core
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:10000
```

## Démarrage

```bash
dotnet run
```

ou en production:

```bash
dotnet IoTDetectorApi.dll
```

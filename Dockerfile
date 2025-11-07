# Multi-stage Dockerfile for .NET 9 IoT Detector API

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY IoTDetectorApi.csproj .
RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copy published app from build stage
COPY --from=build /app/publish .

# Expose port (Render uses PORT env variable)
ENV ASPNETCORE_URLS=http://+:${PORT:-10000}
EXPOSE ${PORT:-10000}

# Run the application
ENTRYPOINT ["dotnet", "IoTDetectorApi.dll"]

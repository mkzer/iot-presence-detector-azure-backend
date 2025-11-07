using Microsoft.EntityFrameworkCore;
using IoTDetectorApi.Models;

namespace IoTDetectorApi.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Device> Devices { get; set; } = null!;
    public DbSet<SensorData> SensorData { get; set; } = null!;
    public DbSet<EventLog> EventLogs { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<SensorData>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<EventLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => e.Username).IsUnique();
        });
    }
}

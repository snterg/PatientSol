using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PatientSolution.Models;
using System.Text.Json;

namespace PatientSolution.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Patient> Patients { get; set; }
    public DbSet<PatientInfo> PatientInfos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Patient>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Gender)
                .HasMaxLength(20);

            entity.Property(e => e.BirthDate)
                .IsRequired();

            // Настройка внешнего ключа (ДОБАВЛЕНО)
            entity.Property(e => e.PatientInfoId)
                .IsRequired();

            // Связь один-к-одному
            entity.HasOne(e => e.Name)
                .WithOne(e => e.Patient)
                .HasForeignKey<Patient>(e => e.PatientInfoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PatientInfo>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Use)
                .HasMaxLength(50);

            entity.Property(e => e.Family)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Given)
                .HasColumnType("nvarchar(max)")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
                );

            entity.Property(e => e.Given).Metadata.SetValueComparer(
                new ValueComparer<List<string>>(
                    (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v != null ? v.GetHashCode() : 0)),
                    c => c.ToList()
                ));
        });
    }
}
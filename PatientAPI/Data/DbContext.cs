using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PatientSolution.Models;
using System.Text.Json;

namespace PatientSolution.Data;

/// <summary>
/// Контекст базы данных для работы с пациентами
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Коллекция пациентов
    /// </summary>
    public DbSet<Patient> Patients { get; set; }

    /// <summary>
    /// Коллекция имен пациентов
    /// </summary>
    public DbSet<PatientInfo> PatientInfos { get; set; }

    /// <summary>
    /// Настройка модели базы данных
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Настройка сущности Patient
        modelBuilder.Entity<Patient>(entity =>
        {
            // Установка первичного ключа
            entity.HasKey(e => e.Id);

            // Настройка поля Gender
            entity.Property(e => e.Gender)
                .HasMaxLength(20); // Ограничение длины

            // Настройка поля BirthDate (обязательное поле)
            entity.Property(e => e.BirthDate)
                .IsRequired();

            // Настройка внешнего ключа PatientInfoId (обязательное поле)
            entity.Property(e => e.PatientInfoId)
                .IsRequired();

            // Настройка связи один-к-одному с PatientInfo
            entity.HasOne(e => e.Name)
                .WithOne(e => e.Patient)
                .HasForeignKey<Patient>(e => e.PatientInfoId)
                .OnDelete(DeleteBehavior.Cascade); // Каскадное удаление
        });

        // Настройка сущности PatientInfo
        modelBuilder.Entity<PatientInfo>(entity =>
        {
            // Установка первичного ключа
            entity.HasKey(e => e.Id);

            // Настройка поля Use
            entity.Property(e => e.Use)
                .HasMaxLength(50); // Ограничение длины

            // Настройка поля Family (обязательное поле)
            entity.Property(e => e.Family)
                .IsRequired()
                .HasMaxLength(100); // Ограничение длины

            // Настройка поля Given - список имен
            entity.Property(e => e.Given)
                .HasColumnType("nvarchar(max)") // Хранение как JSON
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
                );

            // Настройка сравнения для списка Given
            entity.Property(e => e.Given).Metadata.SetValueComparer(
                new ValueComparer<List<string>>(
                    (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v != null ? v.GetHashCode() : 0)),
                    c => c.ToList()
                ));
        });
    }
}
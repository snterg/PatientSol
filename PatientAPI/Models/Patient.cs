using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PatientSolution.Models;

/// <summary>
/// Модель пациента
/// </summary>
public class Patient
{
    /// <summary>
    /// Внутренний идентификатор пациента в БД (не отображается в API)
    /// </summary>
    [Key]
    [JsonIgnore]
    public Guid Id { get; set; }

    /// <summary>
    /// Подробная информация о пациенте (обязательное поле)
    /// </summary>
    [Required(ErrorMessage = "Информация обязательна!!!")]
    public PatientInfo Name { get; set; } = null!;

    /// <summary>
    /// Пол пациента (male, female, other, unknown)
    /// </summary>
    [MaxLength(20)]
    public string? Gender { get; set; }

    /// <summary>
    /// Дата рождения (обязательное поле)
    /// </summary>
    [Required(ErrorMessage = "Дата рождения обязательна!!!")]
    public DateTime BirthDate { get; set; }

    /// <summary>
    /// Статус активности пациента (true/false)
    /// </summary>
    public bool Active { get; set; }

    /// <summary>
    /// Внешний ключ для связи с PatientInfo
    /// </summary>
    [JsonIgnore]
    public Guid PatientInfoId { get; set; }
}
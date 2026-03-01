using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PatientSolution.Models;

/// <summary>
/// Модель подробной информации о пациенте
/// </summary>
public class PatientInfo
{
    /// <summary>
    /// Уникальный идентификатор имени (отображается в API)
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Тип использования имени
    /// </summary>
    [MaxLength(50)]
    public string? Use { get; set; }

    /// <summary>
    /// Фамилия пациента (обязательное поле)
    /// </summary>
    [Required(ErrorMessage = "Фамилия обязательна!!!")]
    [MaxLength(100)]
    public string Family { get; set; } = string.Empty;

    /// <summary>
    /// Имя и отчество
    /// </summary>
    public List<string> Given { get; set; } = new();

    /// <summary>
    /// Ссылка на пациента (не сериализуется в JSON)
    /// </summary>
    [JsonIgnore]
    public Patient? Patient { get; set; }
}
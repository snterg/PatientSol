using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PatientSolution.Models;

public class PatientInfo
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(50)]
    public string? Use { get; set; }

    [Required(ErrorMessage = "Family name is required")]
    [MaxLength(100)]
    public string Family { get; set; } = string.Empty;

    public List<string> Given { get; set; } = new();

    // Навигационное свойство для связи с Patient
    [JsonIgnore]
    public Patient? Patient { get; set; }
}
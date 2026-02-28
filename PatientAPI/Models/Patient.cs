using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PatientSolution.Models;

public class Patient
{
    [Key]
    [JsonIgnore]
    public Guid Id { get; set; }

    [Required]
    public PatientInfo Name { get; set; } = null!;

    [MaxLength(20)]
    public string? Gender { get; set; }

    [Required(ErrorMessage = "BirthDate is required")]
    public DateTime BirthDate { get; set; }

    public bool Active { get; set; }

    // Внешний ключ
    [JsonIgnore]
    public Guid PatientInfoId { get; set; }

}
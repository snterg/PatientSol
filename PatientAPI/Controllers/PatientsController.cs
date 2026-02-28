using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatientSolution.Data;
using PatientSolution.Models;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PatientSolution.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<PatientsController> _logger;

    // Dictionaries from the task
    private static readonly List<string> ValidGenders = new() { "male", "female", "other", "unknown" };
    private static readonly List<bool> ValidActive = new() { true, false };
    private const int MaxPatientsLimit = 1000;

    // Maximum field lengths for DoS protection
    private const int MaxFamilyLength = 100;
    private const int MaxGivenLength = 50;
    private const int MaxUseLength = 50;
    private const int MaxGivenItems = 10;
    private const int MaxStringLength = 500;

    // Regular expression for string validation (letters, spaces, hyphens only)
    private static readonly Regex NameRegex = new(@"^[\p{L}\s\-]+$", RegexOptions.Compiled);

    // Allowed characters for search
    private static readonly Regex SearchTermRegex = new(@"^[\p{L}\s\-0-9]*$", RegexOptions.Compiled);

    public PatientsController(AppDbContext context, ILogger<PatientsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region Get all patients with filtering (GET /api/patients)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Patient>>> GetPatients(
        [FromQuery] string? family = null,
        [FromQuery] string? given = null,
        [FromQuery] string? birthDate = null,
        [FromQuery] string? gender = null,
        [FromQuery] bool? active = null,
        [FromQuery] int? limit = 100,
        [FromQuery] int? offset = 0)
    {
        try
        {
            // Input validation
            if (limit.HasValue && (limit.Value < 1 || limit.Value > 1000))
            {
                return BadRequest(new { error = "Limit must be between 1 and 1000" });
            }

            if (offset.HasValue && offset.Value < 0)
            {
                return BadRequest(new { error = "Offset must be non-negative" });
            }

            // Search term sanitization
            if (!string.IsNullOrWhiteSpace(family) && !IsValidSearchTerm(family))
            {
                return BadRequest(new { error = "Family name contains invalid characters" });
            }

            if (!string.IsNullOrWhiteSpace(given) && !IsValidSearchTerm(given))
            {
                return BadRequest(new { error = "Given name contains invalid characters" });
            }

            // Gender validation
            if (!string.IsNullOrWhiteSpace(gender))
            {
                var genderLower = gender.ToLower();
                if (!ValidGenders.Contains(genderLower))
                {
                    return BadRequest(new
                    {
                        error = "Invalid gender value",
                        message = $"Gender must be one of: {string.Join(", ", ValidGenders)}",
                        providedValue = gender
                    });
                }
            }

            var query = _context.Patients
                .Include(p => p.Name)
                .AsNoTracking()
                .AsQueryable();

            // Filters
            if (!string.IsNullOrWhiteSpace(family) && family.Length <= MaxFamilyLength)
            {
                query = query.Where(p => p.Name != null && p.Name.Family.Contains(family));
            }

            if (!string.IsNullOrWhiteSpace(given) && given.Length <= MaxGivenLength)
            {
                query = query.Where(p => p.Name != null && p.Name.Given != null &&
                    p.Name.Given.Any(g => g != null && g.Contains(given)));
            }

            if (!string.IsNullOrWhiteSpace(gender))
            {
                var genderLower = gender.ToLower();
                query = query.Where(p => p.Gender != null && p.Gender.ToLower() == genderLower);
            }

            if (active.HasValue)
            {
                query = query.Where(p => p.Active == active.Value);
            }

            // FHIR birthDate filter
            if (!string.IsNullOrWhiteSpace(birthDate))
            {
                if (birthDate.Length > 50)
                {
                    return BadRequest(new { error = "BirthDate parameter is too long" });
                }

                var (prefix, date) = ParseFhirDateParameter(birthDate);

                if (date.HasValue)
                {
                    if (date.Value > DateTime.UtcNow)
                    {
                        return BadRequest(new { error = "BirthDate cannot be in the future" });
                    }

                    if (date.Value < DateTime.UtcNow.AddYears(-150))
                    {
                        return BadRequest(new { error = "BirthDate is too far in the past" });
                    }

                    query = prefix switch
                    {
                        "eq" => query.Where(p => p.BirthDate.Date == date.Value.Date),
                        "ne" => query.Where(p => p.BirthDate.Date != date.Value.Date),
                        "lt" => query.Where(p => p.BirthDate.Date < date.Value.Date),
                        "gt" => query.Where(p => p.BirthDate.Date > date.Value.Date),
                        "le" => query.Where(p => p.BirthDate.Date <= date.Value.Date),
                        "ge" => query.Where(p => p.BirthDate.Date >= date.Value.Date),
                        "sa" => query.Where(p => p.BirthDate.Date > date.Value.Date),
                        "eb" => query.Where(p => p.BirthDate.Date < date.Value.Date),
                        "ap" => query.Where(p =>
                            p.BirthDate.Date >= date.Value.AddDays(-1).Date &&
                            p.BirthDate.Date <= date.Value.AddDays(1).Date),
                        _ => query.Where(p => p.BirthDate.Date == date.Value.Date)
                    };
                }
                else
                {
                    return BadRequest(new { error = "Invalid birthDate format. Use YYYY-MM-DD or FHIR format (eq2024-01-13)" });
                }
            }

            // Pagination
            var result = await query
                .Skip(offset.Value)
                .Take(limit.Value)
                .ToListAsync();

            // Pagination headers
            Response.Headers.Add("X-Total-Count", (await query.CountAsync()).ToString());
            Response.Headers.Add("X-Limit", limit.Value.ToString());
            Response.Headers.Add("X-Offset", offset.Value.ToString());

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting patients");
            return StatusCode(500, new { error = "An error occurred while retrieving patients." });
        }
    }
    #endregion

    

    #region Get patient by ID (GET /api/patients/{id})
    /// <summary>
    /// Get a specific patient by ID
    /// </summary>
    /// <param name="id">Patient ID (from PatientInfo)</param>
    /// <returns>The patient with specified ID</returns>
    /// <response code="200">Returns the patient</response>
    /// <response code="400">If the ID is invalid</response>
    /// <response code="404">If the patient is not found</response>
    /// <response code="500">If there was a server error</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Patient), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Patient>> GetPatient(Guid id)
    {
        try
        {
            // Validate ID
            if (id == Guid.Empty)
            {
                return BadRequest(new { error = "Invalid patient ID" });
            }

            // Find patient by Name.Id (since Patient.Id is JsonIgnore)
            var patient = await _context.Patients
                .Include(p => p.Name)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Name.Id == id);

            if (patient == null)
            {
                _logger.LogWarning("Patient with ID {Id} not found", id);
                return NotFound($"Patient with ID {id} not found.");
            }

            return Ok(patient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting patient with ID {Id}", id);
            return StatusCode(500, new { error = "An error occurred while retrieving the patient." });
        }
    }
    #endregion

    #region Get patients count (GET /api/patients/count)
    [HttpGet("count")]
    public async Task<ActionResult<object>> GetPatientsCount(
        [FromQuery] bool? active = null)
    {
        try
        {
            var query = _context.Patients.AsQueryable();

            if (active.HasValue)
            {
                query = query.Where(p => p.Active == active.Value);
            }

            var count = await query.CountAsync();

            return Ok(new
            {
                totalCount = count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting patients count");
            return StatusCode(500, new { error = "An error occurred while counting patients." });
        }
    }
    #endregion

    #region Create new patient (POST /api/patients)
    [HttpPost]
    public async Task<ActionResult<Patient>> CreatePatient(
       Patient patient)
    {
        try
        {
            if (patient == null)
            {
                return BadRequest(new { error = "Patient data is required" });
            }

            // Check patient limit
            var currentCount = await _context.Patients.CountAsync();
            if (currentCount >= MaxPatientsLimit)
            {
                _logger.LogWarning("Attempted to create patient when maximum limit of {Limit} reached", MaxPatientsLimit);
                return BadRequest(new
                {
                    error = "Maximum patients limit reached",
                    message = $"Cannot create more than {MaxPatientsLimit} patients",
                    currentCount = currentCount,
                    limit = MaxPatientsLimit
                });
            }

            var errors = new List<string>();

            // BirthDate validation
            if (patient.BirthDate == default)
            {
                errors.Add("BirthDate is required");
            }
            else
            {
                if (patient.BirthDate > DateTime.UtcNow)
                {
                    errors.Add("BirthDate cannot be in the future");
                }

                if (patient.BirthDate < DateTime.UtcNow.AddYears(-150))
                {
                    errors.Add("BirthDate is too far in the past");
                }
            }

            // Name validation
            if (patient.Name == null)
            {
                errors.Add("Name is required");
            }
            else
            {
                // Family validation
                if (string.IsNullOrWhiteSpace(patient.Name.Family))
                {
                    errors.Add("Family name is required");
                }
                else if (patient.Name.Family.Length > MaxFamilyLength)
                {
                    errors.Add($"Family name cannot exceed {MaxFamilyLength} characters");
                }
                else if (!IsValidName(patient.Name.Family))
                {
                    errors.Add("Family name contains invalid characters. Only letters, spaces, and hyphens are allowed.");
                }

                // Given validation
                if (patient.Name.Given != null)
                {
                    if (patient.Name.Given.Count > MaxGivenItems)
                    {
                        errors.Add($"Cannot have more than {MaxGivenItems} given names");
                    }

                    for (int i = 0; i < patient.Name.Given.Count; i++)
                    {
                        var given = patient.Name.Given[i];
                        if (!string.IsNullOrWhiteSpace(given))
                        {
                            if (given.Length > MaxGivenLength)
                            {
                                errors.Add($"Given name at position {i + 1} cannot exceed {MaxGivenLength} characters");
                            }
                            else if (!IsValidName(given))
                            {
                                errors.Add($"Given name at position {i + 1} contains invalid characters.");
                            }
                        }
                    }
                }

                // Use validation
                if (!string.IsNullOrWhiteSpace(patient.Name.Use) && patient.Name.Use.Length > MaxUseLength)
                {
                    errors.Add($"Use cannot exceed {MaxUseLength} characters");
                }
            }

            // Gender validation
            if (!string.IsNullOrWhiteSpace(patient.Gender))
            {
                if (!ValidGenders.Contains(patient.Gender.ToLower()))
                {
                    errors.Add($"Invalid gender. Allowed values: {string.Join(", ", ValidGenders)}");
                }
                else
                {
                    patient.Gender = patient.Gender.ToLower();
                }
            }

            // Active validation
            if (!ValidActive.Contains(patient.Active))
            {
                errors.Add($"Invalid active value. Allowed values: {string.Join(", ", ValidActive)}");
            }

            if (errors.Any())
            {
                return BadRequest(new { errors });
            }

            // ID generation
            if (patient.Id == Guid.Empty)
            {
                patient.Id = Guid.NewGuid();
            }

            if (patient.Name != null && patient.Name.Id == Guid.Empty)
            {
                patient.Name.Id = Guid.NewGuid();
            }

            // EF Core will set the relationship automatically
            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();

            // Load related data for response
            if (patient.Name != null)
            {
                await _context.Entry(patient)
                    .Reference(p => p.Name)
                    .LoadAsync();
            }

            _logger.LogInformation("Patient created with ID {Id}, Active: {Active}", patient.Name.Id, patient.Active);

            return CreatedAtAction(nameof(GetPatient), new { id = patient.Name.Id }, patient);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error creating patient");
            return StatusCode(500, new { error = "A database error occurred while creating the patient." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating patient");
            return StatusCode(500, new { error = "An error occurred while creating the patient." });
        }
    }
    #endregion

    #region Update patient (PUT /api/patients/{id})
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePatient(
        Guid id,Patient patient)
    {
        try
        {
            if (id != patient.Name.Id)
            {
                return BadRequest(new { error = "ID in URL does not match patient ID" });
            }

            if (id == Guid.Empty)
            {
                return BadRequest(new { error = "Invalid patient ID" });
            }

            var errors = new List<string>();

            // BirthDate validation
            if (patient.BirthDate == default)
            {
                errors.Add("BirthDate is required");
            }
            else
            {
                if (patient.BirthDate > DateTime.UtcNow)
                {
                    errors.Add("BirthDate cannot be in the future");
                }
                if (patient.BirthDate < DateTime.UtcNow.AddYears(-150))
                {
                    errors.Add("BirthDate is too far in the past");
                }
            }

            // Name validation
            if (patient.Name == null)
            {
                errors.Add("Name is required");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(patient.Name.Family))
                {
                    errors.Add("Family name is required");
                }
                else if (patient.Name.Family.Length > MaxFamilyLength)
                {
                    errors.Add($"Family name cannot exceed {MaxFamilyLength} characters");
                }
                else if (!IsValidName(patient.Name.Family))
                {
                    errors.Add("Family name contains invalid characters");
                }
            }

            // Gender validation
            if (!string.IsNullOrWhiteSpace(patient.Gender) && !ValidGenders.Contains(patient.Gender.ToLower()))
            {
                errors.Add($"Invalid gender. Allowed values: {string.Join(", ", ValidGenders)}");
            }

            // Active validation
            if (!ValidActive.Contains(patient.Active))
            {
                errors.Add($"Invalid active value. Allowed values: {string.Join(", ", ValidActive)}");
            }

            if (errors.Any())
            {
                return BadRequest(new { errors });
            }

            var existingPatient = await _context.Patients
                .Include(p => p.Name)
                .FirstOrDefaultAsync(p => p.Name.Id == id);

            if (existingPatient == null)
            {
                return NotFound($"Patient with ID {id} not found.");
            }

            // Update fields
            existingPatient.Gender = !string.IsNullOrWhiteSpace(patient.Gender) ? patient.Gender.ToLower() : null;
            existingPatient.BirthDate = patient.BirthDate;
            existingPatient.Active = patient.Active;

            if (existingPatient.Name != null && patient.Name != null)
            {
                existingPatient.Name.Use = patient.Name.Use;
                existingPatient.Name.Family = patient.Name.Family;
                existingPatient.Name.Given = patient.Name.Given ?? new List<string>();
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Patient updated with ID {Id}, Active: {Active}", id, patient.Active);

            return NoContent();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency conflict updating patient with ID {Id}", id);
            return Conflict(new { error = "The patient was modified by another user. Please reload and try again." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating patient with ID {Id}", id);
            return StatusCode(500, new { error = "An error occurred while updating the patient." });
        }
    }
    #endregion

    #region Partially update patient (PATCH /api/patients/{id})
    [HttpPatch("{id}")]
    public async Task<IActionResult> ToggleActive(Guid id)
    {
        try
        {
            if (id == Guid.Empty)
            {
                return BadRequest(new { error = "Invalid patient ID" });
            }

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.Name.Id == id);

            if (patient == null)
            {
                return NotFound($"Patient with ID {id} not found.");
            }

            // Toggle active status
            patient.Active = !patient.Active;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Patient active status toggled for ID {Id}. New status: {Active}",
                id, patient.Active);

            return Ok(new { id = patient.Name.Id, active = patient.Active });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling active status for patient {Id}", id);
            return StatusCode(500, new { error = "An error occurred while toggling active status." });
        }
    }
    #endregion

    #region Delete patient (DELETE /api/patients/{id})
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePatient(Guid id)
    {
        try
        {
            if (id == Guid.Empty)
            {
                return BadRequest(new { error = "Invalid patient ID" });
            }

            var patient = await _context.Patients
                .Include(p => p.Name)
                .FirstOrDefaultAsync(p => p.Name.Id == id);

            if (patient == null)
            {
                return NotFound($"Patient with ID {id} not found.");
            }

            _context.Patients.Remove(patient);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Patient deleted with ID {Id}", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting patient with ID {Id}", id);
            return StatusCode(500, new { error = "An error occurred while deleting the patient." });
        }
    }
    #endregion#region Get dictionaries (GET /api/patients/valid-values)

    #region Get dictionaries (GET /api/patients/valid-values)
    /// <summary>
    /// Gets valid values for dictionaries and API version
    /// </summary>
    /// <returns>Dictionary values and API version</returns>
    /// <response code="200">Returns dictionary values and version</response>
    [HttpGet("valid-values")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult<object> GetValidValues()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        var buildDate = System.IO.File.GetLastWriteTime(assembly.Location);
        var informationalVersion = assembly
            .GetCustomAttributes<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion ?? version?.ToString() ?? "1.0.0";

        return Ok(new
        {
            // Dictionary values
            genders = ValidGenders,
            active = ValidActive,

            // Version information
            version = informationalVersion,
           

            // Build information
            buildDate = buildDate.ToString("yyyy-MM-dd HH:mm:ss"),

            // API information
            apiName = "Patient API",
            description = "API for patient management",
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",

            // Timestamp
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        });
    }
    #endregion

    #region Helper validation methods
    private bool IsValidName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) &&
               name.Length <= MaxStringLength &&
               NameRegex.IsMatch(name);
    }

    private bool IsValidSearchTerm(string term)
    {
        return string.IsNullOrWhiteSpace(term) ||
               (term.Length <= MaxStringLength &&
                SearchTermRegex.IsMatch(term));
    }

    private (string prefix, DateTime? date) ParseFhirDateParameter(string param)
    {
        if (string.IsNullOrWhiteSpace(param) || param.Length > 50)
        {
            return ("", null);
        }

        var prefixes = new[] { "eq", "ne", "lt", "gt", "le", "ge", "sa", "eb", "ap" };

        foreach (var prefix in prefixes)
        {
            if (param.StartsWith(prefix))
            {
                var dateStr = param.Substring(prefix.Length);
                if (DateTime.TryParse(dateStr, out var date))
                {
                    return (prefix, date);
                }
            }
        }

        if (DateTime.TryParse(param, out var defaultDate))
        {
            return ("eq", defaultDate);
        }

        return ("", null);
    }
    #endregion
}
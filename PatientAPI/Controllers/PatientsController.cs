using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatientSolution.Data;
using PatientSolution.Models;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PatientSolution.Controllers;

/// <summary>
/// Контроллер для управления пациентами
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<PatientsController> _logger;

    /// <summary>
    /// Допустимые значения пола из справочника
    /// </summary>
    private static readonly List<string> ValidGenders = new() { "male", "female", "other", "unknown" };

    /// <summary>
    /// Допустимые значения статуса активности
    /// </summary>
    private static readonly List<bool> ValidActive = new() { true, false };

    /// <summary>
    /// Максимальное количество пациентов в БД (защита от переполнения)
    /// </summary>
    private const int MaxPatientsLimit = 1000;

    /// <summary>
    /// Максимальная длина фамилии (защита от DoS атак)
    /// </summary>
    private const int MaxFamilyLength = 100;

    /// <summary>
    /// Максимальная длина имени (защита от DoS атак)
    /// </summary>
    private const int MaxGivenLength = 50;

    /// <summary>
    /// Максимальная длина поля Use (защита от DoS атак)
    /// </summary>
    private const int MaxUseLength = 50;

    /// <summary>
    /// Максимальное количество имен (защита от DoS атак)
    /// </summary>
    private const int MaxGivenItems = 10;

    /// <summary>
    /// Максимальная длина строки для общих проверок
    /// </summary>
    private const int MaxStringLength = 500;

    /// <summary>
    /// Регулярное выражение для валидации имени (только буквы, пробелы, дефисы)
    /// </summary>
    private static readonly Regex NameRegex = new(@"^[\p{L}\s\-]+$", RegexOptions.Compiled);

    /// <summary>
    /// Регулярное выражение для валидации поисковых запросов
    /// </summary>
    private static readonly Regex SearchTermRegex = new(@"^[\p{L}\s\-0-9]*$", RegexOptions.Compiled);

    public PatientsController(AppDbContext context, ILogger<PatientsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region GET: Получение всех пациентов с пагинацией

    /// <summary>
    /// Получает список всех пациентов с поддержкой пагинации
    /// </summary>
    /// <param name="limit">Лимит записей (по умолчанию 100)</param>
    /// <param name="offset">Смещение для пагинации</param>
    /// <returns>Список пациентов</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Patient>>> GetPatients(
        [FromQuery] int? limit = 100,
        [FromQuery] int? offset = 0)
    {
        _logger.LogInformation("Начало получения пациентов с параметрами: Limit={Limit}, Offset={Offset}", limit, offset);

        try
        {
            int currentLimit = limit ?? 100;
            int currentOffset = offset ?? 0;

            // ВАЖНО: Валидация должна быть ПЕРЕД запросом к БД
            if (currentLimit < 1 || currentLimit > 1000)
            {
                _logger.LogWarning("Неверное значение лимита: {Limit}", currentLimit);
                return BadRequest(new { error = "Лимит должен быть от 1 до 1000" });
            }

            if (currentOffset < 0)
            {
                _logger.LogWarning("Неверное значение смещения: {Offset}", currentOffset);
                return BadRequest(new { error = "Смещение не может быть отрицательным" });
            }

            // Получаем общее количество пациентов
            var totalCount = await _context.Patients.CountAsync();

            // ВАЖНО: Применяем Skip и Take для пагинации
            var patients = await _context.Patients
                .Include(p => p.Name)
                .OrderBy(p => p.BirthDate)
                .Skip(currentOffset)
                .Take(currentLimit)
                .AsNoTracking()
                .ToListAsync();

            // ВАЖНО: Проверяем, что Response не null перед добавлением заголовков
            if (Response != null)
            {
                Response.Headers.Add("X-Total-Count", totalCount.ToString());
                Response.Headers.Add("X-Limit", currentLimit.ToString());
                Response.Headers.Add("X-Offset", currentOffset.ToString());
            }

            _logger.LogInformation("Успешно получено {Count} пациентов из {TotalCount}", patients.Count, totalCount);

            return Ok(patients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении пациентов: {Message}", ex.Message);
            return StatusCode(500, new { error = "Произошла ошибка при получении пациентов." });
        }
    }

    #endregion

    #region GET: Поиск пациентов по дате рождения (FHIR)

    /// <summary>
    /// Поиск пациентов по дате рождения в формате FHIR
    /// </summary>
    /// <param name="birthDate">Дата в формате FHIR (eq2024-01-13, gt2024-01-01, lt2024-12-31, ...)</param>
    /// <returns>Список пациентов</returns>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<Patient>>> SearchByBirthDate(
        [FromQuery] string birthDate)
    {
        _logger.LogInformation("Поиск пациентов по дате: {BirthDate}", birthDate);

        try
        {
            if (string.IsNullOrWhiteSpace(birthDate))
            {
                return BadRequest(new { error = "Параметр birthDate обязателен" });
            }

            var (prefix, date) = ParseFhirDateParameter(birthDate);

            if (!date.HasValue)
            {
                return BadRequest(new { error = "Неверный формат даты. Используйте ГГГГ-ММ-ДД или FHIR формат (eq2024-01-13)" });
            }

            var query = _context.Patients
                .Include(p => p.Name)
                .AsNoTracking()
                .AsQueryable();

            _logger.LogInformation("Применяем фильтр с префиксом: {Prefix}, дата: {Date}", prefix, date.Value);

            // Применяем фильтр в зависимости от префикса согласно FHIR спецификации
            switch (prefix)
            {
                case "eq":
                    query = query.Where(p => p.BirthDate.Date == date.Value.Date);
                    break;
                case "ne":
                    query = query.Where(p => p.BirthDate.Date != date.Value.Date);
                    break;
                case "lt":
                    query = query.Where(p => p.BirthDate.Date < date.Value.Date);
                    break;
                case "gt":
                    query = query.Where(p => p.BirthDate.Date > date.Value.Date);
                    break;
                case "le":
                    query = query.Where(p => p.BirthDate.Date <= date.Value.Date);
                    break;
                case "ge":
                    query = query.Where(p => p.BirthDate.Date >= date.Value.Date);
                    break;
                case "sa":
                    // starts after - дата рождения после указанной даты
                    query = query.Where(p => p.BirthDate.Date > date.Value.Date);
                    break;
                case "eb":
                    // ends before - дата рождения до указанной даты
                    query = query.Where(p => p.BirthDate.Date < date.Value.Date);
                    break;
                case "ap":
                    // approximately - дата в пределах +/- 1 дня
                    query = query.Where(p =>
                        p.BirthDate.Date >= date.Value.AddDays(-1).Date &&
                        p.BirthDate.Date <= date.Value.AddDays(1).Date);
                    break;
                default:
                    query = query.Where(p => p.BirthDate.Date == date.Value.Date);
                    break;
            }

            var patients = await query.ToListAsync();

            _logger.LogInformation("Найдено {Count} пациентов", patients.Count);

            return Ok(patients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при поиске пациентов");
            return StatusCode(500, new { error = "Произошла ошибка при поиске пациентов." });
        }
    }

    #endregion

    #region GET: Получение пациента по ID

    /// <summary>
    /// Получает пациента по идентификатору
    /// </summary>
    /// <param name="id">Идентификатор пациента (Guid из поля Name.Id)</param>
    /// <returns>Данные пациента</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<Patient>> GetPatient(Guid id)
    {
        _logger.LogInformation("Начало получения пациента с ID: {Id}", id);

        try
        {
            // Проверка корректности ID
            if (id == Guid.Empty)
            {
                _logger.LogWarning("Попытка получения пациента с пустым ID");
                return BadRequest(new { error = "Неверный идентификатор пациента" });
            }

            // Поиск пациента по Name.Id
            var patient = await _context.Patients
                .Include(p => p.Name)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Name != null && p.Name.Id == id);

            if (patient == null)
            {
                _logger.LogWarning("Пациент с ID {Id} не найден", id);
                // Возвращаем JSON объект вместо строки
                return NotFound(new { error = $"Пациент с ID {id} не найден." });
            }

            _logger.LogInformation("Пациент с ID {Id} успешно получен", id);
            return Ok(patient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении пациента с ID {Id}", id);
            return StatusCode(500, new { error = "Произошла ошибка при получении пациента." });
        }
    }

    #endregion

    #region GET: Получение количества пациентов

    /// <summary>
    /// Получает количество пациентов с возможностью фильтрации по активности
    /// </summary>
    /// <param name="active">Фильтр по статусу активности</param>
    /// <returns>Количество пациентов</returns>
    [HttpGet("count")]
    public async Task<ActionResult<object>> GetPatientsCount(
        [FromQuery] bool? active = null)
    {
        _logger.LogInformation("Начало подсчета пациентов с фильтром Active={Active}", active);

        try
        {
            var query = _context.Patients.AsQueryable();

            // Фильтр по статусу активности
            if (active.HasValue)
            {
                query = query.Where(p => p.Active == active.Value);
                _logger.LogDebug("Применен фильтр по активности: {Active}", active.Value);
            }

            var count = await query.CountAsync();

            _logger.LogInformation("Подсчет пациентов завершен. Результат: {Count}", count);
            return Ok(new
            {
                totalCount = count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при подсчете пациентов");
            return StatusCode(500, new { error = "Произошла ошибка при подсчете пациентов." });
        }
    }

    #endregion

    #region POST: Создание нового пациента

    /// <summary>
    /// Создает нового пациента
    /// </summary>
    /// <param name="patient">Данные пациента</param>
    /// <returns>Созданный пациент</returns>
    [HttpPost]
    public async Task<ActionResult<Patient>> CreatePatient(
       Patient patient)
    {
        _logger.LogInformation("Начало создания нового пациента");

        try
        {
            // Проверка наличия данных
            if (patient == null)
            {
                _logger.LogWarning("Попытка создания пациента с null данными");
                return BadRequest(new { error = "Данные пациента обязательны" });
            }

            // Проверка лимита пациентов
            var currentCount = await _context.Patients.CountAsync();
            if (currentCount >= MaxPatientsLimit)
            {
                _logger.LogWarning("Попытка создать пациента при достижении лимита {Limit}. Текущее количество: {CurrentCount}",
                    MaxPatientsLimit, currentCount);
                return BadRequest(new
                {
                    error = "Достигнут максимальный лимит пациентов",
                    message = $"Нельзя создать более {MaxPatientsLimit} пациентов",
                    currentCount = currentCount,
                    limit = MaxPatientsLimit
                });
            }

            var errors = new List<string>();

            // Валидация даты рождения
            if (patient.BirthDate == default)
            {
                errors.Add("Дата рождения обязательна");
            }
            else
            {
                if (patient.BirthDate > DateTime.UtcNow)
                {
                    errors.Add("Дата рождения не может быть в будущем");
                }

                if (patient.BirthDate < DateTime.UtcNow.AddYears(-150))
                {
                    errors.Add("Дата рождения слишком далеко в прошлом");
                }
            }

            // Валидация имени
            if (patient.Name == null)
            {
                errors.Add("Информация об имени обязательна");
            }
            else
            {
                // Валидация фамилии
                if (string.IsNullOrWhiteSpace(patient.Name.Family))
                {
                    errors.Add("Фамилия обязательна");
                }
                else if (patient.Name.Family.Length > MaxFamilyLength)
                {
                    errors.Add($"Фамилия не может превышать {MaxFamilyLength} символов");
                }
                else if (!IsValidName(patient.Name.Family))
                {
                    errors.Add("Фамилия содержит недопустимые символы. Разрешены только буквы, пробелы и дефисы.");
                }

                // Валидация списка имен
                if (patient.Name.Given != null)
                {
                    if (patient.Name.Given.Count > MaxGivenItems)
                    {
                        errors.Add($"Нельзя указать более {MaxGivenItems} имен");
                    }

                    for (int i = 0; i < patient.Name.Given.Count; i++)
                    {
                        var given = patient.Name.Given[i];
                        if (!string.IsNullOrWhiteSpace(given))
                        {
                            if (given.Length > MaxGivenLength)
                            {
                                errors.Add($"Имя на позиции {i + 1} не может превышать {MaxGivenLength} символов");
                            }
                            else if (!IsValidName(given))
                            {
                                errors.Add($"Имя на позиции {i + 1} содержит недопустимые символы.");
                            }
                        }
                    }
                }

                // Валидация поля Use
                if (!string.IsNullOrWhiteSpace(patient.Name.Use) && patient.Name.Use.Length > MaxUseLength)
                {
                    errors.Add($"Поле Use не может превышать {MaxUseLength} символов");
                }
            }

            // Валидация пола
            if (!string.IsNullOrWhiteSpace(patient.Gender))
            {
                if (!ValidGenders.Contains(patient.Gender.ToLower()))
                {
                    errors.Add($"Недопустимый пол. Допустимые значения: {string.Join(", ", ValidGenders)}");
                }
                else
                {
                    patient.Gender = patient.Gender.ToLower();
                }
            }

            // Валидация статуса активности
            if (!ValidActive.Contains(patient.Active))
            {
                errors.Add($"Недопустимое значение активности. Допустимые значения: {string.Join(", ", ValidActive)}");
            }

            if (errors.Any())
            {
                _logger.LogWarning("Ошибки валидации при создании пациента: {@Errors}", errors);
                return BadRequest(new { errors });
            }

            // Генерация ID
            if (patient.Id == Guid.Empty)
            {
                patient.Id = Guid.NewGuid();
            }

            if (patient.Name != null && patient.Name.Id == Guid.Empty)
            {
                patient.Name.Id = Guid.NewGuid();
            }

            // Сохранение в БД
            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();

            // Загрузка связанных данных для ответа
            if (patient.Name != null)
            {
                await _context.Entry(patient)
                    .Reference(p => p.Name)
                    .LoadAsync();
            }

            _logger.LogInformation("Пациент успешно создан с ID {Id}, активен: {Active}", patient.Name?.Id, patient.Active);

            return CreatedAtAction(nameof(GetPatient), new { id = patient.Name?.Id }, patient);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Ошибка базы данных при создании пациента");
            return StatusCode(500, new { error = "Произошла ошибка базы данных при создании пациента." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании пациента");
            return StatusCode(500, new { error = "Произошла ошибка при создании пациента." });
        }
    }

    #endregion

    #region PUT: Полное обновление пациента

    /// <summary>
    /// Полностью обновляет данные пациента
    /// </summary>
    /// <param name="id">Идентификатор пациента</param>
    /// <param name="patient">Новые данные пациента</param>
    /// <returns>Статус операции</returns>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePatient(
        Guid id, Patient patient)
    {
        _logger.LogInformation("Начало обновления пациента с ID: {Id}", id);

        try
        {
            // Проверка на null
            if (patient == null)
            {
                return BadRequest(new { error = "Данные пациента обязательны" });
            }

            // Проверка соответствия ID
            if (patient.Name == null)
            {
                return BadRequest(new { error = "Информация об имени обязательна" });
            }

            if (id != patient.Name.Id)
            {
                _logger.LogWarning("Несоответствие ID в URL ({UrlId}) и теле запроса ({BodyId})", id, patient.Name.Id);
                return BadRequest(new { error = "ID в URL не соответствует ID пациента" });
            }

            if (id == Guid.Empty)
            {
                _logger.LogWarning("Попытка обновления пациента с пустым ID");
                return BadRequest(new { error = "Неверный идентификатор пациента" });
            }

            var errors = new List<string>();

            // Валидация даты рождения
            if (patient.BirthDate == default)
            {
                errors.Add("Дата рождения обязательна");
            }
            else
            {
                if (patient.BirthDate > DateTime.UtcNow)
                {
                    errors.Add("Дата рождения не может быть в будущем");
                }
                if (patient.BirthDate < DateTime.UtcNow.AddYears(-150))
                {
                    errors.Add("Дата рождения слишком далеко в прошлом");
                }
            }

            // Валидация имени
            if (patient.Name == null)
            {
                errors.Add("Информация об имени обязательна");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(patient.Name.Family))
                {
                    errors.Add("Фамилия обязательна");
                }
                else if (patient.Name.Family.Length > MaxFamilyLength)
                {
                    errors.Add($"Фамилия не может превышать {MaxFamilyLength} символов");
                }
                else if (!IsValidName(patient.Name.Family))
                {
                    errors.Add("Фамилия содержит недопустимые символы");
                }
            }

            // Валидация пола
            if (!string.IsNullOrWhiteSpace(patient.Gender) && !ValidGenders.Contains(patient.Gender.ToLower()))
            {
                errors.Add($"Недопустимый пол. Допустимые значения: {string.Join(", ", ValidGenders)}");
            }

            // Валидация статуса активности
            if (!ValidActive.Contains(patient.Active))
            {
                errors.Add($"Недопустимое значение активности. Допустимые значения: {string.Join(", ", ValidActive)}");
            }

            if (errors.Any())
            {
                _logger.LogWarning("Ошибки валидации при обновлении пациента {Id}: {@Errors}", id, errors);
                return BadRequest(new { errors });
            }

            var existingPatient = await _context.Patients
                .Include(p => p.Name)
                .FirstOrDefaultAsync(p => p.Name != null && p.Name.Id == id);

            if (existingPatient == null)
            {
                _logger.LogWarning("Пациент с ID {Id} не найден для обновления", id);
                return NotFound(new { error = $"Пациент с ID {id} не найден." });
            }

            // Обновление полей
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

            _logger.LogInformation("Пациент с ID {Id} успешно обновлен. Новый статус активности: {Active}", id, patient.Active);

            return NoContent();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Конфликт параллельного обновления пациента с ID {Id}", id);
            return Conflict(new { error = "Пациент был изменен другим пользователем. Обновите данные и повторите попытку." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обновлении пациента с ID {Id}", id);
            return StatusCode(500, new { error = "Произошла ошибка при обновлении пациента." });
        }
    }

    #endregion

    #region PATCH: Частичное обновление (переключение статуса)

    /// <summary>
    /// Переключает статус активности пациента
    /// </summary>
    /// <param name="id">Идентификатор пациента</param>
    /// <returns>Новый статус активности</returns>
    [HttpPatch("{id}")]
    public async Task<IActionResult> ToggleActive(Guid id)
    {
        _logger.LogInformation("Начало переключения статуса активности для пациента с ID: {Id}", id);

        try
        {
            if (id == Guid.Empty)
            {
                _logger.LogWarning("Попытка переключения статуса с пустым ID");
                return BadRequest(new { error = "Неверный идентификатор пациента" });
            }

            var patient = await _context.Patients
                .Include(p => p.Name)
                .FirstOrDefaultAsync(p => p.Name != null && p.Name.Id == id);

            if (patient == null)
            {
                _logger.LogWarning("Пациент с ID {Id} не найден для переключения статуса", id);
                return NotFound(new { error = $"Пациент с ID {id} не найден." });
            }

            var oldStatus = patient.Active;
            patient.Active = !patient.Active;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Статус активности пациента с ID {Id} переключен. Старый статус: {OldStatus}, новый статус: {NewStatus}",
                id, oldStatus, patient.Active);

            return Ok(new { id = patient.Name?.Id, active = patient.Active });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при переключении статуса активности для пациента {Id}", id);
            return StatusCode(500, new { error = "Произошла ошибка при переключении статуса активности." });
        }
    }

    #endregion

    #region DELETE: Удаление пациента

    /// <summary>
    /// Удаляет пациента
    /// </summary>
    /// <param name="id">Идентификатор пациента</param>
    /// <returns>Статус операции</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePatient(Guid id)
    {
        _logger.LogInformation("Начало удаления пациента с ID: {Id}", id);

        try
        {
            if (id == Guid.Empty)
            {
                _logger.LogWarning("Попытка удаления пациента с пустым ID");
                return BadRequest(new { error = "Неверный идентификатор пациента" });
            }

            var patient = await _context.Patients
                .Include(p => p.Name)
                .FirstOrDefaultAsync(p => p.Name != null && p.Name.Id == id);

            if (patient == null)
            {
                _logger.LogWarning("Пациент с ID {Id} не найден для удаления", id);
                return NotFound(new { error = $"Пациент с ID {id} не найден." });
            }

            _context.Patients.Remove(patient);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Пациент с ID {Id} успешно удален", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении пациента с ID {Id}", id);
            return StatusCode(500, new { error = "Произошла ошибка при удалении пациента." });
        }
    }

    #endregion

    #region GET: Получение информации об API и справочниках

    /// <summary>
    /// Получает допустимые значения для справочников и информацию об API
    /// </summary>
    /// <returns>Справочные значения и версия API</returns>
    [HttpGet("info")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult<object> GetValidValues()
    {
        _logger.LogDebug("Запрос справочной информации API");

        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        var buildDate = System.IO.File.GetLastWriteTime(assembly.Location);
        var informationalVersion = assembly
            .GetCustomAttributes<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion ?? version?.ToString() ?? "1.0.0";

        var response = new
        {
            // Справочные значения
            genders = ValidGenders,
            active = ValidActive,

            // Информация о версии
            version = informationalVersion,

            // Информация о сборке
            buildDate = buildDate.ToString("yyyy-MM-dd HH:mm:ss"),

            // Информация об API
            apiName = "Patient API",
            description = "API для управления пациентами",
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
        };

        _logger.LogInformation("Справочная информация успешно получена. Версия API: {Version}", informationalVersion);
        return Ok(response);
    }

    #endregion

    #region Вспомогательные методы для валидации

    /// <summary>
    /// Проверяет, является ли строка допустимым именем
    /// </summary>
    /// <param name="name">Проверяемая строка</param>
    /// <returns>true, если имя допустимо</returns>
    private bool IsValidName(string name)
    {
        var isValid = !string.IsNullOrWhiteSpace(name) &&
               name.Length <= MaxStringLength &&
               NameRegex.IsMatch(name);

        if (!isValid)
        {
            _logger.LogDebug("Строка '{Name}' не прошла валидацию имени", name);
        }

        return isValid;
    }

    /// <summary>
    /// Проверяет, является ли строка допустимым поисковым запросом
    /// </summary>
    /// <param name="term">Проверяемый запрос</param>
    /// <returns>true, если запрос допустим</returns>
    private bool IsValidSearchTerm(string term)
    {
        var isValid = string.IsNullOrWhiteSpace(term) ||
               (term.Length <= MaxStringLength &&
                SearchTermRegex.IsMatch(term));

        if (!isValid && !string.IsNullOrWhiteSpace(term))
        {
            _logger.LogDebug("Строка '{Term}' не прошла валидацию поискового запроса", term);
        }

        return isValid;
    }

    /// <summary>
    /// Разбирает параметр даты в формате FHIR
    /// </summary>
    /// <param name="param">Строка с префиксом и датой</param>
    /// <returns>Кортеж (префикс, дата)</returns>
    private (string prefix, DateTime? date) ParseFhirDateParameter(string param)
    {
        if (string.IsNullOrWhiteSpace(param) || param.Length > 50)
        {
            _logger.LogDebug("Неверный параметр даты: {Param}", param);
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
                    _logger.LogDebug("Распознан FHIR параметр: {Prefix} с датой {Date}", prefix, date);
                    return (prefix, date);
                }
            }
        }

        if (DateTime.TryParse(param, out var defaultDate))
        {
            _logger.LogDebug("Распознана простая дата: {Date}", defaultDate);
            return ("eq", defaultDate);
        }

        _logger.LogDebug("Не удалось распознать дату: {Param}", param);
        return ("", null);
    }

    #endregion
}
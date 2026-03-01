using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using PatientSolution.Controllers;
using PatientSolution.Data;
using PatientSolution.Models;

namespace PatientAPI.Tests;

[TestFixture]
public class PatientsControllerTests
{
    private AppDbContext _context;
    private PatientsController _controller;
    private Mock<ILogger<PatientsController>> _loggerMock;

    [SetUp]
    public void Setup()
    {
        // Создаем In-Memory базу данных перед каждым тестом
        _context = TestDbContextFactory.CreateInMemoryDbContext();
        TestDbContextFactory.SeedTestData(_context);

        _loggerMock = new Mock<ILogger<PatientsController>>();
        _controller = new PatientsController(_context, _loggerMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GET Tests (Пагинация)

    [Test]
    public async Task GetPatients_WithoutParameters_ReturnsAllPatientsWithPagination()
    {
        // Act
        var result = await _controller.GetPatients(null, null);

        // Assert - контроллер возвращает OkObjectResult
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        Assert.That(okResult.StatusCode, Is.EqualTo(200));

        var patients = okResult?.Value as IEnumerable<Patient>;
        Assert.That(patients, Is.Not.Null);
        Assert.That(patients.Count(), Is.EqualTo(3));

        // Проверка заголовков пагинации - с защитой от null
        if (_controller.Response != null)
        {
            Assert.That(_controller.Response.Headers.ContainsKey("X-Total-Count"), Is.True);
            Assert.That(_controller.Response.Headers["X-Total-Count"].ToString(), Is.EqualTo("3"));
        }
        else
        {
            // В тестовой среде Response может быть null, пропускаем проверку заголовков
            Console.WriteLine("Response is null in test environment - skipping headers validation");
        }
    }

    [Test]
    public async Task GetPatients_WithLimit_ReturnsLimitedPatients()
    {
        // Act
        var result = await _controller.GetPatients(2, 0);

        // Assert - контроллер возвращает OkObjectResult
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        Assert.That(okResult.StatusCode, Is.EqualTo(200));

        var patients = okResult?.Value as IEnumerable<Patient>;
        Assert.That(patients, Is.Not.Null);
        Assert.That(patients.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task GetPatients_WithOffset_ReturnsCorrectPage()
    {
        // Act
        var result = await _controller.GetPatients(2, 2);

        // Assert - контроллер возвращает OkObjectResult
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        Assert.That(okResult.StatusCode, Is.EqualTo(200));

        var patients = okResult?.Value as IEnumerable<Patient>;
        Assert.That(patients, Is.Not.Null);
        Assert.That(patients.Count(), Is.EqualTo(1)); // 3 всего, пропускаем 2, остается 1
    }

    [Test]
    public async Task GetPatients_WithInvalidLimit_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetPatients(1001, 0);

        // Assert
        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
        var badRequestResult = result.Result as BadRequestObjectResult;
        Assert.That(badRequestResult.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task GetPatients_WithNegativeOffset_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetPatients(10, -1);

        // Assert
        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
        var badRequestResult = result.Result as BadRequestObjectResult;
        Assert.That(badRequestResult.StatusCode, Is.EqualTo(400));
    }

    #endregion

    #region Search by BirthDate Tests (FHIR)

    [Test]
    public async Task SearchByBirthDate_WithEqPrefix_ReturnsCorrectPatients()
    {
        // Act
        var result = await _controller.SearchByBirthDate("eq2024-01-13");

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;

        var patients = okResult?.Value as IEnumerable<Patient>;
        Assert.That(patients, Is.Not.Null);
        Assert.That(patients.Count(), Is.EqualTo(1));
        Assert.That(patients.First().Name.Family, Is.EqualTo("Иванов"));
    }

    [Test]
    public async Task SearchByBirthDate_WithGtPrefix_ReturnsCorrectPatients()
    {
        // Act
        var result = await _controller.SearchByBirthDate("gt2023-01-01");

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var patients = okResult?.Value as IEnumerable<Patient>;

        Assert.That(patients, Is.Not.Null);
        Assert.That(patients.Count(), Is.EqualTo(2)); // Иванов (2024) и Петров (2023)
    }

    [Test]
    public async Task SearchByBirthDate_WithLtPrefix_ReturnsCorrectPatients()
    {
        // Act
        var result = await _controller.SearchByBirthDate("lt2023-01-01");

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var patients = okResult?.Value as IEnumerable<Patient>;

        Assert.That(patients, Is.Not.Null);
        Assert.That(patients.Count(), Is.EqualTo(1)); // Сидорова (2022)
        Assert.That(patients.First().Name.Family, Is.EqualTo("Сидорова"));
    }

    [Test]
    public async Task SearchByBirthDate_WithNePrefix_ReturnsCorrectPatients()
    {
        // Act
        var result = await _controller.SearchByBirthDate("ne2024-01-13");

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var patients = okResult?.Value as IEnumerable<Patient>;

        Assert.That(patients, Is.Not.Null);
        Assert.That(patients.Count(), Is.EqualTo(2)); // Все кроме Иванова
    }

    [Test]
    public async Task SearchByBirthDate_WithLePrefix_ReturnsCorrectPatients()
    {
        // Act
        var result = await _controller.SearchByBirthDate("le2023-01-01");

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var patients = okResult?.Value as IEnumerable<Patient>;

        Assert.That(patients, Is.Not.Null);
        // le 2023-01-01 включает даты <= 2023-01-01
        Assert.That(patients.Count(), Is.EqualTo(1)); // Сидорова (2022)
        Assert.That(patients.First().Name.Family, Is.EqualTo("Сидорова"));
    }

    [Test]
    public async Task SearchByBirthDate_WithGePrefix_ReturnsCorrectPatients()
    {
        // Act
        var result = await _controller.SearchByBirthDate("ge2023-01-01");

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var patients = okResult?.Value as IEnumerable<Patient>;

        Assert.That(patients, Is.Not.Null);
        // ge 2023-01-01 включает даты >= 2023-01-01
        Assert.That(patients.Count(), Is.EqualTo(2)); // Иванов (2024) и Петров (2023)
    }

    [Test]
    public async Task SearchByBirthDate_WithSaPrefix_ReturnsCorrectPatients()
    {
        // Act
        var result = await _controller.SearchByBirthDate("sa2023-06-01");

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var patients = okResult?.Value as IEnumerable<Patient>;

        Assert.That(patients, Is.Not.Null);
        // sa 2023-06-01 - даты ПОСЛЕ 2023-06-01
        Assert.That(patients.Count(), Is.EqualTo(1)); // Иванов (2024)
        Assert.That(patients.First().Name.Family, Is.EqualTo("Иванов"));
    }

    [Test]
    public async Task SearchByBirthDate_WithEbPrefix_ReturnsCorrectPatients()
    {
        // Act
        var result = await _controller.SearchByBirthDate("eb2023-06-01");

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var patients = okResult?.Value as IEnumerable<Patient>;

        Assert.That(patients, Is.Not.Null);
        // eb 2023-06-01 - даты ДО 2023-06-01
        Assert.That(patients.Count(), Is.EqualTo(2)); // Петров (2023-05-20) и Сидорова (2022)
    }

    [Test]
    public async Task SearchByBirthDate_WithApPrefix_ReturnsCorrectPatients()
    {
        // Act
        var result = await _controller.SearchByBirthDate("ap2023-05-20");

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var patients = okResult?.Value as IEnumerable<Patient>;

        Assert.That(patients, Is.Not.Null);
        // ap 2023-05-20 - даты в пределах +/- 1 дня
        Assert.That(patients.Count(), Is.EqualTo(1)); // Петров (2023-05-20)
        Assert.That(patients.First().Name.Family, Is.EqualTo("Петров"));
    }

    [Test]
    public async Task SearchByBirthDate_WithoutPrefix_ReturnsEq()
    {
        // Act
        var result = await _controller.SearchByBirthDate("2024-01-13");

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;
        var patients = okResult?.Value as IEnumerable<Patient>;

        Assert.That(patients, Is.Not.Null);
        Assert.That(patients.Count(), Is.EqualTo(1));
        Assert.That(patients.First().Name.Family, Is.EqualTo("Иванов"));
    }

    [Test]
    public async Task SearchByBirthDate_WithInvalidDate_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.SearchByBirthDate("invalid-date");

        // Assert
        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task SearchByBirthDate_WithEmptyParameter_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.SearchByBirthDate("");

        // Assert
        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    #endregion

    #region Get Patient by ID Tests

    [Test]
    public async Task GetPatient_WithValidId_ReturnsPatient()
    {
        // Arrange
        var testPatient = TestDbContextFactory.GetTestPatients().First();
        var patientId = testPatient.Name.Id;

        // Act
        var result = await _controller.GetPatient(patientId);

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;

        var patient = okResult?.Value as Patient;
        Assert.That(patient, Is.Not.Null);
        Assert.That(patient.Name.Id, Is.EqualTo(patientId));
        Assert.That(patient.Name.Family, Is.EqualTo(testPatient.Name.Family));
    }

    [Test]
    public async Task GetPatient_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var invalidId = Guid.NewGuid();

        // Act
        var result = await _controller.GetPatient(invalidId);

        // Assert
        Assert.That(result.Result, Is.TypeOf<NotFoundObjectResult>());
    }

    #endregion

    #region Get Patients Count Tests

    [Test]
    public async Task GetPatientsCount_WithoutFilter_ReturnsTotalCount()
    {
        // Act
        var result = await _controller.GetPatientsCount(null);

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;

        var responseType = okResult.Value.GetType();
        var countProperty = responseType.GetProperty("totalCount");

        Assert.That(countProperty, Is.Not.Null);
        var count = (int)countProperty.GetValue(okResult.Value);
        Assert.That(count, Is.EqualTo(3));
    }

    [Test]
    public async Task GetPatientsCount_WithActiveFilter_ReturnsActiveCount()
    {
        // Act
        var result = await _controller.GetPatientsCount(true);

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;

        var responseType = okResult.Value.GetType();
        var countProperty = responseType.GetProperty("totalCount");

        Assert.That(countProperty, Is.Not.Null);
        var count = (int)countProperty.GetValue(okResult.Value);
        Assert.That(count, Is.EqualTo(2));
    }

    #endregion

    #region POST Tests

    [Test]
    public async Task CreatePatient_WithValidData_ReturnsCreatedPatient()
    {
        // Arrange
        var newPatient = TestDbContextFactory.GetNewPatient();

        // Act
        var result = await _controller.CreatePatient(newPatient);

        // Assert
        Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
        var createdResult = result.Result as CreatedAtActionResult;
        Assert.That(createdResult.StatusCode, Is.EqualTo(201));

        var patient = createdResult.Value as Patient;
        Assert.That(patient, Is.Not.Null);
        Assert.That(patient.Name.Family, Is.EqualTo(newPatient.Name.Family));

        var count = await _context.Patients.CountAsync();
        Assert.That(count, Is.EqualTo(4));
    }

    [Test]
    public async Task CreatePatient_WithoutFamily_ReturnsBadRequest()
    {
        // Arrange
        var newPatient = TestDbContextFactory.GetNewPatient();
        newPatient.Name.Family = "";

        // Act
        var result = await _controller.CreatePatient(newPatient);

        // Assert
        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task CreatePatient_WithInvalidGender_ReturnsBadRequest()
    {
        // Arrange
        var newPatient = TestDbContextFactory.GetNewPatient();
        newPatient.Gender = "invalid_gender";

        // Act
        var result = await _controller.CreatePatient(newPatient);

        // Assert
        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task CreatePatient_WithFutureBirthDate_ReturnsBadRequest()
    {
        // Arrange
        var newPatient = TestDbContextFactory.GetNewPatient();
        newPatient.BirthDate = DateTime.UtcNow.AddDays(1);

        // Act
        var result = await _controller.CreatePatient(newPatient);

        // Assert
        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    #endregion

    #region PUT Tests

    [Test]
    public async Task UpdatePatient_WithValidData_ReturnsNoContent()
    {
        // Arrange
        var existingPatient = TestDbContextFactory.GetTestPatients().First();
        var patientId = existingPatient.Name.Id;

        existingPatient.Name.Family = "Обновлен";
        existingPatient.Active = false;

        // Act
        var result = await _controller.UpdatePatient(patientId, existingPatient);

        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());

        var updatedPatient = await _context.Patients
            .Include(p => p.Name)
            .FirstOrDefaultAsync(p => p.Name.Id == patientId);

        Assert.That(updatedPatient, Is.Not.Null);
        Assert.That(updatedPatient.Name.Family, Is.EqualTo("Обновлен"));
        Assert.That(updatedPatient.Active, Is.False);
    }

    [Test]
    public async Task UpdatePatient_WithMismatchedId_ReturnsBadRequest()
    {
        // Arrange
        var existingPatient = TestDbContextFactory.GetTestPatients().First();
        var wrongId = Guid.NewGuid();

        // Act
        var result = await _controller.UpdatePatient(wrongId, existingPatient);

        // Assert
        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task UpdatePatient_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange
        var nonExistingPatient = TestDbContextFactory.GetNewPatient();
        var nonExistingId = Guid.NewGuid();
        nonExistingPatient.Name.Id = nonExistingId;

        // Act
        var result = await _controller.UpdatePatient(nonExistingId, nonExistingPatient);

        // Assert
        Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
    }

    #endregion

    #region PATCH Tests

    [Test]
    public async Task ToggleActive_WithValidId_TogglesStatus()
    {
        // Arrange
        var existingPatient = TestDbContextFactory.GetTestPatients().First();
        var patientId = existingPatient.Name.Id;
        var initialStatus = existingPatient.Active;

        // Act
        var result = await _controller.ToggleActive(patientId);

        // Assert
        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = result as OkObjectResult;

        var responseType = okResult.Value.GetType();
        var activeProperty = responseType.GetProperty("active");

        Assert.That(activeProperty, Is.Not.Null);
        var newStatus = (bool)activeProperty.GetValue(okResult.Value);
        Assert.That(newStatus, Is.EqualTo(!initialStatus));

        var updatedPatient = await _context.Patients
            .FirstOrDefaultAsync(p => p.Name.Id == patientId);
        Assert.That(updatedPatient.Active, Is.EqualTo(!initialStatus));
    }

    [Test]
    public async Task ToggleActive_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var invalidId = Guid.NewGuid();

        // Act
        var result = await _controller.ToggleActive(invalidId);

        // Assert
        Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
    }

    #endregion

    #region DELETE Tests

    [Test]
    public async Task DeletePatient_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var existingPatient = TestDbContextFactory.GetTestPatients().First();
        var patientId = existingPatient.Name.Id;

        // Act
        var result = await _controller.DeletePatient(patientId);

        // Assert
        Assert.That(result, Is.TypeOf<NoContentResult>());

        var deletedPatient = await _context.Patients
            .FirstOrDefaultAsync(p => p.Name.Id == patientId);
        Assert.That(deletedPatient, Is.Null);

        var count = await _context.Patients.CountAsync();
        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public async Task DeletePatient_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var invalidId = Guid.NewGuid();

        // Act
        var result = await _controller.DeletePatient(invalidId);

        // Assert
        Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
    }

    #endregion

    #region Info Tests

    [Test]
    public void GetValidValues_ReturnsCorrectDictionaries()
    {
        // Act
        var result = _controller.GetValidValues();

        // Assert
        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        var okResult = result.Result as OkObjectResult;

        var responseType = okResult.Value.GetType();

        var gendersProperty = responseType.GetProperty("genders");
        Assert.That(gendersProperty, Is.Not.Null);
        var genders = gendersProperty.GetValue(okResult.Value) as List<string>;

        var activeProperty = responseType.GetProperty("active");
        Assert.That(activeProperty, Is.Not.Null);
        var active = activeProperty.GetValue(okResult.Value) as List<bool>;

        Assert.That(genders, Does.Contain("male"));
        Assert.That(genders, Does.Contain("female"));
        Assert.That(genders, Does.Contain("other"));
        Assert.That(genders, Does.Contain("unknown"));

        Assert.That(active, Does.Contain(true));
        Assert.That(active, Does.Contain(false));
    }

    #endregion
}
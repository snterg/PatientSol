using Microsoft.EntityFrameworkCore;
using PatientSolution.Data;
using PatientSolution.Models;

namespace PatientAPI.Tests;

public static class TestDbContextFactory
{
    public static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public static void SeedTestData(AppDbContext context)
    {
        context.Patients.RemoveRange(context.Patients);
        context.PatientInfos.RemoveRange(context.PatientInfos);
        context.SaveChanges();

        var patients = GetTestPatients();

        foreach (var patient in patients)
        {
            if (patient.Id == Guid.Empty)
                patient.Id = Guid.NewGuid();

            if (patient.Name != null && patient.Name.Id == Guid.Empty)
                patient.Name.Id = Guid.NewGuid();

            if (patient.Name != null)
                patient.PatientInfoId = patient.Name.Id;
        }

        context.Patients.AddRange(patients);
        context.SaveChanges();
    }

    public static List<Patient> GetTestPatients()
    {
        var patientInfo1 = new PatientInfo
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Use = "official",
            Family = "Иванов",
            Given = new List<string> { "Иван", "Иванович" }
        };

        var patientInfo2 = new PatientInfo
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Use = "official",
            Family = "Петров",
            Given = new List<string> { "Петр", "Петрович" }
        };

        var patientInfo3 = new PatientInfo
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Use = "official",
            Family = "Сидорова",
            Given = new List<string> { "Анна", "Сергеевна" }
        };

        var patient1 = new Patient
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Name = patientInfo1,
            PatientInfoId = patientInfo1.Id,
            Gender = "male",
            BirthDate = new DateTime(2024, 1, 13, 18, 25, 43),
            Active = true
        };

        var patient2 = new Patient
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Name = patientInfo2,
            PatientInfoId = patientInfo2.Id,
            Gender = "male",
            BirthDate = new DateTime(2023, 5, 20, 10, 30, 0),
            Active = false
        };

        var patient3 = new Patient
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            Name = patientInfo3,
            PatientInfoId = patientInfo3.Id,
            Gender = "female",
            BirthDate = new DateTime(2022, 11, 8, 15, 45, 0),
            Active = true
        };

        patientInfo1.Patient = patient1;
        patientInfo2.Patient = patient2;
        patientInfo3.Patient = patient3;

        return new List<Patient> { patient1, patient2, patient3 };
    }

    public static Patient GetNewPatient()
    {
        var patientInfo = new PatientInfo
        {
            Id = Guid.NewGuid(),
            Use = "official",
            Family = "Тестов",
            Given = new List<string> { "Тест", "Тестович" }
        };

        return new Patient
        {
            Id = Guid.NewGuid(),
            Name = patientInfo,
            PatientInfoId = patientInfo.Id,
            Gender = "male",
            BirthDate = DateTime.UtcNow.AddYears(-25),
            Active = true
        };
    }
}
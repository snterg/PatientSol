using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using PatientSolution.Controllers;
using System.Reflection;

namespace PatientAPI.Tests;

[TestFixture]
public class FhirDateParserTests
{
    private PatientsController _controller;
    private MethodInfo _parseMethod;

    [SetUp]
    public void Setup()
    {
        var context = TestDbContextFactory.CreateInMemoryDbContext();
        var loggerMock = new Mock<ILogger<PatientsController>>();
        _controller = new PatientsController(context, loggerMock.Object);

        _parseMethod = typeof(PatientsController).GetMethod("ParseFhirDateParameter",
            BindingFlags.NonPublic | BindingFlags.Instance);
    }

    private (string prefix, DateTime? date) ParseFhirDate(string param)
    {
        var result = _parseMethod.Invoke(_controller, new object[] { param });
        return ((string prefix, DateTime? date))result;
    }

    [Test]
    public void ParseFhirDate_WithEqPrefix_ReturnsCorrectPrefixAndDate()
    {
        // Act
        var (prefix, date) = ParseFhirDate("eq2024-01-13");

        // Assert
        Assert.That(prefix, Is.EqualTo("eq"));
        Assert.That(date, Is.Not.Null);
        Assert.That(date.Value.Year, Is.EqualTo(2024));
        Assert.That(date.Value.Month, Is.EqualTo(1));
        Assert.That(date.Value.Day, Is.EqualTo(13));
    }

    [Test]
    public void ParseFhirDate_WithGtPrefix_ReturnsCorrectPrefixAndDate()
    {
        // Act
        var (prefix, date) = ParseFhirDate("gt2024-01-13");

        // Assert
        Assert.That(prefix, Is.EqualTo("gt"));
        Assert.That(date, Is.Not.Null);
        Assert.That(date.Value.Year, Is.EqualTo(2024));
        Assert.That(date.Value.Month, Is.EqualTo(1));
        Assert.That(date.Value.Day, Is.EqualTo(13));
    }

    [Test]
    public void ParseFhirDate_WithLtPrefix_ReturnsCorrectPrefixAndDate()
    {
        // Act
        var (prefix, date) = ParseFhirDate("lt2024-01-13");

        // Assert
        Assert.That(prefix, Is.EqualTo("lt"));
        Assert.That(date, Is.Not.Null);
        Assert.That(date.Value.Year, Is.EqualTo(2024));
        Assert.That(date.Value.Month, Is.EqualTo(1));
        Assert.That(date.Value.Day, Is.EqualTo(13));
    }

    [Test]
    public void ParseFhirDate_WithoutPrefix_ReturnsEqPrefix()
    {
        // Act
        var (prefix, date) = ParseFhirDate("2024-01-13");

        // Assert
        Assert.That(prefix, Is.EqualTo("eq"));
        Assert.That(date, Is.Not.Null);
        Assert.That(date.Value.Year, Is.EqualTo(2024));
        Assert.That(date.Value.Month, Is.EqualTo(1));
        Assert.That(date.Value.Day, Is.EqualTo(13));
    }

    [Test]
    public void ParseFhirDate_WithInvalidDate_ReturnsNullDate()
    {
        // Act
        var (prefix, date) = ParseFhirDate("eqinvalid-date");

        // Assert
        Assert.That(prefix, Is.EqualTo(""));
        Assert.That(date, Is.Null);
    }

    [TestCase("eq2024-01-13", "eq")]
    [TestCase("ne2024-01-13", "ne")]
    [TestCase("lt2024-01-13", "lt")]
    [TestCase("gt2024-01-13", "gt")]
    [TestCase("le2024-01-13", "le")]
    [TestCase("ge2024-01-13", "ge")]
    [TestCase("sa2024-01-13", "sa")]
    [TestCase("eb2024-01-13", "eb")]
    [TestCase("ap2024-01-13", "ap")]
    public void ParseFhirDate_WithAllPrefixes_ReturnsCorrectPrefix(string input, string expectedPrefix)
    {
        // Act
        var (prefix, date) = ParseFhirDate(input);

        // Assert
        Assert.That(prefix, Is.EqualTo(expectedPrefix));
        Assert.That(date, Is.Not.Null);
    }
}
using HealthSyncy.Common.Infrastructure.Validation;

namespace HealthSyncy.Common.UnitTests;

public sealed class MongoConnectionStringAttributeTests
{
    private readonly MongoConnectionStringAttribute _attribute = new();

    [Theory]
    [InlineData("mongodb://127.0.0.1:27017")]
    [InlineData("mongodb://sync:secret@mongo:27017/activity?authSource=activity")]
    [InlineData(null)]
    public void Accepts_connection_strings_and_leaves_missing_values_to_Required(string? value) =>
        _attribute.IsValid(value).Should().BeTrue();

    [Theory]
    [InlineData("not-a-mongo-uri")]
    [InlineData("https://mongo.example.org")]
    [InlineData("mongodb://")]
    public void Rejects_anything_else(string value) => _attribute.IsValid(value).Should().BeFalse();

    [Fact]
    public void Rejects_values_that_are_not_text() => _attribute.IsValid(42).Should().BeFalse();

    [Fact]
    public void Names_the_setting_in_its_message() =>
        _attribute.FormatErrorMessage("MongoUri").Should().Be("MongoUri needs a MongoDB connection string.");
}

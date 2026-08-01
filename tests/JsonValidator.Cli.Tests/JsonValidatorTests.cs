using Validator = JsonValidator.Cli.JsonValidator;

namespace JsonValidator.Cli.Tests;

public class JsonValidatorTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("[1, true, null, \"text\"]")]
    [InlineData("{\"nested\": {\"value\": 42}}")]
    public void Validate_AcceptsValidJson(string json)
    {
        var result = Validator.Validate(json);

        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{'singleQuotes': true}")]
    [InlineData("{\"missing\": }")]
    [InlineData("[1, 2,]")]
    public void Validate_RejectsInvalidJson(string json)
    {
        var result = Validator.Validate(json);

        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Validate_CanAllowJsonExtensions()
    {
        var options = new JsonValidationOptions(
            AllowComments: true,
            AllowTrailingCommas: true);

        var result = Validator.Validate("{/* comment */ \"value\": 1,}", options);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ReportsOneBasedLocation()
    {
        var result = Validator.Validate("{\n  \"value\": nope\n}");

        Assert.False(result.IsValid);
        Assert.Equal(2, result.LineNumber);
        Assert.NotNull(result.BytePositionInLine);
    }
}

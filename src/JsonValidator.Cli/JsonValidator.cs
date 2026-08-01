using System.Text.Json;
using Json.Schema;

namespace JsonValidator.Cli;

public sealed record JsonValidationOptions(
    bool AllowComments = false,
    bool AllowTrailingCommas = false);

public sealed record JsonValidationResult(
    bool IsValid,
    string? Error = null,
    long? LineNumber = null,
    long? BytePositionInLine = null,
    IReadOnlyList<SchemaViolation>? SchemaErrors = null);

public sealed record SchemaViolation(string Path, string Message);

public static class JsonValidator
{
    public static JsonValidationResult Validate(
        string json,
        JsonValidationOptions? options = null)
    {
        options ??= new JsonValidationOptions();

        try
        {
            using var _ = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = options.AllowComments
                    ? JsonCommentHandling.Skip
                    : JsonCommentHandling.Disallow,
                AllowTrailingCommas = options.AllowTrailingCommas
            });

            return new JsonValidationResult(true);
        }
        catch (JsonException ex)
        {
            return new JsonValidationResult(
                false,
                ex.Message,
                ex.LineNumber is null ? null : ex.LineNumber + 1,
                ex.BytePositionInLine is null ? null : ex.BytePositionInLine + 1);
        }
    }

    public static JsonValidationResult ValidateAgainstSchema(
        string json,
        string schemaJson,
        JsonValidationOptions? options = null)
    {
        var syntaxResult = Validate(json, options);
        if (!syntaxResult.IsValid) return syntaxResult;

        try
        {
            var schema = JsonSchema.FromText(schemaJson);
            using var instance = JsonDocument.Parse(json);
            var evaluation = schema.Evaluate(instance.RootElement, new EvaluationOptions
            {
                OutputFormat = OutputFormat.List
            });

            if (evaluation.IsValid) return syntaxResult;

            var violations = (evaluation.Details ?? [])
                .Where(detail => detail.Errors is { Count: > 0 })
                .SelectMany(detail => detail.Errors!.Values.Select(message =>
                    new SchemaViolation(detail.InstanceLocation.ToString(), message)))
                .Distinct()
                .ToArray();

            return new JsonValidationResult(
                false,
                "JSON does not match the supplied schema.",
                SchemaErrors: violations);
        }
        catch (JsonException ex)
        {
            return new JsonValidationResult(false, $"Invalid JSON Schema: {ex.Message}");
        }
        catch (JsonSchemaException ex)
        {
            return new JsonValidationResult(false, $"Invalid JSON Schema: {ex.Message}");
        }
    }
}

using System.Text.Json;

namespace JsonValidator.Cli;

public sealed record JsonValidationOptions(
    bool AllowComments = false,
    bool AllowTrailingCommas = false);

public sealed record JsonValidationResult(
    bool IsValid,
    string? Error = null,
    long? LineNumber = null,
    long? BytePositionInLine = null);

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
}

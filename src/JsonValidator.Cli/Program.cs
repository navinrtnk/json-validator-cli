using System.Text.Json;

namespace JsonValidator.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("serve", StringComparison.OrdinalIgnoreCase))
        {
            await RunServerAsync(args[1..]);
            return ExitCodes.Success;
        }

        return await RunCliAsync(args);
    }

    private static async Task<int> RunCliAsync(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine(HelpText);
            return args.Length == 0 ? ExitCodes.UsageError : ExitCodes.Success;
        }

        var allowComments = args.Contains("--allow-comments");
        var allowTrailingCommas = args.Contains("--allow-trailing-commas");
        var quiet = args.Contains("--quiet") || args.Contains("-q");
        var jsonOutput = args.Contains("--json-output");
        string? schemaPath = null;
        var inputs = new List<string>();

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument == "--schema")
            {
                if (++index >= args.Length)
                {
                    Console.Error.WriteLine("Error: --schema requires a file path.");
                    return ExitCodes.UsageError;
                }
                schemaPath = args[index];
            }
            else if (argument is "--allow-comments" or "--allow-trailing-commas" or "--json-output" or "--quiet" or "-q")
            {
                continue;
            }
            else if (argument.StartsWith('-') && argument != "-")
            {
                Console.Error.WriteLine($"Error: unknown option '{argument}'.");
                return ExitCodes.UsageError;
            }
            else
            {
                inputs.Add(argument);
            }
        }

        if (inputs.Count == 0)
        {
            Console.Error.WriteLine("Error: provide at least one JSON file, or '-' to read stdin.");
            return ExitCodes.UsageError;
        }

        string? schemaJson = null;
        if (schemaPath is not null)
        {
            try
            {
                schemaJson = await File.ReadAllTextAsync(schemaPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"{schemaPath}: error: {ex.Message}");
                return ExitCodes.UsageError;
            }
        }

        var options = new JsonValidationOptions(allowComments, allowTrailingCommas);
        var exitCode = ExitCodes.Success;
        var output = new List<object>();

        foreach (var input in inputs)
        {
            string json;
            try
            {
                json = input == "-"
                    ? await Console.In.ReadToEndAsync()
                    : await File.ReadAllTextAsync(input);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"{input}: error: {ex.Message}");
                exitCode = ExitCodes.UsageError;
                continue;
            }

            var result = schemaJson is null
                ? JsonValidator.Validate(json, options)
                : JsonValidator.ValidateAgainstSchema(json, schemaJson, options);
            var label = input == "-" ? "stdin" : input;

            if (jsonOutput)
            {
                output.Add(new
                {
                    file = label,
                    valid = result.IsValid,
                    line = result.LineNumber,
                    bytePosition = result.BytePositionInLine,
                    message = result.Error,
                    schemaErrors = result.SchemaErrors
                });
            }

            if (result.IsValid)
            {
                if (!quiet && !jsonOutput) Console.WriteLine($"{label}: valid");
            }
            else
            {
                if (!jsonOutput)
                {
                    Console.Error.WriteLine($"{label}:{result.LineNumber}:{result.BytePositionInLine}: {result.Error}");
                    foreach (var violation in result.SchemaErrors ?? [])
                        Console.Error.WriteLine($"  {violation.Path}: {violation.Message}");
                }
                if (exitCode != ExitCodes.UsageError) exitCode = ExitCodes.InvalidJson;
            }
        }

        if (jsonOutput)
        {
            var value = output.Count == 1 ? output[0] : output;
            Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            }));
        }

        return exitCode;
    }

    private static async Task RunServerAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        app.MapGet("/", () => Results.Ok(new
        {
            name = "JSON Validator API",
            endpoint = "POST /validate",
            contentType = "application/json or text/plain"
        }));

        app.MapPost("/validate", async (HttpRequest request) =>
        {
            using var reader = new StreamReader(request.Body);
            var json = await reader.ReadToEndAsync();
            var options = new JsonValidationOptions(
                ParseBoolean(request.Query["allowComments"]),
                ParseBoolean(request.Query["allowTrailingCommas"]));
            var result = JsonValidator.Validate(json, options);

            return result.IsValid
                ? Results.Ok(result)
                : Results.Json(result, statusCode: StatusCodes.Status422UnprocessableEntity);
        });

        await app.RunAsync();
    }

    private static bool ParseBoolean(string? value) =>
        bool.TryParse(value, out var parsed) && parsed;

    private const string HelpText = """
        JSON Validator CLI

        Usage:
          json-validator <file> [file ...] [options]
          command-producing-json | json-validator - [options]
          json-validator serve [ASP.NET Core options]

        Options:
          --allow-comments          Permit // and /* */ comments
          --allow-trailing-commas   Permit a comma before ] or }
          --schema <file>           Validate against a JSON Schema
          --json-output             Emit a machine-readable JSON result
          -q, --quiet               Print only errors
          -h, --help                Show this help

        Exit codes:
          0  All JSON inputs are valid
          1  At least one input contains invalid JSON
          2  Invalid usage or an input could not be read
        """;
}

public static class ExitCodes
{
    public const int Success = 0;
    public const int InvalidJson = 1;
    public const int UsageError = 2;
}

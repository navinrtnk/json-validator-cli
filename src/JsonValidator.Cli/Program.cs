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
        var inputs = args.Where(a => !a.StartsWith('-') || a == "-").ToArray();

        if (inputs.Length == 0)
        {
            Console.Error.WriteLine("Error: provide at least one JSON file, or '-' to read stdin.");
            return ExitCodes.UsageError;
        }

        var options = new JsonValidationOptions(allowComments, allowTrailingCommas);
        var exitCode = ExitCodes.Success;

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

            var result = JsonValidator.Validate(json, options);
            var label = input == "-" ? "stdin" : input;

            if (result.IsValid)
            {
                if (!quiet) Console.WriteLine($"{label}: valid");
            }
            else
            {
                Console.Error.WriteLine($"{label}:{result.LineNumber}:{result.BytePositionInLine}: {result.Error}");
                if (exitCode != ExitCodes.UsageError) exitCode = ExitCodes.InvalidJson;
            }
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

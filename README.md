# JSON Validator CLI

A dependency-light JSON syntax validator built with C#, .NET 10, and ASP.NET Core. Use it as a command-line tool in scripts and CI, or run it as a small HTTP service.

## Build and test

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

## CLI usage

```bash
# Validate one or more files
dotnet run --project src/JsonValidator.Cli -- data.json another.json

# Validate stdin
echo '{\"works\":true}' | dotnet run --project src/JsonValidator.Cli -- -

# Permit common JSON extensions
dotnet run --project src/JsonValidator.Cli -- data.json \
  --allow-comments --allow-trailing-commas
```

The process exits with `0` when every input is valid, `1` for invalid JSON, and `2` for bad usage or an unreadable input. Add `--quiet` for CI checks that print errors only.

### Example output

Valid input is easy to spot:

```console
$ json-validator person.json
person.json: valid
```

Invalid input includes a one-based line and byte position:

```console
$ json-validator broken.json
broken.json:4:12: '}' is invalid after a value. Expected either ',', '}', or ']'.
```

### JSON Schema validation

Use `--schema` to check required fields, types, and other JSON Schema constraints in addition to JSON syntax:

```bash
dotnet run --project src/JsonValidator.Cli -- person.json --schema person.schema.json
```

For example, this schema requires a string-valued `name` property:

```json
{
  "type": "object",
  "required": ["name"],
  "properties": {
    "name": { "type": "string" }
  }
}
```

### Machine-readable output

Add `--json-output` when another program or CI pipeline needs to consume the result:

```console
$ json-validator broken.json --json-output
{
  "file": "broken.json",
  "valid": false,
  "line": 4,
  "bytePosition": 12,
  "message": "'}' is invalid after a value. Expected either ',', '}', or ']'."
}
```

When validating multiple files, the output is a JSON array. Schema failures also include a `schemaErrors` array with the instance path and constraint message.

## Continuous integration

The included GitHub Actions workflow restores, builds, and tests the solution on every push and pull request.

## HTTP API

```bash
dotnet run --project src/JsonValidator.Cli -- serve --urls http://localhost:5080

curl -i http://localhost:5080/validate \
  -H 'Content-Type: application/json' \
  --data-binary '{\"name\":\"example\"}'
```

`POST /validate` returns `200` for valid JSON and `422` with the parse error and location for invalid JSON. Optional query parameters are `allowComments=true` and `allowTrailingCommas=true`.

## Publish as a command

```bash
dotnet publish src/JsonValidator.Cli -c Release -o ./publish
./publish/JsonValidator.Cli --help
```

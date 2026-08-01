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

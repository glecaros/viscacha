# Viscacha CLI

A command-line tool for testing HTTP APIs using YAML definitions.

## Usage

```bash
dotnet tool install --global viscacha
viscacha <file.yaml> [--defaults <defaults.yaml>] [--var name=value ...]
```

- `<file.yaml>`: YAML file with requests or a document (see below)
- `--defaults <defaults.yaml>`: Optional defaults file to merge
- `--var name=value`: Set variables for substitution in the YAML

## YAML Example
```yaml
defaults:
  base-url: https://api.example.com
  headers:
    Accept: application/json
requests:
  - method: GET
    path: /users
  - method: POST
    path: /users
    body: '{"name": "New User"}'
```

## Variable Resolution
You can use `${var}`, `${env:ENV_VAR}`, or `${file:<file>:<format>}` in your YAML to substitute variables or environment variables.

### File resolution
Using the `${file:<file>:<format>}` syntax in the YAML allows iterpolating file contents in the template. Currently only `base64` is supported as format.

## Output
Responses are printed as JSON to stdout.

## Schemas
See [Viscacha core schema](./schema/requests.tsp).

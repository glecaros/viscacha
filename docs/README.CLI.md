# Viscacha CLI

A unified command-line tool for testing HTTP APIs using YAML definitions. Provides both request execution and test suite functionality through subcommands.

## Installation

```bash
dotnet tool install --global Viscacha.CLI
```

## Commands

### Request Command
Execute API requests defined in YAML files.

```bash
viscacha request <file.yaml> [--defaults <defaults.yaml>] [--var name=value ...]
```

- `<file.yaml>`: YAML file with requests or a document (see below)
- `--defaults <defaults.yaml>`: Optional defaults file to merge
- `--var name=value`: Set variables for substitution in the YAML

### Test Command  
Run test suites with multiple configurations and validations.

```bash
viscacha test --input-file <suite.yaml> [--responses-directory <dir>] [--list-tests] [--report-trx] [--report-trx-filename <file>]
```

- `--input-file <suite.yaml>`: Path to the suite YAML file (required)
- `--responses-directory <dir>`: Directory to save test responses (optional)
- `--list-tests`: List available tests without running them
- `--report-trx`: Enable generating TRX report
- `--report-trx-filename <file>`: Specify filename for the TRX report

## Request YAML Example
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

## Test Suite YAML Example
```yaml
variables:
  api_key: secret
configurations:
  - name: default
    path: config.yaml
tests:
  - name: get-users
    request-file: get-users.yaml
    configurations: [default]
    validations:
      - type: status
        status: 200
```

## Variable Resolution
You can use `${var}`, `${env:ENV_VAR}`, or `${file:<file>:<format>}` in your YAML to substitute variables or environment variables.

### Variable Scope and Precedence (Test Command)
Variables are resolved with the following precedence (highest to lowest):
1. **Configuration variables** (defined in configuration files)
2. **Test-level variables** (defined in individual tests)
3. **Suite-level variables** (defined at the top of the suite file)
4. **Environment variables** (accessed via `${env:VAR_NAME}`)

**Important**: Configuration files are parsed independently and only have access to:
- Environment variables via `${env:VAR_NAME}`
- Variables defined within the configuration itself

Suite-level variables are **not** available in configuration files. Use environment variables for values that need to be shared between suite files and configurations.

### File resolution
Using the `${file:<file>:<format>}` syntax in the YAML allows iterpolating file contents in the template. Currently only `base64` is supported as format.

## Output
- **Request command**: Responses are printed as JSON to stdout
- **Test command**: Test results with pass/fail status and detailed validation results

## Examples

### Basic Request Execution
```bash
viscacha request api-requests.yaml --defaults prod-config.yaml
```

### Running Test Suite
```bash
viscacha test --input-file test-suite.yaml --responses-directory ./responses
```

### Listing Tests
```bash
viscacha test --input-file test-suite.yaml --list-tests
```

## Schemas
See [Viscacha core schema](./schema/requests.tsp) and [TestRunner schema](./schema/suite.tsp).

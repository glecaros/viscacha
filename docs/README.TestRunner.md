# Viscacha TestRunner

A tool to run suites of HTTP API tests described in YAML files.

## Usage

```sh
dotnet tool install --global Viscacha.TestRunner
viscacha-test --input-file <suite.yaml> [--responses-directory <dir>]
```

- `--input-file <suite.yaml>`: Path to the suite YAML file (required)
- `--responses-directory <dir>`: Directory to save test responses (optional)


## Variable Resolution
You can use `${var}`, `${env:ENV_VAR}`, or `${file:<file>:<format>}` in your YAML to substitute variables or environment variables.

### Variable Scope and Precedence
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

## Suite YAML Example
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

## Test YAML Example
```yaml
method: GET
path: /users
headers:
  Authorization: Bearer ${api_key}
```

## Schemas
See [TestRunner schema](./schema/suite.tsp) and [Core schema](./schema/requests.tsp).

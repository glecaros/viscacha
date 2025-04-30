# Viscacha TestRunner

A tool to run suites of HTTP API tests described in YAML files.

## Usage

```sh
dotnet tool install --global viscacha-test
viscacha-test --input-file <suite.yaml> [--responses-directory <dir>]
```

- `--input-file <suite.yaml>`: Path to the suite YAML file (required)
- `--responses-directory <dir>`: Directory to save test responses (optional)


## Variable Resolution
You can use `${var}`, `${env:ENV_VAR}`, or `${file:<file>:<format>}` in your YAML to substitute variables or environment variables.

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

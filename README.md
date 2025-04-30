# Viscacha

Viscacha is a toolkit for testing HTTP APIs using YAML definitions. It provides:
- A CLI for running API requests from YAML files
- A TestRunner for running suites of tests with multiple configurations

## Packages
- [CLI Tool](./docs/README.CLI.md)
- [Test Runner](./docs/README.TestRunner.md)

## YAML Structure

### Document Example (for CLI)
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

### Suite Example (for TestRunner)
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

## Schemas
See [Viscacha core schema](./docs/schema/requests.tsp) and [TestRunner schema](./docs/schema/suite.tsp).

## License
See [LICENSE](./LICENSE).

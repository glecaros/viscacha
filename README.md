# Viscacha CLI

A simple command-line tool for quickly testing REST API endpoints defined in YAML files. It supports variable substitution, authentication, and response handling, making it easy to automate and test API interactions.

## Features

- Execute HTTP requests defined in YAML files.
- Supports multiple HTTP methods (GET, POST, PUT, PATCH, DELETE, etc.).
- Variable substitution from environment variables, command-line arguments, and previous responses.
- Supports authentication methods:
  - API Key Authentication
  - Azure Credentials Authentication
- Easy-to-read JSON formatted responses.

## Usage

```bash
viscacha <yaml-file> [--var name=value ...]
```

### Example

```bash
viscacha requests.yaml --var userId=123 --var token=abcdef
```

### YAML File Structure

You can define requests in YAML files as follows:

```yaml
defaults:
  baseUrl: https://api.example.com
  headers:
    Accept: application/json
  authentication:
    type: api-key
    key: ${API_KEY}

requests:
  - method: GET
    path: /users/{{userId}}

  - method: POST
    path: /users
    contentType: application/json
    body: |
      {
        "name": "John Doe",
        "email": "john@example.com"
      }
```

### Variable Substitution

- Environment variables: `${env:VARIABLE_NAME}`
- Command-line variables: `${variableName}`
- Response variables: `#{r0.propertyName}` (where `r0` is the response from the first request)

### Authentication Examples

#### API Key Authentication

```yaml
authentication:
  type: api-key
  key: your-api-key
  header: X-Api-Key
  prefix: Bearer
```

#### Azure Credentials Authentication

```yaml
authentication:
  type: azure-credentials
  scopes:
    - https://management.azure.com/.default
```

### Importing Defaults

You can import default settings from another YAML file using the `import` property within the `defaults` section. This allows you to reuse common configurations across multiple YAML files.

Example:
```yaml
defaults:
  import: common-defaults.yaml
```

Additionally, defaults can be imported from the command line:

```bash
viscacha <request-file> --defaults <defaults-file>
```

If defaults are provided using multiple mechanisms, the order of precedence is as follows: first, those provided via the command line, then those embedded in the file, and finally those imported in the file.

## License

This project is licensed under the MIT License.

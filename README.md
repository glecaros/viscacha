# Viscacha

Viscacha is a toolkit for testing HTTP APIs using YAML definitions. It provides:
- A CLI for running API requests from YAML files
- A TestRunner for running suites of tests with multiple configurations

## Packages
- [CLI Tool](./docs/README.CLI.md) [![NuGet Version](https://img.shields.io/nuget/v/Viscacha.CLI)](https://www.nuget.org/packages/Viscacha.CLI)
- [Test Runner](./docs/README.TestRunner.md) [![NuGet Version](https://img.shields.io/nuget/v/Viscacha.TestRunner)](https://www.nuget.org/packages/Viscacha.TestRunner)

## Quick Start Guide

### Multi-Service API Testing Example

Here's a practical example for testing chat completions across Azure OpenAI and OpenAI services:

**1. Create your test suite (`chat-completions-suite.yaml`)**:
```yaml
configurations:
  - name: azure
    path: ./chat-completions/azure-defaults.yaml
  - name: openai
    path: ./chat-completions/openai-defaults.yaml
tests:
  - name: Basic Text AOAI
    request-file: ./chat-completions/basic-text.yaml
    configurations:
      - azure
    variables:
      model: ${env:MODEL_AZURE}
    validations:
      - type: status
        status: 200
  - name: Basic Text OpenAI
    request-file: ./chat-completions/basic-text.yaml
    configurations:
      - openai
    variables:
      model: ${env:MODEL_OPENAI}
    validations:
      - type: status
        status: 200
```

**2. Create service-specific configurations**:

`chat-completions/azure-defaults.yaml`:
```yaml
base-url: ${env:AZURE_OPENAI_ENDPOINT}/openai/v1/chat/completions
authentication:
  type: api-key
  key: ${env:AZURE_API_KEY}
  header: Authorization
  prefix: Bearer
query:
  api-version: 2024-02-15-preview
```

`chat-completions/openai-defaults.yaml`:
```yaml
base-url: https://api.openai.com/v1/chat/completions
authentication:
  type: api-key
  key: ${env:OPENAI_API_KEY}
  header: Authorization
  prefix: Bearer
```

**3. Create reusable request templates (`chat-completions/basic-text.yaml`)**:
```yaml
requests:
  - method: POST
    content-type: application/json
    body: |
      {
        "model": "${model}",
        "messages": [
          {
            "role": "user",
            "content": "Hello!"
          }
        ]
      }
```

**4. Set up environment variables**:
```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com"
export AZURE_API_KEY="your-azure-key"
export OPENAI_API_KEY="your-openai-key"
export MODEL_AZURE="gpt-4"
export MODEL_OPENAI="o1-mini"
```

**5. Run your tests**:
```bash
viscacha-test --input-file chat-completions-suite.yaml
```

### Key Concepts

- **Reusable Templates**: Write request files once, use across multiple services with different configurations

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

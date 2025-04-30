import "@typespec/json-schema";

using JsonSchema;

@jsonSchema
@id("https://github.com/glecaros/viscacha/docs/schema/suite")
namespace Schemas;

alias Authentication = {
      type: "api-key",
      header?: string,
      prefix?: string,
    } | {
      type: "azure-credentials",
      scopes: string[],
    };

alias ConfigurationReference = {
  name: string,
  path: string,
};

alias Validation =  {
  type: "status",
  status: integer,
} | {
  type: "path-comparison",
  @uniqueItems
  `ignore-paths`: string[],
} | {
  type: "field-format",
  path: string,
  format: "json",
};

alias Test = {
  name: string,
  variables?: Record<string>,
  `request-file`: string,
  configurations: string[],
  validations: Validation[],
  skip?: boolean,
};

model Suite {
  variables?: Record<string>,
  configurations: ConfigurationReference[],
  tests: Test[],
}

import "@typespec/json-schema";

using JsonSchema;

@jsonSchema
@id("https://github.com/glecaros/viscacha/docs/schema/requests")
namespace Schemas;

alias Defaults = {
    `import`?: string,
    baseUrl?: string,
    @oneOf
    authentication?: Authentication,
    headers?: Record<string>,
    query?: Record<string>,
};

alias Authentication = {
    type: "api-key",
    header?: string,
    prefix?: string,
} | {
    type: "azure-credentials",
    scopes: string[],
};

model Requests {
    defaults?: Defaults,
    requests: {
        method: "GET" | "POST" | "PUT" | "DELETE" | "OPTIONS" | "HEAD",
        url?: string,
        path?: string,
        authentication?: Authentication,
        headers?: Record<string>,
        query?: Record<string>,
        `content-type`?: string,
        body?: string,
    }[],
}

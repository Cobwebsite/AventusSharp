# AventusSharp

AventusSharp is a versatile C# package designed to streamline the development of modern applications. By providing robust tools for data modeling, business logic management, API routing, and frontend integration, AventusSharp empowers developers to focus on building features rather than handling repetitive tasks.

Whether you're working on a small project or a large-scale application, AventusSharp simplifies complex processes, ensuring consistency, efficiency, and flexibility across your application stack.

## Install

You can install this package with NuGet :

```shell
dotnet tool install --global AventusSharp.Converter
```

You can update this package with NuGet :

```shell
dotnet tool update --global AventusSharp.Converter
```

## Documentation

### SSE generation

The converter generates `SSEEndPoint` subclasses as AventusJs endpoints and
`SSEEvent<T>` / `SSEEmptyEvent` subclasses as typed client events. Nested payload
DTOs are exported with distinct names (for example, `ChangedEventBody`) and keep
their C# `Fullname` for deserialization. Endpoint attributes, inherited payloads,
generic payloads, collection payloads and `[Export]` / `[NoExport]` are supported.

Constant `GetTopic()` return values become client paths, without constructing
server events. For a nonconstant topic, the generated constructor requires a
`getTopic: () => string` callback. Events call `init()` after construction.
An explicit `[EndPoint]` selects the endpoint; otherwise, generation selects the
main endpoint, the only endpoint, or the default `/sse` client endpoint.

Converter configuration:

```json
{
  "exportSseEndPointByDefault": true,
  "exportSseEventByDefault": true,
  "sseEndpoint": {
    "host": "localhost",
    "port": 8080,
    "useHttps": false,
    "withCredentials": true,
    "listenOnBoot": false,
    "parent": "AventusSharp.SSE.EndPoint"
  }
}
```

The `replacer.sseEndPoint` and `replacer.sseEvent` sections use the same `type`
and `result` format as the WebSocket replacements. Top-level payload classes
use the ordinary export rules; mark them `[Export]` when needed.

The documentation is available here [https://aventussharp.com/](https://aventussharp.com/).

## Contributor

Your support plays a vital role in our ability to enhance Aventus, expand its capabilities, and empower developers like you to create exceptional web experiences. Together, we can invest more time and resources into making Aventus even more powerful and providing new opportunities for programming professionals.

You can also give us financial support via [github donations](https://github.com/sponsors/max529).

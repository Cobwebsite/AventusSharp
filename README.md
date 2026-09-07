# AventusSharp

AventusSharp provides data modeling, business logic, routing and frontend generation for ASP.NET Core and .NET MAUI applications. The packages are split so an application only references its host integration and the database providers it uses.

## Installation

Choose one host integration:

```shell
dotnet add package AventusSharp.AspNetCore
# or
dotnet add package AventusSharp.Maui
```

Then add one or more database providers:

```shell
dotnet add package AventusSharp.Data.Sqlite
dotnet add package AventusSharp.Data.Mysql
dotnet add package AventusSharp.Data.Postgresql
dotnet add package AventusSharp.Data.Mssql
```

`AventusSharp.Core` is brought transitively by the host and provider packages. It can also be referenced directly for host-independent code.

Each project contains an `aventus.sharp.avt` configuration. The generated Aventus files are centralized under `AventusJs/src/generated`.

### .NET MAUI startup

Register the bridge before building the application, then initialize the data managers and portable router:

```csharp
var builder = MauiApp.CreateBuilder();
builder
    .UseMauiApp<App>()
    .AddAventus();

var app = builder.Build();
app.UseAventusData();
app.UseAventusHttp();

return app;
```

`AventusMauiBridge` can then be resolved from `app.Services` and used by a WebView bridge to execute Aventus routes in-process.

## Translations

Library errors and validation messages include English (default) and French translations.
Applications can use the same system for their own messages, without depending on ASP.NET Core.

### ASP.NET Core

Call this before the HTTP, WebSocket and SSE middleware:

```csharp
using AventusSharp;
using AventusSharp.Localization;

app.UseAventusTranslations(options =>
{
    options.DefaultCulture = "fr";
    options.Add("fr", AventusMessageKeys.Validation.Unique, "Cette valeur est déjà utilisée.");
    options.Add("fr", "App.Welcome", "Bonjour {0} !");
    options.Add("en", "App.Welcome", "Hello {0}!");
});
app.UseAventusHttp();
```

The middleware uses ASP.NET Core request localization (query string, culture cookie,
then `Accept-Language`) with English and French enabled by default. `options.Add`
also registers its language; use `options.SupportedCultures.Add("de")` when translations
come from a provider or resources. The default language handles unsupported requests.
Each application's configuration and each request's language are isolated.
If request localization is already configured, place it first and pass
`useRequestLocalization: false`. The optional `configureRequests` callback lets you
customize ASP.NET Core's language selection.

### MAUI, console and workers

For MAUI, call `app.UseAventusTranslations(options => options.DefaultCulture = "fr")`
after building the application and before initializing Aventus features.
For other hosts, configure once at startup:

```csharp
AventusTranslations.Configure(options =>
{
    options.DefaultCulture = "fr";
    options.Add("fr", "App.Welcome", "Bonjour {0} !");
    options.Add("en", "App.Welcome", "Hello {0}!");
});

string welcome = AventusTranslations.Get("App.Welcome", "Alice");
using (AventusTranslations.UseCulture("en"))
{
    string english = AventusTranslations.Get("App.Welcome", "Alice");
}
```

Culture scopes follow asynchronous execution and restore the previous language when
disposed. Outside a scope, the configured default language is used. They do not change
the process's .NET culture. ASP.NET Core configuration applies to requests; use
`Configure` separately for startup work or background jobs, or create an independent
`AventusLocalizer` and use `AventusTranslations.Use(localizer, culture)` for a job.

### Application resources and custom providers

Library keys are generated from `Messages.resx` at build time and exposed as constants,
for example `AventusMessageKeys.Validation.Unique` or `AventusMessageKeys.Data.ManagerNotFound`.
They provide completion and compile-time checking of key names while remaining compatible
with string-based providers and configuration. Do not edit the generated files under `obj`.

To generate the same constants for your application, add this to its `.csproj` (the
generation target is included transitively in the AventusSharp.Core NuGet package):

```xml
<ItemGroup>
  <AventusMessageCatalog Include="Resources/Messages.resx"
                         Namespace="MyApplication"
                         ClassName="AppMessageKeys" />
</ItemGroup>
```

Use the neutral catalog only. Keys must be unique, dot-separated C# identifiers:
`App.Welcome` becomes `AppMessageKeys.App.Welcome`. A key cannot also name a group,
and members cannot have the same name as their containing class. Invalid keys fail
the build with a diagnostic. Generation works with `dotnet build` on all supported
.NET SDK platforms, without PowerShell or an additional executable.

```csharp
options.Add("fr", AppMessageKeys.App.Welcome, "Bonjour {0} !");
string welcome = AventusTranslations.Get(AppMessageKeys.App.Welcome, "Alice");
```

Generating keys does not register translations: use `Add` or `AddResources` below.
With a source `ProjectReference` instead of NuGet, explicitly import
`AventusSharp.Core/buildTransitive/AventusSharp.Core.targets` from your checkout.

For larger catalogs, register your application's `.resx` resource manager:

```csharp
options.AddResources(new System.Resources.ResourceManager(
    "MyApplication.Resources.Messages", typeof(Program).Assembly));
```

Alternatively assign `options.Provider` to an `IAventusMessageProvider` implementation.
Its `GetTemplate(string key, CultureInfo culture)` returns a template or `null` to
continue the fallback search. Providers may read JSON, a database, or other resources;
they must support concurrent reads. Settings are copied when configuration completes.

For each culture, lookup checks `Add` overrides, the provider, then registered resource
managers. It searches the requested culture and parents, then the configured default
and parents, then English, before the built-in catalog. Thus application overrides
also take precedence over library translations in other languages. Unknown keys are
returned unchanged. Templates use .NET composite formatting (`{0}`, `{1:N2}`, `{{` for
a literal brace); invalid templates or mismatched arguments raise `FormatException`.

Built-in keys and their argument positions are listed in
[Messages.resx](AventusSharp.Core/Localization/Messages.resx), with French in
[Messages.fr.resx](AventusSharp.Core/Localization/Messages.fr.resx). .NET builds the
French satellite assembly automatically and includes it with the package.

Messages are resolved when the error or validation result is created. Error codes,
JSON fields and explicitly supplied custom messages are preserved. Exceptions from
.NET/database drivers and technical logging are not automatically translated.

## Documentation

The documentation is available here [https://sharp.aventusjs.com](https://sharp.aventusjs.com).

Run the complete local test suite with:

```shell
dotnet test AventusSharpTest/AventusSharpTest.csproj
```

## Publication

All NuGet packages share the same version and are built before publication:

```shell
npm run release -- 1.2.3
```

The script updates every package project, builds the complete solution, creates the packages under `artifacts/packages`, then calls the custom `dotnet-publish` command for each package. To validate locally without publishing:

```shell
npm run release -- 1.2.3 --skip-publish
npm run release -- 1.2.3 --dry-run
```

## Contributor

Your support plays a vital role in our ability to enhance Aventus, expand its capabilities, and empower developers like you to create exceptional web experiences. Together, we can invest more time and resources into making Aventus even more powerful and providing new opportunities for programming professionals.

You can also give us financial support via [github donations](https://github.com/sponsors/max529).

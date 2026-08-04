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

## Documentation

The documentation is available here [https://sharp.aventusjs.com](https://sharp.aventusjs.com).

Developer references in this repository:

- [AI usage guide](docs/AVENTUSSHARP_AI_GUIDE.md)
- [Testing strategy](docs/TESTING.md)

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

# Architecture des packages AventusSharp

Une application sélectionne explicitement un point d'entrée et zéro, un ou plusieurs
providers de données.

## Points d'entrée

- `AventusSharp.AspNetCore` : HTTP, WebSocket, SSE et adaptation de `HttpContext`.
- `AventusSharp.Maui` : bridge local MAUI utilisant le contexte portable.

Les deux référencent `AventusSharp.Core`, qui contient les contrats, le moteur Data,
le routeur et les services partagés. Ils ne sélectionnent aucun moteur de base de
données.

## Providers

- `AventusSharp.Data.Sqlite`
- `AventusSharp.Data.Mysql`
- `AventusSharp.Data.Postgresql`
- `AventusSharp.Data.Mssql`

Chaque provider possède physiquement ses sources, référence `AventusSharp.Core` et
ne référence aucun autre provider. Plusieurs providers peuvent être installés dans
la même application.

## Exemples

Application MAUI avec stockage local :

```xml
<PackageReference Include="AventusSharp.Maui" Version="..." />
<PackageReference Include="AventusSharp.Data.Sqlite" Version="..." />
```

Serveur ASP.NET Core avec PostgreSQL et SQL Server :

```xml
<PackageReference Include="AventusSharp.AspNetCore" Version="..." />
<PackageReference Include="AventusSharp.Data.Postgresql" Version="..." />
<PackageReference Include="AventusSharp.Data.Mssql" Version="..." />
```

Le package historique `AventusSharp` n'est plus construit : cette refonte ne fournit
pas de façade de compatibilité implicite.

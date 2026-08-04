# B010 — Supprimer ASP.NET Core de Core

## But

Rendre le graphe portable vers MAUI/Android.

## Travail

- Retirer les derniers `using Microsoft.AspNetCore.*` de Core.
- Retirer `Microsoft.AspNetCore.App` de Core.
- Ajouter un test d'architecture interdisant cette dépendance.
- Compiler un projet témoin Android référençant Core.

## Acceptation

- Core compile sans framework reference ASP.NET Core.
- Le projet témoin MAUI Android restaure et compile.

## Avancement

- `AventusSharp.Core` ne contient aucune référence ou directive ASP.NET Core.
- `AventusSharp.Maui` ne contient aucune référence ou directive ASP.NET Core.
- Les usages restants dans le projet historique sont limités à `ApplicationBuilder`, au middleware HTTP et aux hôtes WebSocket/SSE.
- Un projet témoin `net10.0-android` référence Core, Data, Data.Sqlite et MAUI afin de verrouiller leur compatibilité Android.

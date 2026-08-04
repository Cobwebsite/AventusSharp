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


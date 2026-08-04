# Backlog — découpage portable d'AventusSharp

Objectif : rendre la logique AventusSharp utilisable depuis .NET MAUI/Android sans changer le fonctionnement du routeur. Une route doit continuer à produire un élément de `AventusSharp.Routes.Response`.

## Ordre d'exécution

1. [B001 — Établir la référence de tests](B001-reference-tests.md)
2. [B002 — Créer le squelette des bibliothèques](B002-library-skeleton.md)
3. [B003 — Cartographier les dépendances](B003-dependency-map.md)
4. [B004 — Déplacer le code déjà portable](B004-move-portable-code.md)
5. [B005 — Isoler l'hébergement ASP.NET Core](B005-isolate-aspnetcore.md)
6. [B006 — Verrouiller le contrat des réponses](B006-response-contract-tests.md)
7. [B007 — Introduire les contrats de contexte](B007-context-contracts.md)
8. [B008 — Adapter ASP.NET Core](B008-aspnetcore-adapter.md)
9. [B009 — Migrer le routeur sans changer sa logique](B009-router-context-migration.md)
10. [B010 — Supprimer ASP.NET Core de Core](B010-remove-aspnetcore-from-core.md)
11. [B011 — Créer le bridge MAUI](B011-maui-bridge.md)
12. [B012 — Découper les fournisseurs optionnels](B012-optional-providers.md)

## Règles de migration

- Une seule étape structurelle ou fonctionnelle par lot.
- Exécuter les tests après chaque déplacement.
- Ne pas modifier simultanément une API et son comportement.
- Conserver `AventusSharp.Routes.Response.IResponse` comme sortie du routeur.
- Conserver temporairement le package `AventusSharp` comme façade de compatibilité.
- `AventusSharp.Core` ne devra finalement référencer aucun namespace `Microsoft.AspNetCore.*`.


# B003 — Cartographier les dépendances

## But

Décider objectivement de l'assembly de destination de chaque dossier.

## Travail

- Classer les namespaces en `Core`, `AspNetCore`, `Maui` ou fournisseur optionnel.
- Recenser les usages de `HttpContext`, `IApplicationBuilder`, WebSocket et SSE.
- Recenser les dépendances NuGet par fonctionnalité.
- Identifier les cycles qui empêchent un déplacement mécanique.

## Acceptation

- Chaque dossier possède une destination proposée.
- Les cycles et changements d'API nécessaires sont documentés.


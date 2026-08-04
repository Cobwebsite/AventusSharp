# B005 — Isoler l'hébergement ASP.NET Core

## But

Regrouper le code propre à l'hôte serveur sans changer son comportement.

## Travail

- Isoler `ApplicationBuilder`, les middlewares HTTP, WebSocket et SSE.
- Isoler l'écriture concrète dans `HttpResponse` et le cycle de vie serveur.
- Déplacer ce code vers `AventusSharp.AspNetCore` lorsque ses dépendances le permettent.

## Acceptation

- Le serveur expose les mêmes routes.
- HTTP, WebSocket et SSE conservent leurs tests existants.


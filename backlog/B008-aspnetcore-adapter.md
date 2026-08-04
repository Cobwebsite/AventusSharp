# B008 — Adapter ASP.NET Core

## But

Projeter un véritable `HttpContext` sur les contrats portables.

## Travail

- Implémenter les adaptateurs de contexte, requête et réponse ASP.NET Core.
- Déléguer les lectures et écritures au `HttpContext` natif.
- Tester headers, query, body, statut, services, utilisateur et annulation.

## Acceptation

- Les tests prouvent l'équivalence avec le comportement serveur actuel.
- Aucune copie artificielle de l'état ASP.NET n'est nécessaire.


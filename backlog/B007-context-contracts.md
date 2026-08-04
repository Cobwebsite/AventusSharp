# B007 — Introduire les contrats de contexte

## But

Décrire uniquement les capacités utilisées par AventusSharp, sans reproduire tout `HttpContext`.

## Travail

- Ajouter `IAventusContext`, `IAventusRequest` et `IAventusResponse` dans Core.
- Définir les membres depuis les usages existants : services, utilisateur, annulation, items, méthode, chemin, query, headers, body, statut et flux de réponse.
- Prévoir des capacités séparées pour session, WebSocket et SSE si nécessaire.
- Fournir un contexte de test simple.

## Acceptation

- Les contrats ne référencent pas ASP.NET Core ni MAUI.
- Tous les membres ont au moins un consommateur identifié.


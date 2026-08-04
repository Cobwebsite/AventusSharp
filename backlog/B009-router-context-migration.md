# B009 — Migrer le routeur sans changer sa logique

## But

Remplacer progressivement `HttpContext` par le contrat Aventus tout en conservant la résolution et les réponses actuelles.

## Travail

- Migrer successivement scopes, attributs middleware, routeurs, body parser et réponses.
- Migrer ensuite la résolution et l'injection des paramètres.
- Conserver `IResponse` comme résultat et mécanisme d'écriture.
- Maintenir temporairement des points d'entrée ASP.NET compatibles si nécessaire.

## Acceptation

- Les mêmes routes sont sélectionnées.
- Les mêmes types `IResponse` sont produits.
- Les tests de référence restent au vert.


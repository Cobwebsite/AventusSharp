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

## Avancement

### Sous-lot réponses — terminé

- `IResponse.send` reçoit maintenant `IAventusContext`.
- Toutes les réponses concrètes écrivent via `IAventusResponse`.
- `RouterConfig.ViewDir` reçoit le contexte portable.
- Le middleware ASP.NET construit temporairement l'adaptateur avant d'écrire la réponse.
- La résolution, l'invocation des routes et les types de réponse sont inchangés.

### Sous-lot scopes Data — terminé

- `Scope<T>.Where` et `IScope.Where` reçoivent `IAventusContext`.
- Un contexte ambiant portable accompagne temporairement le `HttpContext` ambiant historique.
- HTTP, WebSocket et SSE initialisent et libèrent les deux contextes pendant la transition.
- Les constructeurs de requêtes Data appliquent les scopes avec le contexte portable.

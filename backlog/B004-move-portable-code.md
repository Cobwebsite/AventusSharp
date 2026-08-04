# B004 — Déplacer le code déjà portable

## But

Alimenter `AventusSharp.Core` uniquement avec du code qui ne dépend pas d'ASP.NET Core.

## Travail

- Déplacer par petits lots les outils, erreurs, résultats, scheduler et abstractions Data portables.
- Ajouter les références de projet nécessaires.
- Conserver namespaces et API publiques autant que possible.
- Exécuter les tests après chaque lot.

## Acceptation

- Chaque lot compile et passe les tests avant le lot suivant.
- `AventusSharp.Core` reste indépendant d'ASP.NET Core.


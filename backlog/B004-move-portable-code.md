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

## Avancement

### Lot 1 — terminé

- `ConfigSection` et `ConfigIgnore` déplacés dans Core.
- `Export` et `NoExport` déplacés dans Core.
- `FctName` déplacé dans Core.
- `NoRoute` déplacé dans Core.
- Namespaces et API publiques conservés.
- 54 tests ciblés réussis.
- Suite de référence hors échec WebSocket initial : 598 réussis, 64 ignorés, aucun échec.

### Moteur Data portable — terminé

- Assembly `AventusSharp.Data` créé avec modèles, managers, builders, migrations et stockage abstrait.
- Fournisseurs SQL Server, MySQL, PostgreSQL et SQLite exclus de cet assembly.
- Faux imports vers les packages des fournisseurs supprimés.
- Le projet Android témoin compile un véritable modèle `Storable<T>` depuis cet assembly.
- La suite SQLite historique est réutilisée contre les nouveaux assemblies portables.
- Le moteur portable et son provider SQLite sont empaquetables séparément.

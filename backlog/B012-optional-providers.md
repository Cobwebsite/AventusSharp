# B012 — Découper les fournisseurs optionnels

## But

Éviter d'embarquer dans Android les dépendances réservées aux serveurs.

## Travail

- Auditer SQL Server, MySQL, PostgreSQL, SQLite et les bibliothèques graphiques.
- Extraire les fournisseurs dans des packages dédiés.
- Garder dans Core uniquement les contrats et fonctionnalités portables.

## Acceptation

- Une application MAUI peut référencer Core et SQLite sans restaurer les drivers SQL serveur.
- Les applications serveur peuvent sélectionner leurs fournisseurs indépendamment.


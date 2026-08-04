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

## Avancement

- `AventusSharp.Core` contient le moteur commun sans fournisseur concret.
- `AventusSharp.Data.Sqlite` contient le fournisseur SQLite et référence uniquement Core.
- SQL Server, MySQL et PostgreSQL sont absents du graphe Android.
- Une suite portable ouvre une base SQLite et exécute une commande réelle.
- La suite de régression SQLite historique est également exécutée depuis le projet de tests portable.
- Le projet témoin Android compile un modèle `Storable<T>` et construit un `SqliteStorage`.
- Le graphe portable est protégé contre toute référence à ASP.NET Core.

## État

Terminé pour le besoin MAUI Android : Core, moteur Data, SQLite et bridge MAUI sont
séparés des dépendances d'hébergement et des pilotes SQL serveur.

### Découpage physique final

- Les quatre providers possèdent leurs sources dans leurs propres projets.
- Les utilitaires SQL communs sont dans `AventusSharp.Core/Data/Storage/Relational`.
- Aucun projet Data ne compile un fichier externe avec `Compile Include` et `Link`.
- `AventusSharp.AspNetCore` et `AventusSharp.Maui` ne choisissent aucun provider.
- Le package historique `AventusSharp` a été retiré de la solution.

### Fusion du socle portable

- `AventusSharp.Data` et `AventusSharp.Runtime` ont été fusionnés physiquement dans
  `AventusSharp.Core`.
- Les namespaces publics Data et Routes sont conservés.
- Un hôte et chaque provider ne possèdent plus qu'une dépendance commune : Core.

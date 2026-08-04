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

## Carte initiale

| Composant actuel | Destination | Contraintes observées |
| --- | --- | --- |
| `Tools/Attributes` | Core | Majoritairement autonome. `EnvName.Name` est `internal` et consommé par `Configuration`, donc à déplacer avec ce dernier ou avec une visibilité inter-assembly maîtrisée. |
| `Tools` | Core | Cycle actuel : `TypeTools` et `CSVMapper` utilisent Data ; `ResultWithError` utilise Data et WebSocket ; `AventusLogger` utilise `IApplicationBuilder`. Le dossier ne peut pas être déplacé en bloc. |
| `Scheduler` | Core | Portable sauf `JobManager`, qui utilise `AventusLogger`. Plusieurs membres `internal` relient `JobManager` au reste du scheduler. |
| `Chart` | Core | Dépend seulement de Newtonsoft.Json, mais certains membres `internal` sont écrits depuis Data. |
| `Data/Attributes` et contrats Data | Core | `Scope` expose actuellement `HttpContext`. Certains fichiers Data référencent Routes et Tools. Migration par sous-ensembles nécessaire. |
| `Data/Storage/Sqlite` | futur package SQLite | Dépend de `Microsoft.Data.Sqlite`. Candidat Android, mais à isoler du Core. |
| `Data/Storage/Mssql` | fournisseur SQL Server | Dépend de `Microsoft.Data.SqlClient`. |
| `Data/Storage/Mysql` | fournisseur MySQL | Dépend de `MySql.Data`. |
| `Data/Storage/Postgresql` | fournisseur PostgreSQL | Dépend de `Npgsql`. |
| `Routes` | Core + AspNetCore | Résolution et modèles de route dans Core ; middleware et adaptation HTTP dans AspNetCore. `Routes/Response` reste le format de sortie du routeur. |
| `WebSocket` | Core + AspNetCore | Protocole et événements potentiellement portables ; acceptation de connexion et `HttpContext` dans AspNetCore. |
| `SSE` | Core + AspNetCore | Événements potentiellement portables ; connexion et features HTTP dans AspNetCore. |
| `ApplicationBuilder` | AspNetCore | Dépend directement de `IApplicationBuilder`. |

## Premier lot retenu

Déplacer `ConfigSection`, `Export`, `FctName` et `NoRoute` vers Core. Ces attributs sont publics, autonomes et n'utilisent aucune dépendance externe. `EnvName` reste temporairement dans l'assembly historique à cause de son membre `internal`.


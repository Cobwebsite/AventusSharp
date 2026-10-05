# TODO issus des commentaires du code

Revue du 27 septembre 2026 : 41 occurrences de TODO recensées initialement dans les sources, regroupées ci-dessous après lecture des implémentations et des usages. Analyse statique ; les tests existants ont été consultés, sans être exécutés. Les 14 TODO correspondant aux suppressions déjà prises en charge ont été retirés du code et de cette liste ; 23 occurrences restent dans les sources après implémentation des trois premiers points. Les chemins et lignes renvoient à l'état du dépôt lors de cette revue.

## Fonctionnalités et corrections à réaliser

- [x] **Créer et supprimer des colonnes en migration.** Implémenté pour SQLite, MySQL, PostgreSQL et SQL Server : contraintes, index, valeurs par défaut, références et tables intermédiaires. Un ajout existant est refusé explicitement ; une suppression absente est idempotente. Tests de conservation des données et contraintes, de dépendances entrantes, de triggers et séquences SQLite, et de rollback hors MySQL (DDL avec commit implicite).
  - Source : `AventusSharp.Core/Data/Storage/Default/DefaultDBStorage.cs:2695,2699`.

- [x] **Supprimer les lignes parentes de l'héritage persistant.** Les identifiants sélectionnés sont supprimés par table, du type concret aux parents persistants, dans une transaction commune. Tests locaux sur trois niveaux, filtre sur parent, suppression via interface et rollback ; tests multi-fournisseurs ajoutés (à relancer quand Docker est disponible).
  - Source : `AventusSharp.Data.Mssql/Storage/Queries/Delete.cs:80` ; comparaison avec les autres `Queries/Delete.cs`.

- [x] **Respecter une politique explicite pour la suppression des relations inverses.** `DeleteOnCascade` et `DeleteSetNull` sur la clé étrangère priment sur `AutoDelete` de la relation inverse. Sans politique explicite, `AutoDelete` choisit selon la nullabilité ; désactivé, la contrainte SQL décide. Les politiques incompatibles sont rejetées lorsque des dépendants existent. Tests des relations obligatoires et nullables, du cache et du rollback.
  - Source : `AventusSharp.Core/Data/Storage/Default/DefaultDBStorage.cs:2573`.

- [x] **Traiter `Ignore` sur une relation externe au SQL principal.** L'exclusion est propagée au builder de la sous-requête concernée, y compris sur les chemins imbriqués, sans effacer les valeurs des instances canoniques.
  - Source : `AventusSharp.Core/Data/Manager/DB/DatabaseGenericBuilder.cs:382`.

- [x] **Définir et implémenter le tri et le regroupement sur les relations externes.** Les clés scalaires, y compris `Count` sur une collection, sont évaluées après chargement ; les collections entières sont refusées comme clés. `Limit` et `Offset` sont appliqués après le tri ou le regroupement. Les relations nulles et les chemins imbriqués sont testés.
  - Source : `AventusSharp.Core/Data/Manager/DB/DatabaseGenericBuilder.cs`.

- [x] **Prendre en charge les collections de valeurs simples et d'enums.** Les `List<T>` et `Dictionary<string/int, T>` de valeurs simples sont stockés en JSON dans une colonne ; les enums et valeurs nullables sont pris en charge. Le cas `List<int>` avec `[ForeignKey]` et les collections de `IStorable` gardent leurs relations. Création, lecture, mise à jour, suppression, migration et requêtes sont testées.
  - Source : `AventusSharp.Core/Data/Storage/Default/TableMember/TableMemberInfoSql.cs:183`.

- [x] **Clarifier les liens sans table résolue.** Les liens vers un autre stockage sont autorisés : leurs colonnes et tables intermédiaires sont créées sans clé étrangère SQL vers l'autre base. Une cible sans table résolue provoque une erreur `TypeNotFound` lors de la préparation des liens. La création de table et l'ajout de colonne MySQL suivent la même règle ; les deux situations sont testées.
  - Sources : `AventusSharp.Data.Mssql/Storage/Queries/CreateTable.cs:86`, `AventusSharp.Data.Mysql/Storage/Queries/CreateTable.cs:87`, `AventusSharp.Data.Mysql/Storage/Queries/AddColumn.cs:86`, `AventusSharp.Data.Postgresql/Storage/Queries/CreateTable.cs:80`, `AventusSharp.Data.Sqlite/Storage/Sqlite/Queries/CreateTable.cs:87`.

- [ ] **Exporter les types imbriqués déclarés dans une interface vers TypeScript.** La branche `isInterface` pour un membre de type nommé est vide : ces déclarations sont ignorées. Définir une représentation TypeScript compatible avec le générateur et ajouter un exemple C# compilable avec un type imbriqué dans une interface, puis vérifier la sortie générée. Remplacer le commentaire interrogatif par cette limite précise.
  - Source : `CSharpToTypescript/Container/BaseClassContainer.cs:450`.

- [ ] **Conserver les arguments des constructeurs dans les initialiseurs TypeScript.** Le générateur transforme la création d'un objet dont le type est un identifiant en `new Type()` sans retranscrire les arguments. Traduire les arguments, vérifier les arguments nommés et optionnels, et comparer aussi les syntaxes `new()` et les noms de types qualifiés. Ajouter un exemple dont les arguments changent effectivement la valeur initiale.
  - Source : `CSharpToTypescript/Container/BaseClassContainer.cs:585`.

## Optimisations et évolutions à cadrer

- [x] **Rendre la taille des lots de `BulkCreate` configurable si nécessaire.** `BulkCreateOptions` expose `WithId` et `BatchSize` (500 par défaut) aux variantes statiques, aux listes et aux managers. Le stockage borne la taille demandée selon sa limite de paramètres SQL. Les tailles invalides, le rollback sur plusieurs lots et le plafonnement SQL Server sont testés.
  - Sources : `AventusSharp.Core/Data/Storable.cs:204`, `AventusSharp.Core/Data/ListStorable.cs:60` ; découpage dans `DefaultDBStorage.cs:1777`.

- [x] **Regrouper les lectures de relations inverses par lot.** `ReverseQuery(List<int>)` est le traitement commun : un filtre `Contains` sur les identifiants distincts exécute une seule requête préparée pour le lot, au lieu d'une par identifiant ; l'appel unitaire délègue à la liste. La liste vide n'exécute aucune requête. Les erreurs, les scopes du builder et la déduplication des résultats sont conservés. Test d'intégration ajouté pour les lots, les doublons, la liste vide et l'appel unitaire.
  - Source : `AventusSharp.Core/Data/Storage/Default/TableMember/TableReverseMemberInfo.cs:241`.

- [x] **Évaluer une diffusion concurrente SSE et WebSocket.** Les diffusions utilisent jusqu'à 16 envois simultanés entre connexions. Les diffusions successives et les écritures sur chaque connexion gardent leur ordre ; les erreurs sont isolées par client et les envois en attente sont annulés à la déconnexion. Mesure locale avec 40 clients dont un lent : 1000 ms en séquentiel, 200 ms en concurrence bornée (5 fois plus rapide). Tests ciblés SSE/WebSocket et concurrence : 78 réussis.
  - Sources : `AventusSharp.AspNetCore/Hosting/ConcurrentBroadcast.cs`, `AventusSharp.AspNetCore/Hosting/SendQueue.cs`, `AventusSharpTest/SSE/ConcurrentBroadcastTests.cs`.

- [x] **Clarifier le nom des relations du diagramme.** Le nom inclut maintenant la colonne source (`table_source_colonne_table_cible`) dans le diagramme généré et après fusion. `SourceFieldId` pointe sur cette colonne de relation, y compris lorsqu'elle porte un nom SQL personnalisé. Test d'intégration avec deux liens vers la même table, tests de fusion et de migration : 20 réussis.
  - Sources : `AventusSharp.Core/Data/Storage/Default/DefaultDBStorage.cs`, `AventusSharp.Core/Chart/Schema.cs`, `AventusSharpTest/Integration/DiagramGenerationTests.cs`.

- [ ] **Unifier la localisation des erreurs cron.** Le message par défaut est encore `Crontab error.`, et d'autres erreurs cron sont également écrites directement en anglais. Utiliser les clés de traduction du projet pour les messages utiles à l'utilisateur, avec les valeurs du champ et de l'expression ; conserver les exceptions internes. Le constructeur sans argument n'a pas d'appel identifié dans les sources : son seul message n'est donc pas une priorité fonctionnelle.
  - Source : `AventusSharp.Core/Scheduler/Cron/CrontabFieldImpl.cs:26` ; autres messages dans `CrontabField.cs` et `CrontabSchedule.cs`.

## TODO obsolètes ou à reformuler

- **`DatabaseBuilderInfo.ReverseLinks` : nettoyage possible, avec vérification de compatibilité.** La recherche des usages trouve la déclaration et deux écritures (`Add`), sans lecture dans les sources du dépôt. Supprimer la collection et ses écritures si elle n'est pas utilisée par des consommateurs externes ; c'est une propriété publique, donc vérifier la compatibilité avant retrait. Reformuler le TODO en tâche de nettoyage, pas en défaut de chargement des relations inverses.
  - Source : `AventusSharp.Core/Data/Manager/DB/TypeDefinitionForBuilder.cs:436` ; écritures dans `DefaultDBStorage.cs:1572,2282`.

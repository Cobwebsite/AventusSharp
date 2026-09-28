# TODO issus des commentaires du code

Revue du 27 septembre 2026 : 41 occurrences de TODO recensées initialement dans les sources, regroupées ci-dessous après lecture des implémentations et des usages. Analyse statique ; les tests existants ont été consultés, sans être exécutés. Les 14 TODO correspondant aux suppressions déjà prises en charge ont été retirés du code et de cette liste ; 24 occurrences restent dans les sources après implémentation des créations et suppressions de colonnes et de la suppression des lignes parentes d'héritage. Les chemins et lignes renvoient à l'état du dépôt lors de cette revue.

## Fonctionnalités et corrections à réaliser

- [x] **Créer et supprimer des colonnes en migration.** Implémenté pour SQLite, MySQL, PostgreSQL et SQL Server : contraintes, index, valeurs par défaut, références et tables intermédiaires. Un ajout existant est refusé explicitement ; une suppression absente est idempotente. Tests de conservation des données et contraintes, de dépendances entrantes, de triggers et séquences SQLite, et de rollback hors MySQL (DDL avec commit implicite).
  - Source : `AventusSharp.Core/Data/Storage/Default/DefaultDBStorage.cs:2695,2699`.

- [x] **Supprimer les lignes parentes de l'héritage persistant.** Les identifiants sélectionnés sont supprimés par table, du type concret aux parents persistants, dans une transaction commune. Tests locaux sur trois niveaux, filtre sur parent, suppression via interface et rollback ; tests multi-fournisseurs ajoutés (à relancer quand Docker est disponible).
  - Source : `AventusSharp.Data.Mssql/Storage/Queries/Delete.cs:80` ; comparaison avec les autres `Queries/Delete.cs`.

- [ ] **Respecter une politique explicite pour la suppression des relations inverses.** Le traitement commun décide actuellement de mettre à `null` ou de supprimer uniquement selon la nullabilité du membre inverse. Définir et appliquer la priorité des attributs de suppression et des options d'auto-suppression ; vérifier aussi le cas où aucune suppression automatique n'est demandée. Tester les relations obligatoires et nullables, le cache et le rollback.
  - Source : `AventusSharp.Core/Data/Storage/Default/DefaultDBStorage.cs:2573`.

- [ ] **Traiter `Ignore` sur une relation externe au SQL principal.** La branche externe lève encore `NotImplementedException`. Propager l'exclusion au builder de la sous-requête concernée, y compris sur les chemins imbriqués, et vérifier que les instances canoniques conservent les valeurs des champs ignorés.
  - Source : `AventusSharp.Core/Data/Manager/DB/DatabaseGenericBuilder.cs:382`.

- [ ] **Définir et implémenter le tri et le regroupement sur les relations externes.** Les deux branches externes lèvent `NotImplementedException`. Définir d'abord la sémantique pour les collections et l'interaction avec `Limit`/`Offset`, puis choisir entre traduction SQL et traitement après chargement. Le TODO de `GroupGeneric` parle de tri par erreur : il s'agit bien de regroupement. Tester les relations nulles et les chemins imbriqués. Ce sujet recoupe les sous-requêtes externes déjà mentionnées dans `Waiting.md`.
  - Sources : `AventusSharp.Core/Data/Manager/DB/DatabaseGenericBuilder.cs:424,463`.

- [ ] **Prendre en charge les collections de valeurs simples et d'enums.** Les listes et dictionnaires ordinaires sont reconnus comme relations seulement si leur valeur implémente `IStorable`. Le cas `List<int>` avec `[ForeignKey]` existe déjà et ne doit pas être réimplémenté. Choisir le stockage des collections de valeurs simples, puis couvrir création, lecture, mise à jour, suppression, migration et requêtes ; traiter les enums et les valeurs nullables.
  - Source : `AventusSharp.Core/Data/Storage/Default/TableMember/TableMemberInfoSql.cs:183`.

- [ ] **Clarifier les liens sans table résolue.** Les branches `TableLinked == null` des générateurs de contraintes sont vides. La préparation du stockage résout déjà les types connus et signale les types introuvables : ne pas ajouter aveuglément une clé étrangère vers une autre base. Décider si les liens vers un autre stockage sont autorisés sans contrainte SQL, ou refusés explicitement ; tester les deux situations selon la politique retenue.
  - Sources : `AventusSharp.Data.Mssql/Storage/Queries/CreateTable.cs:86`, `AventusSharp.Data.Mysql/Storage/Queries/CreateTable.cs:87`, `AventusSharp.Data.Mysql/Storage/Queries/AddColumn.cs:86`, `AventusSharp.Data.Postgresql/Storage/Queries/CreateTable.cs:80`, `AventusSharp.Data.Sqlite/Storage/Sqlite/Queries/CreateTable.cs:87`.

- [ ] **Exporter les types imbriqués déclarés dans une interface vers TypeScript.** La branche `isInterface` pour un membre de type nommé est vide : ces déclarations sont ignorées. Définir une représentation TypeScript compatible avec le générateur et ajouter un exemple C# compilable avec un type imbriqué dans une interface, puis vérifier la sortie générée. Remplacer le commentaire interrogatif par cette limite précise.
  - Source : `CSharpToTypescript/Container/BaseClassContainer.cs:450`.

- [ ] **Conserver les arguments des constructeurs dans les initialiseurs TypeScript.** Le générateur transforme la création d'un objet dont le type est un identifiant en `new Type()` sans retranscrire les arguments. Traduire les arguments, vérifier les arguments nommés et optionnels, et comparer aussi les syntaxes `new()` et les noms de types qualifiés. Ajouter un exemple dont les arguments changent effectivement la valeur initiale.
  - Source : `CSharpToTypescript/Container/BaseClassContainer.cs:585`.

## Optimisations et évolutions à cadrer

- [ ] **Rendre la taille des lots de `BulkCreate` configurable si nécessaire.** Les deux TODO restent pertinents pour l'API, mais le découpage existe déjà : taille par défaut de 500, réduite selon la limite de paramètres du fournisseur. Introduire des options communes aux variantes avec/sans erreurs et aux listes, en gardant les surcharges avec `withId`. Une taille demandée doit rester bornée par les limites SQL ; tester les valeurs invalides et le rollback sur plusieurs lots.
  - Sources : `AventusSharp.Core/Data/Storable.cs:204`, `AventusSharp.Core/Data/ListStorable.cs:60` ; découpage dans `DefaultDBStorage.cs:1777`.

- [ ] **Regrouper les lectures de relations inverses par lot.** `ReverseQuery(List<int>)` boucle encore sur `ReverseQuery(int)` et déduplique ensuite les résultats. Faire du traitement par liste la base commune, utiliser un filtre sur les identifiants et déléguer l'appel unitaire à ce traitement. Conserver les scopes, les erreurs et la déduplication ; mesurer le nombre de requêtes et couvrir la liste vide.
  - Source : `AventusSharp.Core/Data/Storage/Default/TableMember/TableReverseMemberInfo.cs:241`.

- [ ] **Évaluer une mise à jour sans relecture.** Le builder de mise à jour appelle encore `DM.GetByIdsWithError` après l'écriture ; cela peut passer par le cache et ne signifie pas systématiquement une requête SQL supplémentaire. Mesurer les requêtes avec/sans cache avant de créer une API retournant seulement le résultat de l'écriture. Préserver les événements, les valeurs générées en base, les relations et la cohérence du cache pour tous les managers.
  - Source : `AventusSharp.Core/Data/Manager/GenericDM.cs:1740` ; appel dans `AventusSharp.Core/Data/Manager/DB/Builders/DatabaseUpdateBuilder.cs:95`.

- [ ] **Profiler la matérialisation avant de mettre les métadonnées en cache.** La méthode reconstruit notamment les clés `alias*colonne` et parcourt les membres pour chaque objet. Le registre d'identité déjà présent évite les doublons d'instances, mais ne remplace pas un plan de matérialisation. Mesurer le coût, puis pré-calculer les correspondances stables par projection si le gain le justifie ; ne pas partager les données propres à une exécution.
  - Source : `AventusSharp.Core/Data/Storage/Default/DefaultDBStorage.cs:1411`.

- [ ] **Évaluer une diffusion concurrente SSE et WebSocket.** Les envois aux clients sont actuellement attendus séquentiellement : un client lent retarde les suivants. Prévoir une concurrence bornée entre connexions, conserver l'ordre et sérialiser les écritures sur une même connexion, isoler les erreurs par client et gérer annulation/déconnexion. Mesurer le gain avec plusieurs clients dont un lent.
  - Sources : `AventusSharp.AspNetCore/SSE/SSEEndPoint.cs:224`, `AventusSharp.AspNetCore/WebSocket/WsEndPoint.cs:409`.

- [ ] **Clarifier le nom des relations du diagramme.** Un nom existe déjà (`table_source_table_cible`), mais deux membres reliant les mêmes tables obtiennent le même nom. Inclure le membre ou la colonne dans le nom et vérifier que `SourceFieldId` désigne bien la colonne de relation : il pointe actuellement sur la clé primaire de la source. Tester plusieurs relations entre deux tables.
  - Source : `AventusSharp.Core/Data/Storage/Default/DefaultDBStorage.cs:3149`.

- [ ] **Unifier la localisation des erreurs cron.** Le message par défaut est encore `Crontab error.`, et d'autres erreurs cron sont également écrites directement en anglais. Utiliser les clés de traduction du projet pour les messages utiles à l'utilisateur, avec les valeurs du champ et de l'expression ; conserver les exceptions internes. Le constructeur sans argument n'a pas d'appel identifié dans les sources : son seul message n'est donc pas une priorité fonctionnelle.
  - Source : `AventusSharp.Core/Scheduler/Cron/CrontabFieldImpl.cs:26` ; autres messages dans `CrontabField.cs` et `CrontabSchedule.cs`.

## TODO obsolètes ou à reformuler

- **Boucle infinie dans `AddDataDependency` : pas de cycle dans la récursion actuelle.** La méthode descend uniquement dans les arguments de types génériques construits, puis normalise leur définition. Elle ne suit ni les propriétés du type ni le graphe de dépendances entre modèles ; les arguments forment une structure finie. Retirer ce TODO ou le remplacer par cette explication. Ne pas confondre ce parcours avec la détection des cycles entre managers.
  - Source : `AventusSharp.Core/Data/DataMainManager.cs:430`.

- **Crash de `Contains` sur une chaîne nulle : risque à reformuler.** Le code construit une expression destinée à la traduction SQL, sans appeler `Contains` sur l'objet stocké. Une colonne SQL nulle ne déclenche pas une `NullReferenceException` lors de cette construction. Retirer l'hypothèse de crash ; compléter plutôt les tests de recherche avec une colonne nullable et vérifier la traduction chez les fournisseurs. Les tests actuels de recherche textuelle ne couvrent pas ce cas.
  - Source : `AventusSharp.Core/Data/Manager/DB/Builders/DatabaseQueryBuilder.cs:181` ; tests dans `AventusSharpTest/Integration/DataTextSearchQueryTests.cs`.

- **`DatabaseBuilderInfo.ReverseLinks` : nettoyage possible, avec vérification de compatibilité.** La recherche des usages trouve la déclaration et deux écritures (`Add`), sans lecture dans les sources du dépôt. Supprimer la collection et ses écritures si elle n'est pas utilisée par des consommateurs externes ; c'est une propriété publique, donc vérifier la compatibilité avant retrait. Reformuler le TODO en tâche de nettoyage, pas en défaut de chargement des relations inverses.
  - Source : `AventusSharp.Core/Data/Manager/DB/TypeDefinitionForBuilder.cs:436` ; écritures dans `DefaultDBStorage.cs:1572,2282`.

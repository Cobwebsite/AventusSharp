# TODO AventusSharp

Ce fichier recense les limites confirmées ou mises en évidence pendant
l'écriture de la suite de tests. Un point doit être retiré uniquement lorsque
son implémentation et ses tests de régression sont terminés.

## Data

### Relations N-N avec `BulkCreate`

- [ ] Étendre le chemin optimisé `BulkCreate` pour créer les lignes des tables
  intermédiaires N-N.
- Les relations ne doivent pas être ignorées silencieusement lorsque les
  propriétaires sont créés avec succès.
- Une relation invalide dans un buffer ultérieur doit annuler les propriétaires
  et les relations écrits dans les buffers précédents.
- Les tests
  `BulkCreate_withId_persists_many_to_many_links_across_buffers` et
  `Invalid_many_to_many_link_in_second_buffer_rolls_back_all_buffers`
  restent explicites jusqu'à cette implémentation.

### `Nullable<T>.GetValueOrDefault` dans LambdaTranslator

- [ ] Traduire `item.Value.GetValueOrDefault()` avec `COALESCE` et la valeur par
  défaut du type.
- Gérer également la surcharge
  `GetValueOrDefault(valeurParDéfaut)`, y compris une valeur capturée.
- Le test de spécification
  `GetValueOrDefault_can_be_used_in_queries` est explicite jusqu'à cette
  implémentation.

### `Contains(null)` sur une collection nullable

- [ ] Traduire une collection contenant `null` en combinant `IN (...)` avec
  `champ IS NULL`, afin de conserver la sémantique de
  `collection.Contains(item.ChampNullable)`.
- Le test `Nullable_collection_contains_matches_null` reste explicite jusqu'à
  cette prise en charge.

### `Contains` sur une relation multiple du modèle

- [ ] Traduire une expression telle que
  `scene => scene.Lamps.Contains(lamp)` pour une relation N-N.
- La traduction devra interroger la table intermédiaire et prendre en charge
  la négation, notamment les objets dont la collection est vide.
- Le test de spécification
  `Many_to_many_collection_can_be_filtered_with_contains_and_its_negation`
  reste explicite jusqu'à cette prise en charge.
- Pour une relation SQL, l'absence d'éléments est représentée par zéro ligne
  liée et non par une collection `NULL`.

### Chargement explicite des relations imbriquées

- [ ] Corriger `Load(x => x.Room.Lamps)` lorsque `Lamps` est un
  `[ReverseLink]`.
- État actuel : `Load(x => x.Room)` fonctionne, mais le reverse link imbriqué
  n'est pas affecté à l'objet joint.
- Résultat attendu : charger le chemin complet sans remplacer les instances
  déjà présentes dans le cache.
- Le test de spécification
  `Explicit_load_supports_a_nested_reverse_link_path` existe dans
  `DataRelationshipTests` et reste explicite jusqu'à la correction.
- Vérifier les variantes :
  - relation directe suivie d'un reverse link ;
  - plusieurs objets racines ;
  - relation nullable ;
  - cache activé et désactivé ;
  - chemin déjà partiellement chargé.

### Cycles AutoRead sans cache local

- [ ] Vérifier et protéger les graphes bidirectionnels `[AutoRead]` lorsque
  `preferLocalCache` vaut `false`.
- État actuel : avec le cache, l'objet est enregistré par `OnItemLoaded` avant
  les sous-requêtes et le cycle `Room -> Lamps -> Room` réutilise la même
  instance.
- Risque à vérifier : sans cache, aucun registre d'instances ne coupe
  nécessairement la récursion.
- Résultat attendu : aucune récursion infinie, même sans cache. Une identité
  locale à la matérialisation de la requête peut être utilisée si nécessaire.
- Le test doit être conçu après la protection : un test non protégé peut
  provoquer un `StackOverflowException` et arrêter entièrement le runner.

### Synchronisation du cache après `DeleteSetNull`

- [ ] Mettre à jour les instances dépendantes déjà en cache lorsqu'une clé
  étrangère est mise à `NULL` par la base pendant la suppression du parent.
- Le parent ne possède pas nécessairement de `[ReverseLink]` vers le dépendant.
  Il faut donc conserver un registre des relations entrantes ou invalider les
  caches des DM concernés après la suppression.
- Le rollback d'une suppression doit restaurer la relation dans le cache.
- Le test
  `DeleteSetNull_updates_the_dependent_instance_already_in_cache`
  reste explicite jusqu'à cette prise en charge.

### Gestionnaire de données pour les tests unitaires

- [ ] Concevoir un `MockDatabaseDM` destiné aux tests unitaires.
- Il devra reproduire les contrats importants de `DatabaseDM` :
  - identité des instances avec cache ;
  - CRUD et validation ;
  - transactions et rollback ;
  - builders de requête ;
  - événements ;
  - relations utiles aux tests.
- Ne pas réintroduire `DummyDM`, qui ne respectait pas suffisamment ces
  contrats et pouvait produire de faux positifs.

### Événements CRUD et transactions externes

- [ ] Différer les événements `OnCreated`, `OnUpdated` et `OnDeleted` jusqu'au
  commit de la transaction externe qui contient l'opération.
- En cas de rollback, aucun événement de succès ne doit être publié.
- Les exceptions levées par un abonné restent isolées : elles ne doivent ni
  annuler une écriture validée, ni empêcher les abonnés suivants d'être appelés.
- Le test de spécification
  `Rolled_back_transaction_does_not_publish_success_event` reste explicite
  jusqu'à l'ajout d'une file d'événements dans le contexte transactionnel.

### Échec d'une transaction imbriquée

- [ ] Marquer le contexte partagé comme définitivement annulé lorsqu'une
  transaction interne effectue un rollback.
- Le callback externe ne doit pas pouvoir ignorer cet échec, ouvrir implicitement
  une nouvelle transaction, puis retourner un succès ou conserver des écritures
  effectuées après le rollback interne.
- Le test de spécification
  `Failed_inner_transaction_cannot_be_ignored_by_the_outer_callback` reste
  explicite jusqu'à la propagation de cet état d'échec au niveau externe.

### BulkCreate et héritage multi-table

- [ ] Faire écrire `BulkCreate` dans la table racine puis dans chaque table
  enfant lorsque l'héritage persistant utilise plusieurs tables.
- Avec `withId: true`, le même identifiant explicite doit être propagé dans
  toutes les tables et l'objet dérivé fourni doit devenir l'instance canonique
  du cache partagé.
- Le test
  `BulkCreate_withId_preserves_canonical_children_in_the_shared_parent_cache`
  reste explicite. Le cas `[ForceInherit]`, stocké dans une seule table
  concrète, est déjà couvert et fonctionnel.

## Migrations

### Modification d'un modèle

- [ ] Implémenter la mise à jour et le renommage des propriétés.
- Le test de spécification existe déjà :
  `RenameProperty_preserves_data_and_exposes_the_new_column`.
- Retirer son attribut `[Explicit]` lorsque l'implémentation est disponible.
- Vérifier au minimum :
  - conservation des données ;
  - renommage aller et retour (`Up` et `Down`) ;
  - type, nullabilité, taille et valeur par défaut ;
  - index, clé étrangère et contrainte unique ;
  - comportement sur SQLite, MySQL, PostgreSQL et SQL Server.

### Suppression d'un modèle

- [ ] Implémenter la suppression d'un modèle et de sa table.
- Le test de spécification existe déjà :
  `DeleteModel_removes_the_table`.
- Retirer son attribut `[Explicit]` lorsque l'implémentation est disponible.
- Vérifier également les tables intermédiaires, index, clés étrangères et
  l'ordre de suppression des modèles dépendants.

## Infrastructure de test

### Dépendance SQLite en préversion

- [ ] Aligner la version du package avec une version stable compatible.
- État actuel : la compilation réussit, mais NuGet produit l'avertissement
  `NU5104` car un package AventusSharp stable dépend d'une préversion de
  `Microsoft.Data.Sqlite`.
# Data cache

- `StartQuery()` matérialise actuellement de nouvelles instances même lorsque `preferLocalCache` est actif. Une correction sûre doit fusionner uniquement les champs réellement sélectionnés dans l'instance canonique (y compris les relations explicitement chargées), sans écraser les propriétés `[NotInDB]` ni conserver des valeurs persistantes obsolètes après un rollback.
- Le test de spécification
  `StartQuery_returns_the_canonical_cached_instance_without_losing_runtime_state`
  reste explicite jusqu'à cette correction.

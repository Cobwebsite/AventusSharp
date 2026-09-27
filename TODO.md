# TODO AventusSharp

Ce fichier recense les limites confirmées ou mises en évidence pendant
l'écriture de la suite de tests. Un point doit être retiré uniquement lorsque
son implémentation et ses tests de régression sont terminés.

## Data

### Relations N-N avec `BulkCreate`

- [x] Étendre le chemin optimisé `BulkCreate` pour créer les lignes des tables
  intermédiaires N-N.
- Les relations ne doivent pas être ignorées silencieusement lorsque les
  propriétaires sont créés avec succès.
- Une relation invalide dans un buffer ultérieur doit annuler les propriétaires
  et les relations écrits dans les buffers précédents.
- Les tests
  `BulkCreate_withId_persists_many_to_many_links_across_buffers` et
  `Invalid_many_to_many_link_in_second_buffer_rolls_back_all_buffers`
  sont activés et réussissent. Les identifiants générés utilisent le parcours
  de création individuel dans la même transaction ; les identifiants fournis
  conservent les insertions groupées. Listes et dictionnaires sont couverts.

### Chargement explicite des relations imbriquées

- [x] Corriger `Load(x => x.Room.Lamps)` lorsque `Lamps` est un
  `[ReverseLink]`.
- Le chemin complet est chargé sur les parents imbriqués, en conservant les
  instances du cache et les parents déjà affectés. Les collections sont
  rafraîchies sans doublons et les enfants référencent le parent du chemin.
- Le test `Explicit_load_supports_a_nested_reverse_link_path` est activé.
- Suite complète avec Docker : 801 tests réussis, aucun échec ni test ignoré.
  Documentation du site mise à jour et compilée.
- Tests de régression ajoutés pour les variantes :
  - relation directe suivie d'un reverse link ;
  - plusieurs objets racines ;
  - relation nullable ;
  - cache activé et désactivé ;
  - chemin déjà partiellement chargé.

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

### BulkCreate et héritage multi-table

- [x] Faire écrire `BulkCreate` dans la table racine puis dans chaque table
  enfant lorsque l'héritage persistant utilise plusieurs tables.
- Avec `withId: true`, le même identifiant explicite doit être propagé dans
  toutes les tables et l'objet dérivé fourni doit devenir l'instance canonique
  du cache partagé.
- Le test
  `BulkCreate_withId_preserves_canonical_children_in_the_shared_parent_cache`
  est activé et réussit. Les lots de types dérivés mixtes et les identifiants
  générés sont également pris en charge ; rollback des tables, du cache et
  des identifiants vérifié. Le cas `[ForceInherit]` reste fonctionnel.
- Validation complète avec Docker : 798 tests réussis, aucun échec ;
  une spécification explicite encore en attente.

## Migrations

### Modification d'un modèle

- [x] Implémenter la mise à jour et le renommage des propriétés. Les 10 tests
  de migration passent sur les quatre fournisseurs SQL ; suite complète avec
  Docker : 758 tests réussis, aucun échec.
- Le test de spécification existe déjà :
  `RenameProperty_preserves_data_and_exposes_the_new_column`.
- Son attribut `[Explicit]` a été retiré.
- Vérifier au minimum :
  - conservation des données ;
  - renommage aller et retour (`Up` et `Down`) ;
  - type, nullabilité, taille et valeur par défaut ;
  - index, clé étrangère et contrainte unique ;
  - comportement sur SQLite, MySQL, PostgreSQL et SQL Server.

### Suppression d'un modèle

- [x] Implémenter la suppression d'un modèle et de sa table, y compris ses
  tables intermédiaires, index et clés étrangères sur les quatre fournisseurs.
- Le test de spécification existe déjà :
  `DeleteModel_removes_the_table`.
- Son attribut `[Explicit]` a été retiré.
- Les modèles à supprimer sont regroupés par fournisseur ; les dépendances
  et cycles internes sont résolus avant la suppression des tables.
- Une clé étrangère depuis un modèle conservé bloque le lot entier avant
  toute suppression avec `DataErrorCode.ModelDeletionBlocked`.
- Validation : 16 tests ciblés réussis ; suite complète avec Docker :
  772 tests réussis, aucun échec ; 4 spécifications explicites en attente.

## Infrastructure de test

### Dépendance SQLite en préversion

- [ ] Aligner la version du package avec une version stable compatible.
- État actuel : la compilation réussit, mais NuGet produit l'avertissement
  `NU5104` car un package AventusSharp stable dépend d'une préversion de
  `Microsoft.Data.Sqlite`.

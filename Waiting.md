# Travaux en attente pour AventusSharp

Liste consolidée à partir de `TODO.md` et des limites publiées dans `D:\Aventus\AventusSharpWebsite`. Un point n'est terminé qu'après implémentation, tests de régression et mise à jour de la documentation.

## Priorité 1 — Fiabilité des données

- [x] Empêcher qu'un échec de transaction imbriquée soit ignoré par la transaction externe : aucun nouveau travail ne doit être validé après le rollback interne, et le résultat externe doit signaler l'échec. Test de spécification activé et tests ciblés réussis ; documentation du site mise à jour.
- [x] Différer `OnCreated`, `OnUpdated` et `OnDeleted` jusqu'au commit externe ; ne rien publier en cas de rollback. Test de spécification activé et tests ciblés réussis ; documentation du site mise à jour.
- [x] Synchroniser le cache après `DeleteSetNull` et restaurer les relations en cas de rollback. Test de spécification activé, test de rollback ajouté et tests de relations réussis ; documentation du site mise à jour.
- [x] Faire retourner à `StartQuery()` l'instance canonique du cache lorsque l'identifiant est sélectionné, sans écraser les champs `[NotInDB]` ou les champs ignorés, et restaurer les valeurs après rollback. Les projections `Field(...)` sans identifiant restent des objets de projection distincts. Tests ciblés réussis ; documentation du site mise à jour.
- [x] Protéger les cycles `[AutoRead]` lorsque `preferLocalCache` vaut `false`. Un registre d'identité limité à la chaîne de lecture évite la récursion et conserve les références du graphe ; tests sans cache et tests de relations réussis. Documentation du site mise à jour.

## Priorité 2 — Persistance

- [x] Implémenter la mise à jour et le renommage de propriétés en migration, avec conservation des données et prise en charge des quatre fournisseurs SQL. Les 10 tests de migration réussissent ; suite complète avec Docker : 758 tests exécutés et réussis, aucun échec. Documentation du site mise à jour.
- [x] Implémenter la suppression de modèles, y compris tables intermédiaires, index, clés étrangères et dépendances. Les suppressions sont regroupées par fournisseur et ordonnées selon les références ; les cycles internes sont pris en charge. Une référence depuis un modèle conservé bloque toute suppression du lot avec `DataErrorCode.ModelDeletionBlocked`. Tables N-N entrantes et sortantes supprimées, modèles liés conservés. Tests ciblés : 16 réussis ; suite complète avec Docker : 772 tests réussis, aucun échec ; 4 spécifications explicites encore en attente. Documentation du site mise à jour.
- [x] Retirer `Down()` du périmètre actuel : méthodes `Down` et `_Down` supprimées, générateur et exemples adaptés. Les migrations utilisent uniquement `Up()`.
- [x] Compléter `BulkCreate` pour les liens N-N et garantir le rollback de tous les buffers en cas de lien invalide. Liens dédupliqués, listes et dictionnaires pris en charge ; identifiants fournis ou générés, auto-création/mise à jour des relations et rollback global. Documentation du site mise à jour et compilée. Suite complète avec Docker : 798 tests réussis, aucun échec ; une spécification explicite encore en attente.
- [x] Compléter `BulkCreate` pour l'héritage persistant multi-table. Identifiants partagés entre parent et enfant, lots de types dérivés mixtes, cache canonique et restauration des identifiants générés au rollback. Buffers adaptés à la limite de paramètres SQL Server. Documentation du site mise à jour et compilée. Suite complète avec Docker : 798 tests réussis, aucun échec.
- [x] Charger les chemins de relations terminés par un `[ReverseLink]` imbriqué sans remplacer les instances du cache. Sous-requêtes exécutées sur les parents du chemin avec leur identifiant ; parents déjà affectés conservés, collections rafraîchies sans doublons et références inverses cohérentes, y compris sans cache. Spécification activée et régressions ajoutées : plusieurs racines, relation nullable, chargements répétés et chemin partiellement chargé. Suite complète avec Docker : 801 tests réussis, aucun échec ni test ignoré. Documentation du site mise à jour et compilée.

## Priorité 3 — Requêtes

- [x] Traduire `Nullable<T>.GetValueOrDefault()` et sa surcharge avec valeur par défaut.
- [x] Respecter `Contains(null)` sur les collections nullables.
- [x] Traduire `Contains` et sa négation sur les relations N-N.
- [ ] Prendre en charge les expressions qui nécessitent une sous-requête externe, mentionnées dans la documentation du site.

## Normalisation SQL

- [x] Centraliser l'échappement des identifiants des requêtes CRUD, jointures, filtres, tris et regroupements avec `QuoteIdentifier` pour les quatre stockages. Test de régression ajouté avec des noms réservés SQL ; validation complète avec Docker réussie.
- [x] Appliquer `QuoteIdentifier` à la création du schéma, aux tables intermédiaires, aux index et contraintes, aux commandes de migration existantes et à la suppression des tables. Suite complète : 747 tests exécutés et réussis, aucun échec ; 6 spécifications explicites non exécutées sur des fonctionnalités encore en attente. MySQL, PostgreSQL et SQL Server testés sous Docker, SQLite testé directement.

## Priorité 4 — Tests, intégration et documentation

- [x] Ajouter le client SSE AventusJs sur le modèle du WebSocket : `Socket`, `Connection`, `EndPoint`, `SSEEvent`, abonnements par canal et conversion des payloads. Connexions partagées, reconnexion native EventSource, fermeture au dernier utilisateur. Six tests client réussis et vérification Aventus réussie.
- [x] Ajouter au convertisseur C# vers AventusJs la génération des endpoints et événements SSE typés. Endpoints, payloads imbriqués, sélection de l’endpoint et canaux dynamiques pris en charge ; deux scénarios de génération compilés avec Aventus, solution compilée sans erreur ni avertissement et documentation du site mise à jour.

- [x] Accepter les tableaux JSON à la racine des requêtes HTTP pour les paramètres de collection (`List<T>`, `T[]`). Tableaux vides et propriétés contenant un tableau pris en charge ; données invalides renvoyées en erreur `422`. Tests HTTP : 50 réussis. Query string lue via le contexte et paramètres URL `{id}` conservés ; ces usages sont déjà documentés.
- [x] Ajouter le binding des formulaires HTTP `application/x-www-form-urlencoded`. Décodage UTF-8, conversions des scalaires, objets imbriqués et listes (clés répétées, `[]` ou indices contigus). Valeurs vides et paramètres optionnels conservés ; erreurs `422` et `RouteErrorCode.InvalidFormData` pour les chemins incohérents. Tests HTTP : 63 réussis. Documentation du site mise à jour.
- [x] Appliquer les règles de méthode et d'en-tête `Accept` des endpoints SSE : `GET` uniquement, sinon `405` avec `Allow: GET` ; `Accept` absent accepté, négociation de `text/event-stream` avec listes, jokers et priorités `q`, sinon `406`. Contrôles avant création de connexion ; chemins inconnus transmis au middleware suivant. Tests SSE et intégration applicative : 50 réussis. Documentation du site mise à jour.
- [x] Permettre un statut de refus WebSocket adapté au contexte : `CanOpenConnection(HttpContext)` retourne un `VoidWithError` avant le handshake. Sans erreur, la connexion est autorisée ; le code de la première erreur sélectionne le statut HTTP entre 400 et 599, avec repli sur `403` sinon. Documentation mise à jour.
- [x] Aligner le refus SSE sur WebSocket : `CanOpenConnection(HttpContext)` retourne un `VoidWithError`, sans body de refus, avant ouverture du flux ; même sélection du statut HTTP et repli sur `403`. Les 48 tests SSE et intégration applicative passent ; documentation mise à jour.
- [x] Corriger la documentation historique du site qui affirme que seul MySQL est pris en charge, et retirer les mentions de limites résolues. Neuf fichiers mis à jour ; validation Astro sans erreur et compilation des 48 pages réussie.

Sources : `TODO.md` ; documentation du site dans `src/content/docs/model`, `data_manager/cache.mdx`, `storage/migrations.mdx`, `tools/logging.mdx`, `route_http/request_response.mdx`, `route_sse/lifecycle.mdx`, `route_ws/lifecycle.mdx` et `md/aventusharp.md`.

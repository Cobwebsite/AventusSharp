# Travaux en attente pour AventusSharp

Liste consolidée à partir de `TODO.md` et des limites publiées dans `D:\Aventus\AventusSharpWebsite`. Un point n'est terminé qu'après implémentation, tests de régression et mise à jour de la documentation.

## Priorité 1 — Fiabilité des données

- [x] Empêcher qu'un échec de transaction imbriquée soit ignoré par la transaction externe : aucun nouveau travail ne doit être validé après le rollback interne, et le résultat externe doit signaler l'échec. Test de spécification activé et tests ciblés réussis ; documentation du site à actualiser.
- [x] Différer `OnCreated`, `OnUpdated` et `OnDeleted` jusqu'au commit externe ; ne rien publier en cas de rollback. Test de spécification activé et tests ciblés réussis ; documentation du site à actualiser.
- [ ] Synchroniser le cache après `DeleteSetNull` et restaurer les relations en cas de rollback.
- [ ] Faire retourner à `StartQuery()` l'instance canonique du cache sans écraser les champs `[NotInDB]` ni garder des valeurs obsolètes après rollback.
- [ ] Protéger les cycles `[AutoRead]` lorsque `preferLocalCache` vaut `false`.

## Priorité 2 — Persistance

- [ ] Implémenter la mise à jour et le renommage de propriétés en migration, avec conservation des données et prise en charge des quatre fournisseurs SQL.
- [ ] Implémenter la suppression de modèles, y compris tables intermédiaires, index, clés étrangères et dépendances.
- [ ] Décider si une commande publique de rollback `Down()` est requise, puis l'implémenter si cette capacité fait partie du périmètre produit.
- [ ] Compléter `BulkCreate` pour les liens N-N et garantir le rollback de tous les buffers en cas de lien invalide.
- [ ] Compléter `BulkCreate` pour l'héritage persistant multi-table.
- [ ] Charger les chemins de relations terminés par un `[ReverseLink]` imbriqué sans remplacer les instances du cache.

## Priorité 3 — Requêtes

- [ ] Traduire `Nullable<T>.GetValueOrDefault()` et sa surcharge avec valeur par défaut.
- [ ] Respecter `Contains(null)` sur les collections nullables.
- [ ] Traduire `Contains` et sa négation sur les relations N-N.
- [ ] Prendre en charge les expressions qui nécessitent une sous-requête externe, mentionnées dans la documentation du site.

## Priorité 4 — Tests, intégration et documentation

- [ ] Créer un `MockDatabaseDM` fidèle aux contrats du gestionnaire réel pour les tests unitaires.
- [ ] Remplacer la dépendance SQLite en préversion à l'origine de `NU5104`.
- [ ] Faire fonctionner `SSEConfig.PrintRoute` et `PrintTrigger`, ou retirer ces options si elles ne font pas partie de l'API voulue.
- [ ] Définir le périmètre HTTP attendu : binding automatique de la query string, formulaires URL encodés et tableaux JSON à la racine ; implémenter les cas retenus.
- [ ] Définir puis appliquer les règles de méthode et d'en-tête `Accept` des endpoints SSE.
- [ ] Permettre un statut de refus WebSocket adapté au contexte si le `302` fixe ne convient pas à l'API voulue.
- [ ] Corriger la documentation historique du site qui affirme que seul MySQL est pris en charge, et retirer les mentions de limites résolues.

Sources : `TODO.md` ; documentation du site dans `src/content/docs/model`, `data_manager/cache.mdx`, `storage/migrations.mdx`, `tools/logging.mdx`, `route_http/request_response.mdx`, `route_sse/lifecycle.mdx`, `route_ws/lifecycle.mdx` et `md/aventusharp.md`.

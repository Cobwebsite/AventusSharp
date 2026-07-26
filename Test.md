# AventusSharp.Test

Ce document liste les éléments à prévoir pour créer un package de test destiné
aux projets qui utilisent AventusSharp.

Le package ne doit pas réimplémenter AventusSharp, son moteur SQL, ses adapters
ou son `LambdaTranslator`. Leur validation appartient à la suite de tests
d'AventusSharp.

## Objectifs

- Simplifier l'initialisation d'AventusSharp dans une suite de tests.
- Isoler chaque test et remettre proprement l'état global à zéro.
- Faciliter la création de données propres au projet consommateur.
- Faciliter les assertions sur les `ResultWithError` et `VoidWithError`.
- Permettre de tester la configuration, les modèles et les règles métier du
  projet sans lui demander de retester toute la librairie.
- Fournir, si nécessaire, une intégration simple avec une vraie base de test.

## Hors périmètre initial

- Réimplémenter `LambdaTranslator` avec LINQ en mémoire.
- Simuler la génération SQL.
- Reproduire les différences entre SQLite, MySQL, PostgreSQL et SQL Server.
- Créer un `MockDatabaseDM` complet.
- Garantir qu'une requête acceptée par un faux gestionnaire fonctionnerait sur
  une vraie base.
- Remplacer la suite d'intégration Docker d'AventusSharp.

Un faux DM ne devra être ajouté que si des projets consommateurs rencontrent un
besoin métier concret qui ne peut pas être satisfait par les outils ci-dessous.

## Structure du package

- [ ] Créer le projet `AventusSharp.Test`.
- [ ] Référencer `AventusSharp`.
- [ ] Choisir les frameworks cibles compatibles avec le package principal.
- [ ] Ne dépendre d'aucun framework de test particulier dans le noyau du
  package.
- [ ] Préparer les métadonnées NuGet, la documentation XML et le README.
- [ ] Ajouter le projet à la solution et à la CI.
- [ ] Vérifier que le package publié ne dépend pas des projets de tests internes
  d'AventusSharp.

## Cycle de vie d'un environnement de test

Créer une API de type `AventusTestEnvironment` permettant de :

- [ ] initialiser `DataMainManager` avec une configuration dédiée au test ;
- [ ] enregistrer les storages et les DM nécessaires ;
- [ ] attendre la fin de l'initialisation asynchrone ;
- [ ] exposer les erreurs d'initialisation via `ResultWithError` ou
  `VoidWithError` ;
- [ ] réinitialiser les registres statiques entre deux tests ;
- [ ] vider les caches locaux des DM ;
- [ ] détacher les événements enregistrés pendant un test ;
- [ ] restaurer la configuration globale modifiée ;
- [ ] implémenter `IAsyncDisposable` pour garantir le nettoyage dans un
  `finally`, même lorsqu'un test échoue ;
- [ ] détecter une initialisation concurrente ou oubliée et retourner une erreur
  compréhensible.

Le nettoyage doit être une API publique supportée. Il ne doit pas demander aux
projets consommateurs d'utiliser la réflexion pour modifier des champs privés.

## Base de données légère

Prévoir un environnement SQLite temporaire pour les tests d'intégration simples
du projet consommateur :

- [ ] créer une base isolée par fixture ou par test ;
- [ ] générer automatiquement le schéma depuis les modèles enregistrés ;
- [ ] supprimer proprement la base à la fin du test ;
- [ ] fournir une option SQLite en mémoire si le cycle de vie des connexions le
  permet ;
- [ ] fournir une option utilisant un fichier temporaire pour les scénarios à
  plusieurs connexions ;
- [ ] permettre de conserver la base lorsqu'un test échoue afin de faciliter le
  diagnostic ;
- [ ] exposer une méthode explicite pour vider toutes les tables sans recréer le
  schéma ;
- [ ] respecter l'ordre des clés étrangères pendant le nettoyage ;
- [ ] retourner les erreurs avec les monades AventusSharp.

Cette base sert à vérifier les modèles et la configuration du projet. Elle ne
constitue pas une preuve de compatibilité avec un autre moteur SQL.

## Support optionnel d'une vraie base

Le package peut fournir des contrats et helpers, sans imposer Docker :

- [ ] définir une abstraction d'environnement de base externe ;
- [ ] accepter une chaîne de connexion fournie par variable d'environnement ou
  par configuration ;
- [ ] fournir les opérations `Initialize`, `Reset` et `Dispose` ;
- [ ] documenter un exemple Testcontainers séparé pour chaque adapter ;
- [ ] ne pas ajouter les dépendances Testcontainers au noyau si elles peuvent
  rester dans des packages optionnels ;
- [ ] permettre à un projet de ne tester que le moteur qu'il utilise en
  production ;
- [ ] rendre explicite l'absence de Docker au lieu d'ignorer silencieusement un
  test demandé comme obligatoire.

Des packages optionnels pourront être envisagés :

- `AventusSharp.Test.Testcontainers`;
- `AventusSharp.Test.NUnit`;
- `AventusSharp.Test.xUnit`;
- `AventusSharp.Test.MSTest`.

Ils ne doivent être créés que si leur valeur dépasse le coût de maintenance.

## Création de données de test

- [ ] fournir des helpers pour créer un objet ou une liste via les vraies API
  AventusSharp ;
- [ ] permettre de construire un graphe avec ses relations ;
- [ ] retourner les objets créés avec leurs identifiants ;
- [ ] agréger toutes les erreurs de création ;
- [ ] prendre en charge un `CancellationToken` si les API principales le
  permettent ;
- [ ] permettre l'enregistrement de factories propres au projet ;
- [ ] éviter les générateurs aléatoires implicites qui rendent les tests
  non déterministes ;
- [ ] fournir une graine explicite si un générateur de valeurs est ajouté ;
- [ ] nettoyer les données créées dans l'ordre inverse des dépendances.

Exemple d'objectif d'utilisation :

```csharp
await using var environment = await AventusTestEnvironment
    .CreateSqlite()
    .RegisterModelsFromAssembly(typeof(Device).Assembly)
    .Initialize();

Device device = await environment.Data.Create(
    new Device { Name = "Kitchen light" });
```

La forme exacte devra suivre les conventions finales de `ResultWithError`.

## Assertions sur les résultats

Le noyau doit rester indépendant de NUnit, xUnit et MSTest. Il peut exposer des
méthodes d'extraction destinées aux tests :

- [ ] obtenir le résultat ou lever une exception de test contenant toutes les
  erreurs formatées ;
- [ ] vérifier la présence d'un `DataErrorCode` ;
- [ ] récupérer toutes les erreurs d'un type donné ;
- [ ] produire un message déterministe et lisible ;
- [ ] conserver l'exception interne et sa stack trace ;
- [ ] prendre en charge `ResultWithError<T>` et `VoidWithError` ;
- [ ] ne pas masquer un résultat partiel lorsqu'il accompagne des erreurs.

Les extensions propres à un framework pourront ensuite proposer une syntaxe
native, par exemple `AssertSuccess`, sans l'imposer au package principal.

## État global, cache et événements

- [ ] fournir un moyen supporté de vider le cache de tous les DM ;
- [ ] fournir un moyen de vider le cache d'un type précis ;
- [ ] vérifier qu'aucune instance mise en cache ne fuit entre deux tests ;
- [ ] permettre de choisir `preferLocalCache` dans la fixture ;
- [ ] fournir un abonnement d'événement jetable ;
- [ ] enregistrer les événements CRUD observés pendant un test ;
- [ ] garantir la désinscription lors du `Dispose` ;
- [ ] documenter les précautions lorsque plusieurs fixtures AventusSharp
  s'exécutent en parallèle.

## Vérification des modèles du projet consommateur

Fournir des diagnostics ciblés qui vérifient uniquement la configuration du
projet :

- [ ] tous les modèles attendus sont enregistrés ;
- [ ] chaque modèle possède un DM valide ;
- [ ] les noms SQL sont uniques ;
- [ ] les relations pointent vers des modèles enregistrés ;
- [ ] les reverse links sont résolus ;
- [ ] les scopes peuvent être construits ;
- [ ] les `SqlTransform` déclarés sont compatibles avec le type du membre ;
- [ ] la génération du schéma termine sans erreur ;
- [ ] les erreurs indiquent le modèle et le membre concernés.

Ces diagnostics ne doivent pas dupliquer les tests génériques déjà présents
dans AventusSharp.

## Transactions

- [ ] fournir un helper exécutant un scénario dans une transaction toujours
  annulée à la fin ;
- [ ] prendre en charge les erreurs retournées par la callback ;
- [ ] garantir le rollback en cas d'exception ;
- [ ] vérifier le comportement des transactions imbriquées ;
- [ ] restaurer aussi l'état du cache lorsque la transaction est annulée ;
- [ ] documenter les limites propres à SQLite en mémoire.

## Documentation à fournir

- [ ] installation du package ;
- [ ] fixture minimale ;
- [ ] exemple NUnit ;
- [ ] exemple xUnit ;
- [ ] création et nettoyage de données ;
- [ ] configuration avec ou sans cache ;
- [ ] utilisation d'une base SQLite temporaire ;
- [ ] utilisation optionnelle de Docker/Testcontainers ;
- [ ] test des erreurs avec `ResultWithError` ;
- [ ] distinction entre test unitaire, test d'intégration du projet et tests
  internes d'AventusSharp ;
- [ ] liste des garanties et des non-garanties du package.

## Tests du package AventusSharp.Test

- [ ] initialisations successives sans fuite d'état ;
- [ ] nettoyage après succès, erreur et exception ;
- [ ] isolation de deux fixtures ;
- [ ] génération automatique du schéma ;
- [ ] remise à zéro des données avec clés étrangères ;
- [ ] cache activé et désactivé ;
- [ ] désinscription des événements ;
- [ ] rollback automatique ;
- [ ] factories déterministes ;
- [ ] messages d'erreur exploitables ;
- [ ] compatibilité avec une exécution parallèle, ou rejet explicite si elle
  n'est pas supportée ;
- [ ] empaquetage et utilisation depuis un projet externe minimal.

## Critères pour envisager un faux DM

Ne créer un faux DM que si au moins un besoin concret apparaît, par exemple :

- simuler de manière déterministe une panne de stockage ;
- forcer une erreur précise difficile à produire avec SQLite ;
- vérifier les appels effectués par un service sans initialiser AventusSharp ;
- tester un service métier totalement isolé de l'infrastructure.

Dans ce cas, préférer un fake configurable et limité :

- réponses CRUD configurables ;
- journal des appels ;
- injection d'erreurs ;
- aucune traduction de lambda ;
- aucune promesse de compatibilité SQL.

Le faux DM devra être présenté comme un outil d'isolation métier, pas comme une
base de données AventusSharp en mémoire.

## Ordre d'implémentation proposé

1. API publique de réinitialisation de l'état global et des caches.
2. Projet NuGet `AventusSharp.Test`.
3. `AventusTestEnvironment` et cycle de vie asynchrone.
4. environnement SQLite temporaire avec génération du schéma ;
5. nettoyage fiable des données ;
6. helpers de création de données ;
7. extraction et formatage des erreurs ;
8. gestion jetable des événements ;
9. helpers de transaction avec rollback ;
10. diagnostics des modèles du projet ;
11. documentation et projet consommateur d'exemple ;
12. éventuels packages d'intégration aux frameworks ou à Testcontainers ;
13. fake DM uniquement si son besoin est confirmé.

# Stratégie de test AventusSharp

## Commande principale

```powershell
dotnet test AventusSharpTest\AventusSharpTest.csproj
```

Avec couverture :

```powershell
dotnet test AventusSharpTest\AventusSharpTest.csproj --collect:"XPlat Code Coverage"
```

Tests conteneurisés :

```powershell
dotnet test AventusSharpTest\AventusSharpTest.csproj --filter "Category=Docker"
```

La suite utilise [docker-compose.databases.yml](../AventusSharpTest/docker-compose.databases.yml)
pour démarrer MySQL, PostgreSQL et SQL Server avec des ports hôte dynamiques. Les
conteneurs et volumes sont supprimés à la fin de l’exécution.

Sur une machine sans Docker, ces tests sont ignorés. En CI, rendre Docker obligatoire :

```powershell
$env:AVENTUS_REQUIRE_DOCKER = "1"
dotnet test AventusSharpTest\AventusSharpTest.csproj --filter "Category=Docker"
```

## Organisation

Un seul projet contient toutes les catégories de tests :

- `Tools` : résultats typés, erreurs, extensions et conversions ;
- `Data` : types temporels et intégration SQLite ;
- `Integration` : modèle vers schéma, CRUD, cache d’identité, transactions, builders et diagrammes ;
- `Routes` : réponses HTTP, découverte, binding et exécution du middleware ;
- `WebSocket` : découverte des endpoints et métadonnées de routage ;
- `SSE` : enregistrement et ouverture du flux ;
- `Scheduler` : cron, registres et exécution de jobs.

Les tests unitaires ne dépendent ni du réseau, ni d’un compte, ni d’un serveur externe. Les tests de stockage utilisent un fichier SQLite local réinitialisé avant chaque test.

## Matrice fonctionnelle

| Domaine | Niveau actuel | Suite recommandée |
|---|---|---|
| Résultats/erreurs | Unitaire | Maintenir les contrats de court-circuit et conversion |
| Extensions/outils | Unitaire partiel | Ajouter CSV, configuration et conversion JSON |
| Date/Datetime | Unitaire | Ajouter cas de fuseau et sérialisation |
| Scheduler/Cron | Unitaire et exécution | Ajouter toutes les unités calendaires et la non-réentrance concurrente |
| SQLite bas niveau | Intégration | Ajouter paramètres volumineux et concurrence |
| Data manager/CRUD | Intégration simple | Ajouter relations 1-N/N-M, héritage, scopes et validations |
| Cache local | Intégration | Ajouter relations et mises à jour concurrentes |
| Builders/lambdas | Matrice SQLite partielle | Répliquer la génération SQL et les cas limites sur chaque moteur |
| Migrations | À compléter | Création, renommage, ajout/suppression de colonnes |
| HTTP | Middleware en mémoire | Ajouter multipart, injection, middlewares et erreurs |
| WebSocket | Découverte/routage | Ajouter une vraie socket locale, événements et déconnexion |
| SSE | Ouverture de flux | Ajouter abonnement, topics, broadcast et déconnexion |
| Vues/ressources | À compléter | Scriban, cache, chemins et types MIME |
| CSV import/export | À compléter | Culture, mapping, erreurs et round-trip |
| Images/fichiers | À compléter | Validation, redimensionnement et nettoyage |
| Diagrammes/export | À compléter | Snapshots déterministes |
| MySQL/PostgreSQL/MSSQL | Optionnel CI | Tests conteneurisés explicitement activés |

## Règles pour les nouveaux tests

- Nommer les tests par comportement observable.
- Tester l’API publique; n’utiliser la réflexion que pour un contrat de métadonnées.
- Un test doit créer et nettoyer son propre état.
- Ne pas dépendre de l’ordre des tests.
- Désactiver le parallélisme uniquement autour d’un état statique global documenté.
- Pour un bug, ajouter d’abord une régression minimale puis corriger la bibliothèque.
- Les tests propres à un moteur externe doivent être marqués et ignorés par défaut quand sa variable de connexion est absente.
- Ne jamais utiliser une base de développement personnelle.


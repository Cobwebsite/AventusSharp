# Stratégie de test AventusSharp

## Commande principale

```powershell
dotnet test AventusSharpTest\AventusSharpTest.csproj
```

Avec couverture :

```powershell
dotnet test AventusSharpTest\AventusSharpTest.csproj --collect:"XPlat Code Coverage"
```

## Organisation

Un seul projet contient toutes les catégories de tests :

- `Tools` : résultats typés, erreurs, extensions et conversions ;
- `Data` : types temporels et intégration SQLite ;
- `Routes` : réponses HTTP et normalisation des attributs ;
- `Scheduler` : construction et validation des expressions cron.

Les tests unitaires ne dépendent ni du réseau, ni d’un compte, ni d’un serveur externe. Les tests de stockage utilisent un fichier SQLite local réinitialisé avant chaque test.

## Matrice fonctionnelle

| Domaine | Niveau actuel | Suite recommandée |
|---|---|---|
| Résultats/erreurs | Unitaire | Maintenir les contrats de court-circuit et conversion |
| Extensions/outils | Unitaire partiel | Ajouter CSV, configuration et conversion JSON |
| Date/Datetime | Unitaire | Ajouter cas de fuseau et sérialisation |
| Scheduler/Cron | Unitaire | Ajouter calendrier, unités et exécution de jobs |
| SQLite bas niveau | Intégration | Ajouter transactions, paramètres et concurrence |
| Data manager/CRUD | À compléter | Fixtures SQLite par modèle, relations 1-N/N-M, héritage |
| Builders/lambdas | À compléter | Matrice d’expressions traduites pour chaque moteur |
| Migrations | À compléter | Création, renommage, ajout/suppression de colonnes |
| HTTP | Unitaire partiel | Hôte ASP.NET en mémoire, binding JSON/form-data, middleware |
| WebSocket | À compléter | Connexion locale, routage, événements et erreurs |
| SSE | À compléter | Abonnement, topics, broadcast et déconnexion |
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

## Défauts détectés pendant la refonte

- Les anciens projets de test ciblaient .NET 8 alors que la bibliothèque cible .NET 10.
- L’ancienne suite exigeait deux bases MySQL locales et contenait beaucoup de code commenté.
- La solution référençait un projet `Test` inexistant.
- La fin d’une transaction disposait la transaction avant de mémoriser sa connexion, empêchant sa libération correcte.
- Le package stable dépend actuellement d’une préversion de `Microsoft.Data.Sqlite`, ce qui produit `NU5104`.


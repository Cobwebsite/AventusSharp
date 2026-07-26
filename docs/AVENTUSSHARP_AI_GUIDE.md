# AventusSharp — guide d’utilisation pour une IA

Ce document décrit les conventions à respecter lorsqu’une IA génère du code avec AventusSharp. La source de vérité reste le code C# et les tests du dépôt.

## 1. Initialisation d’une application

AventusSharp expose quatre middlewares indépendants :

- `UseAventusData` enregistre les modèles et leurs data managers ;
- `UseAventusHttp` découvre les classes dérivées de `Router` ;
- `UseAventusWebsocket` découvre les endpoints, routes et événements WebSocket ;
- `UseAventusSSE` découvre les endpoints et événements Server-Sent Events.

Dans une application ASP.NET Core, appeler uniquement les middlewares nécessaires, après `builder.Build()` et avant `app.Run()`.

```csharp
using AventusSharp;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseAventusHttp(config =>
{
    config.PrintRoute = app.Environment.IsDevelopment();
    config.PrintTrigger = app.Environment.IsDevelopment();
});

app.Run();
```

Ne pas appeler directement `RouterMiddleware.Register()` ou `OnRequest` dans une application normale : les méthodes d’extension gèrent l’initialisation et la propagation des erreurs.

## 2. Résultats et erreurs

Les opérations pouvant échouer retournent généralement :

- `VoidWithError` ou `VoidWithError<TError>` ;
- `ResultWithError<TResult>` ou `ResultWithError<TResult, TError>`.

Toujours vérifier `Success` avant d’utiliser `Result`. Conserver les erreurs d’origine au lieu de les remplacer par une exception générique.

```csharp
ResultWithError<List<Dictionary<string, string?>>> result =
    await storage.Query("SELECT id, name FROM sample");

if (!result.Success || result.Result is null)
{
    foreach (GenericError error in result.Errors)
        logger.LogError("{Code}: {Message}", error.Code, error.Message);
    return;
}
```

Les méthodes `Run`, `RunAsync`, `Extract` et `ExtractAsync` construisent un pipeline court-circuité : après la première erreur, les étapes suivantes ne sont pas exécutées.

## 3. Modèles persistants

Un modèle concret hérite normalement de `Storable<TSelf>`.

```csharp
public sealed class Todo : Storable<Todo>
{
    [Size(1, 200)]
    public string Title { get; set; } = "";

    public bool Completed { get; set; }
}
```

Règles :

- utiliser le type concret comme paramètre générique (`Todo : Storable<Todo>`) ;
- initialiser les propriétés non nullables ;
- utiliser les attributs AventusSharp pour exprimer le schéma et la validation ;
- ne pas masquer `Id`, `CreatedDate` ou `UpdatedDate` ;
- réserver `[NotInDB]` aux données calculées ou purement applicatives ;
- utiliser `[Nullable]` pour une colonne/lien nullable selon le modèle AventusSharp ;
- utiliser `[AutoCreate]`, `[AutoUpdate]`, `[AutoDelete]` ou `[AutoCRUD]` uniquement quand le cycle de vie de l’objet lié appartient réellement au parent ;
- utiliser `[DeleteOnCascade]` avec prudence : la suppression devient transitive.

Attributs importants :

- structure : `Primary`, `AutoIncrement`, `SqlName`, `Default`, `Nullable`, `NotInDB`;
- relations : `ForeignKey`, `ReverseLink`, `AutoCUD`/`AutoCRUD`, `DeleteOnCascade`;
- validation : `Size`, `Unique`;
- sélection : `Storage<TStorage>`, `Scope<TScope>`;
- héritage : `ForceInherit`, `CreateObject`, `CreateTable`.

## 4. Stockages

Implémentations : `SqliteStorage`, `MySQLStorage`, `PostgreSqlStorage` et `MsSqlStorage`.

Pour les tests et exemples reproductibles, préférer SQLite avec un fichier temporaire. Ne jamais intégrer d’identifiants réels dans le code ou les tests.

```csharp
var storage = new SqliteStorage(databasePath);
VoidWithError connection = await storage.ConnectWithError();
if (!connection.Success)
    throw connection.Errors[0].GetException();
```

Utiliser `RunInsideTransaction` pour grouper des opérations. Le callback est validé si son résultat réussit et annulé s’il contient une erreur.

## 5. Requêtes et CRUD

Les APIs existent sous deux formes :

- méthodes pratiques sur `Storable<T>` / les modèles ;
- builders (`StartQuery`, create/update/delete/exist builders) pour sélectionner champs, filtres, tris, groupes et relations.

Bonnes pratiques :

- préférer les expressions lambda aux fragments SQL ;
- utiliser les méthodes `*WithError` lorsqu’il faut remonter le diagnostic ;
- ne pas lire `Result` quand `Success == false` ;
- limiter les champs et relations chargés sur les requêtes volumineuses ;
- traiter explicitement les listes vides et les résultats absents ;
- entourer les mutations liées d’une transaction.

Avec `preferLocalCache`, un identifiant correspond à une instance canonique :
les lectures par identifiant, filtres et `GetAll` doivent partager cette même
référence. `BulkCreateWithError(values, withId: true)` enregistre donc les
objets fournis comme instances canoniques. Leurs membres `SqlTransform` sont
normalisés comme après une lecture SQL, tandis que leurs propriétés `[NotInDB]`
sont conservées. Un rollback retire ces objets du cache et restaure les valeurs
qui avaient été normalisées.

Sans `withId`, un bulk ne connaît pas nécessairement les identifiants générés.
Le cache complet est invalidé et la lecture suivante rematérialise les lignes
avec leurs identifiants réels.

Les appels successifs a `Where` sont combines avec `AND`. `OrWhere` combine
le filtre deja construit avec `OR`. La composition est effectuee de gauche a
droite :

```csharp
Todo.StartQuery()
    .Where(todo => todo.Completed)
    .OrWhere(todo => todo.Title.Contains("urgent"))
    .Where(todo => todo.Id > 10);
// (Completed OR Title.Contains("urgent")) AND Id > 10
```

Un scope est toujours combine avec le groupe utilisateur complet :
`Scope AND (filtres utilisateur)`.

## 6. Routes HTTP

Une route AventusSharp est une classe dérivée de `Router`, généralement décorée par `[Prefix]`. Une méthode porte un attribut HTTP (`[Get]`, `[Post]`, `[Put]`, `[Delete]`, `[Options]`) et `[Path]`.

```csharp
[Prefix("/api/todos")]
public sealed class TodoRouter : Router
{
    [Get]
    [Path("/{id}")]
    public IResponse GetById(int id)
    {
        return new Json(new { Id = id });
    }
}
```

Retourner un `IResponse` explicite :

- `Json` pour JSON ;
- `TextResponse` pour du texte ;
- `ByteResponse`, `StreamResponse` ou `Resource` pour du contenu binaire ;
- `Redirect` pour une redirection ;
- `NoResponse` quand aucune écriture n’est nécessaire ;
- `View`/`ViewDynamic` pour Scriban.

Le JSON par défaut utilise `TypeNameHandling.Auto` et peut donc produire `$type`. Ne jamais désérialiser un JSON non fiable avec une configuration autorisant librement les noms de types ; définir des `JSONSettings` plus restrictifs pour une API publique si les métadonnées ne sont pas requises.

## 7. WebSocket et SSE

- WebSocket : dériver de `WsEndPoint`, `WsRouter` et `WebSocketEvent`/`WsEvent<T>`.
- SSE : dériver de `SSEEndPoint` et `SSEEvent<T>`/`SSEEmptyEvent`.
- Utiliser les attributs `Prefix`, `Path`, `EndPoint`, `ListenOnBoot` et `ResponseType` du namespace correspondant.
- Ne pas mélanger les attributs HTTP, WebSocket et SSE : plusieurs portent le même nom mais appartiennent à des namespaces différents.
- Vérifier le résultat de `Emit()`/`EmitTo<T>()`; un endpoint absent est signalé dans les erreurs.

## 8. Scheduler

`CronBuilder` produit une expression à six champs :

```text
seconde minute heure jour-du-mois mois jour-de-la-semaine
```

Exemple :

```csharp
string cron = new CronBuilder()
    .Second(0)
    .EachMinutes(5)
    .ToString(); // "0 */5 * * * *"
```

Les valeurs sont validées, triées et dédupliquées. Ne pas injecter une expression utilisateur sans validation.

## 9. Types et outils

- `Date` sérialise au format `yyyy-MM-dd` et compare l’égalité au jour.
- `Datetime` sérialise au format `yyyy-MM-dd HH-mm-ss` et compare l’égalité à la seconde.
- `CSVMapper<T>`, `CSVImporter<T>` et `CSVExporter<T>` utilisent CsvHelper et doivent recevoir une culture explicite.
- `AutoConfiguration` et `ConfigurationExtension.Read<T>` chargent la configuration .NET puis appliquent les propriétés/champs décorés par `[EnvName]`.
- Les générateurs de diagrammes et d’exports sont déclenchés par `--db-diagram` et `--export-info`.

## 10. Checklist avant de livrer du code généré

1. Le projet cible la même version .NET que la bibliothèque.
2. Aucun secret ni chemin machine n’est codé en dur.
3. Chaque résultat AventusSharp est contrôlé avant lecture.
4. Les namespaces des attributs correspondent au transport utilisé.
5. Les modèles non nullables sont initialisés.
6. Les effets de cascade et `AutoCRUD` sont voulus.
7. Les tests utilisent SQLite ou des doubles, jamais une base réseau implicite.
8. `dotnet test AventusSharpTest/AventusSharpTest.csproj` passe.

# Utiliser AventusSharp Data dans une application MAUI Android

Une application MAUI Android peut utiliser le moteur Data et SQLite sans référencer
`Microsoft.AspNetCore.App` ni les pilotes SQL destinés au serveur.

## Références

Pendant le développement dans cette solution :

```xml
<ItemGroup>
  <ProjectReference Include="..\AventusSharp.Data.Sqlite\AventusSharp.Data.Sqlite.csproj" />
  <ProjectReference Include="..\AventusSharp.Maui\AventusSharp.Maui.csproj" />
</ItemGroup>
```

Après publication, l'application choisit de la même façon son hôte
`AventusSharp.Maui` et son provider `AventusSharp.Data.Sqlite`. Core, Data et le
runtime sont obtenus transitivement, mais restent référençables directement pour
les usages avancés.

## Modèle et stockage local

```csharp
using AventusSharp.Data;
using AventusSharp.Data.Manager.DB;
using AventusSharp.Data.Storage.Sqlite;

public sealed class TodoItem : Storable<TodoItem>
{
    public string Title { get; set; } = string.Empty;
    public bool Completed { get; set; }
}

string databasePath = Path.Combine(FileSystem.AppDataDirectory, "aventus.db");
var storage = new SqliteStorage(databasePath);

DataMainManager.Configure(config =>
{
    config.defaultStorage = storage;
    config.defaultDM = typeof(SimpleDatabaseDM<>);
    config.AutoCreateModel = true;
});

var initialization = await DataMainManager.Init(typeof(TodoItem).Assembly);
if (!initialization.Success)
{
    throw new InvalidOperationException(string.Join(
        Environment.NewLine,
        initialization.Errors.Select(error => error.Message)));
}
```

L'initialisation doit être exécutée une seule fois au démarrage de l'application.
Le chemin est placé dans `FileSystem.AppDataDirectory`, qui est accessible en écriture
sur Android.

## Routeur local

`AventusSharp.Maui` fournit `AventusMauiBridge`. Il crée un contexte portable par
requête, appelle le même dispatcher et restitue le statut, les en-têtes, le type de
contenu et le corps issus de `AventusSharp.Routes.Response.IResponse`. Il n'émule pas
un `HttpContext`.

Le projet `AventusSharp.Maui.AndroidSmoke` constitue le garde-fou de compilation
Android. La suite `AventusSharp.Data.PortableTest` exécute les tests SQLite sur une
base réelle sans charger les fournisseurs SQL Server, MySQL ou PostgreSQL.

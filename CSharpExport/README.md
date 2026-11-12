# CSharpExport

Le générateur exporte les `IStorable`, les classes `[Export]`, les enums, les classes `IGenericError` et les classes `IWithError` dans une bibliothèque indépendante d'AventusSharp. Il conserve leurs noms, namespaces et propriétés publiques, y compris les propriétés héritées comme `Id`. Les attributs AventusSharp (`SqlName`, `Size`, `Primary`, etc.) sont retirés ; les attributs externes sont conservés. `[NoExport]` exclut une classe, une propriété ou une valeur d'enum. Les méthodes, champs, constructeurs et corps des propriétés ne sont pas exportés. Une propriété dont le type dépend d'AventusSharp provoque une erreur explicite.

```powershell
dotnet run --project CSharpExport/CSharpExport.csproj -- CSharpExport.TestFixture/export.json
dotnet run --project CSharpExport.TestConsumer/CSharpExport.TestConsumer.csproj
```

Le fichier JSON accepte `csProj`, `outputPath`, `assemblyName`, `targetFramework`, `packages`, `exportEnumByDefault`, `exportErrorsByDefault` et `exportHttpRouteByDefault`. Les chemins sont relatifs au JSON. Le framework cible est lu dans le projet source, sauf si `targetFramework` est défini. Les enums sont exportées sur `[Export]` par défaut ; `exportEnumByDefault` permet de toutes les exporter. Les classes d'erreur et les routes HTTP sont exportées par défaut, sauf `[NoExport]`. `packages` contient des entrées `{ "id": "Newtonsoft.Json", "version": "13.0.4" }` qui deviennent des `PackageReference` dans le projet généré. Le générateur écrit les fichiers sous les mêmes chemins relatifs que dans le projet source et compile la DLL dans `outputPath/bin/Debug/<targetFramework>`. Le suffixe `.g.cs` indique simplement un fichier C# généré.

## Routes HTTP

Les routes HTTP proviennent des routes enregistrees au demarrage du projet source. Les classes `IRouter` deviennent des clients `HttpClient` avec les mêmes noms de classes et de méthodes. Les méthodes retournent `Task<HttpResult<T>>` : `StatusCode`, `Value`, `RawText`, `Success` et `Errors` restent disponibles sans dépendance à AventusSharp. `ResultWithError<T>` est déballé dans `Value`; `VoidWithError` conserve son état et ses erreurs. Les réponses texte, binaires, JSON et vides sont prises en charge. Les routes de vue ne produisent pas de méthode client. Le client utilise `Newtonsoft.Json` lorsque ce package est présent pour respecter notamment `[JsonProperty]`, et `System.Text.Json` sinon.

Le générateur tient compte de `[Get]`, `[Post]`, `[Put]`, `[Delete]`, `[Options]`, `[Path]`, `[PathRegex]`, `[Prefix]`, `[FctName]`, `[NoRoute]`, `[NoExport]`, `[AddFormData]`. Plusieurs chemins et verbes produisent plusieurs méthodes. Les paramètres du chemin sont encodés ; les autres forment le corps. Les paramètres de contexte et les types Microsoft injectés par le serveur sont écartés. Les méthodes héritées de `StorableRouter<T>` sont exportées avec le type concret. Une route regex reçoit un `routePath` explicite. Une fonction de segment dynamique non constante devient un paramètre `dynamicNom`. `HttpFile` devient `CSharpExport.Http.HttpUpload` et déclenche l'envoi multipart, y compris dans une liste ou un objet imbriqué.

```json
{
  "exportHttpRouteByDefault": true,
  "httpRouter": {
    "host": "https://example.com",
    "uri": "/api",
    "createRouter": false,
    "routerName": "GeneratedRouter",
    "namespace": "Routes",
    "parent": "HttpRouteClient",
    "parentFile": null
  }
}
```

Lorsqu'au moins un routeur HTTP est exporte, le generateur lance le projet source avec `--export-info`. Le projet source doit appeler `UseAventusExport()` apres l enregistrement des routes. Aucun fichier de routes n est requis dans la configuration. `host` et `uri` definissent l URL par defaut ; le constructeur accepte aussi un `HttpClient` et une `baseUrl`.

L'export des routes HTTP, des routes WebSocket et des événements WebSocket sera ajouté par étapes.

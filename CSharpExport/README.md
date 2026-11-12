# CSharpExport

Le générateur exporte les `IStorable`, les classes `[Export]`, les enums, les classes `IGenericError` et les classes `IWithError` dans une bibliothèque indépendante d'AventusSharp. Il conserve leurs noms, namespaces et propriétés publiques, y compris les propriétés héritées comme `Id`. Les attributs AventusSharp (`SqlName`, `Size`, `Primary`, etc.) sont retirés ; les attributs externes sont conservés. `[NoExport]` exclut une classe, une propriété ou une valeur d'enum. Les méthodes, champs, constructeurs et corps des propriétés ne sont pas exportés. Une propriété dont le type dépend d'AventusSharp provoque une erreur explicite.

```powershell
dotnet run --project CSharpExport/CSharpExport.csproj -- chemin/vers/export.json
```

Le fichier JSON accepte `csProj`, `outputPath`, `assemblyName`, `targetFramework`, `packages`, `exportEnumByDefault`, `exportErrorsByDefault` et `exportHttpRouteByDefault`. Les chemins sont relatifs au JSON. Le framework cible est lu dans le projet source, sauf si `targetFramework` est défini. Les enums sont exportées sur `[Export]` par défaut ; `exportEnumByDefault` permet de toutes les exporter. Les classes d'erreur et les routes HTTP sont exportées par défaut, sauf `[NoExport]`. `packages` contient des entrées `{ "id": "Newtonsoft.Json", "version": "13.0.4" }` qui deviennent des `PackageReference` dans le projet généré. Le générateur écrit les fichiers sous les mêmes chemins relatifs que dans le projet source et compile la DLL dans `outputPath/bin/Debug/<targetFramework>`. Le suffixe `.g.cs` indique simplement un fichier C# généré.

Les types techniques générés utilisent les namespaces `<assemblyName>.Http` et `<assemblyName>.Sse`. Les classes métier conservent leurs namespaces source. Dans un fichier `parentFile`, le marqueur `__RUNTIME_NAMESPACE__` est remplacé par `assemblyName`.

## Routes HTTP

Les routes HTTP proviennent des routes enregistrees au demarrage du projet source. Les classes `IRouter` deviennent des clients `HttpClient` avec les mêmes noms de classes et de méthodes. Les méthodes retournent `Task<HttpResult<T>>` : `StatusCode`, `Value`, `RawText`, `Success` et `Errors` restent disponibles sans dépendance à AventusSharp. `ResultWithError<T>` est déballé dans `Value`; `VoidWithError` conserve son état et ses erreurs. Les réponses texte, binaires, JSON et vides sont prises en charge. Les routes de vue ne produisent pas de méthode client. Le client utilise `Newtonsoft.Json` lorsque ce package est présent pour respecter notamment `[JsonProperty]`, et `System.Text.Json` sinon.

Le générateur tient compte de `[Get]`, `[Post]`, `[Put]`, `[Delete]`, `[Options]`, `[Path]`, `[PathRegex]`, `[Prefix]`, `[FctName]`, `[NoRoute]`, `[NoExport]`, `[AddFormData]`. Plusieurs chemins et verbes produisent plusieurs méthodes. Les paramètres du chemin sont encodés ; les autres forment le corps. Les paramètres de contexte et les types Microsoft injectés par le serveur sont écartés. Les méthodes héritées de `StorableRouter<T>` sont exportées avec le type concret. Une route regex reçoit un `routePath` explicite. Une fonction de segment dynamique non constante devient un paramètre `dynamicNom`. `HttpFile` devient `<assemblyName>.Http.HttpUpload` et déclenche l'envoi multipart, y compris dans une liste ou un objet imbriqué.

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

## SSE

Les classes `ISSEEndPoint` et `ISSEEvent` sont exportées par défaut, sauf `[NoExport]`. Elles gardent leurs noms et namespaces. Les endpoints générés exposent `Path` et `ListenAsync()` ; les événements générés exposent un flux typé `ListenAsync()`, filtré par leur topic. Les deux utilisent le protocole SSE d'AventusSharp (`channel` et `data`) sans référencer AventusSharp dans la DLL. Le générateur lit les chemins et topics constants retournés par `DefinePath()` et `GetTopic()`. Pour un topic calculé, passer `topic` au constructeur de l'événement. Pour un chemin calculé, passer `path` au constructeur de l'endpoint.

La section `sse` de `export.json` accepte `host` et `uri` pour former l'URL de base, `autoReconnect` (activé par défaut) et `reconnectDelayMs` (1 000 ms par défaut). Le client rouvre le flux après une fermeture ou une erreur réseau temporaire ; il ne réessaie pas après une erreur HTTP permanente 4xx. Le constructeur de l'endpoint accepte aussi un `HttpClient`, une `baseUrl`, `autoReconnect` et `reconnectDelay`, et ces deux derniers réglages restent modifiables sur l'instance. L'annulation du `CancellationToken` interrompt aussi le délai de reconnexion. Un événement choisit l'endpoint indiqué par `[EndPoint<T>]`, l'endpoint principal ou l'unique endpoint exporté ; sinon, il faut lui fournir un endpoint. Une classe de charge utile définie dans le projet source est exportée automatiquement, même sans `[Export]`, y compris ses types imbriqués et les classes utilisées par ses propriétés publiques.

```csharp
var updates = new ProfileEvent();
await foreach (var profile in updates.ListenAsync(cancellationToken))
    Console.WriteLine(profile?.Label);
```

## WebSocket

Les classes `IWsEndPoint`, `IWsRouter` et `IWebSocketEvent` sont exportées par défaut, sauf `[NoExport]`. Les options `exportWsEndPointByDefault`, `exportWsRouteByDefault` et `exportWsEventByDefault` contrôlent ces exports. Les classes gardent leurs noms et namespaces. Les routeurs générés utilisent les chemins et endpoints réellement enregistrés par le serveur avec `UseAventusWebsocket()` puis `UseAventusExport()`. Une application qui exporte des routes HTTP et WS n'est lancée qu'une fois pour collecter les deux listes.

Les méthodes de route qui attendent une réponse retournent `Task<WsResult<T>>` : la réponse est associée à la requête par `uid`, et `Success`, `Value`, `Errors`, `RawText` et `Channel` sont disponibles. Les routes `[Others]` et `[Custom]` retournent `Task`, car l'appelant ne reçoit pas nécessairement la diffusion. Les surcharges, les chemins multiples, les paramètres URL/corps, les méthodes héritées, les réponses vides et les réponses `ResultWithError<T>` sont pris en charge. Un routeur peut utiliser plusieurs endpoints ; chaque méthode choisit celui indiqué dans les routes compilées, ou l'endpoint passé en argument. Les types de charge utile des événements sont exportés automatiquement, y compris les classes imbriquées.

La section `ws` de `export.json` accepte `host` (par exemple `ws://localhost:5000`), `uri`, `autoReconnect` (activé par défaut), `reconnectDelayMs` (1 000 ms par défaut) et `requestTimeoutMs` (30 000 ms par défaut). Les constructeurs des endpoints et routeurs permettent de remplacer ces délais avec `reconnectDelay` et `requestTimeout`. Leur argument `configureWebSocket` configure les options de chaque connexion, y compris les en-têtes et cookies du handshake, et s'applique aussi aux reconnexions. Les abonnements `ListenAsync()` se reconnectent après une coupure. Une requête interrompue échoue et n'est pas rejouée automatiquement. Les routeurs et endpoints implémentent `IAsyncDisposable` pour fermer leurs connexions. Le `CancellationToken` arrête les abonnements et les attentes de réponse. Pour un événement `[Path]` contenant `[Segment]`, une méthode qui retourne une constante est résolue à l'export ; sinon le constructeur généré accepte une fonction `Segment` évaluée pour chaque message.

```csharp
await using var endpoint = new DemoEndPoint();
var news = new NewsEvent(endpoint);
await foreach (var item in news.ListenAsync(cancellationToken))
    Console.WriteLine(item?.Text);

await using var router = new DemoRouter();
var reply = await router.Echo(7, "hello", cancellationToken: cancellationToken);
```

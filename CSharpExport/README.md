# CSharpExport

Le générateur exporte les `IStorable`, les classes `[Export]`, les enums, les classes `IGenericError` et les classes `IWithError` dans une bibliothèque indépendante d'AventusSharp. Il conserve leurs noms, namespaces et propriétés publiques, y compris les propriétés héritées comme `Id`. Les attributs AventusSharp (`SqlName`, `Size`, `Primary`, etc.) sont retirés ; les attributs externes sont conservés. `[NoExport]` exclut une classe, une propriété ou une valeur d'enum. Les méthodes, champs, constructeurs et corps des propriétés ne sont pas exportés. Une propriété dont le type dépend d'AventusSharp provoque une erreur explicite.

```powershell
dotnet run --project CSharpExport/CSharpExport.csproj -- CSharpExport.TestFixture/export.json
dotnet run --project CSharpExport.TestConsumer/CSharpExport.TestConsumer.csproj
```

Le fichier JSON accepte `csProj`, `outputPath`, `assemblyName`, `packages`, `exportEnumByDefault` et `exportErrorsByDefault`. Les chemins sont relatifs au JSON. Les enums sont exportées sur `[Export]` par défaut ; `exportEnumByDefault` permet de toutes les exporter. Les classes d'erreur sont exportées par défaut, sauf `[NoExport]`. `packages` contient des entrées `{ "id": "Newtonsoft.Json", "version": "13.0.4" }` qui deviennent des `PackageReference` dans le projet généré. Le générateur écrit les fichiers sous les mêmes chemins relatifs que dans le projet source et compile la DLL dans `outputPath/bin/Debug/net10.0`. Le suffixe `.g.cs` indique simplement un fichier C# généré.

L'export des routes HTTP, des routes WebSocket et des événements WebSocket sera ajouté par étapes.

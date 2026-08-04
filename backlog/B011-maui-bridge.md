# B011 — Créer le bridge MAUI

## But

Héberger localement le routeur AventusSharp derrière un `BlazorWebView` sans simuler `HttpContext`.

## Travail

- Créer le contexte MAUI à partir de méthode, URI, headers et body.
- Créer et libérer un scope DI par requête.
- Exécuter le même routeur et le même `IResponse`.
- Convertir statut, headers, content type et flux vers `SetResponse`.
- Ajouter une façade `[JSInvokable]` optionnelle.

## Acceptation

- Le bridge ne référence aucun type ASP.NET Core.
- Les mêmes cas produisent les mêmes réponses sous ASP.NET Core et MAUI.
- Android et Windows sont testés pour le chemin WebView.

## Avancement

- Package `AventusSharp.Maui` créé sans référence ASP.NET Core.
- Bridge méthode/URI/query/headers/body vers `IAventusContext` implémenté.
- Statut, headers, content type et contenu sont restitués dans une réponse portable.
- Le bridge dépend de `IAventusRequestDispatcher`; le branchement au routeur réel reste à faire après extraction de sa résolution.

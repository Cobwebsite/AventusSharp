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


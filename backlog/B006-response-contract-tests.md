# B006 — Verrouiller le contrat des réponses

## But

Garantir que le routeur continue à répondre avec les types de `AventusSharp.Routes.Response`.

## Travail

- Couvrir `Json`, `TextResponse`, `ByteResponse`, `StreamResponse`, `Redirect`, `NoResponse` et les vues.
- Vérifier type retourné, statut, contenu, content type et headers.
- Couvrir route inexistante et erreur de parsing.

## Acceptation

- Les tests échouent si une route ne retourne plus son `IResponse` attendu.
- Le contenu écrit reste identique à la référence.


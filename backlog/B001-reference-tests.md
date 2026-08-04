# B001 — Établir la référence de tests

## But

Capturer le comportement actuel avant de déplacer du code.

## Travail

- Compiler la solution en Debug.
- Exécuter toute la suite `AventusSharpTest`.
- Relever les tests ignorés, en échec ou dépendants de Docker.
- Documenter la commande et le résultat de référence.

## Acceptation

- Le résultat initial est connu et reproductible.
- Aucun changement de production n'est effectué.

## État au 4 août 2026

- Commande : `dotnet test AventusSharpTest/AventusSharpTest.csproj --configuration Debug --no-restore`
- Total : 663 tests.
- Réussis : 598.
- Ignorés : 64, principalement les tests d'intégration nécessitant les moteurs SQL externes.
- Échec initial reproductible : `WebSocketRoutingTests.Middleware_opens_tracks_and_closes_an_accepted_connection`, ligne 375.
- Avertissement initial : `NU5104`, car le package stable dépend de `Microsoft.Data.Sqlite` en préversion.

L'échec WebSocket a été reproduit isolément avant toute modification du code de production. Il ne doit donc pas être considéré comme une régression du découpage.


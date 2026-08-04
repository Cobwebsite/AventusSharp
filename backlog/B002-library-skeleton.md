# B002 — Créer le squelette des bibliothèques

## But

Préparer la séparation physique sans déplacer ni modifier la logique existante.

## Travail

- Créer `AventusSharp.Core` sans référence ASP.NET Core.
- Préparer ensuite `AventusSharp.AspNetCore` et `AventusSharp.Maui` lorsqu'ils deviennent nécessaires.
- Ajouter les projets à la solution.
- Conserver `AventusSharp` et son API publique actuels.

## Acceptation

- La solution compile.
- Les tests de référence restent au même état.
- Aucun type existant n'a changé d'assembly à cette étape.


# Client SSE

Les tests exécutent les sources du client après transpilation TypeScript dans
un environnement isolé avec un EventSource simulé. Ils couvrent le partage de
connexion, les événements, la reconnexion, la fermeture et les erreurs.

Avec TypeScript installé et accessible à Node :

```powershell
node --test AventusJs/tests/sse-client.test.cjs
```

Si TypeScript est installé ailleurs, définir `AVENTUS_TYPESCRIPT_PATH` avec le
chemin de son module avant cette commande.

La vérification du projet complet s'effectue avec :

```powershell
av check AventusJs/aventus.conf.avt
```

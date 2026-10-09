# Importazione Obsidian tramite NuGet

Il comando **Importa vault Obsidian** crea una nuova mappa SQLite NODILUME,
senza riscrivere le note Markdown o modificare altre mappe.

## Dipendenze

NODILUME importa esclusivamente un NuGet versionato:

- ObsidianNotesKeeper.Vault 0.1.1, prodotto dal repository privato
  slim16165/obsidian-dup-note-finder
- ObsidianNotesKeeper.SyntaxHelper 0.1.1 viene risolto da NuGet come dipendenza
  transitiva (frontmatter YAML).
- Nessuna DLL binaria o copia del parser viene registrata in questo repository.

Feed: https://nuget.pkg.github.com/slim16165/index.json

**Accesso al feed**: i pacchetti GitHub Packages del repository produttore
possono essere privati. Configurare la sorgente NuGet autenticata sul
computer di sviluppo o sul runner con un token read:packages autorizzato.
Non inserire token in NuGet.Config, log, README o commit Git.
Per il runner Windows, registrare un secret GitHub Actions nel repository
NODILUME chiamato OBSIDIAN_PACKAGES_TOKEN (PAT con read:packages dell'account
Idiopathy-shh, autorizzato a leggere il pacchetto privato). Il workflow
.github/workflows/obsidian-import.yml usa questo secret; senza di esso
la verifica remota fallisce in modo esplicito, non utilizza un fallback
insicuro.

Per prove isolate, specificare un feed locale con i due .nupkg costruiti
nel repository ObsidianNotesKeeper; non serve copiare i loro sorgenti.
Esempio con feed già predisposto:

    dotnet restore tests/Nodilume.Tests/Nodilume.Tests.csproj --source /percorso/feed --source https://api.nuget.org/v3/index.json --force-evaluate
    dotnet run --project tests/Nodilume.Tests -c Release --no-restore

## Semantica di importazione

- Scansione Markdown in sola lettura (fino a 10.000 note, 2 MiB per nota;
  limite complessivo dell'importatore: 64 MiB di caratteri Markdown).
- Escluse directory .obsidian, .git, .trash, .graphify-input, .smart-env,
  node_modules e directory nascoste o collegate tramite symlink.
- Cartelle -> nodi gerarchici; nota -> Idea e Placement; contenuto originale
  Markdown conservato, percorso relativo e tag nell'annotazione.
- Wikilink [[Nota]] o [[cartella/Nota|alias]] -> Relation direzionale wikilink.
  Si risolvono percorsi esatti, collegamenti alla cartella corrente e nomi
  univoci. I collegamenti ambigui o inesistenti non vengono inventati.
- Ogni importazione crea una nuova mappa autonoma; non è sincronizzazione
  bidirezionale né sostituzione della mappa corrente.

Le sorgenti possono essere su Dropbox o su disco locale: il percorso viene
letto solo al momento dell'importazione. Per aprire nuove versioni delle note
occorre ripetere il comando, creando un'altra mappa.

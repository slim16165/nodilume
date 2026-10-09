# Nodilume

Desktop Windows per esplorare mappe di idee come grafi 3D multiscala.

## Stato

GRAPH.00-04 integrate (GRAPH.04 PR #5; main@0f6bdac). GRAPH.05 PR #6 e'
chiusa come DRAFT storica e conflittuale, senza merge. I suoi contratti
funzionali sono stati portati sulla base corrente in GRAPH.06.08 (PR #21,
squash 52b9ade), senza trasferirle retroattivamente il PASS.
GRAPH.06.01 e' stata integrata tramite PR #7, squash c515f79: codec JSON
portabile del grafo, senza interfaccia editor completa o file backup.
GRAPH.06.02 e' integrata tramite PR #9 (squash a9df2bd): gestione
di mappe personali in database SQLite separati. GRAPH.06.03 e' integrata
tramite PR #11, squash 9478973: editor di Idee/nodi con gerarchie,
rappresentazioni multiple e note locali, indipendente da GRAPH.05.
GRAPH.06.04 e' integrata tramite PR #13, squash 550ce57: editor Relation
con create/update/inversione/delete. GRAPH.06.05 e' integrata tramite PR #15,
squash cbde111: ricerca indicizzata con scelte Placement esplicite.
GRAPH.06.06 e' integrata tramite PR #17, squash 10ba39c: file JSON
portabili, import sempre in una nuova mappa e limite 64 MiB.
GRAPH.06.07 e' integrata tramite PR #19, squash 073aaeb: snapshot SQLite
verificato, retention 10 e restore non distruttivo.
GRAPH.06.08 e' integrata tramite PR #21, squash 52b9ade: drag/pin,
Undo/Redo e view-state persistente integrati con le slice 06.02-06.07; viewer
14/14, build e smoke WPF/WebView2 PASS. Rerun finale .NET e scala 300k sono
bloccati da Smart App Control, senza modifiche alla policy.
GRAPH.07-08 non iniziate. Vedere docs/coordination.md e docs/roadmap.md.

GRAPH.03 introduce zoom semantico contestuale, aggregazioni e navigazione trasversale.
GRAPH.04 rende raggiungibili i figli oltre la prima pagina, limita nodi/link/etichette,
evita caricamenti globali nel renderer e misura la scala fino a 300.000 Idea.
GRAPH.06.08 completa drag/pin, Undo/Redo e vista
persistente; firma/cifratura dei backup appartengono all'hardening successivo.
Il codec GRAPH.06.01 esporta/importa il grafo in memoria. GRAPH.06.06 lo espone
come file JSON tramite dialog WPF, senza confonderlo con un backup SQLite.
GRAPH.06.02 introduce la gestione mappe; GRAPH.06.03 rende possibile
aggiungere e modificare idee radice o figlie in tali mappe. GRAPH.06.04
aggiunge le Relation concettuali fra Idea con direzione, tipo e spiegazione.
GRAPH.06.05 espone la ricerca per prefisso e apre una rappresentazione scelta.
GRAPH.06.06 esporta file JSON UTF-8 e importa copie indipendenti in nuove mappe.
GRAPH.06.07 salva snapshot SQLite completi e li ripristina con identita' nuova.
GRAPH.06.08 porta sulla base corrente drag/pin, Undo/Redo locale e stato della
vista, sostituendo la vecchia candidata GRAPH.05 senza mergiarne la PR.

## Architettura

C#/.NET 10 e WPF ospitano una vista TypeScript/Three.js attraverso WebView2.
`Nodilume.Core` contiene identità e invarianti senza dipendenze grafiche o database;
`Nodilume.Application` espone operazioni, contratti di proiezione e porte di persistenza;
`Nodilume.Infrastructure` implementa un database SQLite per mappa.

C# è autorevole per grafo, contesto, revisione, aggregazioni e destinazioni. Il viewer
possiede camera, animazioni e stato grafico transitorio. I messaggi GRAPH.03 sono
versionati e correlati tramite `requestId`, `mapId`, revisione e contesto; richieste
superate vengono annullate lato Desktop e risposte obsolete vengono ignorate lato viewer.
Le risorse del renderer sono distribuite localmente.

## Avvio da sorgente

Prerequisiti: Windows 11, .NET SDK 10.0.400 (o patch compatibile), Node 24 e npm,
Microsoft Edge WebView2 Runtime. Il primo restore richiede Internet.
Le dipendenze sono fissate nei lockfile; la vista usa solo risorse locali.

Da PowerShell, nella radice della repository:

```powershell
./scripts/build.ps1
dotnet run --project tests/Nodilume.Smoke -c Release --no-build
./scripts/run.ps1
```

`build.ps1` esegue `npm ci`, dieci test viewer/navigation, compilazione TypeScript,
bundle, dieci gruppi .NET Core/SQLite/proiezione/codec/catalogo/editor/ricerca/file/backup
su database temporanei reali e build .NET con restore bloccato. Compila inoltre
lo scale-smoke e il tool benchmark GRAPH.04.

Lo smoke GRAPH.03 usa una fixture separata dalla demo personale. Apre due finestre
WPF/WebView2 consecutive sullo stesso database temporaneo e verifica tre contesti
annidati, entrata/uscita, scelta fra rappresentazioni, destinazione trasversale e ritorno,
direzioni opposte, focus osservabile, resize e riapertura persistente. Salva
`artifacts/graph-03-smoke.png` e non sovrascrive database esistenti.

## Gestione delle mappe (GRAPH.06.02)

Il menu `Mappa` in alto elenca le mappe personali nella cartella locale.
Scrivi un nome nel campo e premi `Nuova mappa` per creare una mappa
vuota distinta; scegli una voce dal menu per aprirla o usa `Rinomina` per
cambiare il nome di quella attiva. La selezione viene ripristinata alla
riapertura. L'antica demo.sqlite resta disponibile: non viene spostata
o sovrascritta da nuovi file mappa GUID.sqlite. Nessuna cancellazione mappe
e nessun editor di contenuti ancora introdotti da questa patch.

## Editor di idee e nodi (GRAPH.06.03)

Nel pannello a destra scrivi titolo e contenuto, quindi scegli
`Nuova radice` per creare un'Idea indipendente. Seleziona un nodo
della scena per caricare il titolo e il contenuto condiviso della sua
Idea e l'annotazione propria di quel Placement. `Salva idea` aggiorna
il contenuto comune a tutte le sue rappresentazioni; `Salva annotazione
locale` modifica solo il nodo selezionato. `Nuova figlia` crea
una nuova Idea sotto il nodo selezionato usando i campi del pannello.
`Aggiungi rappresentazione qui` colloca la stessa Idea sotto il
contesto corrente, se non viola gli invarianti della gerarchia.
Le modifiche vengono scritte su SQLite; se rifiutate viene mostrato
un messaggio. La creazione di contenuti non include cancellazione di
Idea/Placement o Undo/Redo.

## Editor relazioni (GRAPH.06.04)

Seleziona un nodo e catturalo come origine; naviga e seleziona un'altra
Idea come destinazione. Imposta tipo, direzione e spiegazione, quindi crea
la Relation. L'elenco mostra le relazioni che toccano l'Idea selezionata;
una voce può essere invertita o modificata conservando la propria identità.
L'eliminazione richiede due click e rimuove soltanto la Relation. Oltre 64
risultati l'elenco dichiara che è parziale.

## Ricerca (GRAPH.06.05)

Inserisci un prefisso esatto del titolo nella sidebar. La ricerca è
case-sensitive, usa pagine da 20 Idea e mostra ogni Placement come scelta
separata con percorso e ID breve. Seleziona una riga per vedere il contenuto
condiviso e premi `Apri` per raggiungere quel nodo esatto. I risultati vengono
azzerati al cambio mappa; limiti su rappresentazioni o percorso sono dichiarati.

## Import/export file (GRAPH.06.06)

`Esporta file` salva la mappa selezionata come JSON portabile UTF-8 con sostituzione
atomica del file finale. `Importa file` valida interamente il documento prima di
creare una nuova mappa con MapId distinto e revisione 0; non sovrascrive mai la
mappa di origine. Sono accettati solo file `.json` fino a 64 MiB. Questa funzione
e' pensata per mappe piccole/medie e non sostituisce backup/recovery SQLite.

## Importa vault Obsidian (NuGet)

Il pulsante `Importa vault Obsidian` seleziona una cartella Markdown e crea
una nuova mappa SQLite: cartelle come livelli, note come idee e wikilink
come relazioni direzionate. La lettura usa il pacchetto privato versionato
`ObsidianNotesKeeper.Vault` 0.1.1 (piu' `SyntaxHelper` transitivo) proveniente
dall'altro repository. Non vengono copiate librerie o note in questo codice.
Il vault non viene modificato; importazioni successive creano nuove mappe.
Consultare `docs/obsidian-nuget-import.md` per accesso al feed NuGet privato,
limiti, gestione dei wikilink e prove automatizzate.

## Backup/recovery SQLite (GRAPH.06.07)

`Crea backup` salva uno snapshot consistente nel folder gestito `Maps\Backups`;
sono conservati gli ultimi 10 backup validi per mappa. Il pacchetto contiene il
database completo e un manifest con dimensione e SHA-256. `Ripristina backup`
verifica struttura, checksum, integrita' e schema prima di creare una copia con
MapId nuovo, senza sovrascrivere la sorgente. Il limite e' 8 GiB. Il checksum
rileva corruzione ma non sostituisce firma o cifratura del pacchetto.
Con lo schema v3 il restore reindicizza anche cronologia, ricevute, cursore
Undo/Redo e view-state verso il nuovo MapId.

## Editing locale e vista persistente (GRAPH.06.08)

Maiusc + trascinamento sposta un Placement nel contesto corrente; Esc annulla.
Un nodo fissato deve essere sbloccato prima del drag. `Fissa`, `Annulla` e
`Ripeti` operano sulla mappa attiva e persistono su SQLite; la cronologia conserva
al massimo 100 operazioni per mappa. Contesto, selezione, pagina e camera vengono
ripristinati al cambio mappa e alla riapertura. Undo/Redo copre move/pin, non le
operazioni dell'editor Idea/Relation.

## Comandi della vista

- Trascina col tasto sinistro per ruotare; col destro per spostare la camera; Maiusc + sinistro sposta il nodo selezionato.
- Rotella per avvicinarti o allontanarti.
- Clic sul nodo o sulla sua etichetta per selezionarlo.
- Doppio clic o «Entra» apre un nodo che possiede figli; una foglia viene soltanto messa a fuoco.
- Avvicinarsi a un nodo selezionato o puntato stabilmente può aprirlo automaticamente; entrata e uscita usano soglie diverse.
- «Livello superiore» oppure Esc esce dal contesto; il breadcrumb permette di scegliere un antenato.
- «Pagina» / «Altri» sfogliano progressivamente i figli quando il contesto supera il budget.
- «Panoramica» torna alla proiezione principale.
- «Ritorna» ripristina il contesto/camera precedente dopo una navigazione trasversale.
- Nei collegamenti con più rappresentazioni viene mostrato il percorso di ogni Placement: la vista non sceglie silenziosamente il primo ID.
- Clic sullo sfondo, poi WASD per attraversare la rete, Q/E per scendere/salire e Maiusc per accelerare.

Durante il caricamento la scena corrente resta visibile. Stati ready, partial, leaf,
empty ed errore sono espliciti. Un nodo foglia espone `hasChildren=false` e non viene
trattato come un gruppo apribile.

La demo persistente contiene 25 Placement, 24 Idea e 3 Relation concettuali; una Idea
è rappresentata in due contesti con Placement distinti. Il database predefinito è
`%LOCALAPPDATA%\Nodilume\Maps\demo.sqlite` e viene inizializzato solo se assente.
GRAPH.04 non richiede il caricamento globale della mappa e non scrive posizioni durante
zoom o paging. Se un budget viene esaurito la proiezione resta esplicitamente `partial`.

Le prove GRAPH.02 sono in `docs/validation/graph-02.md`.
Le prove e il playbook GRAPH.03 sono in `docs/validation/graph-03.md`.
La validazione GRAPH.04 è in `docs/validation/graph-04.md` e i benchmark in
`docs/benchmarks/graph-04.md`.
GRAPH.06.02: `docs/plans/graph-06-02-map-manager.md`,
`docs/validation/graph-06-02.md` e `docs/handoffs/GRAPH_06_02_RESULT.md`.
GRAPH.06.03: `docs/plans/graph-06-03-idea-node-editor.md`,
`docs/validation/graph-06-03.md` e `docs/handoffs/GRAPH_06_03_RESULT.md`.
GRAPH.06.04: `docs/plans/graph-06-04-relation-editor.md`,
`docs/validation/graph-06-04.md` e `docs/handoffs/GRAPH_06_04_RESULT.md`.
GRAPH.06.05: `docs/plans/graph-06-05-search-ui.md`,
`docs/validation/graph-06-05.md` e `docs/handoffs/GRAPH_06_05_RESULT.md`.
GRAPH.06.06: `docs/plans/graph-06-06-file-import-export.md`,
`docs/validation/graph-06-06.md` e `docs/handoffs/GRAPH_06_06_RESULT.md`.
GRAPH.06.07: `docs/plans/graph-06-07-backup-recovery.md`,
`docs/validation/graph-06-07.md` e `docs/handoffs/GRAPH_06_07_RESULT.md`.
GRAPH.06.08: `docs/plans/graph-06-08-editor-closure.md`,
`docs/validation/graph-06-08.md` e `docs/handoffs/GRAPH_06_08_RESULT.md`.
GRAPH.06.01: `docs/plans/graph-06-01-portable-map.md` e
`docs/validation/graph-06-01.md`; per verificare solo questa slice:
`dotnet restore tests/Nodilume.Tests --locked-mode`,
`dotnet build tests/Nodilume.Tests -c Release --no-restore`,
`dotnet run --project tests/Nodilume.Tests -c Release --no-build`.
La specifica completa è in `docs/specs/graph-3d-design.md`.
La roadmap è in `docs/roadmap.md`.

## Dati

Solo codice e fixture sintetiche appartengono a questa repository privata.
Libri, database personali, fotografie e credenziali restano fuori da git.
Nome scelto dall'utente; disponibilità legale del marchio non accertata.

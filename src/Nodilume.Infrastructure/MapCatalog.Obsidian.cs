using ObsidianNotesKeeper.Vault;
using Nodilume.Core;
using Nodilume.Infrastructure.Sqlite;

namespace Nodilume.Infrastructure;

/// <summary>
/// Converts an Obsidian vault into a fresh, independent Nodilume SQLite map.
/// The original Markdown tree is never modified.
/// </summary>
public sealed partial class MapCatalog
{
    public async Task<CatalogMap> ImportObsidianAsync(
        string vaultDirectory, CancellationToken cancellationToken = default)
    {
        var notes = await VaultReader.ReadAsync(vaultDirectory, cancellationToken);
        if (notes.Count == 0)
            throw new InvalidDataException("No Markdown notes found in the selected Obsidian vault.");
        if (notes.Sum(note => (long)note.Markdown.Length) > 64L * 1024 * 1024)
            throw new InvalidDataException("The selected vault exceeds the 64 MiB map import limit.");

        var directoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(vaultDirectory)));
        var title = "Obsidian · " + directoryName;
        if (title.Length > 120) title = title[..120];
        title = ValidateTitle(title);
        var graph = await Task.Run(() => BuildGraph(notes, title, cancellationToken), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return await CreateDatabaseAsync(graph, cancellationToken);
    }

    private static MapGraph BuildGraph(
        IReadOnlyList<VaultNote> notes, string title, CancellationToken cancellationToken)
    {
        var map = new MapInfo(MapId.New(), title, 0, SqliteSchema.CurrentVersion);
        var graph = new MapGraph(map);
        var folderPlacements = new Dictionary<string, PlacementId>(StringComparer.OrdinalIgnoreCase);
        var byPath = new Dictionary<string, IdeaId>(StringComparer.OrdinalIgnoreCase);
        var byTitle = new Dictionary<string, List<IdeaId>>(StringComparer.OrdinalIgnoreCase);
        var noteIdeas = new List<(VaultNote Note, IdeaId Id)>(notes.Count);
        var ordinal = 0;

        foreach (var note in notes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var components = note.RelativePath.Split('/');
            PlacementId? parent = null;
            var folderPath = "";
            for (var i = 0; i < components.Length - 1; i++)
            {
                var folderName = components[i];
                folderPath = folderPath.Length == 0 ? folderName : folderPath + "/" + folderName;
                if (folderPlacements.TryGetValue(folderPath, out var existing))
                {
                    parent = existing;
                    continue;
                }

                var folderIdea = IdeaId.New();
                var folderPlacement = PlacementId.New();
                graph.AddIdea(folderIdea, folderName, "Cartella Obsidian: " + folderPath);
                var pos = Position(ordinal++);
                graph.AddPlacement(folderPlacement, folderIdea, parent, pos.X, pos.Y, pos.Z);
                folderPlacements.Add(folderPath, folderPlacement);
                parent = folderPlacement;
            }

            var idea = IdeaId.New();
            var placement = PlacementId.New();
            graph.AddIdea(idea, note.Title, note.Markdown);
            var tags = note.Tags.Count == 0 ? "" : "\nTag: " + string.Join(", ", note.Tags);
            var annotation = "Obsidian: " + note.RelativePath + tags;
            if (annotation.Length > 4000) annotation = annotation[..4000];
            var position = Position(ordinal++);
            graph.AddPlacement(placement, idea, parent,
                position.X, position.Y, position.Z, annotation: annotation);

            var key = WithoutExtension(note.RelativePath);
            if (!byPath.TryAdd(key, idea))
                throw new InvalidDataException("Duplicate Obsidian note path: " + note.RelativePath);
            var baseName = Path.GetFileName(key);
            if (!byTitle.TryGetValue(baseName, out var matching))
                byTitle[baseName] = matching = [];
            matching.Add(idea);
            noteIdeas.Add((note, idea));
        }

        var linksAdded = new HashSet<(IdeaId Source, IdeaId Target)>();
        foreach (var (note, source) in noteIdeas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var rawTarget in note.Links)
            {
                var target = ResolveLink(note.RelativePath, rawTarget, byPath, byTitle);
                if (target is null || target.Value == source || !linksAdded.Add((source, target.Value)))
                    continue;
                graph.AddRelation(RelationId.New(), source, target.Value,
                    "wikilink", isDirected: true, "Collegamento Obsidian");
            }
        }

        return graph;
    }

    private static IdeaId? ResolveLink(
        string sourcePath, string target,
        IReadOnlyDictionary<string, IdeaId> byPath,
        IReadOnlyDictionary<string, List<IdeaId>> byTitle)
    {
        var normalized = target.Replace('\\', '/').Trim().TrimStart('/');
        if (normalized.Length == 0 || normalized.Split('/').Any(s => s is "." or ".."))
            return null;
        if (normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^3];

        if (byPath.TryGetValue(normalized, out var exact))
            return exact;

        var lastSlash = sourcePath.LastIndexOf('/');
        if (lastSlash >= 0
            && byPath.TryGetValue(sourcePath[..(lastSlash + 1)] + normalized, out var sibling))
            return sibling;

        if (!normalized.Contains('/')
            && byTitle.TryGetValue(normalized, out var matches)
            && matches.Count == 1)
            return matches[0];

        return null; // Ambiguous and unresolved links are not guessed.
    }

    private static string WithoutExtension(string path) =>
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? path[..^3] : path;

    private static (double X, double Y, double Z) Position(int order)
    {
        var angle = order % 24 * Math.Tau / 24;
        var radius = 5.0 + order / 24 * 3.0;
        return (Math.Cos(angle) * radius, Math.Sin(angle) * radius, (order % 5) * 0.4);
    }
}

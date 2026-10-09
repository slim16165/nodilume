using Nodilume.Infrastructure;
using Nodilume.Infrastructure.Sqlite;

internal static class ObsidianImportTests
{
    public static async Task RunAsync()
    {
        await using var temp = new TempDatabase("obsidian");
        var vault = Path.Combine(temp.Root, "My Vault");
        Directory.CreateDirectory(Path.Combine(vault, "Topic"));
        Directory.CreateDirectory(Path.Combine(vault, ".obsidian"));
        var sourcePath = Path.Combine(vault, "Topic", "A.md");
        await File.WriteAllTextAsync(sourcePath,
            "---\ntitle: Alpha\ntags: [research]\n---\nBody [[Topic/B|B name]] [[B#section]] [[Missing]]");
        var initial = await File.ReadAllTextAsync(sourcePath);
        await File.WriteAllTextAsync(Path.Combine(vault, "Topic", "B.md"), "Another note");
        await File.WriteAllTextAsync(Path.Combine(vault, ".obsidian", "cache.md"), "Private config");

        var catalog = new MapCatalog(temp.Root, temp.Path);
        var imported = await catalog.ImportObsidianAsync(vault);
        Check.True(imported.Map.Title.Contains("My Vault", StringComparison.Ordinal),
            "Vault name was not recorded.");
        await using (var store = new SqliteMapStore(imported.DatabasePath))
        {
            var graph = await store.LoadGraphAsync();
            Check.Equal(3, graph.Ideas.Count, "Two notes and one folder were expected.");
            Check.Equal(3, graph.Placements.Count, "Folder hierarchy was not imported.");
            Check.Equal(1, graph.Relations.Count, "Alias and heading wikilinks must deduplicate.");
            var alpha = graph.Ideas.Values.Single(x => x.Title == "Alpha");
            var beta = graph.Ideas.Values.Single(x => x.Title == "B");
            var relationship = graph.Relations.Values.Single();
            Check.Equal(alpha.Id, relationship.SourceIdeaId, "Wikilink source mismatch.");
            Check.Equal(beta.Id, relationship.TargetIdeaId, "Wikilink target mismatch.");
            Check.Equal("wikilink", relationship.Kind, "Wikilink kind mismatch.");
            Check.True(graph.Placements.Values.Any(p =>
                    p.IdeaId == alpha.Id && p.Annotation.Contains("Topic/A.md", StringComparison.Ordinal)),
                "Source relative path was not retained.");
        }

        Check.Equal(initial, await File.ReadAllTextAsync(sourcePath), "Import modified Markdown sources.");
        var copied = await catalog.ImportObsidianAsync(vault);
        Check.True(imported.Map.Id != copied.Map.Id, "A second import overwrote the previous map.");
        Check.Equal(2, (await catalog.ListAsync()).Count, "Two separate SQLite maps were expected.");

        await Check.ThrowsAsync<DirectoryNotFoundException>(
            () => catalog.ImportObsidianAsync(Path.Combine(temp.Root, "not-found")),
            "Missing vault directory was silently accepted.");
        Check.Equal(2, (await catalog.ListAsync()).Count,
            "Rejected vault import unexpectedly created another SQLite map.");
    }
}

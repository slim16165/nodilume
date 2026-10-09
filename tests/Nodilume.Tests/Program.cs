internal static class Program
{
    private static async Task<int> Main()
    {
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("Domain invariants", DomainTests.RunAsync),
            ("SQLite integration", SqliteTests.RunAsync),
            ("Semantic projection", SemanticProjectionTests.RunAsync),
            ("Portable map JSON", PortableMapJsonTests.RunAsync),
            ("Portable map files", PortableMapFileTests.RunAsync),
            ("Obsidian NuGet import", ObsidianImportTests.RunAsync),
            ("SQLite backup and recovery", MapBackupRecoveryTests.RunAsync),
            ("Multi-map catalog", MapCatalogTests.RunAsync),
            ("Idea and node editor", MapContentEditorTests.RunAsync),
            ("Relation editor", MapRelationEditorTests.RunAsync),
            ("Map search", MapSearchServiceTests.RunAsync),
            ("Local edits and view state", LocalEditTests.RunAsync)
        };

        var failed = 0;
        foreach (var test in tests)
        {
            try
            {
                await test.Run();
                Console.WriteLine($"PASS: {test.Name}");
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine($"FAIL: {test.Name}");
                Console.Error.WriteLine(exception);
            }
        }

        Console.WriteLine($"{tests.Length - failed}/{tests.Length} groups PASS");
        return failed == 0 ? 0 : 1;
    }
}

using System.IO;
using System.Text.Json;
using System.Windows;
using Nodilume.Application;
using Nodilume.Core;
using Nodilume.Infrastructure.Sqlite;

namespace Nodilume.Desktop;

public partial class MainWindow
{
    private PlacementId? _editorPlacementId;
    private IdeaId? _editorIdeaId;
    private int _editorSelectionSerial;
    private bool _editorBusy;

    private void SetEditorBusy(bool busy)
    {
        _editorBusy = busy;
        var blocked = busy || _mapActionBusy;
        MapPicker.IsEnabled = !blocked;
        MapNameInput.IsEnabled = !blocked;
        NewMapButton.IsEnabled = !blocked;
        RenameMapButton.IsEnabled = !blocked;
        ImportMapButton.IsEnabled = !blocked;
        ImportObsidianButton.IsEnabled = !blocked;
        ExportMapButton.IsEnabled = !blocked && _selectedMap is not null;
        BackupMapButton.IsEnabled = !blocked && _selectedMap is not null;
        RestoreBackupButton.IsEnabled = !blocked;
        IdeaTitleInput.IsEnabled = !blocked;
        IdeaContentInput.IsEnabled = !blocked;
        PlacementAnnotationInput.IsEnabled = !blocked && _editorPlacementId is not null;
        CreateRootIdeaButton.IsEnabled = !blocked && _activeMapId is not null;
        CreateChildIdeaButton.IsEnabled = !blocked && _editorPlacementId is not null;
        SaveIdeaButton.IsEnabled = !blocked && _editorIdeaId is not null;
        SaveAnnotationButton.IsEnabled = !blocked && _editorPlacementId is not null;
        CreateRepresentationButton.IsEnabled = !blocked && _editorIdeaId is not null;
        SetRelationControlsBusy(blocked);
        UpdateSearchControls();
    }

    private void ResetIdeaEditor()
    {
        _editorSelectionSerial++;
        _editorPlacementId = null;
        _editorIdeaId = null;
        EditorSelectionText.Text = "Nessuna idea selezionata";
        IdeaTitleInput.Clear();
        IdeaContentInput.Clear();
        PlacementAnnotationInput.Clear();
        ResetRelationEditor();
        SetEditorBusy(false);
    }

    private async Task HandleEditorSelectionAsync(JsonElement root)
    {
        var mapId = ReadOptionalString(root, "mapId");
        var revision = ReadOptionalInt64(root, "revision");
        if (_closed || _editorBusy || _mapActionBusy || mapId != _activeMapId
            || revision != _activeRevision || _selectedMap is null)
            return;
        var placement = ReadOptionalString(root, "placementId");
        if (string.IsNullOrEmpty(placement))
        {
            ResetIdeaEditor();
            return;
        }
        if (!Guid.TryParseExact(placement, "D", out var guid)) return;
        var serial = ++_editorSelectionSerial;
        var generation = _mapGeneration;
        var path = _databasePath;
        try
        {
            await using var store = new SqliteMapStore(path);
            var node = await store.ReadPlacementAsync(_selectedMap.Map.Id,
                new PlacementId(guid));
            if (node is null) throw new InvalidDataException("Nodo selezionato non trovato.");
            var idea = (await store.ReadIdeasAsync(_selectedMap.Map.Id, [node.IdeaId]))
                .SingleOrDefault() ?? throw new InvalidDataException("Idea selezionata non trovata.");
            if (_closed || _editorBusy || serial != _editorSelectionSerial
                || generation != _mapGeneration || revision != _activeRevision)
                return;
            _editorPlacementId = node.Id;
            _editorIdeaId = idea.Id;
            IdeaTitleInput.Text = idea.Title;
            IdeaContentInput.Text = idea.Content;
            PlacementAnnotationInput.Text = node.Annotation;
            EditorSelectionText.Text = $"Idea: {idea.Title} · nodo {node.Id.ToString()[..8]}";
            EditorStatus.Text = "";
            SetEditorBusy(false);
            await RefreshRelationListAsync(idea.Id, serial, generation, _selectedRelationId);
        }
        catch (Exception ex)
        {
            if (!_closed && serial == _editorSelectionSerial && generation == _mapGeneration)
                EditorStatus.Text = "Selezione non caricata: " + ex.Message;
        }
    }

    private async Task EditAsync(
        Func<MapContentEditor, MapId, long, Task<long>> operation,
        string success)
    {
        if (_closed || _editorBusy || _mapActionBusy
            || _activeMapId is null || _selectedMap is null
            || _activeMapId != _selectedMap.Map.Id.ToString())
            return;
        var mapId = _selectedMap.Map.Id;
        var generation = _mapGeneration;
        var revision = _activeRevision;
        if (revision < 0) return;
        _editorSelectionSerial++;
        SetEditorBusy(true);
        try
        {
            await using var store = new SqliteMapStore(_databasePath);
            var editor = new MapContentEditor(store);
            var nextRevision = await operation(editor, mapId, revision);
            if (_closed || generation != _mapGeneration) return;
            // An in-flight pre-edit projection must never restore an obsolete revision.
            Interlocked.Exchange(ref _projectionCancellation, null)?.Cancel();
            _activeRevision = nextRevision;
            _projectionCache.Clear();
            await RefreshMapPickerAsync(mapId);
            if (_closed || generation != _mapGeneration) return;
            EditorStatus.Text = success;
            Viewer.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
            {
                version = 2, type = "refreshProjection",
                mapId = mapId.ToString(), revision = nextRevision
            }, JsonOptions));
        }
        catch (Exception ex)
        {
            if (!_closed && generation == _mapGeneration)
                EditorStatus.Text = "Modifica rifiutata: " + ex.Message;
        }
        finally
        {
            if (!_closed && generation == _mapGeneration) SetEditorBusy(false);
        }
    }

    private async void CreateRootIdeaButton_Click(object sender, RoutedEventArgs e)
    {
        var title = IdeaTitleInput.Text;
        var content = IdeaContentInput.Text;
        await EditAsync(async (editor, mapId, revision) =>
            (await editor.CreateIdeaAsync(mapId, revision, title, content, null)).Revision,
            "Nuova idea radice salvata.");
    }

    private async void CreateChildIdeaButton_Click(object sender, RoutedEventArgs e)
    {
        var parent = _editorPlacementId;
        if (parent is null) return;
        var title = IdeaTitleInput.Text;
        var content = IdeaContentInput.Text;
        await EditAsync(async (editor, mapId, revision) =>
            (await editor.CreateIdeaAsync(mapId, revision, title, content, parent)).Revision,
            "Nuova idea figlia salvata.");
    }

    private async void SaveIdeaButton_Click(object sender, RoutedEventArgs e)
    {
        var ideaId = _editorIdeaId;
        if (ideaId is null) return;
        var title = IdeaTitleInput.Text;
        var content = IdeaContentInput.Text;
        await EditAsync((editor, mapId, revision) =>
            editor.UpdateIdeaAsync(mapId, revision, ideaId.Value, title, content),
            "Idea condivisa salvata.");
    }

    private async void SaveAnnotationButton_Click(object sender, RoutedEventArgs e)
    {
        var placement = _editorPlacementId;
        if (placement is null) return;
        var annotation = PlacementAnnotationInput.Text;
        await EditAsync((editor, mapId, revision) =>
            editor.UpdateAnnotationAsync(mapId, revision, placement.Value, annotation),
            "Annotazione locale salvata.");
    }

    private async void CreateRepresentationButton_Click(object sender, RoutedEventArgs e)
    {
        var ideaId = _editorIdeaId;
        if (ideaId is null) return;
        var context = _lastContextPlacementId;
        await EditAsync(async (editor, mapId, revision) =>
            (await editor.CreateRepresentationAsync(mapId, revision, ideaId.Value,
                context)).Revision, "Rappresentazione aggiunta al contesto.");
    }
}

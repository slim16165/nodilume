using System.Windows;
using Microsoft.Win32;

namespace Nodilume.Desktop;

public partial class MainWindow
{
    private async void ImportObsidianButton_Click(object sender, RoutedEventArgs e)
    {
        if (_closed || _mapActionBusy) return;
        var dialog = new OpenFolderDialog
        {
            Title = "Scegli il vault o una cartella Obsidian da importare",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
        {
            MapStatus.Text = "Importazione Obsidian annullata.";
            return;
        }

        SetMapControlsBusy(true);
        try
        {
            var imported = await _mapCatalog.ImportObsidianAsync(dialog.FolderName);
            var selected = await _mapCatalog.SelectAsync(imported.Map.Id);
            if (!_closed)
            {
                await ActivateMapAsync(selected);
                MapStatus.Text = "Vault Obsidian importato (copia indipendente): " + selected.Map.Title;
            }
        }
        catch (Exception ex)
        {
            if (!_closed)
            {
                if (_selectedMap is not null)
                    await RefreshMapPickerAsync(_selectedMap.Map.Id);
                MapStatus.Text = "Importazione Obsidian rifiutata: " + ex.Message;
            }
        }
        finally
        {
            if (!_closed) SetMapControlsBusy(false);
        }
    }
}

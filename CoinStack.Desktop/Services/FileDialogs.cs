using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace CoinStack.Desktop.Services;

/// <summary>The system's save and open dialogs, so view models can ask for a file without knowing about windows.</summary>
public interface IFileDialogs
{
    /// <summary>Asks where to save, then writes the file. False when the person cancels.</summary>
    Task<bool> SaveAsync(string suggestedName, byte[] content);

    /// <summary>Asks for a file and reads it. Null when the person cancels or the file is larger than <paramref name="maxBytes"/>.</summary>
    Task<(string Name, byte[] Content)?> OpenAsync(long maxBytes);
}

internal sealed class FileDialogs : IFileDialogs
{
    private static readonly FilePickerFileType Backup = new("CoinStack backup") { Patterns = ["*.coinstack"] };

    private static TopLevel? Window =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;

    public async Task<bool> SaveAsync(string suggestedName, byte[] content)
    {
        if (Window is not { } window)
        {
            return false;
        }

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save backup",
            SuggestedFileName = suggestedName,
            DefaultExtension = "coinstack",
            FileTypeChoices = [Backup]
        });
        if (file is null)
        {
            return false;
        }

        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(content);
        return true;
    }

    public async Task<(string Name, byte[] Content)?> OpenAsync(long maxBytes)
    {
        if (Window is not { } window)
        {
            return null;
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a backup",
            AllowMultiple = false,
            FileTypeFilter = [Backup, FilePickerFileTypes.All]
        });
        if (files is not [var file])
        {
            return null;
        }

        await using var stream = await file.OpenReadAsync();
        if (stream.CanSeek && stream.Length > maxBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return (file.Name, buffer.ToArray());
    }
}

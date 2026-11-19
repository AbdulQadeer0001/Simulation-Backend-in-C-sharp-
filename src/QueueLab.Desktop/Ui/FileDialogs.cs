using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace QueueLab.Desktop.Ui;

/// <summary>Open / save dialogs built on Avalonia's storage provider.</summary>
internal static class FileDialogs
{
    public static async Task<bool> SaveAsync(Control owner, string suggestedName, string description, string pattern, byte[] content)
    {
        TopLevel? top = TopLevel.GetTopLevel(owner);
        if (top == null)
        {
            return false;
        }

        FilePickerSaveOptions options = new FilePickerSaveOptions
        {
            Title = "Save " + suggestedName,
            SuggestedFileName = suggestedName,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new FilePickerFileType(description) { Patterns = new List<string> { pattern } },
            },
        };

        IStorageFile? file = await top.StorageProvider.SaveFilePickerAsync(options);
        if (file == null)
        {
            return false;
        }

        using (Stream stream = await file.OpenWriteAsync())
        {
            if (stream.CanSeek)
            {
                stream.SetLength(0);
            }

            await stream.WriteAsync(content, 0, content.Length);
            await stream.FlushAsync();
        }

        return true;
    }

    /// <summary>Lets the user pick a CSV / Excel file. Returns its name and content, or null if cancelled.</summary>
    public static async Task<(string Name, byte[] Content)?> OpenDatasetAsync(Control owner)
    {
        TopLevel? top = TopLevel.GetTopLevel(owner);
        if (top == null)
        {
            return null;
        }

        FilePickerOpenOptions options = new FilePickerOpenOptions
        {
            Title = "Choose a dataset",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("CSV or Excel") { Patterns = new List<string> { "*.csv", "*.xlsx", "*.xls" } },
            },
        };

        IReadOnlyList<IStorageFile> files = await top.StorageProvider.OpenFilePickerAsync(options);
        if (files.Count == 0)
        {
            return null;
        }

        using (Stream stream = await files[0].OpenReadAsync())
        using (MemoryStream buffer = new MemoryStream())
        {
            await stream.CopyToAsync(buffer);
            return (files[0].Name, buffer.ToArray());
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PixlPunkt.Uno.Core.Platform;

/// <summary>
/// Extension methods for <see cref="IPlatformFileService"/> to simplify common export patterns.
/// </summary>
public static class PlatformFileServiceExtensions
{
    /// <summary>
    /// Saves or shares a file depending on platform capabilities.
    /// On desktop, shows a save dialog. On mobile, shows a share sheet.
    /// </summary>
    /// <param name="service">The file service.</param>
    /// <param name="data">File data to save/share.</param>
    /// <param name="fileName">Suggested file name.</param>
    /// <param name="extension">File extension (e.g., ".png").</param>
    /// <param name="shareTitle">Title for share sheet (mobile only).</param>
    /// <returns>True if the operation was initiated successfully.</returns>
    public static async Task<bool> SaveOrShareAsync(
        this IPlatformFileService service,
        byte[] data,
        string fileName,
        string extension,
        string? shareTitle = null)
    {
        if (service.SupportsSharing && !service.SupportsFileSystem)
        {
            // Mobile: use share sheet
            var mimeType = MimeTypes.FromExtension(extension);
            return await service.ShareFileAsync(data, fileName, mimeType, shareTitle);
        }
        else
        {
            // Desktop: use save dialog
            var description = MimeTypes.GetDescription(extension);
            var result = await service.SaveFileAsync(data, fileName, description, extension);
            return result.Success;
        }
    }

    /// <summary>
    /// Saves or shares multiple files depending on platform capabilities.
    /// </summary>
    public static async Task<bool> SaveOrShareMultipleAsync(
        this IPlatformFileService service,
        IReadOnlyList<(byte[] Data, string FileName)> files,
        string extension,
        string? shareTitle = null)
    {
        if (service.SupportsSharing && !service.SupportsFileSystem)
        {
            // Mobile: share all at once
            var mimeType = MimeTypes.FromExtension(extension);
            var shareFiles = new List<(byte[] Data, string FileName, string MimeType)>();
            foreach (var (data, fileName) in files)
            {
                shareFiles.Add((data, fileName, mimeType));
            }
            return await service.ShareFilesAsync(shareFiles, shareTitle);
        }
        else if (service.SupportsFolderPicking)
        {
            // Desktop: pick folder then save all files
            var folderResult = await service.PickFolderAsync();
            if (!folderResult.Success || string.IsNullOrEmpty(folderResult.FolderPath))
            {
                return false;
            }

            foreach (var (data, fileName) in files)
            {
                var filePath = System.IO.Path.Combine(folderResult.FolderPath, fileName);
                await System.IO.File.WriteAllBytesAsync(filePath, data);
            }
            return true;
        }
        else
        {
            // Fallback: save each file individually (not ideal UX)
            var description = MimeTypes.GetDescription(extension);
            foreach (var (data, fileName) in files)
            {
                var result = await service.SaveFileAsync(data, fileName, description, extension);
                if (!result.Success) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Shows a platform-appropriate export complete message.
    /// </summary>
    public static async Task ShowExportCompleteAsync(
        XamlRoot xamlRoot,
        string title,
        string? filePath = null)
    {
        string message;
        
        if (PlatformHelper.IsIOS || PlatformHelper.IsAndroid)
        {
            message = "Export complete! The file has been shared.";
        }
        else if (!string.IsNullOrEmpty(filePath))
        {
            message = $"Export complete!\n\nSaved to: {filePath}";
        }
        else
        {
            message = "Export complete!";
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = xamlRoot
        };

        await dialog.ShowAsync();
    }

    /// <summary>
    /// Shows an error dialog for export failures.
    /// </summary>
    public static async Task ShowExportErrorAsync(
        XamlRoot xamlRoot,
        string error)
    {
        var dialog = new ContentDialog
        {
            Title = "Export Failed",
            Content = error,
            CloseButtonText = "OK",
            XamlRoot = xamlRoot
        };

        await dialog.ShowAsync();
    }
}

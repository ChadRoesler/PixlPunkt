using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using PixlPunkt.Uno.UI.Helpers;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace PixlPunkt.Uno.Core.Platform;

/// <summary>
/// Desktop implementation of <see cref="IPlatformFileService"/> using traditional file pickers.
/// Works on Windows, Linux, and macOS via Uno Platform's Skia backend.
/// </summary>
public class DesktopPlatformFileService : IPlatformFileService
{
    private readonly Window _window;

    /// <summary>
    /// Creates a new desktop file service.
    /// </summary>
    /// <param name="window">The main window for picker initialization.</param>
    public DesktopPlatformFileService(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    /// <inheritdoc/>
    public bool SupportsFileSystem => true;

    /// <inheritdoc/>
    public bool SupportsSharing => false; // Desktop uses file system instead

    /// <inheritdoc/>
    public bool SupportsFolderPicking => true;

    /// <inheritdoc/>
    public async Task<FileSaveResult> SaveFileAsync(
        byte[] data,
        string suggestedFileName,
        string fileTypeDescription,
        string fileExtension)
    {
        try
        {
            var picker = WindowHost.CreateFileSavePicker(_window, suggestedFileName, fileExtension);
            
            // Add file type choice
            if (!picker.FileTypeChoices.ContainsKey(fileTypeDescription))
            {
                picker.FileTypeChoices.Clear();
                picker.FileTypeChoices.Add(fileTypeDescription, new[] { fileExtension });
            }

            WindowHost.TrySetDefaultFileExtension(picker, fileExtension);

            var file = await picker.PickSaveFileAsync();
            if (file == null)
            {
                return FileSaveResult.Cancelled();
            }

            await FileIO.WriteBytesAsync(file, data);
            return FileSaveResult.Succeeded(file.Path);
        }
        catch (Exception ex)
        {
            return FileSaveResult.Failed($"Save failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<FileOpenResult> OpenFileAsync(
        string fileTypeDescription,
        params string[] fileExtensions)
    {
        try
        {
            var picker = WindowHost.CreateFileOpenPicker(_window, fileExtensions);

            var file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                return FileOpenResult.Cancelled();
            }

            // Read file using stream to avoid IBuffer extension method issues
            using var stream = await file.OpenStreamForReadAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            var bytes = memoryStream.ToArray();

            return FileOpenResult.Succeeded(bytes, file.Name);
        }
        catch (Exception ex)
        {
            return FileOpenResult.Failed($"Open failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<FolderPickResult> PickFolderAsync()
    {
        try
        {
            var picker = WindowHost.CreateFolderPicker(_window, PickerLocationId.DocumentsLibrary);

            var folder = await picker.PickSingleFolderAsync();
            if (folder == null)
            {
                return FolderPickResult.Cancelled();
            }

            return FolderPickResult.Succeeded(folder.Path);
        }
        catch (Exception ex)
        {
            return FolderPickResult.Failed($"Folder pick failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Task<bool> ShareFileAsync(
        byte[] data,
        string fileName,
        string mimeType,
        string? title = null)
    {
        // Desktop doesn't have a native share sheet
        // Users should use SaveFileAsync instead
        System.Diagnostics.Debug.WriteLine(
            "ShareFileAsync called on desktop - use SaveFileAsync instead");
        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public Task<bool> ShareFilesAsync(
        IReadOnlyList<(byte[] Data, string FileName, string MimeType)> files,
        string? title = null)
    {
        // Desktop doesn't have a native share sheet
        System.Diagnostics.Debug.WriteLine(
            "ShareFilesAsync called on desktop - use SaveFileAsync instead");
        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public Task<string> SaveToTempAsync(byte[] data, string fileName)
    {
        var tempDir = Path.GetTempPath();
        var tempPath = Path.Combine(tempDir, fileName);

        // Ensure unique filename
        if (File.Exists(tempPath))
        {
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            tempPath = Path.Combine(tempDir, $"{baseName}_{timestamp}{ext}");
        }

        File.WriteAllBytes(tempPath, data);
        return Task.FromResult(tempPath);
    }
}

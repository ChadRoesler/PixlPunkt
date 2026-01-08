#if __ANDROID__
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Net;
using Android.Provider;
using AndroidX.Core.Content;
using Java.IO;
using Microsoft.UI.Xaml;
using File = System.IO.File;
using Uri = Android.Net.Uri;

namespace PixlPunkt.Uno.Core.Platform;

/// <summary>
/// Android implementation of <see cref="IPlatformFileService"/> using Storage Access Framework.
/// </summary>
/// <remarks>
/// <para>Uses the following Android APIs:</para>
/// <list type="bullet">
/// <item>Storage Access Framework (SAF) for file picking and saving</item>
/// <item><see cref="Intent.ActionSend"/> for sharing via share sheet</item>
/// <item><see cref="FileProvider"/> for secure file sharing</item>
/// </list>
/// </remarks>
public class AndroidPlatformFileService : IPlatformFileService
{
    private const int PickFileRequestCode = 9001;
    private const int SaveFileRequestCode = 9002;
    private const int PickFolderRequestCode = 9003;

    private static TaskCompletionSource<FileOpenResult>? _openFileTcs;
    private static TaskCompletionSource<FileSaveResult>? _saveFileTcs;
    private static TaskCompletionSource<FolderPickResult>? _pickFolderTcs;
    private static byte[]? _pendingSaveData;

    /// <inheritdoc/>
    public bool SupportsFileSystem => true; // Via SAF

    /// <inheritdoc/>
    public bool SupportsSharing => true;

    /// <inheritdoc/>
    public bool SupportsFolderPicking => true; // Via SAF (API 21+)

    /// <inheritdoc/>
    public async Task<FileSaveResult> SaveFileAsync(
        byte[] data,
        string suggestedFileName,
        string fileTypeDescription,
        string fileExtension)
    {
        try
        {
            var activity = GetCurrentActivity();
            if (activity == null)
            {
                return FileSaveResult.Failed("Could not get current activity");
            }

            _saveFileTcs = new TaskCompletionSource<FileSaveResult>();
            _pendingSaveData = data;

            var mimeType = GetMimeType(fileExtension);

            var intent = new Intent(Intent.ActionCreateDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType(mimeType);
            intent.PutExtra(Intent.ExtraTitle, suggestedFileName);

            activity.StartActivityForResult(intent, SaveFileRequestCode);

            return await _saveFileTcs.Task;
        }
        catch (Exception ex)
        {
            _pendingSaveData = null;
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
            var activity = GetCurrentActivity();
            if (activity == null)
            {
                return FileOpenResult.Failed("Could not get current activity");
            }

            _openFileTcs = new TaskCompletionSource<FileOpenResult>();

            var intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);

            // Set MIME types based on extensions
            if (fileExtensions.Length == 1)
            {
                intent.SetType(GetMimeType(fileExtensions[0]));
            }
            else
            {
                intent.SetType("*/*");
                var mimeTypes = new string[fileExtensions.Length];
                for (int i = 0; i < fileExtensions.Length; i++)
                {
                    mimeTypes[i] = GetMimeType(fileExtensions[i]);
                }
                intent.PutExtra(Intent.ExtraMimeTypes, mimeTypes);
            }

            activity.StartActivityForResult(intent, PickFileRequestCode);

            return await _openFileTcs.Task;
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
            var activity = GetCurrentActivity();
            if (activity == null)
            {
                return FolderPickResult.Failed("Could not get current activity");
            }

            _pickFolderTcs = new TaskCompletionSource<FolderPickResult>();

            var intent = new Intent(Intent.ActionOpenDocumentTree);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);

            activity.StartActivityForResult(intent, PickFolderRequestCode);

            return await _pickFolderTcs.Task;
        }
        catch (Exception ex)
        {
            return FolderPickResult.Failed($"Folder pick failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ShareFileAsync(
        byte[] data,
        string fileName,
        string mimeType,
        string? title = null)
    {
        try
        {
            var activity = GetCurrentActivity();
            if (activity == null)
            {
                return false;
            }

            // Save to cache directory first
            var tempPath = await SaveToTempAsync(data, fileName);
            var tempFile = new Java.IO.File(tempPath);

            // Get URI via FileProvider for secure sharing
            var uri = FileProvider.GetUriForFile(
                activity,
                $"{activity.PackageName}.fileprovider",
                tempFile);

            var shareIntent = new Intent(Intent.ActionSend);
            shareIntent.SetType(mimeType);
            shareIntent.PutExtra(Intent.ExtraStream, uri);
            shareIntent.AddFlags(ActivityFlags.GrantReadUriPermission);

            var chooserIntent = Intent.CreateChooser(shareIntent, title ?? "Share");
            activity.StartActivity(chooserIntent);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Share failed: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ShareFilesAsync(
        IReadOnlyList<(byte[] Data, string FileName, string MimeType)> files,
        string? title = null)
    {
        try
        {
            var activity = GetCurrentActivity();
            if (activity == null)
            {
                return false;
            }

            var uris = new List<IParcelable>();
            string? commonMimeType = null;

            foreach (var (data, fileName, mimeType) in files)
            {
                var tempPath = await SaveToTempAsync(data, fileName);
                var tempFile = new Java.IO.File(tempPath);
                
                var uri = FileProvider.GetUriForFile(
                    activity,
                    $"{activity.PackageName}.fileprovider",
                    tempFile);
                
                uris.Add(uri);

                // Track MIME types for the intent
                if (commonMimeType == null)
                {
                    commonMimeType = mimeType;
                }
                else if (commonMimeType != mimeType)
                {
                    commonMimeType = "*/*"; // Mixed types
                }
            }

            var shareIntent = new Intent(Intent.ActionSendMultiple);
            shareIntent.SetType(commonMimeType ?? "*/*");
            shareIntent.PutParcelableArrayListExtra(Intent.ExtraStream, new List<IParcelable>(uris));
            shareIntent.AddFlags(ActivityFlags.GrantReadUriPermission);

            var chooserIntent = Intent.CreateChooser(shareIntent, title ?? "Share Files");
            activity.StartActivity(chooserIntent);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Share files failed: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public Task<string> SaveToTempAsync(byte[] data, string fileName)
    {
        var activity = GetCurrentActivity();
        if (activity == null)
        {
            throw new InvalidOperationException("No activity available");
        }

        // Use app's cache directory for sharing
        var cacheDir = activity.CacheDir?.AbsolutePath ?? Path.GetTempPath();
        var tempPath = Path.Combine(cacheDir, fileName);

        // Ensure unique filename
        if (File.Exists(tempPath))
        {
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            tempPath = Path.Combine(cacheDir, $"{baseName}_{timestamp}{ext}");
        }

        File.WriteAllBytes(tempPath, data);
        return Task.FromResult(tempPath);
    }

    /// <summary>
    /// Handles the result from file picker/saver activities.
    /// Call this from your Activity's OnActivityResult.
    /// </summary>
    public static void HandleActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode == PickFileRequestCode)
        {
            HandleOpenFileResult(resultCode, data);
        }
        else if (requestCode == SaveFileRequestCode)
        {
            HandleSaveFileResult(resultCode, data);
        }
        else if (requestCode == PickFolderRequestCode)
        {
            HandlePickFolderResult(resultCode, data);
        }
    }

    private static void HandleOpenFileResult(Result resultCode, Intent? data)
    {
        if (_openFileTcs == null) return;

        try
        {
            if (resultCode != Result.Ok || data?.Data == null)
            {
                _openFileTcs.TrySetResult(FileOpenResult.Cancelled());
                return;
            }

            var uri = data.Data;
            var activity = GetCurrentActivity();
            if (activity == null)
            {
                _openFileTcs.TrySetResult(FileOpenResult.Failed("No activity"));
                return;
            }

            using var inputStream = activity.ContentResolver?.OpenInputStream(uri);
            if (inputStream == null)
            {
                _openFileTcs.TrySetResult(FileOpenResult.Failed("Could not open file"));
                return;
            }

            using var memoryStream = new MemoryStream();
            inputStream.CopyTo(memoryStream);
            var bytes = memoryStream.ToArray();

            // Try to get filename
            string? fileName = null;
            using (var cursor = activity.ContentResolver?.Query(uri, null, null, null, null))
            {
                if (cursor != null && cursor.MoveToFirst())
                {
                    var displayNameIndex = cursor.GetColumnIndex(OpenableColumns.DisplayName);
                    if (displayNameIndex >= 0)
                    {
                        fileName = cursor.GetString(displayNameIndex);
                    }
                }
            }

            _openFileTcs.TrySetResult(FileOpenResult.Succeeded(bytes, fileName));
        }
        catch (Exception ex)
        {
            _openFileTcs.TrySetResult(FileOpenResult.Failed($"Error: {ex.Message}"));
        }
    }

    private static void HandleSaveFileResult(Result resultCode, Intent? data)
    {
        if (_saveFileTcs == null) return;

        try
        {
            if (resultCode != Result.Ok || data?.Data == null || _pendingSaveData == null)
            {
                _pendingSaveData = null;
                _saveFileTcs.TrySetResult(FileSaveResult.Cancelled());
                return;
            }

            var uri = data.Data;
            var activity = GetCurrentActivity();
            if (activity == null)
            {
                _pendingSaveData = null;
                _saveFileTcs.TrySetResult(FileSaveResult.Failed("No activity"));
                return;
            }

            using var outputStream = activity.ContentResolver?.OpenOutputStream(uri);
            if (outputStream == null)
            {
                _pendingSaveData = null;
                _saveFileTcs.TrySetResult(FileSaveResult.Failed("Could not open output stream"));
                return;
            }

            outputStream.Write(_pendingSaveData);
            outputStream.Flush();

            _pendingSaveData = null;
            _saveFileTcs.TrySetResult(FileSaveResult.Succeeded(uri.Path));
        }
        catch (Exception ex)
        {
            _pendingSaveData = null;
            _saveFileTcs.TrySetResult(FileSaveResult.Failed($"Error: {ex.Message}"));
        }
    }

    private static void HandlePickFolderResult(Result resultCode, Intent? data)
    {
        if (_pickFolderTcs == null) return;

        try
        {
            if (resultCode != Result.Ok || data?.Data == null)
            {
                _pickFolderTcs.TrySetResult(FolderPickResult.Cancelled());
                return;
            }

            var uri = data.Data;
            var activity = GetCurrentActivity();
            if (activity == null)
            {
                _pickFolderTcs.TrySetResult(FolderPickResult.Failed("No activity"));
                return;
            }

            // Take persistable permission
            var takeFlags = data.Flags & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
            activity.ContentResolver?.TakePersistableUriPermission(uri, takeFlags);

            _pickFolderTcs.TrySetResult(FolderPickResult.Succeeded(uri.ToString() ?? ""));
        }
        catch (Exception ex)
        {
            _pickFolderTcs.TrySetResult(FolderPickResult.Failed($"Error: {ex.Message}"));
        }
    }

    private static Activity? GetCurrentActivity()
    {
        // Get the current activity from the Uno Platform
        return ContextHelper.Current as Activity;
    }

    private static string GetMimeType(string extension)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "png" => "image/png",
            "jpg" or "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "bmp" => "image/bmp",
            "webp" => "image/webp",
            "tiff" or "tif" => "image/tiff",
            "ico" => "image/x-icon",
            "cur" => "application/octet-stream", // No standard MIME for cursor
            "mp4" => "video/mp4",
            "webm" => "video/webm",
            "mkv" => "video/x-matroska",
            "avi" => "video/x-msvideo",
            "pxp" => "application/octet-stream",
            "pxpr" => "application/octet-stream",
            "pxpt" => "application/octet-stream",
            "mrk" => "application/octet-stream",
            "json" => "application/json",
            "txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }
}
#endif

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PixlPunkt.Uno.Core.Platform;

/// <summary>
/// Result from a file save operation.
/// </summary>
public class FileSaveResult
{
    /// <summary>Gets whether the operation was successful.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the path where the file was saved (if available).</summary>
    public string? FilePath { get; init; }

    /// <summary>Gets an error message if the operation failed.</summary>
    public string? ErrorMessage { get; init; }

    public static FileSaveResult Succeeded(string? path = null) => new() { Success = true, FilePath = path };
    public static FileSaveResult Failed(string error) => new() { Success = false, ErrorMessage = error };
    public static FileSaveResult Cancelled() => new() { Success = false, ErrorMessage = null };
}

/// <summary>
/// Result from a file open operation.
/// </summary>
public class FileOpenResult
{
    /// <summary>Gets whether the operation was successful.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the file data if successful.</summary>
    public byte[]? Data { get; init; }

    /// <summary>Gets the original file name (if available).</summary>
    public string? FileName { get; init; }

    /// <summary>Gets an error message if the operation failed.</summary>
    public string? ErrorMessage { get; init; }

    public static FileOpenResult Succeeded(byte[] data, string? fileName = null) 
        => new() { Success = true, Data = data, FileName = fileName };
    public static FileOpenResult Failed(string error) 
        => new() { Success = false, ErrorMessage = error };
    public static FileOpenResult Cancelled() 
        => new() { Success = false, ErrorMessage = null };
}

/// <summary>
/// Result from a folder pick operation.
/// </summary>
public class FolderPickResult
{
    /// <summary>Gets whether the operation was successful.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the folder path if successful.</summary>
    public string? FolderPath { get; init; }

    /// <summary>Gets an error message if the operation failed.</summary>
    public string? ErrorMessage { get; init; }

    public static FolderPickResult Succeeded(string path) => new() { Success = true, FolderPath = path };
    public static FolderPickResult Failed(string error) => new() { Success = false, ErrorMessage = error };
    public static FolderPickResult Cancelled() => new() { Success = false, ErrorMessage = null };
}

/// <summary>
/// Cross-platform file service interface for handling file operations
/// across different platforms (Desktop, iOS, Android, WASM).
/// </summary>
/// <remarks>
/// <para>
/// Each platform has different capabilities and UI paradigms for file handling:
/// </para>
/// <list type="bullet">
/// <item><strong>Desktop (Windows/Linux/macOS)</strong>: Traditional file pickers</item>
/// <item><strong>iOS</strong>: UIDocumentPickerViewController for picking, UIActivityViewController for sharing</item>
/// <item><strong>Android</strong>: Storage Access Framework (SAF) via Intents</item>
/// <item><strong>WASM</strong>: Browser-based file input/download</item>
/// </list>
/// </remarks>
public interface IPlatformFileService
{
    /// <summary>
    /// Gets whether this platform supports traditional file system access.
    /// </summary>
    bool SupportsFileSystem { get; }

    /// <summary>
    /// Gets whether this platform supports sharing files (share sheet).
    /// </summary>
    bool SupportsSharing { get; }

    /// <summary>
    /// Gets whether this platform supports folder picking.
    /// </summary>
    bool SupportsFolderPicking { get; }

    /// <summary>
    /// Saves a file using the platform's native save mechanism.
    /// </summary>
    /// <param name="data">The file data to save.</param>
    /// <param name="suggestedFileName">Suggested file name (including extension).</param>
    /// <param name="fileTypeDescription">Human-readable description of the file type.</param>
    /// <param name="fileExtension">File extension (e.g., ".png", ".gif").</param>
    /// <returns>Result indicating success/failure and the saved path if available.</returns>
    Task<FileSaveResult> SaveFileAsync(
        byte[] data,
        string suggestedFileName,
        string fileTypeDescription,
        string fileExtension);

    /// <summary>
    /// Opens a file using the platform's native open mechanism.
    /// </summary>
    /// <param name="fileTypeDescription">Human-readable description of the file type.</param>
    /// <param name="fileExtensions">Allowed file extensions (e.g., ".png", ".jpg").</param>
    /// <returns>Result containing the file data if successful.</returns>
    Task<FileOpenResult> OpenFileAsync(
        string fileTypeDescription,
        params string[] fileExtensions);

    /// <summary>
    /// Picks a folder using the platform's native folder picker.
    /// </summary>
    /// <returns>Result containing the folder path if successful.</returns>
    Task<FolderPickResult> PickFolderAsync();

    /// <summary>
    /// Shares a file using the platform's native share sheet.
    /// </summary>
    /// <param name="data">The file data to share.</param>
    /// <param name="fileName">The file name to use when sharing.</param>
    /// <param name="mimeType">MIME type of the file (e.g., "image/png").</param>
    /// <param name="title">Optional title for the share dialog.</param>
    /// <returns>True if sharing was initiated (not necessarily completed).</returns>
    Task<bool> ShareFileAsync(
        byte[] data,
        string fileName,
        string mimeType,
        string? title = null);

    /// <summary>
    /// Shares multiple files using the platform's native share sheet.
    /// </summary>
    /// <param name="files">List of (data, fileName, mimeType) tuples.</param>
    /// <param name="title">Optional title for the share dialog.</param>
    /// <returns>True if sharing was initiated.</returns>
    Task<bool> ShareFilesAsync(
        IReadOnlyList<(byte[] Data, string FileName, string MimeType)> files,
        string? title = null);

    /// <summary>
    /// Saves a file to a temporary location and returns the path.
    /// Useful for platforms that need intermediate storage.
    /// </summary>
    /// <param name="data">The file data.</param>
    /// <param name="fileName">The file name.</param>
    /// <returns>Path to the temporary file.</returns>
    Task<string> SaveToTempAsync(byte[] data, string fileName);
}

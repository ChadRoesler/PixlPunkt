namespace PixlPunkt.Uno.Core.Platform;

/// <summary>
/// Helper class for MIME type mappings used in file sharing and exports.
/// </summary>
public static class MimeTypes
{
    // ═══════════════════════════════════════════════════════════════
    // IMAGES
    // ═══════════════════════════════════════════════════════════════
    
    public const string Png = "image/png";
    public const string Jpeg = "image/jpeg";
    public const string Gif = "image/gif";
    public const string Bmp = "image/bmp";
    public const string WebP = "image/webp";
    public const string Tiff = "image/tiff";
    public const string Ico = "image/x-icon";

    // ═══════════════════════════════════════════════════════════════
    // VIDEO
    // ═══════════════════════════════════════════════════════════════
    
    public const string Mp4 = "video/mp4";
    public const string WebM = "video/webm";
    public const string Mkv = "video/x-matroska";
    public const string Avi = "video/x-msvideo";

    // ═══════════════════════════════════════════════════════════════
    // APPLICATION-SPECIFIC
    // ═══════════════════════════════════════════════════════════════
    
    public const string OctetStream = "application/octet-stream";
    public const string Json = "application/json";
    public const string Text = "text/plain";

    /// <summary>
    /// Gets the MIME type for a file extension.
    /// </summary>
    /// <param name="extension">File extension (with or without leading dot).</param>
    /// <returns>The MIME type string.</returns>
    public static string FromExtension(string extension)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "png" => Png,
            "jpg" or "jpeg" => Jpeg,
            "gif" => Gif,
            "bmp" => Bmp,
            "webp" => WebP,
            "tiff" or "tif" => Tiff,
            "ico" => Ico,
            "cur" => OctetStream,
            "mp4" => Mp4,
            "webm" => WebM,
            "mkv" => Mkv,
            "avi" => Avi,
            "pxp" => OctetStream,
            "pxpr" => OctetStream,
            "pxpt" => OctetStream,
            "mrk" => OctetStream,
            "json" => Json,
            "txt" => Text,
            _ => OctetStream
        };
    }

    /// <summary>
    /// Gets an appropriate file description for UI display.
    /// </summary>
    /// <param name="extension">File extension (with or without leading dot).</param>
    /// <returns>Human-readable description.</returns>
    public static string GetDescription(string extension)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "png" => "PNG Image",
            "jpg" or "jpeg" => "JPEG Image",
            "gif" => "GIF Image",
            "bmp" => "Bitmap Image",
            "webp" => "WebP Image",
            "tiff" or "tif" => "TIFF Image",
            "ico" => "Windows Icon",
            "cur" => "Windows Cursor",
            "mp4" => "MP4 Video",
            "webm" => "WebM Video",
            "mkv" => "MKV Video",
            "avi" => "AVI Video",
            "pxp" => "PixlPunkt Document",
            "pxpr" => "PixlPunkt Animation Reel",
            "pxpt" => "PixlPunkt Template",
            "mrk" => "PixlPunkt Brush",
            "json" => "JSON File",
            "txt" => "Text File",
            _ => "File"
        };
    }
}

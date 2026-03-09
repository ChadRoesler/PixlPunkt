using System;
using System.IO;
using System.Threading.Tasks;
using PixlPunkt.Core.Imaging;

namespace PixlPunkt.UI.Voxel;

/// <summary>
/// Pure-static helpers for image export operations (PNG encoding, trimming,
/// upscaling). Extracted from <c>VoxelWorkspaceControl</c>.
/// </summary>
internal static class VoxelImageExporter
{
    internal static void UpscaleNearestBgra(
        byte[] source,
        int sourceWidth,
        int sourceHeight,
        byte[] destination,
        int destinationWidth,
        int destinationHeight,
        int scale)
    {
        if (source == null || destination == null)
            return;
        if (sourceWidth <= 0 || sourceHeight <= 0 || destinationWidth <= 0 || destinationHeight <= 0 || scale <= 0)
            return;
        if (source.Length < sourceWidth * sourceHeight * 4 || destination.Length < destinationWidth * destinationHeight * 4)
            return;

        for (int sy = 0; sy < sourceHeight; sy++)
        {
            int dstBaseY = sy * scale;
            for (int sx = 0; sx < sourceWidth; sx++)
            {
                int si = (sy * sourceWidth + sx) * 4;
                byte b0 = source[si];
                byte b1 = source[si + 1];
                byte b2 = source[si + 2];
                byte b3 = source[si + 3];
                int dstBaseX = sx * scale;

                for (int py = 0; py < scale; py++)
                {
                    int dy = dstBaseY + py;
                    if (dy < 0 || dy >= destinationHeight) continue;
                    for (int px = 0; px < scale; px++)
                    {
                        int dx = dstBaseX + px;
                        if (dx < 0 || dx >= destinationWidth) continue;
                        int di = (dy * destinationWidth + dx) * 4;
                        destination[di] = b0;
                        destination[di + 1] = b1;
                        destination[di + 2] = b2;
                        destination[di + 3] = b3;
                    }
                }
            }
        }
    }

    internal static void TrimTransparentBoundsBgra(
        byte[] source,
        int sourceWidth,
        int sourceHeight,
        int padding,
        out byte[] trimmed,
        out int trimmedWidth,
        out int trimmedHeight)
    {
        trimmed = Array.Empty<byte>();
        trimmedWidth = 1;
        trimmedHeight = 1;

        if (source == null || sourceWidth <= 0 || sourceHeight <= 0 || source.Length < sourceWidth * sourceHeight * 4)
        {
            trimmed = new byte[4];
            return;
        }

        int minX = sourceWidth;
        int minY = sourceHeight;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < sourceHeight; y++)
        {
            int row = y * sourceWidth;
            for (int x = 0; x < sourceWidth; x++)
            {
                int a = source[((row + x) * 4) + 3];
                if (a == 0)
                    continue;

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX || maxY < minY)
        {
            trimmed = new byte[4];
            return;
        }

        padding = Math.Clamp(padding, 0, 128);
        minX = Math.Max(0, minX - padding);
        minY = Math.Max(0, minY - padding);
        maxX = Math.Min(sourceWidth - 1, maxX + padding);
        maxY = Math.Min(sourceHeight - 1, maxY + padding);

        trimmedWidth = Math.Max(1, (maxX - minX) + 1);
        trimmedHeight = Math.Max(1, (maxY - minY) + 1);
        trimmed = new byte[trimmedWidth * trimmedHeight * 4];

        for (int y = 0; y < trimmedHeight; y++)
        {
            int srcY = minY + y;
            int srcOffset = ((srcY * sourceWidth) + minX) * 4;
            int dstOffset = y * trimmedWidth * 4;
            Buffer.BlockCopy(source, srcOffset, trimmed, dstOffset, trimmedWidth * 4);
        }
    }

    internal static async Task<byte[]> EncodeBgraPngBytesAsync(
        int width,
        int height,
        byte[] pixels,
        bool transparentBackground)
    {
        try
        {
            using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,
                stream);

            encoder.SetPixelData(
                Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                transparentBackground
                    ? Windows.Graphics.Imaging.BitmapAlphaMode.Straight
                    : Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                (uint)Math.Max(1, width),
                (uint)Math.Max(1, height),
                96,
                96,
                pixels ?? Array.Empty<byte>());

            await encoder.FlushAsync();
            stream.Seek(0);

            using var ms = new MemoryStream();
            using var src = stream.AsStreamForRead();
            await src.CopyToAsync(ms);
            return ms.ToArray();
        }
        catch (NotImplementedException)
        {
            return await Task.Run(() =>
                SkiaImageEncoder.EncodeToBytes(
                    pixels ?? Array.Empty<byte>(),
                    Math.Max(1, width),
                    Math.Max(1, height),
                    SkiaImageEncoder.ImageFormat.Png));
        }
    }

    internal static async Task SaveBgraPngAsync(
        string filePath,
        int width,
        int height,
        byte[] pixels,
        bool transparentBackground)
    {
        if (!File.Exists(filePath))
        {
            await File.WriteAllBytesAsync(filePath, Array.Empty<byte>());
        }

        try
        {
            var storageFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
            using var stream = await storageFile.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
            var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);

            encoder.SetPixelData(
                Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                transparentBackground
                    ? Windows.Graphics.Imaging.BitmapAlphaMode.Straight
                    : Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                (uint)Math.Max(1, width), (uint)Math.Max(1, height),
                96, 96,
                pixels);

            await encoder.FlushAsync();
        }
        catch (NotImplementedException)
        {
            await Task.Run(() =>
                SkiaImageEncoder.Encode(
                    pixels,
                    Math.Max(1, width),
                    Math.Max(1, height),
                    filePath,
                    SkiaImageEncoder.ImageFormat.Png));
        }
    }

    internal static async Task SaveBgraTextureToPngAsync(string filePath, int width, int height, byte[] pixels)
        => await SaveBgraPngAsync(filePath, width, height, pixels, transparentBackground: false);
}

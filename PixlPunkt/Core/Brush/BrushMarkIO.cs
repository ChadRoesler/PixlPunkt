using System;
using System.IO;
using System.Text;
using PixlPunkt.Constants;
using PixlPunkt.Core.Settings;

namespace PixlPunkt.Core.Brush
{
    /// <summary>
    /// Handles serialization and deserialization of custom brush files (.mrk format).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The .mrk (PixlPunkt Mark) format stores custom brush definitions with:
    /// <br/>• Author and brush name for identification
    /// <br/>• Single 16x16 mask (32 bytes, 1-bit packed)
    /// <br/>• Pivot point for brush alignment
    /// <br/>• Icon preview for UI display
    /// </para>
    /// <para><strong>File Structure (Version 3):</strong></para>
    /// <code>
    /// ┌─────────────────────────────────────────┐
    /// │ Header                                  │
    /// │   Magic: "PXMK" (4 bytes)               │
    /// │   Version: uint16 (2 bytes) = 3         │
    /// │   AuthorLen: uint16 (2 bytes)           │
    /// │   Author: UTF-8 string (AuthorLen)      │
    /// │   NameLen: uint16 (2 bytes)             │
    /// │   Name: UTF-8 string (NameLen bytes)    │
    /// │   PivotX: float (4 bytes)               │
    /// │   PivotY: float (4 bytes)               │
    /// ├─────────────────────────────────────────┤
    /// │ Mask (32 bytes)                         │
    /// │   16x16 1-bit packed mask               │
    /// ├─────────────────────────────────────────┤
    /// │ Icon                                    │
    /// │   Width: uint16 (2 bytes)               │
    /// │   Height: uint16 (2 bytes)              │
    /// │   DataLen: uint32 (4 bytes)             │
    /// │   BGRA: byte[] (W*H*4 bytes)            │
    /// └─────────────────────────────────────────┘
    /// </code>
    /// </remarks>
    public static class BrushMarkIO
    {
        /// <summary>
        /// Magic bytes identifying a .mrk file: "PXMK" (PixlPunkt MarK).
        /// </summary>
        private static readonly byte[] Magic = [(byte)'P', (byte)'X', (byte)'M', (byte)'K'];

        /// <summary>
        /// Current file format version (3 = single 16x16 mask).
        /// </summary>
        private const ushort CurrentVersion = 3;

        /// <summary>
        /// File extension for custom brush files.
        /// </summary>
        public const string FileExtension = ".mrk";

        /// <summary>
        /// Saves a brush template to a file.
        /// </summary>
        /// <param name="brush">The brush template to save.</param>
        /// <param name="filePath">Target file path.</param>
        public static void Save(BrushTemplate brush, string filePath)
        {
            if (brush == null) throw new ArgumentNullException(nameof(brush));
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            using var fs = File.Create(filePath);
            using var bw = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false);

            //////////////////////////////////////////////////////////////////
            // HEADER
            //////////////////////////////////////////////////////////////////

            // Magic
            bw.Write(Magic);

            // Version
            bw.Write(CurrentVersion);

            // Author (length-prefixed UTF-8)
            var authorBytes = Encoding.UTF8.GetBytes(brush.Author ?? BrushExportConstants.DefaultAuthor);
            bw.Write((ushort)authorBytes.Length);
            bw.Write(authorBytes);

            // Name (length-prefixed UTF-8)
            var nameBytes = Encoding.UTF8.GetBytes(brush.Name ?? "");
            bw.Write((ushort)nameBytes.Length);
            bw.Write(nameBytes);

            // Pivot
            bw.Write(brush.PivotX);
            bw.Write(brush.PivotY);

            //////////////////////////////////////////////////////////////////
            // MASK (32 bytes for 16x16 1-bit)
            //////////////////////////////////////////////////////////////////

            // Ensure mask is exactly 32 bytes
            var mask = brush.Mask;
            if (mask == null || mask.Length != 32)
            {
                mask = new byte[32];
                if (brush.Mask != null)
                    Array.Copy(brush.Mask, mask, Math.Min(brush.Mask.Length, 32));
            }
            bw.Write(mask);

            //////////////////////////////////////////////////////////////////
            // ICON
            //////////////////////////////////////////////////////////////////

            bw.Write((ushort)brush.IconWidth);
            bw.Write((ushort)brush.IconHeight);
            bw.Write((uint)(brush.IconData?.Length ?? 0));
            if (brush.IconData != null && brush.IconData.Length > 0)
            {
                bw.Write(brush.IconData);
            }

            bw.Flush();
        }

        /// <summary>
        /// Loads a brush template from a file.
        /// </summary>
        /// <param name="filePath">Source file path.</param>
        /// <returns>The loaded <see cref="BrushTemplate"/>.</returns>
        public static BrushTemplate Load(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException("Brush file not found.", filePath);

            using var fs = File.OpenRead(filePath);
            using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false);

            //////////////////////////////////////////////////////////////////
            // HEADER
            //////////////////////////////////////////////////////////////////

            // Magic
            var magic = br.ReadBytes(4);
            if (magic.Length != 4 || magic[0] != 'P' || magic[1] != 'X' || magic[2] != 'M' || magic[3] != 'K')
                throw new InvalidDataException("Invalid brush file: missing PXMK magic.");

            // Version
            ushort version = br.ReadUInt16();

            // Only support version 3 (single 16x16 mask)
            if (version != 3)
                throw new InvalidDataException($"Brush file version {version} is not supported. Only version 3 is supported.");

            // Author
            ushort authorLen = br.ReadUInt16();
            var authorBytes = br.ReadBytes(authorLen);
            string author = Encoding.UTF8.GetString(authorBytes);

            // Name
            ushort nameLen = br.ReadUInt16();
            var nameBytes = br.ReadBytes(nameLen);
            string name = Encoding.UTF8.GetString(nameBytes);

            // Pivot
            float pivotX = br.ReadSingle();
            float pivotY = br.ReadSingle();

            var brush = new BrushTemplate(author, name)
            {
                PivotX = pivotX,
                PivotY = pivotY
            };

            //////////////////////////////////////////////////////////////////
            // MASK (32 bytes for 16x16 1-bit)
            //////////////////////////////////////////////////////////////////

            brush.Mask = br.ReadBytes(32);

            //////////////////////////////////////////////////////////////////
            // ICON
            //////////////////////////////////////////////////////////////////

            brush.IconWidth = br.ReadUInt16();
            brush.IconHeight = br.ReadUInt16();
            uint iconLen = br.ReadUInt32();
            if (iconLen > 0)
            {
                brush.IconData = br.ReadBytes((int)iconLen);
            }

            return brush;
        }

        /// <summary>
        /// Tries to load a brush template, returning null on failure.
        /// </summary>
        public static BrushTemplate? TryLoad(string filePath)
        {
            try
            {
                return Load(filePath);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the default brush storage directory.
        /// </summary>
        public static string GetBrushDirectory() => AppPaths.BrushesDirectory;

        /// <summary>
        /// Enumerates all .mrk brush files in the default brush directory.
        /// </summary>
        public static System.Collections.Generic.IReadOnlyList<string> EnumerateBrushFiles()
        {
            var dir = GetBrushDirectory();
            if (!Directory.Exists(dir))
                return [];

            return Directory.GetFiles(dir, $"*{FileExtension}", SearchOption.TopDirectoryOnly);
        }
    }
}

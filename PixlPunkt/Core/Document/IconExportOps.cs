using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using PixlPunkt.Core.Compositing.Helpers;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Imaging;

namespace PixlPunkt.Core.Document
{
    /// <summary>
    /// Lets a document supply its own artwork for particular icon sizes, by naming a folder after
    /// the size it is for.
    /// </summary>
    /// <remarks>
    /// Shrinking one detailed drawing all the way down to sixteen pixels turns it to mush, whatever
    /// resampler is used, which is the same problem a font has at a size between grid steps. The
    /// answer is the same too: let the artist draw a simpler version for the small sizes, and fall
    /// back to scaling only where they have not.
    ///
    /// A folder named for a size is drawn at the document's own resolution and scaled down from
    /// there, so it can be authored beside the large art with no second document to keep in step.
    /// Those folders are then kept out of every other size, or the small versions would show up
    /// inside the large one.
    /// </remarks>
    public static class IconExportOps
    {
        /// <summary>Matches "16", "16x16", "16 x 16" and the same with a capital X.</summary>
        private static readonly Regex SizeName = new(
            @"^\s*(\d{1,4})\s*(?:[xX×]\s*(\d{1,4})\s*)?$", RegexOptions.Compiled);

        /// <summary>
        /// The icon size a folder name asks for, or null when the name is not a size.
        /// </summary>
        /// <remarks>
        /// A rectangle is rejected rather than guessed at. Icons are square, and a folder called
        /// "32x16" is far more likely to be a mistake than a request.
        /// </remarks>
        public static int? ParseSizeName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            var match = SizeName.Match(name);
            if (!match.Success) return null;

            if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int width))
                return null;

            if (match.Groups[2].Success)
            {
                if (!int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int height))
                    return null;
                if (height != width) return null;
            }

            return width is > 0 and <= 1024 ? width : null;
        }

        /// <summary>
        /// Every folder in the document that names an icon size, wherever it sits in the tree.
        /// </summary>
        /// <remarks>
        /// The nearest match wins when two folders claim the same size, meaning the first found in
        /// document order. Two folders claiming one size is a mistake either way, and picking one
        /// quietly beats refusing to export.
        /// </remarks>
        public static IReadOnlyDictionary<int, LayerFolder> SizeFolders(CanvasDocument doc)
        {
            var found = new Dictionary<int, LayerFolder>();
            if (doc is null) return found;

            void Walk(IReadOnlyList<LayerBase> items)
            {
                foreach (var item in items)
                {
                    if (item is not LayerFolder folder) continue;

                    if (ParseSizeName(folder.Name) is { } size)
                    {
                        if (!found.ContainsKey(size)) found[size] = folder;
                        continue;   // a size folder's own children are its business
                    }

                    Walk(folder.Children);
                }
            }

            Walk(doc.RootItems);
            return found;
        }

        /// <summary>Whether a particular size has artwork of its own.</summary>
        public static bool HasFolderFor(CanvasDocument doc, int size) =>
            SizeFolders(doc).ContainsKey(size);

        /// <summary>
        /// Which folder's artwork a size is drawn from: its own if it has one, otherwise the
        /// smallest folder larger than it, and null when nothing above it exists.
        /// </summary>
        /// <remarks>
        /// Sizes inherit downward because each one is already a simplification of the one above.
        /// A sixteen with no folder of its own is far better served by a hand-drawn thirty two than
        /// by the full detail of the master, which is what it would otherwise be shrunk from.
        ///
        /// It is the artwork that is inherited, not a chain of reductions. The thirty two folder is
        /// drawn at canvas resolution like every other, so the sixteen is produced by one scale from
        /// that drawing rather than by scaling twice.
        /// </remarks>
        public static int? FolderSizeFor(CanvasDocument doc, int size)
        {
            var folders = SizeFolders(doc);
            if (folders.Count == 0) return null;
            if (folders.ContainsKey(size)) return size;

            int? nearest = null;
            foreach (int candidate in folders.Keys)
            {
                if (candidate <= size) continue;
                if (nearest is null || candidate < nearest) nearest = candidate;
            }

            return nearest;
        }

        /// <summary>
        /// The pixels to shrink for one icon size: the folder named for that size, or failing that
        /// the nearest larger size folder, or failing that the rest of the document with every size
        /// folder left out.
        /// </summary>
        /// <param name="doc">The document being exported.</param>
        /// <param name="size">The icon size being produced.</param>
        /// <returns>A composite at the document's own dimensions, ready to be scaled down.</returns>
        public static byte[] CompositeSourceFor(CanvasDocument doc, int size)
        {
            var surface = new PixelSurface(doc.PixelWidth, doc.PixelHeight);
            var folders = SizeFolders(doc);

            var layers = FolderSizeFor(doc, size) is { } from && folders.TryGetValue(from, out var own)
                ? CollectVisible(own)
                : CollectMaster(doc, folders.Values);

            Compositor.CompositeLinear(layers, surface);
            return surface.Pixels;
        }

        /// <summary>
        /// The master artwork: everything visible except the folders that belong to a single size.
        /// </summary>
        private static List<RasterLayer> CollectMaster(CanvasDocument doc, IEnumerable<LayerFolder> sizeFolders)
        {
            var excluded = new HashSet<LayerFolder>(sizeFolders);
            var layers = new List<RasterLayer>();

            void Walk(IReadOnlyList<LayerBase> items)
            {
                foreach (var item in items)
                {
                    if (item is RasterLayer raster)
                    {
                        if (raster.IsEffectivelyVisible()) layers.Add(raster);
                    }
                    else if (item is LayerFolder folder)
                    {
                        if (excluded.Contains(folder)) continue;
                        if (!folder.IsEffectivelyVisible()) continue;
                        Walk(folder.Children);
                    }
                }
            }

            Walk(doc.RootItems);
            return layers;
        }

        /// <summary>
        /// A size folder's own visible layers. The folder's own visibility is ignored on purpose, so
        /// the small versions can be hidden while drawing and still export.
        /// </summary>
        private static List<RasterLayer> CollectVisible(LayerFolder folder)
        {
            var layers = new List<RasterLayer>();

            void Walk(IReadOnlyList<LayerBase> items)
            {
                foreach (var item in items)
                {
                    if (item is RasterLayer raster)
                    {
                        if (raster.Visible) layers.Add(raster);
                    }
                    else if (item is LayerFolder child && child.Visible)
                    {
                        Walk(child.Children);
                    }
                }
            }

            Walk(folder.Children);
            return layers;
        }
    }
}

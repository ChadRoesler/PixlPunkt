using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using PixlPunkt.Uno.Core.Compositing.Effects;

namespace PixlPunkt.Uno.Core.Document.Layer
{
    internal static class LayerCloneUtil
    {
        public static LayerBase CloneLayerTree(LayerBase src, CanvasDocument doc)
        {
            return src switch
            {
                RasterLayer rl => CloneRaster(rl, doc),
                LayerFolder lf => CloneFolder(lf, doc),
                _ => throw new NotSupportedException($"Unsupported layer type: {src.GetType().Name}")
            };
        }

        private static RasterLayer CloneRaster(RasterLayer src, CanvasDocument doc)
        {
            var dst = new RasterLayer(doc.PixelWidth, doc.PixelHeight, MakeDuplicateName(src.Name));

            // base props
            dst.Visible = src.Visible;
            dst.Locked = src.Locked;
            dst.Opacity = src.Opacity;
            dst.Blend = src.Blend;

            // pixels
            Array.Copy(src.Surface.Pixels, dst.Surface.Pixels, Math.Min(src.Surface.Pixels.Length, dst.Surface.Pixels.Length));
            dst.UpdatePreview();

            // tile mappings (optional)
            if (src.HasTileMappings())
            {
                var srcMap = src.GetOrCreateTileMapping(doc.TileCounts.Width, doc.TileCounts.Height);
                var dstMap = dst.GetOrCreateTileMapping(doc.TileCounts.Width, doc.TileCounts.Height);

                for (int y = 0; y < doc.TileCounts.Height; y++)
                    for (int x = 0; x < doc.TileCounts.Width; x++)
                        dstMap.SetTileId(x, y, srcMap.GetTileId(x, y));
            }

            // effects: ctor created registry list already, copy per ID
            foreach (var srcFx in src.Effects)
            {
                var dstFx = dst.Effects.FirstOrDefault(e => e.EffectId == srcFx.EffectId);
                if (dstFx != null)
                {
                    dstFx.IsEnabled = srcFx.IsEnabled;
                    CopyEffectProps(srcFx, dstFx);
                }
            }

            return dst;
        }

        private static LayerFolder CloneFolder(LayerFolder src, CanvasDocument doc)
        {
            var dst = new LayerFolder(MakeDuplicateName(src.Name))
            {
                Visible = src.Visible,
                Locked = src.Locked,
                IsExpanded = src.IsExpanded
            };

            // preserve model order (bottom->top)
            foreach (var child in src.Children)
            {
                var childClone = CloneLayerTree(child, doc);
                dst.AddChild(childClone);
            }

            return dst;
        }

        /// <summary>
        /// Copies effect properties using reflection.
        /// DynamicDependency attributes ensure properties are preserved during trimming.
        /// </summary>
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(AsciiEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ChromaticAberrationEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ColorAdjustEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(CrtEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(DropShadowEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(GlowBloomEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(GrainEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(OrphanedEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(OutlineEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(PaletteQuantizeEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(PixelateEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ScanLinesEffect))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(VignetteEffect))]
        private static void CopyEffectProps(LayerEffectBase src, LayerEffectBase dst)
        {
            var t = src.GetType();
            if (dst.GetType() != t) return;

            foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!p.CanRead || !p.CanWrite) continue;
                if (p.GetIndexParameters().Length != 0) continue;
                if (p.Name == nameof(LayerEffectBase.IsEnabled)) continue;

                try
                {
                    p.SetValue(dst, p.GetValue(src));
                }
                catch { /* ignore non-copyable props */ }
            }
        }

        private static string MakeDuplicateName(string name)
        {
            name = (name ?? "Layer").Trim();

            const string token = " copy";
            var idx = name.LastIndexOf(token, StringComparison.OrdinalIgnoreCase);

            if (idx < 0)
                return name + token;

            var tail = name[(idx + token.Length)..].Trim();
            if (int.TryParse(tail, out var n))
                return name[..(idx + token.Length)] + $" {n + 1}";

            if (name.EndsWith(token, StringComparison.OrdinalIgnoreCase))
                return name + " 2";

            return name + token;
        }
    }
}

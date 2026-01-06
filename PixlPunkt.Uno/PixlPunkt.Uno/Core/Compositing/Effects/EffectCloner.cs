using System;
using System.Text.Json;
using PixlPunkt.Uno.Core.Serialization;

namespace PixlPunkt.Uno.Core.Compositing.Effects
{
    /// <summary>
    /// Provides methods for cloning layer effects using source-generated JSON serialization.
    /// </summary>
    /// <remarks>
    /// This class avoids reflection-based serialization which fails in trimmed/AOT Release builds.
    /// </remarks>
    public static class EffectCloner
    {
        /// <summary>
        /// Clones a layer effect using type-safe JSON serialization.
        /// </summary>
        /// <param name="source">The effect to clone.</param>
        /// <returns>A deep clone of the effect, or null if cloning fails.</returns>
        public static LayerEffectBase? Clone(LayerEffectBase source)
        {
            if (source == null) return null;

            try
            {
                // Use pattern matching to handle each known effect type with proper serialization
                LayerEffectBase? clone = source switch
                {
                    AsciiEffect e => CloneTyped(e, EffectJsonContext.Default.AsciiEffect),
                    ChromaticAberrationEffect e => CloneTyped(e, EffectJsonContext.Default.ChromaticAberrationEffect),
                    ColorAdjustEffect e => CloneTyped(e, EffectJsonContext.Default.ColorAdjustEffect),
                    CrtEffect e => CloneTyped(e, EffectJsonContext.Default.CrtEffect),
                    DropShadowEffect e => CloneTyped(e, EffectJsonContext.Default.DropShadowEffect),
                    GlowBloomEffect e => CloneTyped(e, EffectJsonContext.Default.GlowBloomEffect),
                    GrainEffect e => CloneTyped(e, EffectJsonContext.Default.GrainEffect),
                    OrphanedEffect e => CloneTyped(e, EffectJsonContext.Default.OrphanedEffect),
                    OutlineEffect e => CloneTyped(e, EffectJsonContext.Default.OutlineEffect),
                    PaletteQuantizeEffect e => CloneTyped(e, EffectJsonContext.Default.PaletteQuantizeEffect),
                    PixelateEffect e => CloneTyped(e, EffectJsonContext.Default.PixelateEffect),
                    ScanLinesEffect e => CloneTyped(e, EffectJsonContext.Default.ScanLinesEffect),
                    VignetteEffect e => CloneTyped(e, EffectJsonContext.Default.VignetteEffect),
                    _ => CloneFallback(source) // For plugin effects or unknown types
                };

                if (clone != null)
                {
                    clone.EffectId = source.EffectId;
                }

                return clone;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EffectCloner] Failed to clone effect: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Clones a typed effect using source-generated JSON type info.
        /// </summary>
        private static T? CloneTyped<T>(T effect, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
            where T : LayerEffectBase
        {
            var json = JsonSerializer.Serialize(effect, typeInfo);
            return JsonSerializer.Deserialize(json, typeInfo);
        }

        /// <summary>
        /// Fallback clone method for unknown effect types (plugin effects).
        /// Uses manual property copying since reflection-based serialization isn't available.
        /// </summary>
        private static LayerEffectBase? CloneFallback(LayerEffectBase source)
        {
            // For plugin effects, we can't use reflection-based serialization in trimmed builds.
            // Return a shallow reference or create a simple copy based on the base properties.
            // Plugin effects should ideally implement ICloneable or provide their own clone method.

            System.Diagnostics.Debug.WriteLine($"[EffectCloner] Unknown effect type '{source.GetType().Name}' - clone may be incomplete");

            // Return null for unknown types - this is safer than returning a broken clone
            return null;
        }
    }
}

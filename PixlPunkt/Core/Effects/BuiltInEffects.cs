using PixlPunkt.Core.Compositing.Effects;
using PixlPunkt.Core.Effects.Builders;
using PixlPunkt.Core.Effects.Settings;

namespace PixlPunkt.Core.Effects
{
    /// <summary>
    /// Static class for registering all built-in layer effects at application startup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class serves as the entry point for initializing the effect system. Call
    /// <see cref="RegisterAll"/> during application initialization (typically in <c>App.OnLaunched</c>)
    /// to populate the <see cref="EffectRegistry.Shared"/> with all built-in effects.
    /// </para>
    /// <para>
    /// <strong>Built-in Effects:</strong>
    /// </para>
    /// <list type="table">
    /// <listheader>
    /// <term>Category</term>
    /// <description>Effects</description>
    /// </listheader>
    /// <item>
    /// <term>Stylize</term>
    /// <description>Drop Shadow, Outline, Glow/Bloom, Chromatic Aberration</description>
    /// </item>
    /// <item>
    /// <term>Filter</term>
    /// <description>Scan Lines, Grain, Vignette, CRT, Pixelate</description>
    /// </item>
    /// <item>
    /// <term>Color</term>
    /// <description>Color Adjust, Palette Quantize, ASCII</description>
    /// </item>
    /// </list>
    /// </remarks>
    /// <seealso cref="EffectRegistry"/>
    /// <seealso cref="EffectBuilder"/>
    public static class BuiltInEffects
    {
        private static bool _registered;

        /// <summary>
        /// Registers all built-in effects with the shared registry.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is idempotent - calling it multiple times has no additional effect.
        /// The first call registers all 12 built-in effects and raises the
        /// <see cref="IEffectRegistry.EffectsChanged"/> event.
        /// </para>
        /// <para>
        /// Effects are registered in category order: Stylize, then Filter, then Color.
        /// Uses the fluent <see cref="EffectBuilder"/> API for consistent registration pattern.
        /// </para>
        /// </remarks>
        public static void RegisterAll()
        {
            if (_registered) return;
            _registered = true;

            var registry = EffectRegistry.Shared;

            //////////////////////////////////////////////////////////////////
            // STYLIZE EFFECTS
            //////////////////////////////////////////////////////////////////
            registry.AddEffect(EffectIds.DropShadow)
                .WithCategory(EffectCategory.Stylize)
                .WithDisplayName("Drop Shadow")
                .WithDescription("Adds a shadow beneath the layer with configurable offset and blur.")
                .WithFactory<DropShadowEffect>()
                .WithSettings(e => e is DropShadowEffect ds ? new DropShadowEffectSettings(ds) : null)
                .Register();

            registry.AddEffect(EffectIds.Outline)
                .WithCategory(EffectCategory.Stylize)
                .WithDisplayName("Outline")
                .WithDescription("Draws a colored outline around the opaque regions of the layer.")
                .WithFactory<OutlineEffect>()
                .WithSettings(e => e is OutlineEffect o ? new OutlineEffectSettings(o) : null)
                .Register();

            registry.AddEffect(EffectIds.GlowBloom)
                .WithCategory(EffectCategory.Stylize)
                .WithDisplayName("Glow / Bloom")
                .WithDescription("Adds a soft glow or bloom around bright areas of the layer.")
                .WithFactory<GlowBloomEffect>()
                .WithSettings(e => e is GlowBloomEffect gb ? new GlowBloomEffectSettings(gb) : null)
                .Register();

            registry.AddEffect(EffectIds.ChromaticAberration)
                .WithCategory(EffectCategory.Stylize)
                .WithDisplayName("Chromatic Aberration")
                .WithDescription("Simulates lens chromatic aberration by splitting color channels.")
                .WithFactory<ChromaticAberrationEffect>()
                .WithSettings(e => e is ChromaticAberrationEffect ca ? new ChromaticAberrationEffectSettings(ca) : null)
                .Register();

            //////////////////////////////////////////////////////////////////
            // FILTER EFFECTS
            //////////////////////////////////////////////////////////////////
            registry.AddEffect(EffectIds.ScanLines)
                .WithCategory(EffectCategory.Filter)
                .WithDisplayName("Scan Lines")
                .WithDescription("Adds horizontal scan lines to simulate CRT monitor appearance.")
                .WithFactory<ScanLinesEffect>()
                .WithSettings(e => e is ScanLinesEffect sl ? new ScanLinesEffectSettings(sl) : null)
                .Register();

            registry.AddEffect(EffectIds.Grain)
                .WithCategory(EffectCategory.Filter)
                .WithDisplayName("Grain")
                .WithDescription("Adds film grain or noise texture to the layer.")
                .WithFactory<GrainEffect>()
                .WithSettings(e => e is GrainEffect g ? new GrainEffectSettings(g) : null)
                .Register();

            registry.AddEffect(EffectIds.Vignette)
                .WithCategory(EffectCategory.Filter)
                .WithDisplayName("Vignette")
                .WithDescription("Darkens the edges of the layer to draw focus to the center.")
                .WithFactory<VignetteEffect>()
                .WithSettings(e => e is VignetteEffect v ? new VignetteEffectSettings(v) : null)
                .Register();

            registry.AddEffect(EffectIds.Crt)
                .WithCategory(EffectCategory.Filter)
                .WithDisplayName("CRT")
                .WithDescription("Simulates a cathode ray tube monitor with curvature and scan effects.")
                .WithFactory<CrtEffect>()
                .WithSettings(e => e is CrtEffect c ? new CrtEffectSettings(c) : null)
                .Register();

            registry.AddEffect(EffectIds.Pixelate)
                .WithCategory(EffectCategory.Filter)
                .WithDisplayName("Pixelate")
                .WithDescription("Reduces resolution to create a pixelated mosaic effect.")
                .WithFactory<PixelateEffect>()
                .WithSettings(e => e is PixelateEffect p ? new PixelateEffectSettings(p) : null)
                .Register();

            //////////////////////////////////////////////////////////////////
            // COLOR EFFECTS
            //////////////////////////////////////////////////////////////////
            registry.AddEffect(EffectIds.ColorAdjust)
                .WithCategory(EffectCategory.Color)
                .WithDisplayName("Color Adjust")
                .WithDescription("Adjusts hue, saturation, and brightness in HSV color space.")
                .WithFactory<ColorAdjustEffect>()
                .WithSettings(e => e is ColorAdjustEffect ca ? new ColorAdjustEffectSettings(ca) : null)
                .Register();

            registry.AddEffect(EffectIds.PaletteQuantize)
                .WithCategory(EffectCategory.Color)
                .WithDisplayName("Palette Quantize")
                .WithDescription("Reduces colors to match a specific palette using quantization.")
                .WithFactory<PaletteQuantizeEffect>()
                .WithSettings(e => e is PaletteQuantizeEffect pq ? new PaletteQuantizeEffectSettings(pq) : null)
                .Register();

            registry.AddEffect(EffectIds.Ascii)
                .WithCategory(EffectCategory.Color)
                .WithDisplayName("ASCII")
                .WithDescription("Converts the layer to ASCII art representation.")
                .WithFactory<AsciiEffect>()
                .WithSettings(e => e is AsciiEffect a ? new AsciiEffectSettings(a) : null)
                .Register();

            // Notify UI that effects are available
            if (registry is EffectRegistry r)
                r.NotifyEffectsChanged();
        }
    }
}

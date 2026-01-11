using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Settings;

namespace PixlPunkt.UI
{
    /// <summary>
    /// Partial class for theme management:
    /// - Application-wide theme control (Light/Dark/System)
    /// - Stripe/transparency pattern theme control
    /// - Theme synchronization across all hosts
    /// </summary>
    public sealed partial class PixlPunktMainWindow : Window
    {
        //////////////////////////////////////////////////////////////////
        // THEME STATE
        //////////////////////////////////////////////////////////////////

        private ElementTheme? _stripeForcedTheme = null;

        /// <summary>
        /// Gets the forced stripe theme, if any.
        /// </summary>
        public ElementTheme? StripeForcedTheme => _stripeForcedTheme;

        //////////////////////////////////////////////////////////////////
        // APPLICATION THEME MANAGEMENT
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the application-wide theme (affects all UI elements).
        /// </summary>
        /// <param name="choice">The theme choice (System, Light, or Dark).</param>
        public void SetAppTheme(AppThemeChoice choice)
        {
            var theme = choice switch
            {
                AppThemeChoice.Light => ElementTheme.Light,
                AppThemeChoice.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default // System follows OS preference
            };

            // Apply to the root element first
            if (Root != null)
            {
                Root.RequestedTheme = theme;
            }

            // Explicitly set theme on UserControls that don't inherit properly
            // This is needed because UserControls don't automatically inherit RequestedTheme
            // Setting RequestedTheme will trigger ActualThemeChanged on the control,
            // which causes ThemeResource bindings to re-evaluate
            if (ToolRail != null)
            {
                ToolRail.RequestedTheme = theme;
            }
            if (OptionsBar != null)
            {
                OptionsBar.RequestedTheme = theme;
            }

            // Also apply to the right sidebar panels (they're wrapped in SectionCards)
            if (PreviewCard != null) PreviewCard.RequestedTheme = theme;
            if (PaletteCard != null) PaletteCard.RequestedTheme = theme;
            if (TilesCard != null) TilesCard.RequestedTheme = theme;
            if (LayersCard != null) LayersCard.RequestedTheme = theme;

            // Sync stripe theme if it follows app theme
            if (AppSettings.Instance.StripeTheme == StripeThemeChoice.System)
            {
                SyncStripeTheme();
            }
        }

        /// <summary>
        /// Gets the currently effective application theme.
        /// </summary>
        public ElementTheme EffectiveAppTheme => Root?.ActualTheme ?? ElementTheme.Dark;

        //////////////////////////////////////////////////////////////////
        // STRIPE THEME MANAGEMENT
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets the stripe/transparency pattern theme choice.
        /// </summary>
        /// <param name="choice">The theme choice (System, Light, or Dark).</param>
        public void SetStripeTheme(StripeThemeChoice choice)
        {
            switch (choice)
            {
                case StripeThemeChoice.System:
                    _stripeForcedTheme = null;
                    break;
                case StripeThemeChoice.Light:
                    _stripeForcedTheme = ElementTheme.Light;
                    break;
                case StripeThemeChoice.Dark:
                    _stripeForcedTheme = ElementTheme.Dark;
                    break;
            }
            SyncStripeTheme();
        }

        /// <summary>
        /// Synchronizes the stripe theme across all canvas hosts and the export mixer.
        /// </summary>
        private void SyncStripeTheme()
        {
            var effectiveTheme = _stripeForcedTheme ?? Root.ActualTheme;

            // Update export mixer (used by preview compositing during export dialogs)
            Rendering.TransparencyStripeMixer.ApplyTheme(effectiveTheme);

            // Update all open tab hosts
            foreach (var item in DocsTab.TabItems)
            {
                if (item is TabViewItem tvi && tvi.Content is CanvasHost.CanvasViewHost host)
                    host.UpdateTransparencyPatternForTheme(effectiveTheme);
            }

            // Detached document windows
            foreach (var kv in _docWindows)
                kv.Value.Host?.UpdateTransparencyPatternForTheme(effectiveTheme);
        }
    }
}

using System.Collections.Generic;
using FluentIcons.Common;
using VirtualKey = PixlPunkt.PluginSdk.Settings.VirtualKey;

namespace PixlPunkt.Uno.Core.Tools.Settings
{
    /// <summary>
    /// Settings for the Dropper (eyedropper/color picker) utility tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Dropper samples colors from the canvas. Settings include:
    /// </para>
    /// <list type="bullet">
    /// <item>Icon and shortcut for toolbar</item>
    /// <item>Future: sample size (1px, 3x3 avg, 5x5 avg)</item>
    /// <item>Future: sample from current layer vs. composite</item>
    /// </list>
    /// </remarks>
    public sealed class DropperToolSettings : ToolSettingsBase
    {
        /// <inheritdoc/>
        public override Icon Icon => Icon.Eyedropper;

        /// <inheritdoc/>
        public override string DisplayName => "Dropper";

        /// <inheritdoc/>
        public override string Description => "Sample colors from canvas (I key, or RMB hold)";

        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.I);

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            // Dropper has no configurable options currently
            // Future: sample size, sample source (layer vs composite)
            yield break;
        }
    }
}

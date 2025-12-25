using System.Collections.Generic;
using FluentIcons.Common;
using VirtualKey = PixlPunkt.PluginSdk.Settings.VirtualKey;

namespace PixlPunkt.Core.Tools.Settings
{
    /// <summary>
    /// Settings for the Pan utility tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pan allows viewport navigation via drag. Settings include:
    /// </para>
    /// <list type="bullet">
    /// <item>Icon and shortcut for toolbar</item>
    /// <item>Future: inertia, acceleration settings</item>
    /// </list>
    /// </remarks>
    public sealed class PanToolSettings : ToolSettingsBase
    {
        /// <inheritdoc/>
        public override Icon Icon => Icon.HandLeft;

        /// <inheritdoc/>
        public override string DisplayName => "Pan";

        /// <inheritdoc/>
        public override string Description => "Pan the canvas view (Space hold, or MMB drag)";

        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.H);

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            // Pan has no configurable options currently
            yield break;
        }
    }
}

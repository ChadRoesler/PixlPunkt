using System.Collections.Generic;
using FluentIcons.Common;
using VirtualKey = PixlPunkt.PluginSdk.Settings.VirtualKey;

namespace PixlPunkt.Core.Tools.Settings
{
    /// <summary>
    /// Settings for the Zoom utility tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Zoom allows viewport magnification. Settings include:
    /// </para>
    /// <list type="bullet">
    /// <item>Icon and shortcut for toolbar</item>
    /// <item>Future: zoom increment, smooth zoom settings</item>
    /// </list>
    /// </remarks>
    public sealed class ZoomToolSettings : ToolSettingsBase
    {
        /// <inheritdoc/>
        public override Icon Icon => Icon.ZoomIn;

        /// <inheritdoc/>
        public override string DisplayName => "Zoom";

        /// <inheritdoc/>
        public override string Description => "Zoom in (LMB) or out (RMB) on the canvas";

        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.Z);

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            // Zoom has no configurable options currently
            yield break;
        }
    }
}

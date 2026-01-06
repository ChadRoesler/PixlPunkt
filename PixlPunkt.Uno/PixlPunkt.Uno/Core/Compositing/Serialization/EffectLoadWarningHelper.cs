using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PixlPunkt.Uno.Core.Compositing.Serialization
{
    /// <summary>
    /// Provides helper methods for working with effect load warnings.
    /// </summary>
    public static class EffectLoadWarningHelper
    {
        /// <summary>
        /// Gets whether there are any plugin-related warnings.
        /// </summary>
        /// <param name="warnings">The warnings to check.</param>
        /// <returns>True if any warnings are related to missing plugins.</returns>
        public static bool HasPluginWarnings(IEnumerable<EffectLoadWarning> warnings)
        {
            return warnings.Any(w => w.IsPluginEffect);
        }

        /// <summary>
        /// Gets a user-friendly summary message for the warnings.
        /// </summary>
        /// <param name="warnings">The warnings to summarize.</param>
        /// <returns>A formatted message suitable for display in a dialog.</returns>
        public static string GetSummaryMessage(IEnumerable<EffectLoadWarning> warnings)
        {
            var warningList = warnings.ToList();
            if (warningList.Count == 0)
                return string.Empty;

            var pluginWarnings = warningList.Where(w => w.IsPluginEffect).ToList();
            var otherWarnings = warningList.Where(w => !w.IsPluginEffect).ToList();

            var sb = new StringBuilder();

            if (pluginWarnings.Count > 0)
            {
                sb.AppendLine("Some effects could not be loaded because their plugins are not installed:");
                sb.AppendLine();

                // Group by plugin
                var byPlugin = pluginWarnings.GroupBy(w => w.PluginId);
                foreach (var group in byPlugin)
                {
                    sb.AppendLine($"� Plugin '{group.Key}':");
                    foreach (var warning in group)
                    {
                        sb.AppendLine($"    - {warning.DisplayName}");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("These effects have been disabled but their settings are preserved.");
                sb.AppendLine("Install the missing plugins to restore them.");
            }

            if (otherWarnings.Count > 0)
            {
                if (pluginWarnings.Count > 0)
                    sb.AppendLine();

                sb.AppendLine("Some effects encountered errors during loading:");
                sb.AppendLine();

                foreach (var warning in otherWarnings)
                {
                    sb.AppendLine($"� {warning.DisplayName}: {warning.Message}");
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Gets a 
        /// title for the warnings dialog.
        /// </summary>
        /// <param name="warnings">The warnings to summarize.</param>
        /// <returns>A short title string.</returns>
        public static string GetTitle(IEnumerable<EffectLoadWarning> warnings)
        {
            var warningList = warnings.ToList();
            if (warningList.Count == 0)
                return string.Empty;

            int pluginCount = warningList.Count(w => w.IsPluginEffect);
            int otherCount = warningList.Count - pluginCount;

            if (pluginCount > 0 && otherCount > 0)
                return "Document Loaded with Warnings";
            else if (pluginCount > 0)
                return pluginCount == 1 ? "Missing Plugin Effect" : "Missing Plugin Effects";
            else
                return otherCount == 1 ? "Effect Loading Warning" : "Effect Loading Warnings";
        }

        /// <summary>
        /// Gets the list of unique missing plugin IDs.
        /// </summary>
        /// <param name="warnings">The warnings to check.</param>
        /// <returns>List of distinct plugin IDs that are missing.</returns>
        public static IReadOnlyList<string> GetMissingPluginIds(IEnumerable<EffectLoadWarning> warnings)
        {
            return warnings
                .Where(w => w.IsPluginEffect)
                .Select(w => w.PluginId)
                .Distinct()
                .ToList();
        }
    }
}

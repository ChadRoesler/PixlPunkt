using System;
using System.Collections.Generic;
using System.Linq;
using PixlPunkt.Uno.Core.Logging;
using PixlPunkt.Uno.Core.Tools;
using PixlPunkt.Uno.Core.Tools.Settings;
using PixlPunkt.PluginSdk.Settings;

namespace PixlPunkt.Uno.Core.Settings
{
    /// <summary>
    /// Detects keyboard shortcut conflicts between tools (built-in and plugins).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The conflict detector scans all registered tools and their shortcuts to identify
    /// cases where multiple tools share the same keyboard binding. Conflicts are reported
    /// at startup so users can be warned and resolve them via the settings window.
    /// </para>
    /// </remarks>
    public sealed class ShortcutConflictDetector
    {
        private readonly ToolState _toolState;
        private readonly ShortcutSettings _shortcutSettings;

        /// <summary>
        /// Initializes a new instance of <see cref="ShortcutConflictDetector"/>.
        /// </summary>
        /// <param name="toolState">The tool state containing all registered tools.</param>
        /// <param name="shortcutSettings">The shortcut settings with custom bindings.</param>
        public ShortcutConflictDetector(ToolState toolState, ShortcutSettings? shortcutSettings = null)
        {
            _toolState = toolState ?? throw new ArgumentNullException(nameof(toolState));
            _shortcutSettings = shortcutSettings ?? ShortcutSettings.Instance;
        }

        /// <summary>
        /// Gets all effective shortcut bindings for registered tools.
        /// </summary>
        /// <returns>
        /// A dictionary mapping tool IDs to their effective shortcut bindings.
        /// Tools without shortcuts are excluded.
        /// </returns>
        public Dictionary<string, (KeyBinding Binding, string DisplayName)> GetAllEffectiveBindings()
        {
            var result = new Dictionary<string, (KeyBinding, string)>();

            // Built-in tools
            foreach (var (toolId, settings) in _toolState.GetAllToolSettingsById())
            {
                var effectiveBinding = _shortcutSettings.GetEffectiveBinding(toolId, settings.Shortcut);
                if (effectiveBinding != null)
                {
                    result[toolId] = (effectiveBinding, settings.DisplayName);
                }
            }

            // Plugin tools
            foreach (var registration in _toolState.AllRegistrations)
            {
                // Skip if already added (built-in)
                if (result.ContainsKey(registration.Id))
                    continue;

                if (registration.Settings?.Shortcut != null)
                {
                    var effectiveBinding = _shortcutSettings.GetEffectiveBinding(
                        registration.Id,
                        registration.Settings.Shortcut);

                    if (effectiveBinding != null)
                    {
                        result[registration.Id] = (effectiveBinding, registration.DisplayName);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Detects all shortcut conflicts among registered tools.
        /// </summary>
        /// <param name="includePlugins">Whether to include plugin tools in conflict detection.</param>
        /// <returns>A list of detected conflicts.</returns>
        public List<ShortcutConflict> DetectConflicts(bool includePlugins = true)
        {
            var conflicts = new List<ShortcutConflict>();
            var bindingGroups = new Dictionary<string, List<(string ToolId, string DisplayName, KeyBinding Binding)>>();

            // Gather all effective bindings
            foreach (var (toolId, settings) in _toolState.GetAllToolSettingsById())
            {
                var effectiveBinding = _shortcutSettings.GetEffectiveBinding(toolId, settings.Shortcut);
                if (effectiveBinding == null)
                    continue;

                var bindingKey = GetBindingKey(effectiveBinding);
                if (!bindingGroups.ContainsKey(bindingKey))
                    bindingGroups[bindingKey] = [];

                bindingGroups[bindingKey].Add((toolId, settings.DisplayName, effectiveBinding));
            }

            // Plugin tools
            if (includePlugins)
            {
                foreach (var registration in _toolState.AllRegistrations)
                {
                    // Skip built-in tools (already processed)
                    if (ToolIds.IsBuiltIn(registration.Id))
                        continue;

                    if (registration.Settings?.Shortcut == null)
                        continue;

                    var effectiveBinding = _shortcutSettings.GetEffectiveBinding(
                        registration.Id,
                        registration.Settings.Shortcut);

                    if (effectiveBinding == null)
                        continue;

                    var bindingKey = GetBindingKey(effectiveBinding);
                    if (!bindingGroups.ContainsKey(bindingKey))
                        bindingGroups[bindingKey] = [];

                    bindingGroups[bindingKey].Add((registration.Id, registration.DisplayName, effectiveBinding));
                }
            }

            // Find conflicts (bindings with more than one tool)
            foreach (var (bindingKey, tools) in bindingGroups)
            {
                if (tools.Count > 1)
                {
                    var conflict = new ShortcutConflict
                    {
                        BindingKey = bindingKey,
                        ShortcutDisplay = tools[0].Binding.ToString(),
                        ConflictingToolIds = tools.Select(t => t.ToolId).ToList(),
                        ConflictingToolNames = tools.Select(t => t.DisplayName).ToList()
                    };

                    conflicts.Add(conflict);

                    LoggingService.Warning(
                        "Shortcut conflict detected: {Shortcut} is bound to multiple tools: {Tools}",
                        conflict.ShortcutDisplay,
                        string.Join(", ", conflict.ConflictingToolNames));
                }
            }

            return conflicts;
        }

        /// <summary>
        /// Detects conflicts that haven't been dismissed by the user.
        /// </summary>
        /// <returns>A list of undismissed conflicts.</returns>
        public List<ShortcutConflict> DetectUndismissedConflicts()
        {
            if (!_shortcutSettings.ShowConflictWarnings)
                return [];

            return DetectConflicts()
                .Where(c => !_shortcutSettings.IsConflictDismissed(c.BindingKey))
                .ToList();
        }

        /// <summary>
        /// Checks if a proposed binding would conflict with existing bindings.
        /// </summary>
        /// <param name="toolId">The tool ID being modified.</param>
        /// <param name="proposedBinding">The proposed new binding.</param>
        /// <returns>A list of tool IDs that would conflict, or empty if no conflict.</returns>
        public List<(string ToolId, string DisplayName)> CheckForConflicts(string toolId, KeyBinding proposedBinding)
        {
            var conflicts = new List<(string, string)>();
            var proposedKey = GetBindingKey(proposedBinding);

            // Check built-in tools
            foreach (var (otherToolId, settings) in _toolState.GetAllToolSettingsById())
            {
                if (otherToolId == toolId)
                    continue;

                var effectiveBinding = _shortcutSettings.GetEffectiveBinding(otherToolId, settings.Shortcut);
                if (effectiveBinding != null && GetBindingKey(effectiveBinding) == proposedKey)
                {
                    conflicts.Add((otherToolId, settings.DisplayName));
                }
            }

            // Check plugin tools
            foreach (var registration in _toolState.AllRegistrations)
            {
                if (registration.Id == toolId)
                    continue;

                // Skip if already checked (built-in)
                if (conflicts.Any(c => c.Item1 == registration.Id))
                    continue;

                if (registration.Settings?.Shortcut == null)
                    continue;

                var effectiveBinding = _shortcutSettings.GetEffectiveBinding(
                    registration.Id,
                    registration.Settings.Shortcut);

                if (effectiveBinding != null && GetBindingKey(effectiveBinding) == proposedKey)
                {
                    conflicts.Add((registration.Id, registration.DisplayName));
                }
            }

            return conflicts;
        }

        /// <summary>
        /// Gets a unique key for a binding (for grouping/comparison).
        /// </summary>
        private static string GetBindingKey(KeyBinding binding)
        {
            return $"{(int)binding.Key}:{binding.Ctrl}:{binding.Shift}:{binding.Alt}";
        }
    }
}

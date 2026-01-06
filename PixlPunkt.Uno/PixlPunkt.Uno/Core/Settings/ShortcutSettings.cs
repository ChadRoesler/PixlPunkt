using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PixlPunkt.Uno.Core.Logging;
using PixlPunkt.Uno.Core.Serialization;
using PixlPunkt.PluginSdk.Settings;

namespace PixlPunkt.Uno.Core.Settings
{
    /// <summary>
    /// Represents a custom keyboard shortcut binding for a tool.
    /// </summary>
    public sealed class ShortcutBinding
    {
        /// <summary>
        /// Gets or sets the tool ID this shortcut is for.
        /// </summary>
        public string ToolId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the virtual key code.
        /// </summary>
        public int KeyCode { get; set; }

        /// <summary>
        /// Gets or sets whether Ctrl modifier is required.
        /// </summary>
        public bool Ctrl { get; set; }

        /// <summary>
        /// Gets or sets whether Shift modifier is required.
        /// </summary>
        public bool Shift { get; set; }

        /// <summary>
        /// Gets or sets whether Alt modifier is required.
        /// </summary>
        public bool Alt { get; set; }

        /// <summary>
        /// Creates a KeyBinding from this shortcut binding.
        /// </summary>
        public KeyBinding ToKeyBinding() => new((VirtualKey)KeyCode, Ctrl, Shift, Alt);

        /// <summary>
        /// Creates a ShortcutBinding from a KeyBinding.
        /// </summary>
        public static ShortcutBinding FromKeyBinding(string toolId, KeyBinding binding)
        {
            return new ShortcutBinding
            {
                ToolId = toolId,
                KeyCode = (int)binding.Key,
                Ctrl = binding.Ctrl,
                Shift = binding.Shift,
                Alt = binding.Alt
            };
        }

        /// <summary>
        /// Returns a human-readable string representation of the shortcut.
        /// </summary>
        public override string ToString() => ToKeyBinding().ToString();

        /// <summary>
        /// Gets a unique key for this binding (for conflict detection).
        /// </summary>
        public string GetBindingKey() => $"{KeyCode}:{Ctrl}:{Shift}:{Alt}";
    }

    /// <summary>
    /// Represents a shortcut conflict between two or more tools.
    /// </summary>
    public sealed class ShortcutConflict
    {
        /// <summary>
        /// Gets or sets the binding key that is conflicting.
        /// </summary>
        public string BindingKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human-readable shortcut string.
        /// </summary>
        public string ShortcutDisplay { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of tool IDs that conflict.
        /// </summary>
        public List<string> ConflictingToolIds { get; set; } = [];

        /// <summary>
        /// Gets or sets the list of tool display names that conflict.
        /// </summary>
        public List<string> ConflictingToolNames { get; set; } = [];
    }

    /// <summary>
    /// Manages custom shortcut settings persisted to JSON file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shortcut settings are stored in <c>%LocalAppData%/PixlPunkt/shortcuts.json</c>.
    /// This allows users to customize tool keyboard shortcuts while preserving defaults.
    /// </para>
    /// </remarks>
    public sealed class ShortcutSettings
    {
        private static ShortcutSettings? _instance;

        /// <summary>
        /// Gets the singleton settings instance.
        /// </summary>
        public static ShortcutSettings Instance => _instance ??= Load();

        /// <summary>
        /// Gets the path to the shortcuts JSON file.
        /// </summary>
        public static string ShortcutsFilePath => Path.Combine(AppPaths.RootDirectory, "shortcuts.json");

        // ====================================================================
        // SETTINGS PROPERTIES
        // ====================================================================

        /// <summary>
        /// Gets or sets the custom shortcut bindings.
        /// Key is the tool ID, value is the custom shortcut binding.
        /// </summary>
        public Dictionary<string, ShortcutBinding> CustomBindings { get; set; } = [];

        /// <summary>
        /// Gets or sets whether to show conflict warnings on startup.
        /// </summary>
        public bool ShowConflictWarnings { get; set; } = true;

        /// <summary>
        /// Gets or sets tool IDs whose conflicts have been acknowledged/dismissed.
        /// </summary>
        public HashSet<string> DismissedConflicts { get; set; } = [];

        // ====================================================================
        // METHODS
        // ====================================================================

        /// <summary>
        /// Gets the effective shortcut for a tool, considering custom overrides.
        /// </summary>
        /// <param name="toolId">The tool ID.</param>
        /// <param name="defaultBinding">The default binding from the tool settings.</param>
        /// <returns>The custom binding if set, otherwise the default.</returns>
        public KeyBinding? GetEffectiveBinding(string toolId, KeyBinding? defaultBinding)
        {
            if (CustomBindings.TryGetValue(toolId, out var custom))
                return custom.ToKeyBinding();
            return defaultBinding;
        }

        /// <summary>
        /// Sets a custom shortcut binding for a tool.
        /// </summary>
        /// <param name="toolId">The tool ID.</param>
        /// <param name="binding">The new binding, or null to reset to default.</param>
        public void SetCustomBinding(string toolId, KeyBinding? binding)
        {
            if (binding == null)
            {
                CustomBindings.Remove(toolId);
            }
            else
            {
                CustomBindings[toolId] = ShortcutBinding.FromKeyBinding(toolId, binding);
            }
        }

        /// <summary>
        /// Resets a tool's shortcut to its default.
        /// </summary>
        /// <param name="toolId">The tool ID to reset.</param>
        public void ResetToDefault(string toolId)
        {
            CustomBindings.Remove(toolId);
        }

        /// <summary>
        /// Resets all shortcuts to defaults.
        /// </summary>
        public void ResetAllToDefaults()
        {
            CustomBindings.Clear();
            DismissedConflicts.Clear();
        }

        /// <summary>
        /// Dismisses a conflict for a binding key (user acknowledges it).
        /// </summary>
        /// <param name="bindingKey">The binding key to dismiss.</param>
        public void DismissConflict(string bindingKey)
        {
            DismissedConflicts.Add(bindingKey);
        }

        /// <summary>
        /// Checks if a conflict has been dismissed.
        /// </summary>
        public bool IsConflictDismissed(string bindingKey)
        {
            return DismissedConflicts.Contains(bindingKey);
        }

        // ====================================================================
        // LOAD / SAVE
        // ====================================================================

        /// <summary>
        /// Loads shortcut settings from the JSON file, or returns defaults if not found.
        /// </summary>
        /// <returns>The loaded or default settings instance.</returns>
        public static ShortcutSettings Load()
        {
            try
            {
                var path = ShortcutsFilePath;
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var loaded = JsonSerializer.Deserialize(json, ShortcutSettingsJsonContext.Default.ShortcutSettings);
                    if (loaded != null)
                    {
                        LoggingService.Debug("Loaded shortcut settings with {Count} custom bindings",
                            loaded.CustomBindings.Count);
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Warning("Failed to load shortcut settings, using defaults: {Error}", ex.Message);
            }

            return new ShortcutSettings();
        }

        /// <summary>
        /// Saves current shortcut settings to the JSON file.
        /// </summary>
        public void Save()
        {
            try
            {
                var path = ShortcutsFilePath;
                AppPaths.EnsureDirectoryExists(AppPaths.RootDirectory);

                var json = JsonSerializer.Serialize(this, ShortcutSettingsJsonContext.Default.ShortcutSettings);
                File.WriteAllText(path, json);

                LoggingService.Debug("Saved shortcut settings with {Count} custom bindings",
                    CustomBindings.Count);
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to save shortcut settings", ex);
            }
        }

        /// <summary>
        /// Reloads settings from disk, updating the singleton instance.
        /// </summary>
        public static void Reload()
        {
            _instance = null;
            _ = Instance; // Force reload
        }
    }
}

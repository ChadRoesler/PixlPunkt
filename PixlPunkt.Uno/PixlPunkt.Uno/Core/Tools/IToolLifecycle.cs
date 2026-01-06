namespace PixlPunkt.Uno.Core.Tools
{
    /// <summary>
    /// Lifecycle interface for tools that need to respond to activation and deactivation events.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IToolLifecycle"/> provides hooks for tools that need to perform setup or cleanup
    /// when the user switches tools. This is optional - implement only if your tool requires it.
    /// </para>
    /// <para>
    /// <strong>Common Use Cases:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item>Selection tools: Reset drag state, clear preview geometry</item>
    /// <item>Custom tools: Start/stop timers, subscribe/unsubscribe from events</item>
    /// <item>Resource-heavy tools: Load/unload assets on demand</item>
    /// <item>Stateful tools: Save/restore tool state across activations</item>
    /// </list>
    /// <para>
    /// <strong>Lifecycle Order:</strong>
    /// <br/>1. Previous tool's <see cref="OnDeactivate"/> is called
    /// <br/>2. ToolState updates active tool ID
    /// <br/>3. New tool's <see cref="OnActivate"/> is called
    /// <br/>4. ToolActivated event fires
    /// </para>
    /// <para>
    /// <strong>Implementation Notes:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="OnActivate"/> may be called multiple times if tool is reselected</item>
    /// <item><see cref="OnDeactivate"/> is guaranteed before unload (app close, plugin unload)</item>
    /// <item>Implementations should be idempotent (safe to call multiple times)</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class MyCustomTool : IToolLifecycle
    /// {
    ///     private bool _isActive;
    ///     
    ///     public void OnActivate()
    ///     {
    ///         _isActive = true;
    ///         // Subscribe to events, load resources, etc.
    ///     }
    ///     
    ///     public void OnDeactivate()
    ///     {
    ///         _isActive = false;
    ///         // Cleanup, unsubscribe, release resources
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IToolLifecycle
    {
        /// <summary>
        /// Called when the tool becomes the active tool.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Use this to:
        /// </para>
        /// <list type="bullet">
        /// <item>Subscribe to canvas events</item>
        /// <item>Initialize tool-specific state</item>
        /// <item>Load resources needed during tool use</item>
        /// <item>Show tool-specific UI overlays</item>
        /// </list>
        /// <para>
        /// This method should be safe to call multiple times (idempotent).
        /// </para>
        /// </remarks>
        void OnActivate();

        /// <summary>
        /// Called when the tool is no longer the active tool.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Use this to:
        /// </para>
        /// <list type="bullet">
        /// <item>Unsubscribe from canvas events</item>
        /// <item>Clear any in-progress operations</item>
        /// <item>Release resources not needed when inactive</item>
        /// <item>Hide tool-specific UI overlays</item>
        /// </list>
        /// <para>
        /// This method should be safe to call multiple times (idempotent).
        /// It will be called before the app closes or the plugin is unloaded.
        /// </para>
        /// </remarks>
        void OnDeactivate();
    }
}

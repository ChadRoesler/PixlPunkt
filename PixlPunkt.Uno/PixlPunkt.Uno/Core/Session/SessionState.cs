using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PixlPunkt.Uno.Core.Logging;
using PixlPunkt.Uno.Core.Serialization;
using PixlPunkt.Uno.Core.Settings;

namespace PixlPunkt.Uno.Core.Session
{
    /// <summary>
    /// Represents a document that was open in a session.
    /// </summary>
    public sealed class SessionDocument
    {
        /// <summary>
        /// Gets or sets the file path of the document (empty if unsaved).
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the document name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "Untitled";

        /// <summary>
        /// Gets or sets the auto-save file path (for recovery).
        /// </summary>
        [JsonPropertyName("autoSavePath")]
        public string? AutoSavePath { get; set; }

        /// <summary>
        /// Gets or sets whether the document had unsaved changes.
        /// </summary>
        [JsonPropertyName("dirty")]
        public bool IsDirty { get; set; }

        /// <summary>
        /// Gets or sets whether this was the active document.
        /// </summary>
        [JsonPropertyName("active")]
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Represents the state of a PixlPunkt session for crash recovery.
    /// </summary>
    public sealed class SessionState
    {
        /// <summary>
        /// Gets or sets the session start time.
        /// </summary>
        [JsonPropertyName("startTime")]
        public DateTime StartTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets the last update time.
        /// </summary>
        [JsonPropertyName("lastUpdate")]
        public DateTime LastUpdate { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets or sets whether the session was cleanly closed.
        /// </summary>
        [JsonPropertyName("cleanExit")]
        public bool CleanExit { get; set; }

        /// <summary>
        /// Gets or sets the list of open documents.
        /// </summary>
        [JsonPropertyName("documents")]
        public List<SessionDocument> Documents { get; set; } = [];

        /// <summary>
        /// Gets whether this session has recoverable documents.
        /// </summary>
        [JsonIgnore]
        public bool HasRecoverableDocuments => Documents.Count > 0 && !CleanExit;
    }

    /// <summary>
    /// Handles saving and loading session state for crash recovery.
    /// </summary>
    public static class SessionStateIO
    {
        private const string SessionFileName = "session.json";

        /// <summary>
        /// Gets the session state file path.
        /// </summary>
        public static string GetSessionFilePath()
        {
            return Path.Combine(AppPaths.RootDirectory, SessionFileName);
        }

        /// <summary>
        /// Saves the session state to disk.
        /// </summary>
        /// <param name="state">The session state to save.</param>
        public static void Save(SessionState state)
        {
            try
            {
                state.LastUpdate = DateTime.Now;
                var path = GetSessionFilePath();
                var json = JsonSerializer.Serialize(state, SessionJsonContext.Default.SessionState);
                File.WriteAllText(path, json);
                LoggingService.Debug("Session state saved: {DocumentCount} documents, cleanExit={CleanExit}",
                    state.Documents.Count, state.CleanExit);
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to save session state", ex);
            }
        }

        /// <summary>
        /// Loads the session state from disk.
        /// </summary>
        /// <returns>The loaded session state, or null if not found or invalid.</returns>
        public static SessionState? Load()
        {
            try
            {
                var path = GetSessionFilePath();
                if (!File.Exists(path))
                    return null;

                var json = File.ReadAllText(path);
                var state = JsonSerializer.Deserialize(json, SessionJsonContext.Default.SessionState);

                if (state != null)
                {
                    LoggingService.Debug("Session state loaded: {DocumentCount} documents, cleanExit={CleanExit}",
                        state.Documents.Count, state.CleanExit);
                }

                return state;
            }
            catch (Exception ex)
            {
                LoggingService.Warning("Failed to load session state: {Error}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Deletes the session state file.
        /// </summary>
        public static void Delete()
        {
            try
            {
                var path = GetSessionFilePath();
                if (File.Exists(path))
                {
                    File.Delete(path);
                    LoggingService.Debug("Session state file deleted");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Warning("Failed to delete session state: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Marks the current session as cleanly exited.
        /// </summary>
        public static void MarkCleanExit()
        {
            try
            {
                var state = Load();
                if (state != null)
                {
                    state.CleanExit = true;
                    Save(state);
                    LoggingService.Info("Session marked as clean exit");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Warning("Failed to mark clean exit: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Creates a new session state and marks it as in-progress (not clean exit).
        /// </summary>
        /// <returns>The new session state.</returns>
        public static SessionState StartNewSession()
        {
            var state = new SessionState
            {
                StartTime = DateTime.Now,
                LastUpdate = DateTime.Now,
                CleanExit = false
            };
            Save(state);
            LoggingService.Info("New session started");
            return state;
        }
    }
}

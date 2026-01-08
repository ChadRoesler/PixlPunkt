using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Uno.Core.AutoSave;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Logging;
using PixlPunkt.Uno.Core.Session;
using PixlPunkt.Uno.UI.CanvasHost;

namespace PixlPunkt.Uno.UI
{
    /// <summary>
    /// Partial class for session state management (crash recovery).
    /// </summary>
    public sealed partial class PixlPunktMainWindow : Window
    {
        private SessionState? _currentSession;
        private DispatcherQueueTimer? _sessionUpdateTimer;
        private bool _sessionUpdatePending;

        /// <summary>
        /// Tracks which documents have been auto-saved at least once.
        /// Only documents that have been auto-saved should be included in session state
        /// to prevent recovery prompts when no auto-save file exists.
        /// </summary>
        private readonly HashSet<CanvasDocument> _autoSavedDocuments = new();

        /// <summary>
        /// Initializes session tracking. Call this after window is loaded.
        /// </summary>
        private async Task InitializeSessionAsync()
        {
            // Check for previous session that wasn't cleanly exited
            var previousSession = SessionStateIO.Load();

            if (previousSession != null && previousSession.HasRecoverableDocuments)
            {
                LoggingService.Info("Found incomplete session with {Count} documents",
                    previousSession.Documents.Count);

                // Prompt user to recover
                await PromptSessionRecoveryAsync(previousSession);
            }

            // Start a new session
            _currentSession = SessionStateIO.StartNewSession();

            // Hook up auto-save completed event to update session state
            _autoSave.AutoSaveCompleted += OnAutoSaveCompleted;
        }

        /// <summary>
        /// Called when an auto-save operation completes.
        /// Marks the document as having been auto-saved and updates session state.
        /// </summary>
        private void OnAutoSaveCompleted(CanvasDocument doc, bool success, string? pathOrError)
        {
            if (success && doc != null)
            {
                // Mark this document as having been auto-saved
                bool wasFirstAutoSave = !_autoSavedDocuments.Contains(doc);
                _autoSavedDocuments.Add(doc);

                // Update session state (now includes this document since it has an auto-save)
                if (wasFirstAutoSave)
                {
                    LoggingService.Debug("First auto-save for document '{Name}' - adding to session state", doc.Name);
                }

                // Trigger session update on UI thread
                DispatcherQueue.TryEnqueue(() => UpdateSessionState());
            }
        }

        /// <summary>
        /// Prompts the user to recover documents from a previous incomplete session.
        /// </summary>
        private async Task PromptSessionRecoveryAsync(SessionState previousSession)
        {
            var recoverableCount = previousSession.Documents.Count;
            var docNames = string.Join("\n", previousSession.Documents
                .Take(10)
                .Select(d => $"• {d.Name}{(d.IsDirty ? " (unsaved changes)" : "")}"));

            if (recoverableCount > 10)
            {
                docNames += $"\n... and {recoverableCount - 10} more";
            }

            var lastUpdate = previousSession.LastUpdate.ToString("g");

            var dlg = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Recover Previous Session?",
                Content = $"PixlPunkt was not closed properly.\n\n" +
                         $"Last session ({lastUpdate}) had {recoverableCount} document(s):\n{docNames}\n\n" +
                         $"Would you like to recover these documents?",
                PrimaryButtonText = "Recover",
                SecondaryButtonText = "Start Fresh",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await ShowDialogGuardedAsync(dlg);

            if (result == ContentDialogResult.Primary)
            {
                await RecoverSessionDocumentsAsync(previousSession);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                // User chose to start fresh - delete old session
                SessionStateIO.Delete();
                LoggingService.Info("User chose to start fresh - old session discarded");
            }
            // Cancel - do nothing, will start new session anyway
        }

        /// <summary>
        /// Attempts to recover documents from a previous session.
        /// </summary>
        private async Task RecoverSessionDocumentsAsync(SessionState session)
        {
            int recovered = 0;
            int failed = 0;
            TabViewItem? activeTab = null;

            foreach (var docInfo in session.Documents)
            {
                try
                {
                    CanvasDocument? doc = null;

                    // Try to load from auto-save first (more recent)
                    if (!string.IsNullOrEmpty(docInfo.AutoSavePath) && File.Exists(docInfo.AutoSavePath))
                    {
                        doc = DocumentIO.Load(docInfo.AutoSavePath);
                        doc.Name = docInfo.Name + " (Recovered)";
                        LoggingService.Info("Recovered document from auto-save: {Name}", docInfo.Name);
                    }
                    // Fall back to original file if it exists
                    else if (!string.IsNullOrEmpty(docInfo.Path) && File.Exists(docInfo.Path))
                    {
                        doc = DocumentIO.Load(docInfo.Path);
                        LoggingService.Info("Recovered document from file: {Path}", docInfo.Path);
                    }

                    if (doc != null)
                    {
                        _workspace.Add(doc);
                        _documentPaths[doc] = string.IsNullOrEmpty(docInfo.Path) ? null : docInfo.Path;
                        _autoSave.RegisterDocument(doc);

                        // Mark recovered documents as having been auto-saved (they came from an auto-save)
                        if (!string.IsNullOrEmpty(docInfo.AutoSavePath))
                        {
                            _autoSavedDocuments.Add(doc);
                        }

                        var tab = MakeTab(doc);
                        DocsTab.TabItems.Add(tab);

                        if (docInfo.IsActive)
                        {
                            activeTab = tab;
                        }

                        recovered++;
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Warning("Failed to recover document {Name}: {Error}",
                        docInfo.Name, ex.Message);
                    failed++;
                }
            }

            // Defer selection to ensure tab is fully in visual tree (fixes Release build timing issue)
            if (activeTab != null)
            {
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    DocsTab.SelectedItem = activeTab;
                });
            }

            // Show result
            if (recovered > 0 || failed > 0)
            {
                var message = recovered > 0
                    ? $"Successfully recovered {recovered} document(s)."
                    : "No documents could be recovered.";

                if (failed > 0)
                {
                    message += $"\n{failed} document(s) could not be recovered.";
                }

                await ShowDialogGuardedAsync(new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Session Recovery",
                    Content = message,
                    CloseButtonText = "OK"
                });
            }

            LoggingService.Info("Session recovery complete: {Recovered} recovered, {Failed} failed",
                recovered, failed);
        }

        /// <summary>
        /// Schedules a debounced session state update.
        /// Call this when documents are opened, closed, or saved.
        /// Updates are coalesced to prevent excessive disk writes.
        /// </summary>
        private void UpdateSessionState()
        {
            // Mark that an update is pending
            _sessionUpdatePending = true;

            // Initialize timer if needed
            if (_sessionUpdateTimer == null)
            {
                _sessionUpdateTimer = DispatcherQueue.CreateTimer();
                _sessionUpdateTimer.Interval = TimeSpan.FromSeconds(2);
                _sessionUpdateTimer.IsRepeating = false;
                _sessionUpdateTimer.Tick += (_, _) => FlushSessionState();
            }

            // Restart the timer (debounce)
            _sessionUpdateTimer.Stop();
            _sessionUpdateTimer.Start();
        }

        /// <summary>
        /// Immediately writes the current session state to disk.
        /// Called by the debounce timer or when exiting.
        /// Only includes documents that have been auto-saved at least once,
        /// or documents that have a saved file path.
        /// </summary>
        private void FlushSessionState()
        {
            if (!_sessionUpdatePending)
                return;

            _sessionUpdatePending = false;

            if (_currentSession == null)
            {
                _currentSession = new SessionState();
            }

            _currentSession.Documents.Clear();
            _currentSession.CleanExit = false;

            foreach (TabViewItem tab in DocsTab.TabItems)
            {
                if (tab.Content is CanvasViewHost host)
                {
                    var doc = host.Document;
                    var docPath = _documentPaths.TryGetValue(doc, out var p) ? p : null;
                    var autoSavePath = _autoSave.GetAutoSavePath(doc);

                    // Only include documents that have:
                    // 1. Been auto-saved at least once (so recovery has a file to use), OR
                    // 2. Have a saved file path (so recovery can open the original file)
                    bool hasAutoSave = _autoSavedDocuments.Contains(doc) &&
                                       !string.IsNullOrEmpty(autoSavePath) &&
                                       File.Exists(autoSavePath);
                    bool hasSavedPath = !string.IsNullOrEmpty(docPath) && File.Exists(docPath);

                    if (!hasAutoSave && !hasSavedPath)
                    {
                        // Skip this document - no file to recover from
                        LoggingService.Debug("Skipping document '{Name}' from session - no auto-save or saved file yet",
                            doc.Name);
                        continue;
                    }

                    _currentSession.Documents.Add(new SessionDocument
                    {
                        Path = docPath ?? string.Empty,
                        Name = doc.Name ?? "Untitled",
                        AutoSavePath = hasAutoSave ? autoSavePath : null,
                        IsDirty = doc.IsDirty,
                        IsActive = tab == DocsTab.SelectedItem
                    });
                }
            }

            SessionStateIO.Save(_currentSession);
        }

        /// <summary>
        /// Marks the session as cleanly closed.
        /// Call this when the app is closing normally.
        /// </summary>
        private void MarkSessionCleanExit()
        {
            // Stop the debounce timer
            _sessionUpdateTimer?.Stop();

            // Unhook auto-save event
            _autoSave.AutoSaveCompleted -= OnAutoSaveCompleted;

            // Flush any pending updates immediately
            if (_sessionUpdatePending)
            {
                FlushSessionState();
            }

            if (_currentSession != null)
            {
                _currentSession.CleanExit = true;
                SessionStateIO.Save(_currentSession);
            }
            else
            {
                SessionStateIO.MarkCleanExit();
            }

            LoggingService.Info("Session marked as clean exit");
        }

        /// <summary>
        /// Cleans up auto-save tracking when a document is closed.
        /// </summary>
        private void OnDocumentClosed(CanvasDocument doc)
        {
            _autoSavedDocuments.Remove(doc);
        }
    }
}

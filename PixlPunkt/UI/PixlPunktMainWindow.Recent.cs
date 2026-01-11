using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Logging;

namespace PixlPunkt.UI
{
    public sealed partial class PixlPunktMainWindow : Window
    {
        private readonly RecentDocumentsService _recentDocs = new();

        // Call this once from your ctor after InitializeComponent().
        private void InitOpenRecentMenus()
        {
            _recentDocs.Load();

            // Keep menus fresh as autosaves land.
            _autoSave.AutoSaveCompleted += (_, __, ___) =>
            {
                try
                {
                    DispatcherQueue.TryEnqueue(RebuildAutosaveMenu);
                }
                catch (Exception ex)
                {
                    LoggingService.Debug("Failed to enqueue autosave menu rebuild: {Error}", ex.Message);
                }
            };

            RebuildRecentMenu();
            RebuildAutosaveMenu();
        }

        private void TrackRecent(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                _recentDocs.Touch(path);
                _recentDocs.Save();
                RebuildRecentMenu();
            }
            catch (Exception ex)
            {
                LoggingService.Debug("Failed to track recent document '{Path}': {Error}", path, ex.Message);
            }
        }

        private void RebuildRecentMenu()
        {
            if (File_OpenRecent_Submenu == null || File_OpenRecent_Separator == null)
                return;

            // Remove everything after the separator.
            int sepIndex = File_OpenRecent_Submenu.Items.IndexOf(File_OpenRecent_Separator);
            if (sepIndex < 0) return;

            while (File_OpenRecent_Submenu.Items.Count > sepIndex + 1)
                File_OpenRecent_Submenu.Items.RemoveAt(sepIndex + 1);

            // Optional: prune dead entries silently.
            _recentDocs.PruneMissingFiles();

            if (_recentDocs.Entries.Count == 0)
            {
                File_OpenRecent_Submenu.Items.Add(new MenuFlyoutItem
                {
                    Text = "(No recent files)",
                    IsEnabled = false
                });
                return;
            }

            foreach (var entry in _recentDocs.Entries)
            {
                var path = entry.FilePath;
                var name = System.IO.Path.GetFileName(path);

                var item = new MenuFlyoutItem { Text = name };
                ToolTipService.SetToolTip(item, path);

                item.Click += async (_, __) => await OpenRecentPathAsync(path);
                File_OpenRecent_Submenu.Items.Add(item);
            }

            // Nice-to-have: clear list
            File_OpenRecent_Submenu.Items.Add(new MenuFlyoutSeparator());
            var clear = new MenuFlyoutItem { Text = "Clear Recent" };
            clear.Click += (_, __) =>
            {
                try
                {
                    foreach (var e in _recentDocs.Entries.ToList())
                        _recentDocs.Remove(e.FilePath);
                    _recentDocs.Save();
                    RebuildRecentMenu();
                }
                catch (Exception ex)
                {
                    LoggingService.Debug("Failed to clear recent documents: {Error}", ex.Message);
                }
            };
            File_OpenRecent_Submenu.Items.Add(clear);
        }

        private async Task OpenRecentPathAsync(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    _recentDocs.Remove(path);
                    _recentDocs.Save();
                    RebuildRecentMenu();

                    _ = new ContentDialog
                    {
                        XamlRoot = Content.XamlRoot,
                        Title = "File missing",
                        Content = "That recent file can't be found anymore. I removed it from the list.",
                        CloseButtonText = "OK"
                    }.ShowAsync();
                    return;
                }

                var doc = DocumentIO.Load(path);

                // Reload audio tracks from stored file paths
                await doc.CanvasAnimationState.ReloadAudioTracksAsync();

                _workspace.Add(doc);
                _documentPaths[doc] = path;
                _autoSave.RegisterDocument(doc);

                var tab = MakeTab(doc);
                DocsTab.TabItems.Add(tab);
                DocsTab.SelectedItem = tab;

                TrackRecent(path);

                LoggingService.Info($"Opened recent document: {path}");
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to open recent document: {path}", ex);
                _ = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Open failed",
                    Content = $"Could not open file.\n{ex.Message}",
                    CloseButtonText = "OK"
                }.ShowAsync();
            }
        }

        private void RebuildAutosaveMenu()
        {
            if (File_OpenFromAutosave_Submenu == null)
                return;

            File_OpenFromAutosave_Submenu.Items.Clear();

            // If autosave is disabled, show a hint.
            if (!_autoSave.IsEnabled || string.IsNullOrWhiteSpace(_autoSave.SaveFolderPath) || !Directory.Exists(_autoSave.SaveFolderPath))
            {
                File_OpenFromAutosave_Submenu.Items.Add(new MenuFlyoutItem
                {
                    Text = "(Autosave disabled)",
                    IsEnabled = false
                });
                return;
            }

            var files = Directory.GetFiles(_autoSave.SaveFolderPath, "*.pxp")
                                 .Select(p => new FileInfo(p))
                                 .OrderByDescending(f => f.LastWriteTime)
                                 .Take(12)
                                 .ToList();

            if (files.Count == 0)
            {
                File_OpenFromAutosave_Submenu.Items.Add(new MenuFlyoutItem
                {
                    Text = "(No autosaves yet)",
                    IsEnabled = false
                });
                return;
            }

            foreach (var f in files)
            {
                var display = BuildAutosaveDisplayName(f);
                var item = new MenuFlyoutItem { Text = display };
                ToolTipService.SetToolTip(item, f.FullName);
                item.Click += async (_, __) => await OpenAutosavePathAsync(f.FullName);
                File_OpenFromAutosave_Submenu.Items.Add(item);
            }

            File_OpenFromAutosave_Submenu.Items.Add(new MenuFlyoutSeparator());
            var openFolder = new MenuFlyoutItem { Text = "Open Autosave Folder" };
            openFolder.Click += (_, __) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _autoSave.SaveFolderPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    LoggingService.Debug("Failed to open autosave folder: {Error}", ex.Message);
                }
            };
            File_OpenFromAutosave_Submenu.Items.Add(openFolder);
        }

        private static string BuildAutosaveDisplayName(FileInfo f)
        {
            // File format: {safeName}_yyyy-MM-dd-HH-mm-ss.pxp
            var name = Path.GetFileNameWithoutExtension(f.Name);
            var lastUnderscore = name.LastIndexOf('_');
            if (lastUnderscore > 0)
            {
                var baseName = name.Substring(0, lastUnderscore);
                var stamp = name.Substring(lastUnderscore + 1);

                if (DateTime.TryParseExact(stamp, "yyyy-MM-dd-HH-mm-ss", null,
                        System.Globalization.DateTimeStyles.None, out var dt))
                {
                    return $"{baseName}  ({dt:yyyy-MM-dd HH:mm:ss})";
                }
            }

            // Fallback to last write time
            return $"{name}  ({f.LastWriteTime:yyyy-MM-dd HH:mm:ss})";
        }

        private async Task OpenAutosavePathAsync(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    RebuildAutosaveMenu();
                    return;
                }

                var doc = DocumentIO.Load(path);

                // Reload audio tracks from stored file paths
                await doc.CanvasAnimationState.ReloadAudioTracksAsync();

                // Make it obvious this is a recovery snapshot.
                doc.Name = $"Recovered_{doc.Name ?? Path.GetFileNameWithoutExtension(path)}";

                _workspace.Add(doc);
                _documentPaths[doc] = string.Empty; // treat as unsaved until user Save As…
                _autoSave.RegisterDocument(doc);

                var tab = MakeTab(doc);
                DocsTab.TabItems.Add(tab);
                DocsTab.SelectedItem = tab;

                LoggingService.Info($"Opened autosave document: {path}");
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to open autosave document: {path}", ex);
                _ = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "Open autosave failed",
                    Content = $"Could not open autosave.\n{ex.Message}",
                    CloseButtonText = "OK"
                }.ShowAsync();
            }
        }
    }
}

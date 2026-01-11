#if __IOS__
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Foundation;
using UIKit;
using UniformTypeIdentifiers;

namespace PixlPunkt.Core.Platform;

/// <summary>
/// iOS implementation of <see cref="IPlatformFileService"/> using native iOS APIs.
/// </summary>
/// <remarks>
/// <para>Uses the following iOS APIs:</para>
/// <list type="bullet">
/// <item><see cref="UIDocumentPickerViewController"/> for file picking</item>
/// <item><see cref="UIActivityViewController"/> for sharing (share sheet)</item>
/// <item>App's Documents directory for temporary storage</item>
/// </list>
/// </remarks>
public class IosPlatformFileService : IPlatformFileService
{
    /// <inheritdoc/>
    public bool SupportsFileSystem => false; // iOS uses sandboxed file access

    /// <inheritdoc/>
    public bool SupportsSharing => true;

    /// <inheritdoc/>
    public bool SupportsFolderPicking => false; // iOS doesn't support traditional folder picking

    /// <inheritdoc/>
    public async Task<FileSaveResult> SaveFileAsync(
        byte[] data,
        string suggestedFileName,
        string fileTypeDescription,
        string fileExtension)
    {
        try
        {
            // On iOS, "saving" a file typically means exporting it via the share sheet
            // or saving to the app's document directory
            
            // First, save to a temporary location
            var tempPath = await SaveToTempAsync(data, suggestedFileName);
            var tempUrl = NSUrl.FromFilename(tempPath);

            var tcs = new TaskCompletionSource<FileSaveResult>();

            // Use UIDocumentPickerViewController in export mode
            var picker = new UIDocumentPickerViewController(new[] { tempUrl }, asCopy: true);
            
            picker.DidPickDocument += (sender, e) =>
            {
                tcs.TrySetResult(FileSaveResult.Succeeded(e.Url?.Path));
            };

            picker.DidPickDocumentAtUrls += (sender, e) =>
            {
                var urls = e.Urls;
                if (urls != null && urls.Length > 0)
                {
                    tcs.TrySetResult(FileSaveResult.Succeeded(urls[0].Path));
                }
                else
                {
                    tcs.TrySetResult(FileSaveResult.Cancelled());
                }
            };

            picker.WasCancelled += (sender, e) =>
            {
                tcs.TrySetResult(FileSaveResult.Cancelled());
            };

            var viewController = GetPresentingViewController();
            if (viewController == null)
            {
                return FileSaveResult.Failed("Could not find presenting view controller");
            }

            await viewController.PresentViewControllerAsync(picker, true);
            
            return await tcs.Task;
        }
        catch (Exception ex)
        {
            return FileSaveResult.Failed($"Save failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<FileOpenResult> OpenFileAsync(
        string fileTypeDescription,
        params string[] fileExtensions)
    {
        try
        {
            var tcs = new TaskCompletionSource<FileOpenResult>();

            // Convert extensions to UTTypes
            var utTypes = new List<UTType>();
            foreach (var ext in fileExtensions)
            {
                var cleanExt = ext.TrimStart('.');
                var utType = UTType.CreateFromExtension(cleanExt);
                if (utType != null)
                {
                    utTypes.Add(utType);
                }
            }

            // Fallback to generic data if no specific types found
            if (utTypes.Count == 0)
            {
                utTypes.Add(UTTypes.Data);
            }

            var picker = new UIDocumentPickerViewController(utTypes.ToArray(), asCopy: true);

            picker.DidPickDocumentAtUrls += async (sender, e) =>
            {
                try
                {
                    var urls = e.Urls;
                    if (urls != null && urls.Length > 0)
                    {
                        var url = urls[0];
                        
                        // Start accessing security-scoped resource
                        bool accessGranted = url.StartAccessingSecurityScopedResource();
                        
                        try
                        {
                            var data = NSData.FromUrl(url);
                            if (data != null)
                            {
                                var bytes = data.ToArray();
                                var fileName = url.LastPathComponent;
                                tcs.TrySetResult(FileOpenResult.Succeeded(bytes, fileName));
                            }
                            else
                            {
                                tcs.TrySetResult(FileOpenResult.Failed("Could not read file data"));
                            }
                        }
                        finally
                        {
                            if (accessGranted)
                            {
                                url.StopAccessingSecurityScopedResource();
                            }
                        }
                    }
                    else
                    {
                        tcs.TrySetResult(FileOpenResult.Cancelled());
                    }
                }
                catch (Exception ex)
                {
                    tcs.TrySetResult(FileOpenResult.Failed($"Error reading file: {ex.Message}"));
                }
            };

            picker.WasCancelled += (sender, e) =>
            {
                tcs.TrySetResult(FileOpenResult.Cancelled());
            };

            var viewController = GetPresentingViewController();
            if (viewController == null)
            {
                return FileOpenResult.Failed("Could not find presenting view controller");
            }

            await viewController.PresentViewControllerAsync(picker, true);
            
            return await tcs.Task;
        }
        catch (Exception ex)
        {
            return FileOpenResult.Failed($"Open failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Task<FolderPickResult> PickFolderAsync()
    {
        // iOS doesn't support traditional folder picking
        // Return a failure directing users to use share functionality
        return Task.FromResult(FolderPickResult.Failed(
            "Folder picking is not supported on iOS. Use the Share feature to export files."));
    }

    /// <inheritdoc/>
    public async Task<bool> ShareFileAsync(
        byte[] data,
        string fileName,
        string mimeType,
        string? title = null)
    {
        try
        {
            // Save to temp first
            var tempPath = await SaveToTempAsync(data, fileName);
            var tempUrl = NSUrl.FromFilename(tempPath);

            var activityItems = new NSObject[] { tempUrl };
            var activityController = new UIActivityViewController(activityItems, null);

            // Set title if provided
            if (!string.IsNullOrEmpty(title))
            {
                activityController.SetValueForKey(new NSString(title), new NSString("subject"));
            }

            var viewController = GetPresentingViewController();
            if (viewController == null)
            {
                return false;
            }

            // For iPad, we need to set the popover presentation
            if (UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Pad)
            {
                var popover = activityController.PopoverPresentationController;
                if (popover != null)
                {
                    var bounds = viewController.View.Bounds;
                    popover.SourceView = viewController.View;
                    popover.SourceRect = new CoreGraphics.CGRect(
                        bounds.X + bounds.Width / 2,
                        bounds.Y + bounds.Height / 2,
                        0, 0);
                    popover.PermittedArrowDirections = UIPopoverArrowDirection.Any;
                }
            }

            await viewController.PresentViewControllerAsync(activityController, true);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Share failed: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ShareFilesAsync(
        IReadOnlyList<(byte[] Data, string FileName, string MimeType)> files,
        string? title = null)
    {
        try
        {
            var tempUrls = new List<NSUrl>();

            foreach (var (data, fileName, _) in files)
            {
                var tempPath = await SaveToTempAsync(data, fileName);
                tempUrls.Add(NSUrl.FromFilename(tempPath));
            }

            var activityItems = tempUrls.Cast<NSObject>().ToArray();
            var activityController = new UIActivityViewController(activityItems, null);

            if (!string.IsNullOrEmpty(title))
            {
                activityController.SetValueForKey(new NSString(title), new NSString("subject"));
            }

            var viewController = GetPresentingViewController();
            if (viewController == null)
            {
                return false;
            }

            // For iPad
            if (UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Pad)
            {
                var popover = activityController.PopoverPresentationController;
                if (popover != null)
                {
                    var bounds = viewController.View.Bounds;
                    popover.SourceView = viewController.View;
                    popover.SourceRect = new CoreGraphics.CGRect(
                        bounds.X + bounds.Width / 2,
                        bounds.Y + bounds.Height / 2,
                        0, 0);
                    popover.PermittedArrowDirections = UIPopoverArrowDirection.Any;
                }
            }

            await viewController.PresentViewControllerAsync(activityController, true);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Share files failed: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public Task<string> SaveToTempAsync(byte[] data, string fileName)
    {
        // Use the app's temporary directory
        var tempDir = Path.GetTempPath();
        var tempPath = Path.Combine(tempDir, fileName);

        // Ensure unique filename if exists
        if (File.Exists(tempPath))
        {
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            tempPath = Path.Combine(tempDir, $"{baseName}_{timestamp}{ext}");
        }

        File.WriteAllBytes(tempPath, data);
        return Task.FromResult(tempPath);
    }

    /// <summary>
    /// Gets the topmost view controller for presenting UI.
    /// </summary>
    private static UIViewController? GetPresentingViewController()
    {
        var window = UIApplication.SharedApplication.KeyWindow 
            ?? UIApplication.SharedApplication.Windows.FirstOrDefault();
        
        if (window == null) return null;

        var viewController = window.RootViewController;
        
        // Navigate to the topmost presented controller
        while (viewController?.PresentedViewController != null)
        {
            viewController = viewController.PresentedViewController;
        }

        return viewController;
    }
}
#endif

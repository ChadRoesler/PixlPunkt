using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace PixlPunkt.Core.Reference
{
    /// <summary>
    /// Manages reference image overlays for a document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ReferenceImageService maintains a collection of reference images that can be
    /// displayed as overlays on the canvas. Reference images are document-specific and
    /// are stored/loaded with the document.
    /// </para>
    /// <para>
    /// This service handles image loading, the collection lifecycle, and provides
    /// events for UI synchronization.
    /// </para>
    /// </remarks>
    public sealed class ReferenceImageService : IDisposable
    {
        private ReferenceImage? _selectedImage;
        private bool _overlaysVisible = true;

        /// <summary>
        /// Gets the collection of reference images.
        /// </summary>
        public ObservableCollection<ReferenceImage> Images { get; } = [];

        /// <summary>
        /// Gets or sets the currently selected reference image for editing.
        /// </summary>
        public ReferenceImage? SelectedImage
        {
            get => _selectedImage;
            set
            {
                if (_selectedImage != value)
                {
                    _selectedImage = value;
                    SelectedImageChanged?.Invoke(_selectedImage);
                }
            }
        }

        /// <summary>
        /// Gets or sets whether reference image overlays are visible globally.
        /// </summary>
        public bool OverlaysVisible
        {
            get => _overlaysVisible;
            set
            {
                if (_overlaysVisible != value)
                {
                    _overlaysVisible = value;
                    VisibilityChanged?.Invoke(_overlaysVisible);
                }
            }
        }

        /// <summary>
        /// Gets the count of reference images.
        /// </summary>
        public int Count => Images.Count;

        /// <summary>
        /// Gets whether there are any reference images.
        /// </summary>
        public bool HasImages => Images.Count > 0;

        /// <summary>
        /// Raised when the collection changes.
        /// </summary>
        public event Action? CollectionChanged;

        /// <summary>
        /// Raised when the selected image changes.
        /// </summary>
        public event Action<ReferenceImage?>? SelectedImageChanged;

        /// <summary>
        /// Raised when global visibility changes.
        /// </summary>
        public event Action<bool>? VisibilityChanged;

        /// <summary>
        /// Raised when any reference image's transform changes.
        /// </summary>
        public event Action? ImageTransformChanged;

        /// <summary>
        /// Creates a new ReferenceImageService.
        /// </summary>
        public ReferenceImageService()
        {
            Images.CollectionChanged += (_, _) => CollectionChanged?.Invoke();
        }

        /// <summary>
        /// Adds a reference image from a file.
        /// </summary>
        /// <param name="file">The image file to load.</param>
        /// <returns>The created reference image, or null if loading failed.</returns>
        public async Task<ReferenceImage?> AddFromFileAsync(StorageFile file)
        {
            try
            {
                using var stream = await file.OpenReadAsync();
                var decoder = await BitmapDecoder.CreateAsync(stream);

                var transform = new BitmapTransform();
                var pixelData = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    transform,
                    ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.DoNotColorManage);

                var pixels = pixelData.DetachPixelData();
                int width = (int)decoder.PixelWidth;
                int height = (int)decoder.PixelHeight;

                var name = Path.GetFileNameWithoutExtension(file.Name);
                var refImage = new ReferenceImage(name, pixels, width, height, file.Path);

                // Wire up transform changed event
                refImage.TransformChanged += () => ImageTransformChanged?.Invoke();

                Images.Add(refImage);
                SelectedImage = refImage;

                return refImage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load reference image: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Adds a reference image from pixel data.
        /// </summary>
        /// <param name="name">Display name.</param>
        /// <param name="pixels">Pixel data in BGRA format.</param>
        /// <param name="width">Image width.</param>
        /// <param name="height">Image height.</param>
        /// <returns>The created reference image.</returns>
        public ReferenceImage AddFromPixels(string name, byte[] pixels, int width, int height)
        {
            var refImage = new ReferenceImage(name, pixels, width, height);
            refImage.TransformChanged += () => ImageTransformChanged?.Invoke();
            Images.Add(refImage);
            SelectedImage = refImage;
            return refImage;
        }

        /// <summary>
        /// Removes a reference image.
        /// </summary>
        /// <param name="image">The image to remove.</param>
        /// <returns>True if the image was removed.</returns>
        public bool Remove(ReferenceImage image)
        {
            if (image == null) return false;

            bool removed = Images.Remove(image);
            if (removed && _selectedImage == image)
            {
                SelectedImage = Images.FirstOrDefault();
            }
            return removed;
        }

        /// <summary>
        /// Removes a reference image by ID.
        /// </summary>
        /// <param name="id">The ID of the image to remove.</param>
        /// <returns>True if the image was removed.</returns>
        public bool Remove(Guid id)
        {
            var image = Images.FirstOrDefault(i => i.Id == id);
            return image != null && Remove(image);
        }

        /// <summary>
        /// Clears all reference images.
        /// </summary>
        public void Clear()
        {
            Images.Clear();
            SelectedImage = null;
        }

        /// <summary>
        /// Gets a reference image by ID.
        /// </summary>
        /// <param name="id">The ID of the image.</param>
        /// <returns>The reference image, or null if not found.</returns>
        public ReferenceImage? GetById(Guid id)
        {
            return Images.FirstOrDefault(i => i.Id == id);
        }

        /// <summary>
        /// Moves a reference image up in the z-order (renders later/on top).
        /// </summary>
        /// <param name="image">The image to move.</param>
        public void MoveUp(ReferenceImage image)
        {
            int index = Images.IndexOf(image);
            if (index < 0 || index >= Images.Count - 1) return;
            Images.Move(index, index + 1);
        }

        /// <summary>
        /// Moves a reference image down in the z-order (renders earlier/behind).
        /// </summary>
        /// <param name="image">The image to move.</param>
        public void MoveDown(ReferenceImage image)
        {
            int index = Images.IndexOf(image);
            if (index <= 0) return;
            Images.Move(index, index - 1);
        }

        /// <summary>
        /// Performs hit testing on all visible reference images.
        /// Returns the topmost image at the given point.
        /// </summary>
        /// <param name="docX">X coordinate in document space.</param>
        /// <param name="docY">Y coordinate in document space.</param>
        /// <returns>The topmost reference image at the point, or null.</returns>
        public ReferenceImage? HitTest(float docX, float docY)
        {
            if (!_overlaysVisible) return null;

            // Test in reverse order (topmost first)
            for (int i = Images.Count - 1; i >= 0; i--)
            {
                var image = Images[i];
                if (image.IsVisible && image.HitTest(docX, docY))
                {
                    return image;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets all visible reference images for rendering.
        /// </summary>
        /// <returns>Enumerable of visible images in render order (bottom to top).</returns>
        public System.Collections.Generic.IEnumerable<ReferenceImage> GetVisibleImages()
        {
            if (!_overlaysVisible) yield break;

            foreach (var image in Images)
            {
                if (image.IsVisible && image.HasPixels)
                {
                    yield return image;
                }
            }
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public void Dispose()
        {
            Clear();
        }
    }
}

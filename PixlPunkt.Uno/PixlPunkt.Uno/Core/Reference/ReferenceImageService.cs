using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PixlPunkt.Uno.Core.Imaging;
using Windows.Storage;

namespace PixlPunkt.Uno.Core.Reference
{
    /// <summary>
    /// Manages reference image overlays for a document.
    /// </summary>
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

        public event Action? CollectionChanged;
        public event Action<ReferenceImage?>? SelectedImageChanged;
        public event Action<bool>? VisibilityChanged;
        public event Action? ImageTransformChanged;

        public ReferenceImageService()
        {
            Images.CollectionChanged += (_, _) => CollectionChanged?.Invoke();
        }

        /// <summary>
        /// Adds a reference image from a file.
        /// </summary>
        public async Task<ReferenceImage?> AddFromFileAsync(StorageFile file)
        {
            try
            {
                // Use SkiaSharp for cross-platform image loading
                using var stream = await file.OpenStreamForReadAsync();
                var (pixels, width, height) = SkiaImageEncoder.DecodeFromStream(stream);

                var name = Path.GetFileNameWithoutExtension(file.Name);
                var refImage = new ReferenceImage(name, pixels, width, height, file.Path);
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
        /// Adds a reference image from a file path.
        /// </summary>
        public ReferenceImage? AddFromPath(string filePath)
        {
            try
            {
                var (pixels, width, height) = SkiaImageEncoder.Decode(filePath);
                var name = Path.GetFileNameWithoutExtension(filePath);
                var refImage = new ReferenceImage(name, pixels, width, height, filePath);
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
        public ReferenceImage? GetById(Guid id)
        {
            return Images.FirstOrDefault(i => i.Id == id);
        }

        /// <summary>
        /// Moves a reference image up in the z-order.
        /// </summary>
        public void MoveUp(ReferenceImage image)
        {
            int index = Images.IndexOf(image);
            if (index < 0 || index >= Images.Count - 1) return;
            Images.Move(index, index + 1);
        }

        /// <summary>
        /// Moves a reference image down in the z-order.
        /// </summary>
        public void MoveDown(ReferenceImage image)
        {
            int index = Images.IndexOf(image);
            if (index <= 0) return;
            Images.Move(index, index - 1);
        }

        /// <summary>
        /// Performs hit testing on all visible reference images.
        /// </summary>
        public ReferenceImage? HitTest(float docX, float docY)
        {
            if (!_overlaysVisible) return null;

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

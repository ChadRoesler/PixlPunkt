using System;
using System.Collections.Generic;
using System.Linq;

namespace PixlPunkt.Uno.Core.Canvas
{
    /// <summary>
    /// Service for managing custom user canvas templates stored in AppData.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CustomTemplateService provides centralized access to custom template files (.json format).
    /// It automatically scans the template directory on initialization and caches loaded templates
    /// for efficient runtime access.
    /// </para>
    /// <para><strong>Storage Location:</strong></para>
    /// <para>
    /// Templates are stored in: <c>%AppData%\PixlPunkt\Templates\*.json</c>
    /// </para>
    /// </remarks>
    public sealed class CustomTemplateService
    {
        private static readonly Lazy<CustomTemplateService> _instance = new(() => new CustomTemplateService());

        /// <summary>
        /// Gets the singleton instance of the custom template service.
        /// </summary>
        public static CustomTemplateService Instance => _instance.Value;

        private readonly List<CustomCanvasTemplate> _templates = [];
        private bool _isInitialized;

        /// <summary>
        /// Event raised when the template collection changes.
        /// </summary>
        public event EventHandler? TemplatesChanged;

        private CustomTemplateService() { }

        /// <summary>
        /// Gets whether the service has been initialized.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Gets the list of loaded custom templates.
        /// </summary>
        public IReadOnlyList<CustomCanvasTemplate> Templates
        {
            get
            {
                EnsureInitialized();
                return _templates;
            }
        }

        /// <summary>
        /// Gets the count of loaded templates.
        /// </summary>
        public int Count
        {
            get
            {
                EnsureInitialized();
                return _templates.Count;
            }
        }

        /// <summary>
        /// Initializes the service by creating the directory and loading templates.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
                return;

            CustomTemplateIO.EnsureDirectoryExists();
            RefreshTemplates();
        }

        /// <summary>
        /// Ensures the service is initialized.
        /// </summary>
        private void EnsureInitialized()
        {
            if (!_isInitialized)
                Initialize();
        }

        /// <summary>
        /// Refreshes the template collection by rescanning the directory.
        /// </summary>
        public void RefreshTemplates()
        {
            _templates.Clear();

            var files = CustomTemplateIO.EnumerateTemplateFiles();
            foreach (var file in files)
            {
                var template = CustomTemplateIO.Load(file);
                if (template != null && !string.IsNullOrEmpty(template.Name))
                {
                    _templates.Add(template);
                }
            }

            // Sort by name
            _templates.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            _isInitialized = true;
            TemplatesChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gets a template by name (case-insensitive).
        /// </summary>
        public CustomCanvasTemplate? GetTemplate(string name)
        {
            EnsureInitialized();
            return _templates.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Saves a new custom template.
        /// </summary>
        public CustomCanvasTemplate SaveTemplate(string name, int tileWidth, int tileHeight, int tileCountX, int tileCountY)
        {
            var template = new CustomCanvasTemplate(name, tileWidth, tileHeight, tileCountX, tileCountY);
            CustomTemplateIO.Save(template);

            // Refresh to pick up the new template
            RefreshTemplates();

            return template;
        }

        /// <summary>
        /// Saves a custom template from a CanvasTemplate.
        /// </summary>
        public CustomCanvasTemplate SaveTemplate(CanvasTemplate template)
        {
            return SaveTemplate(template.Name, template.TileWidth, template.TileHeight, template.TileCountX, template.TileCountY);
        }

        /// <summary>
        /// Deletes a custom template by name.
        /// </summary>
        public bool DeleteTemplate(string name)
        {
            var template = GetTemplate(name);
            if (template == null)
                return false;

            var dir = CustomTemplateIO.GetTemplatesDirectory();
            var path = System.IO.Path.Combine(dir, template.GetFileName());

            if (CustomTemplateIO.Delete(path))
            {
                RefreshTemplates();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets all template names.
        /// </summary>
        public IReadOnlyList<string> GetTemplateNames()
        {
            EnsureInitialized();
            return _templates.Select(t => t.Name).ToList();
        }

        /// <summary>
        /// Gets all templates as CanvasTemplate objects for use in dialogs.
        /// </summary>
        public IReadOnlyList<CanvasTemplate> GetAsCanvasTemplates()
        {
            EnsureInitialized();
            return _templates.Select(t => t.ToCanvasTemplate()).ToList();
        }

        /// <summary>
        /// Checks if a template with the given name exists.
        /// </summary>
        public bool HasTemplate(string name)
        {
            return GetTemplate(name) != null;
        }
    }
}

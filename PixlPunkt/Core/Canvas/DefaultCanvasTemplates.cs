using System;
using System.Collections.Generic;

namespace PixlPunkt.Core.Canvas
{
    public class DefaultCanvasTemplates
    {
        public static IReadOnlyList<CanvasTemplate> All { get; }


        public static readonly Dictionary<string, CanvasTemplate> ByName;


        static DefaultCanvasTemplates()
        {
            var list = new List<CanvasTemplate>
            {
               PixlPunktDefault(),
               SimpleIcon(),
               SimpleCursor(),
               HighResIcon(),
               HighResCursor(),
               CustomBrush(),
            };

            All = list;
            ByName = new(StringComparer.OrdinalIgnoreCase);
            foreach (var p in list) ByName[p.Name] = p;
        }

        private static CanvasTemplate PixlPunktDefault() => new()
        {
            Name = "PixlPunkt Default",
            TileWidth = 16,
            TileHeight = 16,
            TileCountX = 8,
            TileCountY = 8,
            IsBuiltIn = true
        };

        private static CanvasTemplate SimpleCursor() => new()
        {
            Name = "Simple Cursor",
            TileWidth = 32,
            TileHeight = 32,
            TileCountX = 1,
            TileCountY = 1,
            IsBuiltIn = true
        };

        private static CanvasTemplate HighResCursor() => new()
        {
            Name = "High Res Cursor",
            TileWidth = 32,
            TileHeight = 32,
            TileCountX = 4,
            TileCountY = 4,
            IsBuiltIn = true
        };

        private static CanvasTemplate SimpleIcon() => new()
        {
            Name = "Simple Icon",
            TileWidth = 64,
            TileHeight = 64,
            TileCountX = 1,
            TileCountY = 1,
            IsBuiltIn = true
        };

        private static CanvasTemplate HighResIcon() => new()
        {
            Name = "High Res Icon",
            TileWidth = 64,
            TileHeight = 64,
            TileCountX = 4,
            TileCountY = 4,
            IsBuiltIn = true
        };

        /// <summary>
        /// Creates a template for custom brush creation with pre-named layers for each tier.
        /// </summary>
        private static CanvasTemplate CustomBrush() => new BrushCanvasTemplate();
    }
}

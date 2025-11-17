using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;

namespace Amicus.UI
{
    /// <summary>
    /// Represents a decoration item with its variants
    /// </summary>
    public class DecorationItem
    {
        public string Name { get; set; } = string.Empty;
        public int Columns { get; set; }
        public int Rows { get; set; }
        public List<CroppedBitmap> Variants { get; set; } = new();
        public int CellWidth { get; set; }
        public int CellHeight { get; set; }
    }

    /// <summary>
    /// Represents a placed decoration in the room
    /// </summary>
    public class PlacedDecoration
    {
        public string DecorationName { get; set; } = string.Empty;
        public int VariantIndex { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Scale { get; set; } = 1.0;
    }

    /// <summary>
    /// Manages decoration sprites and placement
    /// </summary>
    public class DecorationManager
    {
        private readonly ILogger<DecorationManager> _logger;
        private const string DECORATIONS_BASE_PATH = "Resources/Sprites/RetroCatsPaid/CatItems/Decorations/";

        // Cache of loaded decorations
        private Dictionary<string, DecorationItem> _decorationCache = new();

        // Currently placed decorations in the room
        private List<PlacedDecoration> _placedDecorations = new();

        public DecorationManager(ILogger<DecorationManager> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Loads all decoration assets from the decorations folder
        /// </summary>
        public void LoadAllDecorations()
        {
            try
            {
                _logger.LogInformation("Loading all decorations...");

                string[] decorationFiles = new[]
                {
                    "bed-2x3.png",
                    "climber1-1x2.png",
                    "climber2_bottom-5x1.png",
                    "climber2_top-6x1.png",
                    "foodbowl_empty-2x3.png",
                    "foodbowl_full-2x3.png",
                    "mouse-1x1.png",
                    "picture0-2x1.png",
                    "picture1-3x2.png",
                    "picture2-3x3.png",
                    "plant_large-1x4.png",
                    "plant_small-2x3.png",
                    "scratcher1-3x1.png",
                    "shelf-1x5.png",
                    "window_left-3x1.png",
                    "window_right-2x2.png",
                    "window_small-2x2.png",
                    "table-5x1.png",
                    "toy_fish-1x4.png",
                };

                foreach (var file in decorationFiles)
                {
                    LoadDecoration(file);
                }

                _logger.LogInformation($"Loaded {_decorationCache.Count} decorations");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load decorations");
            }
        }

        /// <summary>
        /// Loads a single decoration from file
        /// </summary>
        private void LoadDecoration(string filename)
        {
            try
            {
                // Parse filename: {name}-{cols}x{rows}.png
                var match = Regex.Match(filename, @"^(.+)-(\d+)x(\d+)\.png$");
                if (!match.Success)
                {
                    _logger.LogWarning($"Skipping file with invalid format: {filename}");
                    return;
                }

                string name = match.Groups[1].Value;
                int cols = int.Parse(match.Groups[2].Value);
                int rows = int.Parse(match.Groups[3].Value);

                string filePath = $"{DECORATIONS_BASE_PATH}{filename}";
                _logger.LogDebug($"Loading decoration: {name} ({cols}x{rows})");

                // Load the image
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath, UriKind.Relative);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                // Calculate cell size
                int cellWidth = bitmap.PixelWidth / cols;
                int cellHeight = bitmap.PixelHeight / rows;

                // Extract all variants from the grid
                var variants = new List<CroppedBitmap>();
                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        int x = col * cellWidth;
                        int y = row * cellHeight;
                        var sourceRect = new System.Windows.Int32Rect(x, y, cellWidth, cellHeight);
                        var cropped = new CroppedBitmap(bitmap, sourceRect);
                        cropped.Freeze();
                        variants.Add(cropped);
                    }
                }

                // Cache the decoration
                var decoration = new DecorationItem
                {
                    Name = name,
                    Columns = cols,
                    Rows = rows,
                    Variants = variants,
                    CellWidth = cellWidth,
                    CellHeight = cellHeight
                };

                _decorationCache[name] = decoration;
                _logger.LogInformation($"Loaded decoration '{name}': {variants.Count} variants ({cellWidth}x{cellHeight}px each)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to load decoration: {filename}");
            }
        }

        /// <summary>
        /// Gets a decoration by name
        /// </summary>
        public DecorationItem? GetDecoration(string name)
        {
            return _decorationCache.TryGetValue(name, out var decoration) ? decoration : null;
        }

        /// <summary>
        /// Adds a decoration to the room at specified position
        /// </summary>
        public void PlaceDecoration(string decorationName, int variantIndex, double x, double y, double scale = 1.0)
        {
            if (!_decorationCache.ContainsKey(decorationName))
            {
                _logger.LogWarning($"Decoration '{decorationName}' not found");
                return;
            }

            var placed = new PlacedDecoration
            {
                DecorationName = decorationName,
                VariantIndex = variantIndex,
                X = x,
                Y = y,
                Scale = scale
            };

            _placedDecorations.Add(placed);
            _logger.LogDebug($"Placed decoration '{decorationName}' variant {variantIndex} at ({x}, {y}) scale {scale}");
        }

        /// <summary>
        /// Gets all placed decorations
        /// </summary>
        public List<PlacedDecoration> GetPlacedDecorations()
        {
            return _placedDecorations;
        }

        /// <summary>
        /// Clears all placed decorations
        /// </summary>
        public void ClearDecorations()
        {
            _placedDecorations.Clear();
        }
    }
}

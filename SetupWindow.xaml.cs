using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace AMICUS
{
    /// <summary>
    /// First startup setup window for naming pet and customizing decorations
    /// </summary>
    public partial class SetupWindow : Window
    {
        // Public properties for results
        public string PetName { get; private set; } = "";
        public Dictionary<string, int> DecorationColors { get; private set; } = new();
        public bool RemindersEnabled { get; private set; } = true;

        // Decoration data
        private readonly Dictionary<string, DecorationData> _decorations = new();
        private readonly Dictionary<string, System.Windows.Controls.Image> _decorationImages = new();

        // Customizable decorations with their variant counts and positions
        // Positions match actual LoadRoom positions (232x232 display)
        private readonly List<CustomizableDecoration> _customizableDecorations = new()
        {
            new("window_left", "window_left-3x1.png", 3, 40, 33, 0.59),    // Left window
            new("window_right", "window_right-2x2.png", 4, 160, 47, 0.59), // Right window
            new("bed", "bed-2x3.png", 6, 12, 117, 0.5),              // Main bed
            new("table", "table-5x1.png", 5, 175, 115, 0.69),        // Table
            new("plant_small", "plant_small-2x3.png", 6, 185, 110, 0.69),  // Small plant
        };

        private const string DECORATIONS_PATH = "Resources/Sprites/RetroCatsPaid/CatItems/Decorations/";
        private const string ROOMS_PATH = "Resources/Sprites/RetroCatsPaid/CatItems/Rooms/";

        public SetupWindow()
        {
            InitializeComponent();

            // Initialize decoration colors with defaults (variant 0)
            foreach (var dec in _customizableDecorations)
            {
                DecorationColors[dec.Name] = 0;
            }

            // Load room and decorations
            LoadRoomPreview();
            LoadDecorations();
            RenderDecorations();
        }

        /// <summary>
        /// Loads the room background image
        /// </summary>
        private void LoadRoomPreview()
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri($"{ROOMS_PATH}Room1.png", UriKind.Relative);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                PreviewRoomImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load room: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads all customizable decoration sprites
        /// </summary>
        private void LoadDecorations()
        {
            foreach (var dec in _customizableDecorations)
            {
                try
                {
                    // Parse filename for grid size
                    var match = Regex.Match(dec.Filename, @"^(.+)-(\d+)x(\d+)\.png$");
                    if (!match.Success) continue;

                    int cols = int.Parse(match.Groups[2].Value);
                    int rows = int.Parse(match.Groups[3].Value);

                    // Load the sprite sheet
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri($"{DECORATIONS_PATH}{dec.Filename}", UriKind.Relative);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    // Calculate cell size
                    int cellWidth = bitmap.PixelWidth / cols;
                    int cellHeight = bitmap.PixelHeight / rows;

                    // Extract all variants
                    var variants = new List<CroppedBitmap>();
                    for (int row = 0; row < rows; row++)
                    {
                        for (int col = 0; col < cols; col++)
                        {
                            var rect = new Int32Rect(col * cellWidth, row * cellHeight, cellWidth, cellHeight);
                            var cropped = new CroppedBitmap(bitmap, rect);
                            cropped.Freeze();
                            variants.Add(cropped);
                        }
                    }

                    _decorations[dec.Name] = new DecorationData
                    {
                        Variants = variants,
                        CellWidth = cellWidth,
                        CellHeight = cellHeight
                    };
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load decoration {dec.Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Renders all decorations on the preview canvas
        /// </summary>
        private void RenderDecorations()
        {
            PreviewDecorationsCanvas.Children.Clear();
            _decorationImages.Clear();

            foreach (var dec in _customizableDecorations)
            {
                if (!_decorations.TryGetValue(dec.Name, out var data)) continue;

                int variantIndex = DecorationColors.TryGetValue(dec.Name, out int v) ? v : 0;
                if (variantIndex >= data.Variants.Count) variantIndex = 0;

                var image = new System.Windows.Controls.Image
                {
                    Source = data.Variants[variantIndex],
                    Width = data.CellWidth * dec.Scale,
                    Height = data.CellHeight * dec.Scale,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = dec.Name
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);

                // Add click handler
                image.MouseLeftButtonDown += Decoration_Click;

                // Position on canvas
                Canvas.SetLeft(image, dec.X);
                Canvas.SetTop(image, dec.Y);

                PreviewDecorationsCanvas.Children.Add(image);
                _decorationImages[dec.Name] = image;
            }
        }

        /// <summary>
        /// Handles decoration click to cycle variants
        /// </summary>
        private void Decoration_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not System.Windows.Controls.Image image || image.Tag is not string decorationName) return;
            if (!_decorations.TryGetValue(decorationName, out var data)) return;

            // Find the customizable decoration info
            var decInfo = _customizableDecorations.Find(d => d.Name == decorationName);
            if (decInfo == null) return;

            // Cycle to next variant
            int currentVariant = DecorationColors.TryGetValue(decorationName, out int v) ? v : 0;
            int nextVariant = (currentVariant + 1) % decInfo.VariantCount;
            DecorationColors[decorationName] = nextVariant;

            // Update the image
            if (nextVariant < data.Variants.Count)
            {
                image.Source = data.Variants[nextVariant];
            }

            // Add a subtle scale animation for feedback
            var scaleTransform = new ScaleTransform(1.0, 1.0);
            image.RenderTransform = scaleTransform;
            image.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);

            var animation = new DoubleAnimation
            {
                From = 1.0,
                To = 1.15,
                Duration = TimeSpan.FromMilliseconds(100),
                AutoReverse = true
            };

            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);

            e.Handled = true;
        }

        /// <summary>
        /// Handles pet name text changes
        /// </summary>
        private void PetNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Enable save button only if name is not empty
            SaveButton.IsEnabled = !string.IsNullOrWhiteSpace(PetNameTextBox.Text);
        }

        /// <summary>
        /// Handles save button click
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            PetName = PetNameTextBox.Text.Trim();
            RemindersEnabled = RemindersCheckBox.IsChecked ?? true;
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Allow window dragging
        /// </summary>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.OriginalSource is not System.Windows.Controls.TextBox)
            {
                DragMove();
            }
        }

        /// <summary>
        /// Data class for decoration variants
        /// </summary>
        private class DecorationData
        {
            public List<CroppedBitmap> Variants { get; set; } = new();
            public int CellWidth { get; set; }
            public int CellHeight { get; set; }
        }

        /// <summary>
        /// Configuration for customizable decorations
        /// </summary>
        private class CustomizableDecoration
        {
            public string Name { get; }
            public string Filename { get; }
            public int VariantCount { get; }
            public double X { get; }
            public double Y { get; }
            public double Scale { get; }

            public CustomizableDecoration(string name, string filename, int variantCount, double x, double y, double scale)
            {
                Name = name;
                Filename = filename;
                VariantCount = variantCount;
                X = x;
                Y = y;
                Scale = scale;
            }
        }
    }
}

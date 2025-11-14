using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AMICUS.Animation
{
    /// <summary>
    /// Manages sprite loading and frame extraction from individual sprite strip files
    /// </summary>
    public class SpriteManager
    {
        private const int FRAME_WIDTH = 64;
        private const int FRAME_HEIGHT = 64;
        private const string SPRITE_BASE_PATH = "Resources/Sprites/RetroCatsPaid/Cats/Sprites/";

        private Dictionary<string, List<CroppedBitmap>> _animationCache;

        public SpriteManager()
        {
            _animationCache = new Dictionary<string, List<CroppedBitmap>>();
        }

        /// <summary>
        /// Loads a sprite strip and extracts all frames automatically
        /// Frame count is calculated by dividing width by 64
        /// </summary>
        /// <param name="spriteName">Name of the sprite file (without .png extension)</param>
        /// <returns>List of cropped bitmap frames</returns>
        public List<CroppedBitmap> LoadAnimation(string spriteName)
        {
            // Check cache first
            if (_animationCache.ContainsKey(spriteName))
            {
                return _animationCache[spriteName];
            }

            try
            {
                string spritePath = $"{SPRITE_BASE_PATH}{spriteName}.png";
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(spritePath, UriKind.Relative);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                // Calculate frame count from bitmap width
                int frameCount = bitmap.PixelWidth / FRAME_WIDTH;

                // Extract frames from the horizontal strip
                var frames = new List<CroppedBitmap>();
                for (int i = 0; i < frameCount; i++)
                {
                    int x = i * FRAME_WIDTH;
                    var sourceRect = new System.Windows.Int32Rect(x, 0, FRAME_WIDTH, FRAME_HEIGHT);
                    frames.Add(new CroppedBitmap(bitmap, sourceRect));
                }

                // Cache the frames
                _animationCache[spriteName] = frames;
                return frames;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load sprite '{spriteName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Pre-defined animation loaders for different pet states
        /// </summary>
        public List<CroppedBitmap> GetIdleFrames()
        {
            return LoadAnimation("Idle");
        }

        public List<CroppedBitmap> GetRunningFrames()
        {
            return LoadAnimation("Running");
        }

        public List<CroppedBitmap> GetSleepingFrames()
        {
            return LoadAnimation("Sleeping");
        }

        public List<CroppedBitmap> GetExcitedFrames()
        {
            return LoadAnimation("Excited");
        }

        public List<CroppedBitmap> GetHappyFrames()
        {
            return LoadAnimation("Happy");
        }

        public List<CroppedBitmap> GetJumpFrames()
        {
            return LoadAnimation("Jump");
        }

        public List<CroppedBitmap> GetDanceFrames()
        {
            return LoadAnimation("Dance");
        }
    }
}

using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;

namespace Amicus.UI
{
    /// <summary>
    /// Manages room backgrounds and rendering for the pet house
    /// </summary>
    public class RoomManager
    {
        private readonly ILogger<RoomManager> _logger;
        private const string ROOMS_BASE_PATH = "Resources/Sprites/RetroCatsPaid/CatItems/Rooms/";
        private const int ROOM_SIZE = 512; // Rooms are 512x512 pixels

        private BitmapImage? _currentRoomImage;
        private string _currentRoomName = "Room1"; // Default room

        public RoomManager(ILogger<RoomManager> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Loads the default room (Room1.png)
        /// </summary>
        public BitmapImage LoadDefaultRoom()
        {
            return LoadRoom("Room1");
        }

        /// <summary>
        /// Loads a specific room by name (e.g., "Room1", "Room2", etc.)
        /// </summary>
        /// <param name="roomName">The room name without extension (e.g., "Room1")</param>
        /// <returns>BitmapImage of the room</returns>
        public BitmapImage LoadRoom(string roomName)
        {
            try
            {
                string roomPath = $"{ROOMS_BASE_PATH}{roomName}.png";
                _logger.LogDebug($"Loading room: {roomPath}");

                var bitmap = new BitmapImage();

                bitmap.BeginInit();
                bitmap.UriSource = new Uri(roomPath, UriKind.Relative);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze(); // Make it thread-safe and improve performance

                _currentRoomImage = bitmap;
                _currentRoomName = roomName;

                _logger.LogInformation($"Room loaded successfully: {roomName} ({bitmap.PixelWidth}x{bitmap.PixelHeight})");

                return bitmap;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to load room: {roomName}");
                throw;
            }
        }

        /// <summary>
        /// Gets the currently loaded room image
        /// </summary>
        public BitmapImage? GetCurrentRoom()
        {
            return _currentRoomImage;
        }

        /// <summary>
        /// Gets the name of the currently loaded room
        /// </summary>
        public string GetCurrentRoomName()
        {
            return _currentRoomName;
        }

        /// <summary>
        /// Gets the standard room size (512x512)
        /// </summary>
        public int GetRoomSize()
        {
            return ROOM_SIZE;
        }
    }
}

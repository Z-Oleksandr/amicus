using System;
using System.IO;
using System.Text.Json;
using System.Diagnostics;

namespace Amicus.Data
{
    /// <summary>
    /// Manages saving and loading game state to/from JSON files
    /// </summary>
    public static class SaveManager
    {
        private static readonly string SaveDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Amicus"
        );

        private static readonly string SaveFilePath = Path.Combine(SaveDirectory, "save.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true, // Pretty-print for readability
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Saves the current game state to disk
        /// </summary>
        /// <param name="saveData">The data to save</param>
        /// <returns>True if save was successful, false otherwise</returns>
        public static bool SaveGame(SaveData saveData)
        {
            try
            {
                // Ensure directory exists
                if (!Directory.Exists(SaveDirectory))
                {
                    Directory.CreateDirectory(SaveDirectory);
                    Debug.WriteLine($"[SaveManager] Created save directory: {SaveDirectory}");
                }

                // Update exit time
                saveData.Session.LastExitTime = DateTime.UtcNow;

                // Serialize to JSON
                string json = JsonSerializer.Serialize(saveData, JsonOptions);

                // Write to file
                File.WriteAllText(SaveFilePath, json);

                Debug.WriteLine($"[SaveManager] Game saved successfully to: {SaveFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SaveManager] ERROR: Failed to save game: {ex.Message}");
                Debug.WriteLine($"[SaveManager] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Loads the game state from disk
        /// </summary>
        /// <returns>Loaded SaveData, or null if load failed or file doesn't exist</returns>
        public static SaveData? LoadGame()
        {
            try
            {
                // Check if save file exists
                if (!File.Exists(SaveFilePath))
                {
                    Debug.WriteLine($"[SaveManager] No save file found at: {SaveFilePath}");
                    Debug.WriteLine("[SaveManager] Starting with default values");
                    return null;
                }

                // Read and deserialize
                string json = File.ReadAllText(SaveFilePath);
                SaveData? saveData = JsonSerializer.Deserialize<SaveData>(json, JsonOptions);

                if (saveData != null)
                {
                    Debug.WriteLine($"[SaveManager] Game loaded successfully from: {SaveFilePath}");
                    Debug.WriteLine($"[SaveManager] Last exit: {saveData.Session.LastExitTime}");
                    Debug.WriteLine($"[SaveManager] Pet was in room: {saveData.PetState.IsInRoom}");
                    Debug.WriteLine($"[SaveManager] Needs - H:{saveData.PetState.Hunger:F1} C:{saveData.PetState.Cleanliness:F1} Hp:{saveData.PetState.Happiness:F1}");
                }
                else
                {
                    Debug.WriteLine("[SaveManager] WARNING: Deserialization returned null");
                }

                return saveData;
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[SaveManager] ERROR: Corrupt save file: {ex.Message}");
                Debug.WriteLine("[SaveManager] Starting with default values");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SaveManager] ERROR: Failed to load game: {ex.Message}");
                Debug.WriteLine($"[SaveManager] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Deletes the save file (for debugging/testing)
        /// </summary>
        public static bool DeleteSave()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    File.Delete(SaveFilePath);
                    Debug.WriteLine($"[SaveManager] Save file deleted: {SaveFilePath}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SaveManager] ERROR: Failed to delete save: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the save file path (for debugging)
        /// </summary>
        public static string GetSavePath() => SaveFilePath;
    }
}

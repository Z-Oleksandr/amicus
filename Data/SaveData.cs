using System;

namespace Amicus.Data
{
    /// <summary>
    /// Root container for all persisted data
    /// </summary>
    public class SaveData
    {
        public PetStateData PetState { get; set; } = new();
        public UserSettingsData UserSettings { get; set; } = new();
        public RoomStateData RoomState { get; set; } = new();
        public SessionData Session { get; set; } = new();
    }

    /// <summary>
    /// Pet's current state and needs
    /// </summary>
    public class PetStateData
    {
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public string CurrentState { get; set; } = "Idle";
        public bool IsInRoom { get; set; }

        // Needs (0-100)
        public double Hunger { get; set; } = 100.0;
        public double Cleanliness { get; set; } = 100.0;
        public double Happiness { get; set; } = 100.0;
    }

    /// <summary>
    /// User preferences and settings
    /// </summary>
    public class UserSettingsData
    {
        public bool HouseLocked { get; set; }
        public string PetName { get; set; } = "Cat"; // Future feature
        public bool SoundEnabled { get; set; } = true; // Future feature

        // Future: decoration color preferences
        // public Dictionary<string, int> DecorationColors { get; set; } = new();
    }

    /// <summary>
    /// Room and decoration states
    /// </summary>
    public class RoomStateData
    {
        public bool FoodBowlFull { get; set; }
    }

    /// <summary>
    /// Session tracking data
    /// </summary>
    public class SessionData
    {
        public DateTime LastExitTime { get; set; } = DateTime.UtcNow;
    }
}

using System;
using System.Collections.Generic;

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
        public ReminderData Reminders { get; set; } = new();
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
        public string PetName { get; set; } = "";
        public bool SoundEnabled { get; set; } = true;
        public bool HasCompletedSetup { get; set; } = false;
        public Dictionary<string, int> DecorationColors { get; set; } = new();
        public bool RemindersEnabled { get; set; } = true;
    }

    /// <summary>
    /// Room and decoration states
    /// </summary>
    public class RoomStateData
    {
        public bool FoodBowlFull { get; set; }
        public List<PoopPositionData> PoopPositions { get; set; } = new();
    }

    /// <summary>
    /// Individual poop position data for persistence
    /// </summary>
    public class PoopPositionData
    {
        public double X { get; set; }
        public double Y { get; set; }
        public DateTime SpawnTime { get; set; }
    }

    /// <summary>
    /// Session tracking data
    /// </summary>
    public class SessionData
    {
        public DateTime LastExitTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Reminder system data
    /// </summary>
    public class ReminderData
    {
        public DateTime LastWaterReminder { get; set; } = DateTime.MinValue;
        public DateTime LastExerciseReminder { get; set; } = DateTime.MinValue;
        public List<CustomReminderData> CustomReminders { get; set; } = new();
    }

    /// <summary>
    /// Individual custom reminder data
    /// </summary>
    public class CustomReminderData
    {
        public string Message { get; set; } = "";
        public DateTime ScheduledTime { get; set; }
        public bool HasBeenDisplayed { get; set; } = false;
    }
}

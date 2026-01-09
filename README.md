# Amicus - Desktop Pet Cat

A cozy desktop companion that lives on your screen. Amicus is a retro-style pixel art cat that keeps you company, reminds you to drink water and exercise, and brings a touch of joy to your Windows desktop.

![Framework](https://img.shields.io/badge/.NET-9.0-blue)
![Platform](https://img.shields.io/badge/Platform-Windows-blue)
![Status](https://img.shields.io/badge/Status-Beta-blue)

## Features

### Pet Behavior

-   Wanders around your desktop with smooth animations
-   Multiple states: Idle, Walking, Running, Happy, Chasing, Attacking, Sleeping
-   Needs system (hunger, cleanliness, happiness) that degrade over time
-   Click to pet and increase happiness
-   Mouse chasing behavior with attack animations

### Pet's House

-   Cozy room with customizable decorations
-   Food bowl to feed your cat
-   Interactive brush for grooming
-   Poop system with scoop and garbage disposal
-   Lock/unlock pet in room

### Reminder System

-   Water reminders every 1.5 hours
-   Exercise reminders every hour
-   Custom reminders via table click
-   Cute cat-style messages
-   Click to dismiss

### First Startup Setup

-   Name your cat
-   Customize decoration colors
-   Toggle reminders on/off

### Persistence

-   Auto-saves on exit and shutdown
-   Remembers pet state, needs, and position
-   Tracks time away for realistic need degradation

## Requirements

-   Windows 10/11 (64-bit)
-   .NET 9.0 Runtime (included in installer)

# Installation

## Using Installer

1. Download the latest `Setup_AMICUS_vX.X.X.exe` from releases (or installer__output folder)
2. Run the installer
3. Choose installation options (desktop shortcut, startup)
4. Launch Amicus

### Building from Source

1. Clone the repository

    ```bash
    git clone <repository-url>
    cd Amicus
    ```

2. Build and run

    ```bash
    dotnet run
    ```

3. For release build
    ```bash
    dotnet publish -c Release -r win-x64 --self-contained true
    ```

## Usage

-   **Drag pet**: Click and drag the cat around your desktop
-   **Pet the cat**: Click on the cat to increase happiness
-   **Open house**: Click the house button (bottom-right)
-   **Feed**: Click food bowl in house
-   **Groom**: Pick up brush and drag over cat
-   **Custom reminder**: Click table in house
-   **Exit**: Right-click system tray icon or click left window in house

## Project Structure

```
Amicus/
├── Animation/         # AnimationController, SpriteManager, PetState
├── Data/              # SaveManager, SaveData models
├── UI/                # DecorationManager, RoomManager
├── Resources/
│   ├── Sprites/       # Pet and room sprite sheets
│   ├── Icon/          # Application icon
│   └── elements/      # UI elements (message bubbles)
├── MainWindow.xaml    # Main game UI
├── SetupWindow.xaml   # First startup setup
└── CustomReminderWindow.xaml  # Custom reminder dialog
```

## Technology Stack

-   **Framework**: WPF (Windows Presentation Foundation)
-   **.NET Version**: 9.0
-   **Language**: C#
-   **Graphics**: Pixel art sprites with NearestNeighbor scaling
-   **Persistence**: JSON save files
-   **Installer**: Inno Setup

## Credits

-   Sprite pack: Retro Cats (ToffeeCraft)
-   Framework: WPF / .NET 9.0

## License

Private project - All rights reserved

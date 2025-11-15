# Amicus - Desktop Pet Cat

A desktop companion that lives on your screen. Amicus is a retro-style pixel art cat that keeps you company on your Windows desktop.

![Phase](https://img.shields.io/badge/Phase-2%20Complete-brightgreen)
![Framework](https://img.shields.io/badge/.NET-9.0-blue)
![Platform](https://img.shields.io/badge/Platform-Windows-blue)
![Status](https://img.shields.io/badge/Status-Active%20Development-yellow)

## Requirements

-   Windows 10/11
-   .NET 9.0 SDK

## Quick Start

1. **Clone the repository**

    ```bash
    git clone <repository-url>
    cd Amicus
    ```

2. **Build and run**

    ```bash
    dotnet run
    ```

3. **Exit the app**
    - Right-click the system tray icon → Exit
    - Or open the house panel → Exit Application button

## Project Structure

```
Amicus/
├── Core/              # Pet logic and controllers (Phase 2)
├── UI/                # Additional UI components
├── Animation/         # Animation system (Phase 2)
├── Data/              # Save system and reminders (Phase 2+)
├── Resources/
│   └── Sprites/       # Pet sprite sheets
├── MainWindow.xaml    # Main UI
└── MainWindow.xaml.cs # Application logic
```

## Technology Stack

-   **Framework**: WPF (Windows Presentation Foundation)
-   **.NET Version**: 9.0
-   **Language**: C#
-   **Graphics**: Pixel art sprites with NearestNeighbor scaling
-   **Logging**: Microsoft.Extensions.Logging (console output)

## Development Status

| Phase                 | Status      | Features                                                      |
| --------------------- | ----------- | ------------------------------------------------------------- |
| Phase 1: Foundation   | ✅ Complete | Transparent window, dragging, house panel, basic interactions |
| Phase 2: Pet Behavior | ✅ Complete | Animations, wandering, needs degradation, sprite flipping     |
| Phase 3: Interactions | 📋 Planned  | Mouse chasing, advanced interactions                          |
| Phase 4: Polish       | 📋 Planned  | Notifications, settings, sounds                               |

## Credits

-   Sprite pack: Retro Cats (ToffeeCraft)
-   Framework: WPF / .NET 9.0

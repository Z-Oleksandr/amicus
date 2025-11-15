# Desktop pet cat app

## Technology Stack Recommendation

For a Windows desktop pet app with these features, I'd recommend:

-   **Framework**: WPF (Windows Presentation Foundation) with C#/.NET (.NET 9.0)
-   **Why WPF**: Native Windows support, excellent transparency/layering capabilities, smooth animations, and the ability to create always-on-top borderless windows
-   **Alternative**: Electron with web technologies if you want cross-platform potential

## Architecture Plan

### 1. Core Components

**Main Window Manager**

-   Borderless, transparent window that spans the entire screen
-   Always-on-top functionality with click-through for non-interactive areas
-   Manages both the pet and house UI elements

**Pet Entity System**

-   State machine for pet behaviors (idle, walking, playing, sleeping, etc.)
-   Needs system (hunger, cleanliness, happiness)
-   Position and movement controller
-   Animation controller

**House UI Component**

-   Dockable panel in bottom-right corner
-   Can minimize/expand
-   Contains needs meters and interaction buttons

### 2. Key Technical Implementation

**Window Setup (WPF)**

```csharp
// Transparent, always-on-top window
WindowStyle = WindowStyle.None
AllowsTransparency = true
Background = Transparent
Topmost = true
ShowInTaskbar = false
```

**Hit Testing**

-   Make most of the window click-through except pet and house
-   Use Win32 API calls for proper click-through behavior
-   Pet remains interactive for dragging

### 3. Feature Implementation Plan

**Phase 1: Foundation ✅ COMPLETE**

Test Checklist:

1. ✅ Run the app: Builds and starts successfully
2. ✅ Verify transparency: Desktop is visible through empty areas
3. ✅ Test dragging: Cat is draggable
4. ✅ Test click-through: Works correctly on empty areas
5. ✅ Test house panel: Opens/closes with smooth animations
6. ✅ Test action buttons: All buttons functional (Feed, Clean, Play)
7. ✅ Test exit: Both system tray and in-app exit button work

**Implemented Features:**

-   ✅ Transparent overlay window with always-on-top functionality
-   ✅ Intelligent hit-testing (interactive elements clickable, transparent areas click-through)
-   ✅ Pet sprite rendering (91x91 pixels, 1.42x scale from 64x64)
-   ✅ Drag-and-drop functionality for pet
-   ✅ House UI panel with show/hide animations
-   ✅ Three needs meters (Hunger, Cleanliness, Happiness) - fully visible
-   ✅ Action buttons (Feed, Clean, Play) with temporary effects
-   ✅ System tray integration with context menu
-   ✅ Exit application functionality (system tray + in-app button)
-   ✅ Proper z-ordering (cat renders on top of house panel)
-   ✅ Correct positioning (house panel doesn't block Windows clock)

**Technical Implementation:**

-   Window: Borderless, maximized, transparent with WS_EX_LAYERED flag
-   Hit-testing: Win32 WndProc hook for selective click-through
-   Canvas: No background to allow click-through while keeping pet interactive
-   Sprite: Single-frame Idle1.png loaded from Resources/Sprites/
-   Animation: Smooth slide-in/slide-out for house panel (300ms with cubic easing)

**Known Limitations:**

-   Pet is static (no animations yet - Phase 2)
-   Needs don't degrade over time (Phase 2)
-   No mouse chasing behavior (Phase 3)
-   No persistence between sessions (Phase 2)

**Sprite Decision:**

-   Using existing PNGs as-is (each PNG contains multiple animation frames)
-   Phase 2 will implement frame extraction for animations

**Phase 2: Pet Behavior (Week 2-3) - 🚧 IN PROGRESS**

**Sprite Information:**
-   Sprite strips located at: Resources/Sprites/RetroCatsPaid/Cats/Sprites/
-   Each PNG contains horizontal sprite strip with multiple frames
-   Frame size: 64x64 pixels
-   Frame count calculated automatically (width / 64)
-   Available animations: Idle, Running, Sleeping, Excited, Happy, Jump, Dance, etc.

**Completed:**
-   ✅ Created Animation folder structure
-   ✅ Implemented PetState.cs (enum for Idle, Walking, Sleeping, Playing, Eating)
-   ✅ Implemented PetDirection.cs (enum for Left, Right)
-   ✅ Implemented SpriteManager.cs:
    -   Loads sprite strips from individual PNG files
    -   Automatically calculates frame count from bitmap width
    -   Extracts and caches frames as CroppedBitmap objects
    -   Helper methods for different animations (GetIdleFrames, GetRunningFrames, etc.)
-   ✅ Implemented AnimationController.cs:
    -   State machine with PetState transitions
    -   Frame-based animation system with configurable FPS
    -   Update() method for frame advancement
    -   GetCurrentFrame() to retrieve current animation frame
-   ✅ Added to MainWindow.xaml.cs:
    -   AnimationController instance
    -   DispatcherTimer for game loop (60 FPS)
    -   Fields for wandering behavior (timers, intervals, random)
    -   Fields for needs degradation system
    -   Pet velocity variables for movement

**Completed (Current Session):**
-   ✅ Complete MainWindow integration:
    -   ✅ Implemented GameTimer_Tick method (main game loop with 60 FPS update)
    -   ✅ Update animation frames each tick via AnimationController
    -   ✅ Removed old LoadPetSprite method
-   ✅ Implement random wandering behavior:
    -   ✅ Random direction changes every 2-5 seconds while walking
    -   ✅ Idle/walking state transitions (50% chance to walk after 3-8 sec idle)
    -   ✅ 30% chance to go idle while walking
    -   ✅ 2D movement (X and Y velocity) with diagonal support
    -   ✅ Pet faces left or right based on X velocity direction
    -   ✅ Boundary checking on all edges (top, bottom, left, right)
    -   ✅ Bounce behavior when hitting screen edges
    -   ✅ Pause wandering when being dragged
-   ✅ Implement needs degradation:
    -   ✅ Decay hunger (-5), cleanliness (-3), happiness (-4) every 30 seconds
    -   ✅ Update UI meters automatically
-   ✅ Fixed nullable reference warnings in AnimationController

**TODO (Testing & Refinement):**
-   ⏳ Test all features together
-   ⏳ Fix any bugs or issues discovered during testing
-   ⏳ Adjust animation speeds if needed
-   ⏳ Tune wandering behavior parameters (speed, intervals, etc.)

**Phase 3: Interactions (Week 3-4)**

-   Mouse cursor tracking and chasing behavior
-   Click interactions (petting, feeding, playing)
-   Drag-to-house functionality
-   Needs satisfaction mechanics

**Phase 4: Reminders & Polish (Week 4-5)**

-   Notification system for reminders
-   System tray integration
-   Settings menu for customization
-   Sound effects and purring

### 4. Data Structure Design

```csharp
class Pet {
    // Position & Movement
    Point Position
    Vector2 Velocity

    // Needs (0-100)
    float Hunger
    float Cleanliness
    float Happiness

    // State
    PetState CurrentState
    AnimationFrame CurrentAnimation

    // Behaviors
    bool IsChasingMouse
    bool IsBeingDragged
    bool IsInHouse
}

class Reminder {
    string Message
    TimeSpan Interval
    DateTime LastTriggered
    bool IsEnabled
}
```

### 5. Animation System

**Sprite Management**

-   Use sprite sheets for smooth animations
-   Each animation state has multiple frames
-   Implement smooth transitions between states

**Animation States**

-   Idle (sitting, standing variations)
-   Walking (left/right directions)
-   Running (chasing mouse)
-   Sleeping (z's animation)
-   Playing (batting at toys)
-   Happy/Sad expressions

### 6. Special Considerations

**Performance**

-   Use hardware acceleration for rendering
-   Implement efficient collision detection for mouse interaction
-   Throttle animation updates to 30-60 FPS

**User Experience**

-   Include "Do Not Disturb" mode
-   Save pet state between sessions (most likely needs a DB)

**Mouse Chasing Behavior**

-   Calculate distance to cursor
-   Randomly trigger chase mode when cursor is within range
-   Use easing functions for smooth pursuit
-   Add playful "pounce" animation when close

### 7. Development Tools Needed

-   **Visual Studio** for C#/WPF development
-   **Graphics software** for sprite creation (or use free sprite packs)
-   **Git** for version control
-   **WiX Toolset** or similar for creating installer

### 8. File Structure

```
CatDesktopPet/
├── Core/
│   ├── PetController.cs
│   ├── NeedsSystem.cs
│   └── StateManager.cs
├── UI/
│   ├── MainWindow.xaml
│   ├── HousePanel.xaml
│   └── SettingsWindow.xaml
├── Animation/
│   ├── AnimationController.cs
│   └── SpriteManager.cs
├── Data/
│   ├── SaveManager.cs
│   └── ReminderSystem.cs
└── Resources/
    ├── Sprites/
    └── Sounds/
```

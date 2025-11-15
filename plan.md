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

**Technical Implementation:**

-   Window: Borderless, maximized, transparent with WS_EX_LAYERED flag
-   Hit-testing: Win32 WndProc hook for selective click-through
-   Canvas: No background to allow click-through while keeping pet interactive
-   Sprite: Single-frame Idle1.png loaded from Resources/Sprites/
-   Animation: Smooth slide-in/slide-out for house panel (300ms with cubic easing)

**Known Limitations:**

-   Needs don't degrade over time (Phase 2)
-   No mouse chasing behavior (Phase 3)
-   No persistence between sessions (Phase 2)

**Sprite Decision:**

-   Using existing PNGs as-is (each PNG contains multiple animation frames)
-   Phase 2 will implement frame extraction for animations

**Phase 2: Pet Behavior (Week 2-3)**

**Sprite Information:**

-   Sprite strips located at: Resources/Sprites/RetroCatsPaid/Cats/Sprites/
-   Each PNG contains horizontal sprite strip with multiple frames
-   Frame size: 64x64 pixels
-   Frame count calculated automatically (width / 64)
-   Available animations: Idle, Running, Sleeping, Excited, Happy, Jump, Dance, etc.

**Phase 2 Status: ✅ COMPLETE**
All core pet behavior features implemented and tested successfully:

-   Pet wanders randomly in 2D space with smooth animations
-   Proper state management (Idle ↔ Walking transitions)
-   Sprite flipping based on movement direction
-   Edge detection and bouncing with proper physics
-   Needs degradation system working
-   Comprehensive logging for debugging
-   No known bugs

**TODO (Future Enhancements):**

-   ⏳ Fine-tune animation speeds if needed
-   ⏳ Adjust wandering behavior parameters (speed, intervals, etc.)
-   ⏳ Add more animation states (Sleeping, Playing, Eating)
-   ⏳ Tune needs degration

**Phase 3: Interactions (Week 3-4) - ⚠️ IN PROGRESS**

**Implemented Features:**
-   ✅ Mouse cursor tracking across entire window
-   ✅ Click interactions (petting increases happiness)
-   ✅ Drag-to-house functionality (opens house panel)
-   ✅ Action button animations (Feed → Eating, Clean → Playing, Play → Playing)
-   ✅ Attack animation state added
-   ✅ Petting cooldown (2 seconds)
-   ✅ Action animation duration (3 seconds)

**Known Issues (CRITICAL - Must Fix):**

1. **Attack Animation Bug** (MainWindow.xaml.cs:586-600)
   - Cat keeps attacking even when mouse is far away
   - Attack state doesn't properly transition back to chasing
   - Need to add distance check to exit attack state
   - Attack should be brief (1-2 seconds max), not continuous

2. **Chasing Behavior Broken** (MainWindow.xaml.cs:610-647)
   - Cat only moves if mouse is directly over it
   - Gets stuck in one spot showing running animation but not moving
   - Velocity calculation appears correct but pet doesn't actually move
   - Issue likely in position update logic or state management
   - Direction glitching persists despite cooldown implementation

3. **Idle Animation Movement** (MainWindow.xaml.cs:660-715)
   - Cat sometimes moves around screen while showing idle animation
   - Velocity not being properly zeroed when transitioning to idle
   - Need to ensure velocity is cleared on state change to idle

**Technical Details:**

Current Implementation:
- Trigger distance: 100px radius
- Chase duration: 10-15 seconds (randomized)
- Chase speed: 120 px/s
- Attack distance: 80px
- Min chase time before attack: 5 seconds
- Direction change cooldown: 0.2 seconds

**Next Steps:**
1. Fix attack state - add timer to exit attack after 1-2 seconds
2. Debug chasing movement - check why pet gets stuck
3. Fix velocity not clearing properly on idle transitions
4. Test all state transitions thoroughly
5. Add more robust logging for debugging state/velocity issues

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

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

**Work Session (2025-11-15) - Chase/Attack Logic Refactor:**

**✅ Completed Changes:**

1. **Updated Chase/Attack Constants** (MainWindow.xaml.cs:66-79)
   - Trigger distance: 100px → **200px radius**
   - Chase duration: **Fixed 15 seconds** (removed randomization)
   - Chase speed: 120 px/s (unchanged)
   - Attack distance: 80px → **20px radius**
   - Attack duration: 1.5 seconds (unchanged)
   - New: **Proximity duration = 2 seconds** (must stay within 20px for 2s to attack)
   - Removed: MIN_CHASE_TIME_BEFORE_ATTACK (old logic)

2. **Fixed Mouse Position Initialization** (MainWindow.xaml.cs:68, 282-286)
   - Added `_hasMousePosition` flag to track valid mouse position
   - Prevents chase triggering before mouse moves (was defaulting to 0,0)
   - Chase only checks after mouse has moved at least once

3. **Implemented Entry Detection** (MainWindow.xaml.cs:70, 607-628)
   - Added `_wasMouseInRange` flag to detect mouse ENTERING 200px radius
   - Chase only triggers when mouse transitions from outside → inside radius
   - Prevents immediate re-trigger after chase ends if mouse still within range
   - Flag resets after chase ends (attack/timeout/edge collision)

4. **New Proximity-Based Attack Logic** (MainWindow.xaml.cs:654-683)
   - Cat must stay within 20px continuously for 2 seconds to attack
   - Proximity timer increments when within range
   - Proximity timer **resets to 0** when mouse moves outside 20px
   - After attack finishes → always return to Idle (normal behavior)

5. **Chase Timeout Behavior** (MainWindow.xaml.cs:720-738)
   - 15 second timeout → returns to Idle
   - Resets check timer and range tracking flag

6. **Edge Collision Handling** (MainWindow.xaml.cs:880-960)
   - All 4 edges properly stop chase
   - Reset proximity timer, check timer, and range tracking flag

7. **Added Deceleration System** (MainWindow.xaml.cs:694-705)
   - Cat decelerates when within 50px of mouse
   - Full speed (120 px/s) at >50px
   - Min speed (20 px/s) at close range
   - Intended to prevent oscillation/overshooting

**❌ KNOWN ISSUES (UNRESOLVED):**

1. **Cat Glitching/Oscillating Near Mouse**
   - Symptom: Cat moves ~40px toward mouse, then starts rapid back-and-forth movement (5px oscillations)
   - Cause: Likely overshooting mouse position and reversing direction each frame
   - Attempted Fix: Added deceleration system (lines 694-705) - **DID NOT RESOLVE**
   - Status: **STILL BROKEN**

2. **Chase Loop After Attack**
   - Symptom: chase → attack → normal → immediate chase again (infinite loop)
   - Attempted Fix: Entry detection with `_wasMouseInRange` flag
   - Status: **NEEDS TESTING** (may still be broken)

3. **Cat Not Moving During Chase**
   - Symptom: Cat enters chase state but doesn't actually move
   - Previous Cause: Velocity set to 0 when within 5px ("Very close to target, stopping")
   - Attempted Fix: Removed 5px stop logic (line 692)
   - Status: **NEEDS TESTING**

**Current Implementation Details:**

Constants:
- CHASE_TRIGGER_DISTANCE: 200px radius (mouse enters this → chase starts)
- CHASE_DURATION: 15 seconds fixed
- CHASE_SPEED: 120 px/s
- ATTACK_DISTANCE: 20px radius (proximity range)
- PROXIMITY_DURATION: 2 seconds (time within 20px to trigger attack)
- DECEL_START_DISTANCE: 50px (where deceleration begins)
- MIN_SPEED: 20 px/s (minimum speed when close)
- CHASE_CHECK_INTERVAL: 2 seconds (how often to check for chase trigger)
- DIRECTION_CHANGE_COOLDOWN: 0.2 seconds

Logic Flow (INTENDED):
1. Initial state: Normal behavior (idle/walking)
2. Mouse ENTERS 200px radius → Chase starts (15s timer)
3. Cat chases mouse (follows even if mouse leaves 200px radius)
4. Cat stays within 20px for 2 consecutive seconds → Attack
5. Attack animation (1.5s) → Return to Idle
6. Chase ends if: (a) attack triggered, (b) 15s timeout, (c) edge collision
7. After chase ends, mouse must EXIT and RE-ENTER 200px to trigger new chase

**Debug Logs Location:** debug.md
- Shows cat getting stuck at 3.7px distance (line 67-363)
- Shows "Very close to target, stopping" messages
- Shows immediate chase restart after attack (line 401)

**TODO (Next Session):**
1. ⚠️ Fix oscillation/glitching issue (primary blocker)
   - Investigate velocity calculations vs frame rate
   - Consider damping factor or dead zone
   - May need to cap maximum movement per frame
2. ⚠️ Test entry detection (does it prevent chase loop?)
3. ⚠️ Test if cat actually moves during chase now
4. Review attack trigger logic (proximity timer implementation)
5. Add more detailed velocity/position logging to diagnose oscillation

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

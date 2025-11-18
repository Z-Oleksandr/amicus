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

**Phase 3: Interactions (Week 3-4) - ✅ COMPLETE**

**Implemented Features:**

-   ✅ Click interactions (petting increases happiness)
-   ✅ Drag-to-house functionality (opens house panel)
-   ✅ Action button animations (Feed → Eating, Clean → Playing, Play → Playing)
-   ✅ Petting cooldown (2 seconds)
-   ✅ Action animation duration (3 seconds)

**Work Session (2025-11-17) - Mouse Chasing & Attack System Implemented:**

Successfully implemented complete mouse chasing and attack behavior with clean architecture:

**Implemented Features:**

1. ✅ Mouse position tracking using Win32 API (GetCursorPos)
2. ✅ Proximity detection (200px radius)
3. ✅ Chase trigger system:
    - 2-second proximity timer before triggering
    - 42% chance to start chasing (adds personality!)
    - Logs when cat ignores the mouse
4. ✅ Chase behavior:
    - Duration: Random 10-15 seconds
    - Speed: Dynamic based on distance to mouse
        - Base speed (≤700px): 150 px/s
        - Medium speed (700-1200px): 200 px/s
        - Far speed (>1200px): 250 px/s
    - Continues for full duration regardless of mouse distance
    - Uses Running.png animation
5. ✅ Attack behavior:
    - Triggers when distance < 69px during chase
    - Plays Attack.png animation for 2 seconds
    - Can attack multiple times during single chase
    - Resumes chasing after each attack
6. ✅ New PetState enums: `Chasing` and `Attacking`
7. ✅ Clean state management and logging

**Technical Implementation:**

-   Mouse tracking: `MainWindow.xaml.cs:272-291` (UpdateMousePosition)
-   Chase trigger: `MainWindow.xaml.cs:567-603` (proximity timer + random chance)
-   Chase movement: `MainWindow.xaml.cs:638-720` (chase & attack logic)
-   State definitions: `Animation/PetState.cs:10-11`
-   Animation mapping: `Animation/AnimationController.cs:89-90`

**Behavior Flow:**

```
Mouse within 300px for 2s → 42% chance → Chase starts (10-15s) →
Distance < 69px → Attack (2s) → Resume chase → Repeat until duration ends → Idle
```

**Phase 4: Pet's House (Week 4-5) - ⚠️ IN PROGRESS**

**Overview:**
Implementing room view system where the pet can enter/exit its house, with decorations and interactive elements.

**Completed Features (Steps 1-4, 6):**

✅ **Step 1: Basic Room Display**

-   Replaced house panel with Room1.png (512×512) display
-   Room rendering at bottom-right corner with proper scaling
-   Toggle button functionality working
-   Implementation: `UI/RoomManager.cs`, `MainWindow.xaml`

✅ **Step 2: Needs Indicators**

-   Compact needs display above the room
-   3 progress bars (Hunger, Cleanliness, Happiness)
-   5px height, rounded corners, soft colors
-   Bold white text labels with black stroke
-   Position: Centered above room

✅ **Step 3: Decoration System Foundation**

-   Created `UI/DecorationManager.cs`
-   Sprite extraction from grid PNGs (columns × rows pattern)
-   Decoration data model and caching system
-   Position mapping system

✅ **Step 4: Decorations Placed**

-   11 decorations positioned and scaled:
    -   bed (12, 117) scale 0.5
    -   foodbowl_empty (127, 150) scale 0.69
    -   climber1, windows, table, pictures, toys, plants
-   All use variant index 0 (first color)

✅ **Step 6: Pet In House Functionality**

-   Added `InRoom` state to `PetState` enum
-   Pet can be dragged into room (appears on bed at position 12, 90)
-   Pet scales to 0.8x when in room, returns to 1.0x when grabbed
-   Drag-to-exit: Pet can be dragged out anywhere on desktop
-   Lock/unlock button (under hide button):
    -   Always visible when house panel open
    -   Icon: locked.png / unlocked.png (27×27)
    -   State persists between transitions
-   Random exit behavior:
    -   When unlocked: 40% chance every 30-60 seconds
    -   When locked: Pet cannot exit randomly
-   Uses Chilling.png or Sleeping.png animation (50% random choice)
-   Pet position in room: (12, 90) - 28px above bed decoration

**Work Session (2025-11-17) - Food Bowl System Implemented:**

✅ **Interactive Food Bowl:**

-   Clickable food bowl decoration
-   Message bubble UI with "Fill up food?" prompt
-   Bubble positioned over bowl with rounded background
-   Yes/No buttons for user interaction
-   Message bubble hides when house panel closes

✅ **Food Bowl State Management:**

-   Automatic switching between `foodbowl_empty.png` and `foodbowl_full.png`
-   Fill amount: 75% of hunger bar (max 100)
-   Auto-eat threshold: When hunger < 60%
-   Food persists in bowl until cat is hungry enough to eat

✅ **Auto-Eating Logic:**

-   Cat automatically eats when bowl is full AND hunger < 60%
-   No eating animation (instant hunger restoration)
-   Hunger increases by 75 (capped at 100)
-   Bowl empties after eating

**Technical Implementation:**

-   Food bowl state: `MainWindow.xaml.cs:127-131`
-   Message bubble UI: `MainWindow.xaml:195-245`
-   Clickable bowl: `MainWindow.xaml.cs:316-322`
-   Fill logic: `MainWindow.xaml.cs:750-783`
-   Auto-eat: `MainWindow.xaml.cs:1292-1304`

**UI Polish:**

-   Toggle house button now has rounded corners (CornerRadius="12")
-   Drop shadow effect applied
-   Smooth animations maintained

**TODO (Next Steps - Step 5 Skipped, Steps 7-9 Remaining):**

## Step 5: Color Variant Selection

**Goal:** Allow users to customize decoration colors

⏳ **Step 7: Lock/Unlock System** - PARTIALLY COMPLETE

-   ✅ Lock UI control and state
-   ✅ Lock prevents/allows random exit
-   ⏳ Manual "Let Pet Out" button (optional enhancement)

⏳ **Step 8: Random Exit Behavior** - ✅ COMPLETE

-   ✅ Exit timer implemented (30-60 seconds when unlocked)
-   ✅ Probability-based decision (40% chance)
-   ✅ Exit animation and repositioning

⏳ **Step 9: Polish & Testing**

-   Test all interactions thoroughly
-   Fix any edge cases or bugs
-   Update documentation

**Known Issues / Future Improvements:**

-   Step 5 (Color Variant Selection) - Skipped for now
-   Multiple rooms not yet implemented
-   Could add eating animation when bowl is consumed
-   Consider adding sound effects for food bowl interaction

**Phase 5: Persistence & Interactions (Week 6-7) - ✅ COMPLETE**

**Work Session (2025-11-17) - Persistence System Implemented:**

✅ **Data Models Created** (`Data/SaveData.cs`):

-   `SaveData` - Root container for all persisted data
-   `PetStateData` - Pet position, current state, needs (hunger, cleanliness, happiness)
-   `UserSettingsData` - House lock state, pet name, sound settings
-   `RoomStateData` - Food bowl state
-   `SessionData` - Last exit timestamp for time-away degradation

✅ **SaveManager Implemented** (`Data/SaveManager.cs`):

-   Uses JSON file storage at `%AppData%\Amicus\save.json`
-   `SaveGame()` - Auto-saves on app exit
-   `LoadGame()` - Auto-loads on app startup
-   Uses `System.Text.Json` with pretty-printing
-   Handles missing/corrupt files gracefully

✅ **Exit Game via Left Window**:

-   Left window decoration in house is clickable
-   Shows message bubble with `message_bubble_left.png`
-   Prompts "Do you want to exit?" with Yes/No buttons
-   Yes → saves game state and exits gracefully
-   Implementation: `MainWindow.xaml.cs:920-945`, `MainWindow.xaml:266-316`

✅ **Updated Needs Degradation System**:

**Active Gameplay Rates (while app running):**

-   Hunger: 66.67/hour (100→0 in 1.5 hours) - same everywhere
-   Cleanliness: 50/hour outside (100→0 in 2 hours), 0/hour inside house
-   Happiness: -10/hour inside house (gets bored), +5/hour outside (happy exploring)

**Time-Away Rates (while app is closed):**

-   Hunger: 66.67/hour (matches active)
-   Cleanliness: 0.333/hour in house (1 point per 3 hours), 50/hour outside
-   Happiness: -5/hour (fixed, regardless of location)

**Technical Implementation:**

-   Continuous degradation based on deltaTime (not interval-based)
-   Location-aware logic checks `_isPetInRoom` state
-   Applied in `GameTimer_Tick` (lines 1500-1533) and `ApplyTimeAwayDegradation` (lines 1689-1721)

✅ **Bug Fixes**:

-   Fixed PetInRoomCanvas blocking clicks to decorations (removed Background property)
-   All house decorations remain interactive when pet is in room

**Work Session (2025-11-18) - Interactive Brush & Grooming System Implemented:**

✅ **Interactive Brush - COMPLETE**:

**Goal:** Add draggable brush with grooming interaction

**Implemented Features:**

**Brush Item & Interaction:**
-   Brush decoration visible in house at position (85, 150)
-   Brush image: `brush.png` (819×643px) scaled to 0.035 (→ ~29×22px)
-   Pickup: Scales up to 0.05, moves to PetCanvas for top-level rendering
-   Drag: Follows cursor with 10px right, 15px up offset
-   Drop: Returns to original position in DecorationsCanvas with elastic animation
-   Files: `MainWindow.xaml.cs:341-385` (LoadBrush), `1020-1089` (handlers)

**Brushing Mechanic:**
-   Continuous stroking interaction system
-   Detection: Brush within 80px of pet center triggers Happy state
-   Animation: New `PetState.Happy` enum using `Happy.png` sprite
-   Pet behavior: Stops moving, stays still during brushing session
-   Stroke detection: Each 25px of brush movement = 1 stroke
-   Rewards per stroke: +2 cleanliness, +1 happiness (capped at 100)
-   Grace period: 3 seconds after brush leaves before ending session (prevents flicker)
-   Files: `MainWindow.xaml.cs:153-162` (state vars), `1093-1210` (brushing methods)

**Animation Updates:**
-   Added `PetState.Happy` to enum (`Animation/PetState.cs:16`)
-   Mapped Happy state to `Happy.png` animation (`Animation/AnimationController.cs:97`)

**Edge Cases Handled:**
-   Cannot brush while pet is in house
-   Cannot brush while pet is being dragged
-   Brushing ends when brush is released
-   Brushing resumes if brush returns within 3-second grace period

**Technical Implementation:**
-   Fixed RenderTransformOrigin issue (was 0.5,0.5 → changed to 0,0 to match decorations)
-   Fixed coordinate transformation (DecorationsCanvas → PetCanvas using TransformToVisual)
-   Fixed parent removal race condition with defensive Contains() checks
-   Integrated detection into GameTimer_Tick (line 1352-1356)

**TODO - Reminder System:**

-   Reminder creation:
    -   Drink water reminders
    -   Exercise reminders
    -   Custom reminders
-   Notification system for reminders

**TODO - Settings Menu:**

-   Settings menu for customization
    -   On first start up of the app pet customisation
    -   Settings button somewhere (one of the decoration elements in the room)

**Phase 6: Next steps**

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

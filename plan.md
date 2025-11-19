# Desktop Pet Cat App - AMICUS

## Technology Stack

-   **Framework**: WPF (Windows Presentation Foundation) with C#/.NET 9.0
-   **Architecture**: Borderless transparent window with selective click-through
-   **Animation**: Sprite-based system with state machine

## Development Progress

**Phase 1: Foundation ✅ COMPLETE**

-   Borderless transparent window with Win32 click-through
-   Basic pet rendering and house panel UI

**Phase 2: Pet Behavior ✅ COMPLETE**

-   Random wandering with smooth sprite animations (64×64 frames)
-   State machine (Idle, Walking, Running, Happy, Chasing, Attacking, etc.)
-   Needs degradation system (hunger, cleanliness, happiness)
-   Edge detection and physics

**Phase 3: Interactions ✅ COMPLETE**

-   Click petting (increases happiness)
-   Drag-to-house functionality
-   Mouse chasing & attack system:
    -   Win32 cursor tracking
    -   Proximity detection (200px) with 2s timer
    -   42% chance to trigger chase
    -   Dynamic speed based on distance
    -   Attack animation when close (<69px)

**Phase 4: Pet's House ✅ COMPLETE**

**Room System:**

-   Room1.png (512×512) display at bottom-right
-   Needs indicators (hunger, cleanliness, happiness) with progress bars
-   DecorationManager system for sprite-based decorations
-   11 decorations placed (bed, food bowl, climber, windows, toys, plants)

**Pet In Room:**

-   Drag pet into/out of room
-   Pet appears on bed, scales to 0.8x in room
-   Lock/unlock system prevents/allows random exit
-   Random exit: 40% chance every 30-60s when unlocked
-   Uses Chilling or Sleeping animation in room

**Food Bowl System:**

-   Clickable bowl with message bubble UI
-   Switches between empty/full states
-   Auto-eat when hunger < 60%
-   Fills 75% hunger

**Phase 5: Persistence & Save System ✅ COMPLETE**

**Save/Load System:**

-   JSON storage at `%AppData%\Amicus\save.json`
-   Saves pet state, needs, room state, user settings
-   Auto-save on exit, auto-load on startup
-   Time-away degradation calculation

**Exit Game:**

-   Clickable left window in room with confirmation bubble
-   Saves state before exiting

**Needs Degradation:**

-   Active rates: Hunger 66.67/hr, Cleanliness 50/hr (outside), Happiness varies by location
-   Time-away rates: Calculated based on last session timestamp
-   Location-aware degradation (inside vs outside house)

**Work Session (2025-11-18) - Interactive Brush & Grooming System Implemented:**

✅ **Interactive Brush - COMPLETE**:

**Brush Item & Interaction:**

-   Brush decoration visible in house at position (85, 150)
-   Pickup/drag/drop system with scale animation (0.035 → 0.05)
-   Canvas switching (DecorationsCanvas ↔ PetCanvas) for proper z-ordering

**Brushing Mechanic:**

-   Continuous stroking interaction system
-   Detection: Brush within 80px of pet triggers Happy state
-   Stroke detection: Each 25px of movement = 1 stroke
-   Rewards: +2 cleanliness, +1 happiness per stroke
-   Grace period: 3 seconds after brush leaves contact

**Work Session (2025-11-19) - Additional Interactive Items:**

✅ **Phase 5.5: Interactive Room Items - In Prgoress**

**Scoop (Pickupable Item):**

-   Location: `scoop.png` from CatItems/CatToys
-   Position: (180, -5) - top-right corner of house panel
-   Scale: 0.05 normal, 0.08 when picked up
-   Behavior: Draggable like brush, no special interaction yet
-   Implementation: Following exact brush pattern
-   Files: `MainWindow.xaml.cs:164-169` (vars), `407-453` (LoadScoop), `1159-1266` (handlers)

**Garbage (Animated GIF - Click-to-Animate):**

-   Location: `garbage.gif` (120×120px, animated) from CatItems/CatToys
-   Position: (10, -5) - top-left corner (symmetrical with scoop)
-   Scale: 0.3 (not pickupable - stays static)
-   Animation: Plays full GIF loop once when clicked (10 FPS)
-   GIF frame extraction using WPF's built-in `GifBitmapDecoder`
-   Idle state: Shows first frame only
-   Implementation: Custom frame-based animation integrated into game loop
-   Files: `MainWindow.xaml.cs:171-179` (vars), `468-521` (LoadGarbage), `1342-1359` (handler), `1638-1659` (animation update)

**Technical Notes:**

-   Both items load automatically in LoadRoom()
-   RenderDecorations() updated to include both items
-   Garbage demonstrates new pattern: static clickable animated objects

## Next Steps / TODO

**In this phase**

-   **Cat poop**
    -   Implement cat leaving poop on the screen every once in a while
    -   When cat leaves a poop - cleaniness goes down
    -   Poop drops from a cat (left at the position where cat was) at random times
-   Poop pick up functionality
    -   use scoop to pick up a poop:
        -   pick up scoop => bring it over to poop => move scoop over the poop => remove the poop from the screen, replace scoop with scoop with poop
    -   bring it to the garbage can
    -   when scoop with poop is over garbage can -> remove poop from scoop, play garbage animation (remove play animation on click from garbage)
-   Resources:
    -   In `Resources\Sprites\RetroCatsPaid\CatItems\CatToys` use:
        -   poop.png for poop
        -   poop_on_scoop.png to replace scoop when pop is picked up by it

**Phase 6: Additional Features**

⏳ **Reminder System:**

-   Drink water reminders
-   Exercise reminders
-   Custom user reminders
-   Notification system

⏳ **Settings Menu:**

-   Pet customization on first startup
-   Settings button in room
-   Sound toggle, reminder management

⏳ **Polish:**

-   Sound effects and purring
-   Additional room types
-   More interactive items
-   Scoop litter box cleaning mechanic
-   Garbage special interaction

## Project Structure

```
AMICUS/
├── Animation/          # AnimationController, SpriteManager, PetState
├── Data/              # SaveManager, SaveData models
├── UI/                # DecorationManager, RoomManager
├── Resources/         # Sprites, sounds
└── MainWindow.xaml.cs # Main game loop and logic
```

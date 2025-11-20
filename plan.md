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

**Work Session (2025-11-19) - Additional Interactive Items & Poop System:**

✅ **Phase 5.5: Interactive Room Items & Poop System - COMPLETE**

**Scoop (Pickupable Item):**

-   Location: `scoop.png` from CatItems/CatToys
-   Position: (180, -5) - top-right corner of house panel
-   Scale: 0.05 normal, 0.08 when picked up
-   Behavior: Draggable like brush, pickup poop interaction
-   Implementation: Following exact brush pattern
-   Files: `MainWindow.xaml.cs:165-169` (vars), `452-498` (LoadScoop), handlers

**Garbage (Animated GIF - Proximity-Triggered):**

-   Location: `garbage.gif` (120×120px, animated) from CatItems/CatToys
-   Position: (10, -5) - top-left corner (symmetrical with scoop)
-   Scale: 0.37 (not pickupable - stays static)
-   Animation: Plays full GIF loop once when scoop with poop is near (28 FPS)
-   GIF frame extraction using WPF's built-in `GifBitmapDecoder`
-   Idle state: Shows first frame only
-   Trigger: Proximity detection with scoop holding poop (80px threshold)
-   Implementation: Custom frame-based animation integrated into game loop
-   Files: `MainWindow.xaml.cs:172-179` (vars), `500-556` (LoadGarbage), `1763-1784` (animation update)

**Poop System:**

-   Random spawning: 50% chance every 1 min (debug) / 1 hour (production)
-   Spawn conditions: Outside house, Idle/Walking states only
-   Position: Behind cat based on facing direction (64px right/left, 64px down)
-   Cleanliness impact: -10 per poop spawn
-   Visual: `poop.png` sprite at 0.06 scale
-   Files: `MainWindow.xaml.cs:181-212` (vars/class), `591-647` (SpawnPoop), `2222-2246` (spawn logic)

**Poop Pickup & Disposal:**

-   Scoop proximity detection: 60px threshold to pick up poop
-   Visual feedback: Scoop changes to `poop_on_scoop.png` when holding poop
-   Garbage disposal: 80px proximity threshold
-   Disposal triggers garbage animation automatically
-   Files: `MainWindow.xaml.cs:1615-1667` (DetectScoopPoopProximity), `1669-1718` (DetectScoopGarbageProximity)

**Poop Persistence:**

-   Save/load: Poop positions saved to `save.json` on exit
-   Restoration: All poops restored at exact positions on startup
-   Cleanliness rule: Set to 0 if any poops existed on exit
-   Time-away poop: 1 random poop spawned if game off >= 3 min (debug) / 3 hours (production)
-   Time-away cleanliness: Normal -10 decrease (doesn't force to 0)
-   Random location: Time-away poop spawns at random screen position (not at pet)
-   Implementation: Deferred loading pattern (restore after images load)
-   Files:
    -   `Data/SaveData.cs:51,57-62` (PoopPositionData class, PoopPositions list)
    -   `MainWindow.xaml.cs:210-212` (restoration state vars)
    -   `MainWindow.xaml.cs:2463-2468` (save poops)
    -   `MainWindow.xaml.cs:2641-2672` (load poop data)
    -   `MainWindow.xaml.cs:2691-2719` (ApplyPoopRestoration)
    -   `MainWindow.xaml.cs:649-694` (RestorePoop)
    -   `MainWindow.xaml.cs:696-752` (SpawnRandomPoop)

**Debug Tools:**

-   Debug button available: `poop_debug.md` contains re-add instructions
-   Button spawns poop at current pet position for testing
-   Can be re-enabled for future debugging (XAML + handler code documented)

**Technical Notes:**

-   Both items load automatically in LoadRoom()
-   Garbage changed from click-to-animate to proximity-based
-   Poop instances managed in `List<PoopInstance>` with positions and spawn times
-   Game loop integration: Spawn timer, proximity detection, animation updates
-   Persistence uses deferred loading: save data → load images → restore poops

## Next Steps / TODO

**Future Enhancements**

**Phase 6: Additional Features**

⏳ **Reminder System:**

    - The pet should sometimes give friendly reminders to the user.
    - In the SetupWindow there should be a checkbox, which will say "Give reminders" => if checked the cat will do reminders functionality, if not, the cat won't.
    - Visually the reminders should use `Resources\elements\control\message_bubble.png` as background and display text on that. If the cat is at the top of the screen or if too close to one of the sides, so that the reminder doesn't fit onto the screen, it should be pushed away from the edge of the screen until it fits.
    - To dismiss the reminder, the user should just click on the message bubble.

    -   Drink water reminders: the drink water reminders should come once every 1,5 hours (for development make it once every 3 minutes). Claude should create several messages in a "cute cat style" (some funny, some a bit sarcastic) which would remind the user to drink some water. Every time it is time to give water reminder a random one should be chosen.

    -   Exercise reminders: exercise reminder should come once every hour and for the rest it should follow the template of the water reminder.

    -   Custom user reminders: to create a custom reminder, the user should click on the table in the cat's house. When clicked a window (similar to setupwindow) should pop up. In this window the user can enter a reminder message (limit a reasonable amount of characters, make the message bubble adjust it's size based on the amount of characters), enter a date and time at which the cat should display the reminder. App should use system time for this. If the cat was off while the reminder was supposed to come the cat should display the reminder at next start up and put "MISSED" at the beginning of the text.

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

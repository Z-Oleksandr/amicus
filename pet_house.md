# Phase 4 Implementation Plan: Pet's House

## Overview
Replace the current house panel with a room view system, implementing features step-by-step to create an interactive house for the pet.

---

## Step 1: Basic Room Display (Empty Room) ✅ COMPLETE
**Goal:** Replace current house panel with empty room display

### 1.1 Create Room Manager Component
- Create `UI/RoomManager.cs` class
- Load Room1.png (512×512) as default room
- Implement room rendering at same location as current house panel
- Scale room to fit panel size (~250px wide)

### 1.2 Modify House Panel in MainWindow.xaml
- Replace current panel content with Image control for room display
- Keep same position (bottom-right corner)
- Adjust dimensions to accommodate room (wider/taller)
- Keep toggle button functionality unchanged

### 1.3 Update Toggle Logic
- Modify `ToggleHousePanel_Click` to show room instead of needs panel
- Keep slide-in/out animation
- Test room visibility toggling

---

## Step 2: Redesign Needs Indicators ✅ COMPLETE
**Goal:** Create compact needs display above the room

### 2.1 Design Compact Needs UI
- Position: Centered above the room
- Layout: Vertical stack of three bars (Hunger, Cleanliness, Happiness)
- Bars: ~5px high, rounded corners, soft/cozy colors
- Background: Transparent
- Labels: Above each bar, left-aligned, bold white text with black border
- Label size: 75% of bar height
- Room position: Move down 15px to make space

### 2.2 Implement in XAML
- Add StackPanel for needs above room
- Each need: Label + ProgressBar
- ProgressBar styling: 5px height, rounded corners, soft colors
- Label styling: Bold white text with black stroke/border effect
- Transparent backgrounds throughout
- Center the needs panel above room

### 2.3 Update Needs Update Logic
- Connect existing needs values (Hunger, Cleanliness, Happiness)
- Bind ProgressBar values to pet state
- Verify real-time updates work correctly

---

## Step 3: Decoration System Foundation
**Goal:** Build infrastructure for placing decorations

### 3.1 Create Decoration Manager
- Create `UI/DecorationManager.cs` class
- Implement sprite extraction from grid PNGs (columns × rows pattern)
- Create decoration data model (type, position, color variant)

### 3.2 Parse Decoration Assets
- Read all decoration files from `CatItems/Decorations/`
- Parse filename pattern: `{name}-{cols}x{rows}.png`
- Extract individual item sprites from grids
- Cache extracted sprites

### 3.3 Create Decoration Placement System
- Define fixed positions for each decoration type
- Create position mapping (item type → X,Y coordinates in room)
- Implement decoration rendering on room background

---

## Step 4: Place Decorations One-by-One
**Goal:** Add each decoration type with fixed positioning

### 4.1-4.17 Add Individual Decorations (iterative process)
For each decoration type:
- `bed`, `foodbowl_full`, `foodbowl_empty`, `climber1`, `climber2`, `scratcher1`, `plant_small`, `plant_large`, `shelf`, `window_left`, `window_right`, `window_small`, `picture1`, `picture2`, `pictures`, `mouse`

**Process for each:**
1. Extract sprites from grid PNG
2. Choose fixed position in room (visually appropriate)
3. Select default color variant (first variant)
4. Render on room display
5. Test and adjust position if needed

---

## Step 5: Color Variant Selection
**Goal:** Allow users to customize decoration colors

### 5.1 Create Variant Selection UI
- Add decoration selection mode in room view
- Highlight selected decoration
- Show color variant options (thumbnails)

### 5.2 Implement Variant Switching
- Click decoration to select
- Display available variants
- Apply selected variant to room
- Persist selection (in-memory for now)

---

## Step 6: Pet In House Functionality
**Goal:** Move pet from desktop into house room

### 6.1 Add New Pet State
- Add `InHouse` or `InRoom` state to `PetState` enum
- Map to `Chilling.png` animation
- Update AnimationController state mapping

### 6.2 Implement Drag-to-House Detection
- Extend `CheckDragToHouse()` method
- Detect when pet is dropped in house button area
- Trigger room transition

### 6.3 Pet Transition Logic
- Hide pet from desktop (`PetImage.Visibility = Collapsed`)
- Set pet state to `InRoom`
- Show pet inside room at appropriate position
- Keep house button visible on desktop

### 6.4 Pet Animation in Room
- Render pet sprite inside room view
- Use `Chilling.png` or `Idle.png` animation
- Pet should appear to sit/relax in room

---

## Step 7: Lock/Unlock System
**Goal:** Control whether pet can exit house

### 7.1 Add Lock UI Control
- Add lock/unlock toggle button to room view
- Visual indicator (🔒/🔓 icon)
- Display lock status clearly

### 7.2 Implement Lock State
- Add `_isHouseLocked` boolean field
- Lock prevents pet from exiting
- Unlock allows random exit behavior

### 7.3 Manual Exit Option
- Add "Let Pet Out" button (always available)
- Works regardless of lock state
- Returns pet to desktop

---

## Step 8: Random Exit Behavior
**Goal:** Pet randomly exits when unlocked

### 8.1 Implement Exit Timer
- When unlocked, start random timer (e.g., 30-120 seconds)
- Check if pet wants to exit
- Probability-based decision (e.g., 30% chance)

### 8.2 Exit Animation
- Transition pet from room to desktop
- Set random position on desktop
- Resume normal desktop behaviors (wandering, etc.)
- Hide room view, show toggle button

---

## Step 9: Polish & Testing
**Goal:** Ensure everything works smoothly

### 9.1 Test All Interactions
- Toggle room visibility
- Drag pet to house
- Lock/unlock functionality
- Random exits
- Decoration customization
- Needs display updates

### 9.2 Fix Any Bugs
- Edge cases
- Animation glitches
- State management issues

### 9.3 Update plan.md
- Mark Phase 4 as complete
- Document any technical decisions
- Note any future improvements

---

## Technical Notes
- Maintain 60 FPS game loop
- Use existing animation patterns (150ms/frame)
- Keep NearestNeighbor scaling for pixel art
- Log state changes for debugging
- Keep code modular for future room additions

## Files to Create/Modify
**New Files:**
- `UI/RoomManager.cs`
- `UI/DecorationManager.cs`

**Modified Files:**
- `MainWindow.xaml` (house panel redesign)
- `MainWindow.xaml.cs` (room toggle, pet transition logic)
- `Animation/PetState.cs` (add InRoom state)
- `Animation/AnimationController.cs` (map InRoom state)
- `plan.md` (update progress)

---

**Total Steps:** 9 major steps, ~30-40 individual tasks
**Approach:** Incremental, testable at each step
**First Implementation:** Empty room display (Step 1)

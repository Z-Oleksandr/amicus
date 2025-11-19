# Poop Debug Button - Quick Re-add Guide

This document provides instructions for quickly re-adding the debug poop spawn button for testing purposes.

## Overview

The debug button allows you to manually trigger poop spawning without waiting for the automatic timer. This is useful for testing:
- Poop positioning (behind cat based on facing direction)
- Scoop pickup interaction
- Garbage disposal interaction
- Cleanliness decrease
- Multiple poop instances

---

## Step 1: Add Button to XAML UI

**File**: `MainWindow.xaml`

**Location**: After the `ToggleHouseButton` definition (around line 374)

**Code to Add**:

```xml
        <!-- DEBUG: Spawn Poop Button (Top-left corner) -->
        <Button x:Name="DebugSpawnPoopButton"
                Content="💩 Spawn Poop"
                Width="120"
                Height="40"
                Background="#FF6B6B"
                BorderThickness="0"
                FontSize="14"
                FontWeight="Bold"
                Foreground="White"
                HorizontalAlignment="Left"
                VerticalAlignment="Top"
                Margin="20,20,0,0"
                Click="DebugSpawnPoopButton_Click"
                Cursor="Hand"
                Style="{StaticResource RoundedToggleButton}">
        </Button>
```

**Exact Placement**:

```xml
        <!-- Toggle House Button (Bottom-right corner) -->
        <Button x:Name="ToggleHouseButton"
                Content="🏠"
                Width="50"
                Height="50"
                Background="#4A90E2"
                BorderThickness="0"
                FontSize="24"
                HorizontalAlignment="Right"
                VerticalAlignment="Bottom"
                Margin="0,0,20,60"
                Click="ToggleHouseButton_Click"
                Cursor="Hand"
                Style="{StaticResource RoundedToggleButton}">
        </Button>

        <!-- ADD DEBUG BUTTON HERE -->

        <!-- Pet will be rendered here (on top of everything else) -->
        <Canvas x:Name="PetCanvas">
```

---

## Step 2: Add Click Handler to Code-Behind

**File**: `MainWindow.xaml.cs`

**Location**: After the `ToggleHouseButton_Click` method (around line 1069)

**Code to Add**:

```csharp
        private void DebugSpawnPoopButton_Click(object sender, RoutedEventArgs e)
        {
            // DEBUG: Manually trigger poop spawn
            App.Logger.LogInformation("DEBUG: Manual poop spawn triggered");
            SpawnPoop();
        }
```

**Exact Placement**:

```csharp
        private void ToggleHouseButton_Click(object sender, RoutedEventArgs e)
        {
            if (HousePanel.Visibility == Visibility.Collapsed)
            {
                ShowHousePanel();
            }
            else
            {
                HideHousePanel();
            }
        }

        // ADD DEBUG HANDLER HERE

        private void CloseHouseButton_Click(object sender, RoutedEventArgs e)
        {
            HideHousePanel();
        }
```

---

## Step 3: Build and Run

```bash
dotnet build
dotnet run
```

The debug button will appear in the **top-left corner** of the screen as a red button with "💩 Spawn Poop" text.

---

## Button Behavior

When clicked, the button will:

1. **Spawn poop** at the cat's current position with offset based on facing direction:
   - **Facing Left**: 64px to the right, 64px down
   - **Facing Right**: 64px to the left, 64px down

2. **Decrease cleanliness** by 10 points

3. **Log to console**: "DEBUG: Manual poop spawn triggered"

4. **Add to scene**: Poop rendered on PetCanvas (visible on screen)

---

## Testing Workflow

1. **Spawn poop**: Click the debug button
2. **Verify position**: Check poop appears behind cat
3. **Test pickup**:
   - Click and drag the scoop from the room
   - Move scoop close to poop (within 60px)
   - Verify poop disappears and scoop changes to poop_on_scoop.png
4. **Test disposal**:
   - Move scoop with poop toward garbage can (within 80px)
   - Verify garbage animates and scoop returns to normal

---

## Customization Options

### Change Button Position

Modify the `Margin` property in XAML:

```xml
Margin="20,20,0,0"  <!-- Top-left: (left, top, right, bottom) -->
```

Examples:
- Top-right: `Margin="0,20,20,0"` + `HorizontalAlignment="Right"`
- Bottom-left: `Margin="20,0,0,60"` + `VerticalAlignment="Bottom"`
- Center-top: `Margin="0,20,0,0"` + `HorizontalAlignment="Center"`

### Change Button Appearance

```xml
Background="#FF6B6B"     <!-- Red background -->
Foreground="White"       <!-- White text -->
Width="120"              <!-- Button width -->
Height="40"              <!-- Button height -->
FontSize="14"            <!-- Font size -->
Content="💩 Spawn Poop"  <!-- Button text -->
```

### Add Multiple Debug Buttons

You can add additional debug buttons for other features:

```xml
<!-- Example: Clear All Poops Button -->
<Button x:Name="DebugClearPoopsButton"
        Content="🧹 Clear Poops"
        Width="120"
        Height="40"
        Background="#4CAF50"
        BorderThickness="0"
        FontSize="14"
        FontWeight="Bold"
        Foreground="White"
        HorizontalAlignment="Left"
        VerticalAlignment="Top"
        Margin="150,20,0,0"
        Click="DebugClearPoopsButton_Click"
        Cursor="Hand"
        Style="{StaticResource RoundedToggleButton}">
</Button>
```

With corresponding handler:

```csharp
private void DebugClearPoopsButton_Click(object sender, RoutedEventArgs e)
{
    App.Logger.LogInformation("DEBUG: Clearing all poops");

    // Remove all poop images from canvas
    foreach (var poop in _poopInstances)
    {
        PetCanvas.Children.Remove(poop.Image);
    }

    // Clear the list
    _poopInstances.Clear();

    App.Logger.LogInformation("All poops cleared");
}
```

---

## Removal Instructions

When done testing, remove the button by:

1. **Delete the button XAML** from `MainWindow.xaml`
2. **Delete the click handler** from `MainWindow.xaml.cs`
3. **Rebuild**: `dotnet build`

---

## Related Files

- **Poop Spawn Logic**: `MainWindow.xaml.cs` → `SpawnPoop()` method (lines ~591-643)
- **Poop System Variables**: `MainWindow.xaml.cs` (lines ~181-208)
- **Automatic Spawning**: `MainWindow.xaml.cs` → `GameTimer_Tick()` method (lines ~2063-2087)

---

## Notes

- The `SpawnPoop()` method is already implemented and doesn't need modification
- The button can be clicked while the cat is anywhere on screen (inside or outside room)
- Poop will only spawn if `_poopImage` is loaded (happens automatically in `LoadPoopImages()`)
- Multiple poops can be spawned without limit (no maximum count restriction)
- The debug button uses the existing `RoundedToggleButton` style for consistency

---

## Quick Copy-Paste

**Full XAML Button (Single Block)**:
```xml
        <Button x:Name="DebugSpawnPoopButton"
                Content="💩 Spawn Poop"
                Width="120"
                Height="40"
                Background="#FF6B6B"
                BorderThickness="0"
                FontSize="14"
                FontWeight="Bold"
                Foreground="White"
                HorizontalAlignment="Left"
                VerticalAlignment="Top"
                Margin="20,20,0,0"
                Click="DebugSpawnPoopButton_Click"
                Cursor="Hand"
                Style="{StaticResource RoundedToggleButton}">
        </Button>
```

**Full C# Handler (Single Block)**:
```csharp
        private void DebugSpawnPoopButton_Click(object sender, RoutedEventArgs e)
        {
            // DEBUG: Manually trigger poop spawn
            App.Logger.LogInformation("DEBUG: Manual poop spawn triggered");
            SpawnPoop();
        }
```

---

**Last Updated**: 2025-11-19
**Status**: Removed from production, documented for future re-add

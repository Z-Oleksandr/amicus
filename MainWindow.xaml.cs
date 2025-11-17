using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using WinForms = System.Windows.Forms;
using AMICUS.Animation;
using Amicus.UI;

namespace AMICUS
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Win32 API imports for click-through functionality
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int GWL_EXSTYLE = -20;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        // Pet dragging state
        private bool _isDraggingPet = false;
        private System.Windows.Point _petDragOffset;

        // Pet position
        private double _petX = 100;
        private double _petY = 100;
        private double _petVelocityX = 0;
        private double _petVelocityY = 0;
        private const double PET_SPEED = 50; // pixels per second

        // Pet needs (0-100)
        private double _hunger = 75;
        private double _cleanliness = 85;
        private double _happiness = 90;

        // Animation system
        private AnimationController _animationController;

        // Room management
        private RoomManager _roomManager;
        private DecorationManager _decorationManager;

        // Game loop timer
        private DispatcherTimer _gameTimer;
        private DateTime _lastUpdateTime;

        // Wandering behavior
        private Random _random;
        private double _wanderTimer = 0;
        private double _wanderInterval = 3.0; // Change direction every 3 seconds
        private double _idleTimer = 0;
        private double _idleInterval = 5.0; // Go idle every 5 seconds

        // Needs degradation
        private double _needsTimer = 0;
        private const double NEEDS_DECAY_INTERVAL = 30.0; // Decay every 30 seconds

        // Mouse tracking
        private System.Windows.Point _mousePosition;
        private bool _hasMousePosition = false;
        private const double DETECTION_RADIUS = 300.0; // Distance within which mouse position triggers

        // Mouse chasing behavior
        private double _proximityTimer = 0; // Tracks how long mouse has been within detection radius
        private bool _isChasing = false;
        private double _chaseTimer = 0;
        private double _chaseDuration = 0;
        private bool _isAttacking = false;
        private double _attackTimer = 0;
        private const double PROXIMITY_THRESHOLD = 2.0; // Seconds mouse must be within radius to trigger chase
        private const double CHASE_SPEED_BASE = 150.0; // Base chase speed
        private const double CHASE_SPEED_MEDIUM = 200.0; // Medium distance chase speed
        private const double CHASE_SPEED_FAR = 250.0; // Far distance chase speed
        private const double CHASE_DISTANCE_THRESHOLD_MEDIUM = 700.0; // Distance threshold for medium speed
        private const double CHASE_DISTANCE_THRESHOLD_FAR = 1200.0; // Distance threshold for far speed
        private const double CHASE_MIN_DURATION = 10.0;
        private const double CHASE_MAX_DURATION = 15.0;
        private const double ATTACK_DISTANCE = 69.0; // Distance to trigger attack animation
        private const double ATTACK_DURATION = 2.0; // Attack animation duration in seconds
        private const double CHASE_CHANCE = 0.42; // 42% chance to start chasing

        // Chase cooldown (prevents chasing immediately after a chase)
        private bool _chaseCooldownActive = false;
        private double _chaseCooldownTimer = 0;
        private double _chaseCooldownDuration = 0;
        private const double CHASE_COOLDOWN_MIN = 30.0; // Minimum cooldown in seconds
        private const double CHASE_COOLDOWN_MAX = 300.0; // Maximum cooldown in seconds (5 minutes)

        // Petting interaction
        private double _timeSinceLastInteraction = 0;
        private const double PET_COOLDOWN = 2.0; // Cooldown between petting in seconds

        // Action animations
        private bool _isPerformingAction = false;
        private double _actionTimer = 0;
        private const double ACTION_DURATION = 3.0; // Duration of action animations in seconds
        private PetState _stateBeforeAction = PetState.Idle;

        // System tray icon
        private WinForms.NotifyIcon? _notifyIcon;

        // Pet in room state
        private bool _isPetInRoom = false;
        private bool _isRoomLocked = false;
        private bool _isDraggingPetFromRoom = false;
        private System.Windows.Point _petInRoomDragOffset;
        private double _exitRoomTimer = 0;
        private double _exitRoomInterval = 50.0; // Initial interval value = 50 seconds

        // Food bowl state
        private bool _isFoodBowlFull = false;
        private const double FOOD_BOWL_FILL_AMOUNT = 75.0;
        private const double AUTO_EAT_THRESHOLD = 60.0;
        private System.Windows.Controls.Image? _foodBowlImage = null;

        public MainWindow()
        {
            InitializeComponent();
            SetupSystemTray();

            // Initialize animation system
            _animationController = new AnimationController();
            _random = new Random();

            // Initialize room manager
            var loggerFactory = App.ServiceProvider.GetRequiredService<ILoggerFactory>();
            _roomManager = new RoomManager(loggerFactory.CreateLogger<RoomManager>());
            _decorationManager = new DecorationManager(loggerFactory.CreateLogger<DecorationManager>());

            // Setup game loop timer (60 FPS)
            _gameTimer = new DispatcherTimer();
            _gameTimer.Interval = TimeSpan.FromMilliseconds(16.67); // ~60 FPS
            _gameTimer.Tick += GameTimer_Tick;
            _lastUpdateTime = DateTime.Now;
        }

        private void SetupSystemTray()
        {
            _notifyIcon = new WinForms.NotifyIcon();
            _notifyIcon.Icon = SystemIcons.Application; // Default icon for now
            _notifyIcon.Text = "Amicus - Desktop Pet";
            _notifyIcon.Visible = true;

            // Create context menu
            var contextMenu = new WinForms.ContextMenuStrip();

            var showHouseMenuItem = new WinForms.ToolStripMenuItem("Show House");
            showHouseMenuItem.Click += (s, e) => ShowHousePanel();

            var exitMenuItem = new WinForms.ToolStripMenuItem("Exit");
            exitMenuItem.Click += (s, e) => ExitApplication();

            contextMenu.Items.Add(showHouseMenuItem);
            contextMenu.Items.Add(new WinForms.ToolStripSeparator());
            contextMenu.Items.Add(exitMenuItem);

            _notifyIcon.ContextMenuStrip = contextMenu;

            // Double-click to show house
            _notifyIcon.DoubleClick += (s, e) => ShowHousePanel();
        }

        private void ExitApplication()
        {
            _notifyIcon?.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _notifyIcon?.Dispose();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                App.Logger.LogInformation("Window loaded, initializing pet...");

                // Set initial pet position
                UpdatePetPosition(_petX, _petY);

                // Load the default room
                LoadRoom();

                // Initialize pet to idle state
                _animationController.ChangeState(PetState.Idle);
                _animationController.ChangeDirection(PetDirection.Right);

                App.Logger.LogInformation("Pet initialized, starting game timer...");

                // Start the game timer
                _gameTimer.Start();

                // Set up click-through behavior after window is fully loaded
                this.SourceInitialized += MainWindow_SourceInitialized;
            }
            catch (Exception ex)
            {
                App.Logger.LogError(ex, "Error during window load");
            }
        }

        private void LoadRoom()
        {
            try
            {
                App.Logger.LogInformation("Loading room...");
                var roomImage = _roomManager.LoadDefaultRoom();
                RoomImage.Source = roomImage;

                // Load close button arrow
                var arrowImage = new BitmapImage();
                arrowImage.BeginInit();
                arrowImage.UriSource = new Uri("Resources/elements/navigation/right_arrow.png", UriKind.Relative);
                arrowImage.CacheOption = BitmapCacheOption.OnLoad;
                arrowImage.EndInit();
                CloseHouseArrow.Source = arrowImage;

                // Load unlocked icon (default state)
                UpdateLockIcon();

                // Load message bubble background
                var messageBubbleImage = new BitmapImage();
                messageBubbleImage.BeginInit();
                messageBubbleImage.UriSource = new Uri("Resources/elements/control/message_bubble.png", UriKind.Relative);
                messageBubbleImage.CacheOption = BitmapCacheOption.OnLoad;
                messageBubbleImage.EndInit();
                MessageBubbleImage.Source = messageBubbleImage;

                // Load all decorations
                _decorationManager.LoadAllDecorations();

                // Place decorations - positioning iteratively
                _decorationManager.PlaceDecoration("bed", 0, 12, 117, 0.5);
                _decorationManager.PlaceDecoration("foodbowl_empty", 0, 127, 150, 0.69);
                _decorationManager.PlaceDecoration("climber1", 0, 90, 19, 0.59);
                _decorationManager.PlaceDecoration("window_right", 0, 160, 47, 0.59);
                _decorationManager.PlaceDecoration("window_left", 0, 40, 33, 0.59);
                _decorationManager.PlaceDecoration("table", 0, 175, 115, 0.69);
                _decorationManager.PlaceDecoration("picture2", 0, 10, 80, 1);
                _decorationManager.PlaceDecoration("picture1", 0, 198, 75, 0.69);
                _decorationManager.PlaceDecoration("mouse", 0, 80, 105, 0.49);
                _decorationManager.PlaceDecoration("plant_small", 0, 185, 110, 0.69);
                _decorationManager.PlaceDecoration("toy_fish", 0, 120, 110, 0.49);



                // Render decorations on the canvas
                RenderDecorations();

                App.Logger.LogInformation("Room loaded successfully");
            }
            catch (Exception ex)
            {
                App.Logger.LogError(ex, "Failed to load room");
            }
        }

        private void RenderDecorations()
        {
            try
            {
                // Clear existing decorations from canvas
                DecorationsCanvas.Children.Clear();

                // Get all placed decorations
                var placedDecorations = _decorationManager.GetPlacedDecorations();

                foreach (var placed in placedDecorations)
                {
                    // Check if this is a food bowl - use appropriate state
                    string decorationName = placed.DecorationName;
                    if (decorationName == "foodbowl_empty" || decorationName == "foodbowl_full")
                    {
                        // Use the correct bowl based on state
                        decorationName = _isFoodBowlFull ? "foodbowl_full" : "foodbowl_empty";
                    }

                    var decoration = _decorationManager.GetDecoration(decorationName);
                    if (decoration == null || placed.VariantIndex >= decoration.Variants.Count)
                    {
                        continue;
                    }

                    // Create an Image element for this decoration
                    var image = new System.Windows.Controls.Image
                    {
                        Source = decoration.Variants[placed.VariantIndex],
                        Stretch = Stretch.None
                    };
                    RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);

                    // Apply scale transform if needed
                    if (placed.Scale != 1.0)
                    {
                        var scaleTransform = new ScaleTransform(placed.Scale, placed.Scale);
                        image.RenderTransform = scaleTransform;
                        image.RenderTransformOrigin = new System.Windows.Point(0, 0);
                    }

                    // Position the image on the canvas
                    Canvas.SetLeft(image, placed.X);
                    Canvas.SetTop(image, placed.Y);

                    // Make food bowl clickable
                    if (placed.DecorationName == "foodbowl_empty" || placed.DecorationName == "foodbowl_full")
                    {
                        image.MouseLeftButtonDown += FoodBowl_MouseLeftButtonDown;
                        image.Cursor = System.Windows.Input.Cursors.Hand;
                        _foodBowlImage = image;
                    }

                    // Add to canvas
                    DecorationsCanvas.Children.Add(image);
                }

                App.Logger.LogDebug($"Rendered {placedDecorations.Count} decorations");
            }
            catch (Exception ex)
            {
                App.Logger.LogError(ex, "Failed to render decorations");
            }
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            // Set up selective click-through
            SetupClickThrough();
        }

        private void SetupClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            // Get current window style
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            // Add layered window style but NOT transparent style
            // We'll handle hit testing ourselves
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | 0x00080000); // WS_EX_LAYERED

            // Set up hit test hook to make transparent areas click-through
            HwndSource source = HwndSource.FromHwnd(hwnd);
            source.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTTRANSPARENT = -1;
            const int HTCLIENT = 1;

            if (msg == WM_NCHITTEST)
            {
                // Get mouse position
                int x = lParam.ToInt32() & 0xFFFF;
                int y = lParam.ToInt32() >> 16;

                System.Windows.Point screenPoint = new System.Windows.Point(x, y);
                System.Windows.Point clientPoint = PointFromScreen(screenPoint);

                // Check if mouse is over PetImage
                if (IsMouseOverElement(PetImage, clientPoint))
                {
                    handled = true;
                    return new IntPtr(HTCLIENT);
                }

                // Check if mouse is over ToggleHouseButton
                if (ToggleHouseButton.Visibility == Visibility.Visible &&
                    IsMouseOverElement(ToggleHouseButton, clientPoint))
                {
                    handled = true;
                    return new IntPtr(HTCLIENT);
                }

                // Check if mouse is over HousePanel
                if (HousePanel.Visibility == Visibility.Visible &&
                    IsMouseOverElement(HousePanel, clientPoint))
                {
                    handled = true;
                    return new IntPtr(HTCLIENT);
                }

                // If not over interactive element, make it click-through
                handled = true;
                return new IntPtr(HTTRANSPARENT);
            }

            return IntPtr.Zero;
        }

        private bool IsMouseOverElement(FrameworkElement element, System.Windows.Point point)
        {
            if (element == null || element.Visibility != Visibility.Visible)
                return false;

            try
            {
                System.Windows.Point elementPoint = element.TransformToAncestor(this).Transform(new System.Windows.Point(0, 0));
                Rect elementBounds = new Rect(elementPoint.X, elementPoint.Y, element.ActualWidth, element.ActualHeight);
                return elementBounds.Contains(point);
            }
            catch
            {
                return false;
            }
        }

        private void UpdatePetPosition(double x, double y)
        {
            _petX = x;
            _petY = y;
            Canvas.SetLeft(PetImage, x);
            Canvas.SetTop(PetImage, y);
        }

        // Get current mouse position in window coordinates
        private void UpdateMousePosition()
        {
            try
            {
                // Get cursor position in screen coordinates
                if (GetCursorPos(out POINT screenPoint))
                {
                    // Convert to WPF window coordinates
                    System.Windows.Point screenPt = new System.Windows.Point(screenPoint.X, screenPoint.Y);
                    System.Windows.Point windowPt = PointFromScreen(screenPt);

                    _mousePosition = windowPt;
                    _hasMousePosition = true;
                }
            }
            catch (Exception ex)
            {
                App.Logger.LogError(ex, "Error updating mouse position");
            }
        }

        // Pet dragging event handlers
        private void PetImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Check if this is a petting interaction (not dragging)
            if (!_isDraggingPet && _timeSinceLastInteraction >= PET_COOLDOWN)
            {
                // Petting interaction - increase happiness
                _happiness = Math.Min(100, _happiness + 10);
                UpdateNeedsDisplay();
                _timeSinceLastInteraction = 0;

                // Play happy animation briefly
                if (!_isPerformingAction)
                {
                    _stateBeforeAction = _animationController.CurrentState;
                    _animationController.ChangeState(PetState.Playing);
                    _isPerformingAction = true;
                    _actionTimer = 0;
                }

                App.Logger.LogInformation("Pet petted! Happiness increased.");
            }

            _isDraggingPet = true;
            _petDragOffset = e.GetPosition(PetImage);
            PetImage.CaptureMouse();
            e.Handled = true;
        }

        private void PetImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingPet)
            {
                _isDraggingPet = false;
                PetImage.ReleaseMouseCapture();

                // Check if pet was dragged to the house area (bottom-right corner)
                CheckDragToHouse();

                e.Handled = true;
            }
        }

        private void CheckDragToHouse()
        {
            // Calculate the house panel area (bottom-right corner)
            double houseLeft = MainCanvas.ActualWidth - 270; // 250 width + 20 margin
            double houseTop = MainCanvas.ActualHeight - 552; // 492 height + 60 margin
            double houseRight = MainCanvas.ActualWidth - 20;
            double houseBottom = MainCanvas.ActualHeight - 60;

            // Get pet center position
            double petCenterX = _petX + (PetImage.ActualWidth / 2);
            double petCenterY = _petY + (PetImage.ActualHeight / 2);

            // Check if pet is in the house area
            if (petCenterX >= houseLeft && petCenterX <= houseRight &&
                petCenterY >= houseTop && petCenterY <= houseBottom)
            {
                // Pet was dragged to the house!
                App.Logger.LogInformation("Pet dragged to house area!");

                // Show the house panel if not already visible
                if (HousePanel.Visibility == Visibility.Collapsed)
                {
                    ShowHousePanel();
                }

                // If house panel is visible, transition pet into room
                if (HousePanel.Visibility == Visibility.Visible)
                {
                    TransitionPetIntoRoom();
                }
                else
                {
                    // Increase happiness for bringing pet home
                    _happiness = Math.Min(100, _happiness + 5);
                    UpdateNeedsDisplay();
                }
            }
        }

        private void TransitionPetIntoRoom()
        {
            App.Logger.LogInformation("Transitioning pet into room");

            // Hide pet from desktop
            PetImage.Visibility = Visibility.Collapsed;

            // Set pet state to InRoom
            _isPetInRoom = true;
            _animationController.ChangeState(PetState.InRoom);

            // Stop any movement
            _petVelocityX = 0;
            _petVelocityY = 0;
            _isChasing = false;
            _isAttacking = false;
            _isPerformingAction = false;

            // Show pet in room at bed position (bed is at 12, 117 with scale 0.5)
            // Pet should be positioned on the bed (28px up from bed decoration)
            Canvas.SetLeft(PetInRoomImage, 12);
            Canvas.SetTop(PetInRoomImage, 90);

            // Scale down the pet in the room to 0.8x
            PetInRoomImage.RenderTransform = new ScaleTransform(0.8, 0.8);
            PetInRoomImage.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);

            PetInRoomCanvas.Visibility = Visibility.Visible;

            // Reset exit timer
            _exitRoomTimer = 0;
            _exitRoomInterval = _random.Next(30, 60); // Random 30-60 seconds

            // Increase happiness for being in room
            _happiness = Math.Min(100, _happiness + 5);
            UpdateNeedsDisplay();
        }

        private void TransitionPetOutOfRoom()
        {
            App.Logger.LogInformation("Transitioning pet out of room");

            // Hide pet from room
            PetInRoomCanvas.Visibility = Visibility.Collapsed;

            // Set pet state back to normal
            _isPetInRoom = false;

            // Show pet on desktop near the house button
            PetImage.Visibility = Visibility.Visible;
            _petX = MainCanvas.ActualWidth - 300;
            _petY = MainCanvas.ActualHeight - 200;
            UpdatePetPosition(_petX, _petY);

            // Set to idle state
            _animationController.ChangeState(PetState.Idle);
        }

        private void UpdateLockIcon()
        {
            try
            {
                var lockImage = new BitmapImage();
                lockImage.BeginInit();
                lockImage.UriSource = new Uri(_isRoomLocked ?
                    "Resources/elements/control/locked.png" :
                    "Resources/elements/control/unlocked.png", UriKind.Relative);
                lockImage.CacheOption = BitmapCacheOption.OnLoad;
                lockImage.EndInit();
                LockRoomIcon.Source = lockImage;
            }
            catch (Exception ex)
            {
                App.Logger.LogError(ex, "Failed to load lock icon");
            }
        }

        private void PetImage_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDraggingPet && e.LeftButton == MouseButtonState.Pressed)
            {
                System.Windows.Point currentPosition = e.GetPosition(MainCanvas);
                double newX = currentPosition.X - _petDragOffset.X;
                double newY = currentPosition.Y - _petDragOffset.Y;

                // Keep pet within window bounds
                newX = Math.Max(0, Math.Min(newX, MainCanvas.ActualWidth - PetImage.Width));
                newY = Math.Max(0, Math.Min(newY, MainCanvas.ActualHeight - PetImage.Height));

                UpdatePetPosition(newX, newY);
                e.Handled = true;
            }
        }

        // House panel toggle
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

        private void CloseHouseButton_Click(object sender, RoutedEventArgs e)
        {
            HideHousePanel();
        }

        private void ShowHousePanel()
        {
            HousePanel.Visibility = Visibility.Visible;

            // Animate the panel sliding in
            var slideIn = new ThicknessAnimation
            {
                From = new Thickness(0, 0, -250, 60),
                To = new Thickness(0, 0, 20, 60),
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            HousePanel.BeginAnimation(MarginProperty, slideIn);

            // Hide the toggle button
            ToggleHouseButton.Visibility = Visibility.Collapsed;
        }

        private void HideHousePanel()
        {
            // Hide message bubble when house closes
            FoodBowlMessageCanvas.Visibility = Visibility.Collapsed;

            // Animate the panel sliding out
            var slideOut = new ThicknessAnimation
            {
                From = new Thickness(0, 0, 20, 60),
                To = new Thickness(0, 0, -250, 60),
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            slideOut.Completed += (s, e) => HousePanel.Visibility = Visibility.Collapsed;
            HousePanel.BeginAnimation(MarginProperty, slideOut);

            // Show the toggle button
            ToggleHouseButton.Visibility = Visibility.Visible;
        }

        // Action button handlers
        private void FeedButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isPerformingAction) return; // Don't interrupt ongoing actions

            _hunger = Math.Min(100, _hunger + 25);
            UpdateNeedsDisplay();

            // Stop any movement
            _petVelocityX = 0;
            _petVelocityY = 0;

            // Play eating animation
            _stateBeforeAction = _animationController.CurrentState;
            _animationController.ChangeState(PetState.Eating);
            _isPerformingAction = true;
            _actionTimer = 0;

            App.Logger.LogInformation("Fed the pet - hunger restored!");
        }

        private void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isPerformingAction) return; // Don't interrupt ongoing actions

            _cleanliness = Math.Min(100, _cleanliness + 25);
            UpdateNeedsDisplay();

            // Stop any movement
            _petVelocityX = 0;
            _petVelocityY = 0;

            // Play happy animation
            _stateBeforeAction = _animationController.CurrentState;
            _animationController.ChangeState(PetState.Playing);
            _isPerformingAction = true;
            _actionTimer = 0;

            App.Logger.LogInformation("Cleaned the pet - cleanliness restored!");
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isPerformingAction) return; // Don't interrupt ongoing actions

            _happiness = Math.Min(100, _happiness + 25);
            UpdateNeedsDisplay();

            // Stop any movement
            _petVelocityX = 0;
            _petVelocityY = 0;

            // Play excited animation
            _stateBeforeAction = _animationController.CurrentState;
            _animationController.ChangeState(PetState.Playing);
            _isPerformingAction = true;
            _actionTimer = 0;

            App.Logger.LogInformation("Played with the pet - happiness increased!");
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show("Are you sure you want to exit?", "Amicus", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                ExitApplication();
            }
        }

        // Food bowl event handlers
        private void FoodBowl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Show message bubble
            FoodBowlMessageCanvas.Visibility = Visibility.Visible;
            e.Handled = true;
            App.Logger.LogInformation("Food bowl clicked - showing message bubble");
        }

        private void FoodBowlYesButton_Click(object sender, RoutedEventArgs e)
        {
            // Hide message bubble
            FoodBowlMessageCanvas.Visibility = Visibility.Collapsed;

            // Check if bowl is already full
            if (_isFoodBowlFull)
            {
                App.Logger.LogInformation("Food bowl is already full");
                return;
            }

            // Fill the bowl
            _isFoodBowlFull = true;
            RenderDecorations(); // Re-render to show full bowl

            App.Logger.LogInformation("Food bowl filled");
        }

        private void FoodBowlNoButton_Click(object sender, RoutedEventArgs e)
        {
            // Just hide the message bubble
            FoodBowlMessageCanvas.Visibility = Visibility.Collapsed;
            App.Logger.LogInformation("Food bowl fill cancelled");
        }

        private void LockRoomButton_Click(object sender, RoutedEventArgs e)
        {
            _isRoomLocked = !_isRoomLocked;
            UpdateLockIcon();
            App.Logger.LogInformation("Room {Status}", _isRoomLocked ? "locked" : "unlocked");
        }

        // Pet in room dragging event handlers
        private void PetInRoomImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingPetFromRoom = true;
            // Store where in the image we clicked
            _petInRoomDragOffset = e.GetPosition(PetInRoomImage);

            // Reset scale to normal size when grabbed
            PetInRoomImage.RenderTransform = Transform.Identity;

            PetInRoomImage.CaptureMouse();
            e.Handled = true;
        }

        private void PetInRoomImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingPetFromRoom)
            {
                _isDraggingPetFromRoom = false;
                PetInRoomImage.ReleaseMouseCapture();

                // Check if pet was dragged out of the room
                CheckDragOutOfRoom();

                e.Handled = true;
            }
        }

        private void PetInRoomImage_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDraggingPetFromRoom && e.LeftButton == MouseButtonState.Pressed)
            {
                // Get mouse position relative to room canvas
                System.Windows.Point currentPosition = e.GetPosition(PetInRoomCanvas);

                // Calculate new position (mouse position - offset)
                double newX = currentPosition.X - _petInRoomDragOffset.X;
                double newY = currentPosition.Y - _petInRoomDragOffset.Y;

                // Update position (can go outside room bounds)
                Canvas.SetLeft(PetInRoomImage, newX);
                Canvas.SetTop(PetInRoomImage, newY);
                e.Handled = true;
            }
        }

        private void CheckDragOutOfRoom()
        {
            // Get pet position in room canvas
            double petXInRoom = Canvas.GetLeft(PetInRoomImage);
            double petYInRoom = Canvas.GetTop(PetInRoomImage);

            // Get room canvas position in main canvas
            System.Windows.Point roomPosition = PetInRoomCanvas.TransformToAncestor(MainCanvas).Transform(new System.Windows.Point(0, 0));

            // Calculate pet's absolute position on screen
            double absolutePetX = roomPosition.X + petXInRoom;
            double absolutePetY = roomPosition.Y + petYInRoom;

            // Check if pet is outside the room bounds (with some margin)
            bool isOutsideRoom = petXInRoom < -20 || petXInRoom > PetInRoomCanvas.Width + 20 ||
                                 petYInRoom < -20 || petYInRoom > PetInRoomCanvas.Height + 20;

            if (isOutsideRoom)
            {
                App.Logger.LogInformation("Pet dragged outside room - exiting at position ({X}, {Y})", absolutePetX, absolutePetY);

                // Transition pet out and place at the dragged position
                PetInRoomCanvas.Visibility = Visibility.Collapsed;
                _isPetInRoom = false;

                // Show pet on desktop at the dragged position
                PetImage.Visibility = Visibility.Visible;
                _petX = absolutePetX;
                _petY = absolutePetY;

                // Keep pet within screen bounds
                _petX = Math.Max(0, Math.Min(_petX, MainCanvas.ActualWidth - PetImage.ActualWidth));
                _petY = Math.Max(0, Math.Min(_petY, MainCanvas.ActualHeight - PetImage.ActualHeight));

                UpdatePetPosition(_petX, _petY);

                // Set to idle state
                _animationController.ChangeState(PetState.Idle);
            }
            else
            {
                // Snap back to bed position if not dragged outside
                Canvas.SetLeft(PetInRoomImage, 12);
                Canvas.SetTop(PetInRoomImage, 84);

                // Re-apply scale when snapping back
                PetInRoomImage.RenderTransform = new ScaleTransform(0.8, 0.8);
                PetInRoomImage.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            }
        }

        private void UpdateNeedsDisplay()
        {
            // Update needs indicators above the room
            HungerBar.Value = _hunger;
            CleanlinessBar.Value = _cleanliness;
            HappinessBar.Value = _happiness;
        }

        private void UpdateSpriteDirection()
        {
            // Flip sprite horizontally when facing left
            if (_animationController.CurrentDirection == PetDirection.Left)
            {
                PetImage.RenderTransform = new ScaleTransform(-1, 1, PetImage.ActualWidth / 2, PetImage.ActualHeight / 2);
            }
            else
            {
                PetImage.RenderTransform = Transform.Identity;
            }
        }

        private void GameTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                // Calculate delta time
                DateTime currentTime = DateTime.Now;
                double deltaTime = (currentTime - _lastUpdateTime).TotalSeconds;
                _lastUpdateTime = currentTime;

            // Update mouse position every frame
            UpdateMousePosition();

            // Update interaction cooldown timer
            _timeSinceLastInteraction += deltaTime;

            // Handle pet in room logic
            if (_isPetInRoom)
            {
                // Update exit timer if room is unlocked
                if (!_isRoomLocked && !_isDraggingPetFromRoom)
                {
                    _exitRoomTimer += deltaTime;
                    if (_exitRoomTimer >= _exitRoomInterval)
                    {
                        // Random chance to exit (40% chance)
                        if (_random.NextDouble() < 0.4)
                        {
                            App.Logger.LogInformation("Pet decided to exit room randomly");
                            TransitionPetOutOfRoom();
                        }
                        else
                        {
                            // Reset timer for next check
                            _exitRoomTimer = 0;
                            _exitRoomInterval = _random.Next(30, 60);
                        }
                    }
                }

                // Update animation for pet in room
                _animationController.Update(deltaTime);
                var roomFrame = _animationController.GetCurrentFrame();
                if (roomFrame != null)
                {
                    PetInRoomImage.Source = roomFrame;
                }

                // Skip normal pet behavior and needs degradation when in room
                UpdateNeedsDisplay();
                return;
            }

            // Mouse proximity detection and chase trigger
            if (_hasMousePosition && !_isDraggingPet && !_isPerformingAction)
            {
                // Calculate distance to mouse cursor
                double petCenterX = _petX + (PetImage.ActualWidth / 2);
                double petCenterY = _petY + (PetImage.ActualHeight / 2);
                double deltaX = _mousePosition.X - petCenterX;
                double deltaY = _mousePosition.Y - petCenterY;
                double distanceToMouse = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                // Handle proximity timer and chase triggering
                if (distanceToMouse < DETECTION_RADIUS && !_isChasing && !_chaseCooldownActive)
                {
                    // Mouse is within detection radius - increment proximity timer
                    _proximityTimer += deltaTime;

                    // Trigger chase after 2 seconds of proximity (42% chance)
                    if (_proximityTimer >= PROXIMITY_THRESHOLD)
                    {
                        // Random chance to start chasing
                        if (_random.NextDouble() < CHASE_CHANCE)
                        {
                            // Start chasing!
                            _isChasing = true;
                            _chaseTimer = 0;
                            _chaseDuration = CHASE_MIN_DURATION + (_random.NextDouble() * (CHASE_MAX_DURATION - CHASE_MIN_DURATION));
                            _animationController.ChangeState(PetState.Chasing);

                            App.Logger.LogInformation("Chase started! Duration: {Duration:F1}s", _chaseDuration);
                        }
                        else
                        {
                            // Cat decided not to chase - reset timer to try again
                            App.Logger.LogDebug("Cat ignored the mouse (no chase triggered)");
                            _proximityTimer = 0;
                        }
                    }
                }
                else if (!_isChasing)
                {
                    // Mouse is outside detection radius or cooldown is active - reset proximity timer
                    if (_proximityTimer > 0)
                    {
                        if (_chaseCooldownActive)
                        {
                            App.Logger.LogDebug("Chase cooldown active, ignoring proximity");
                        }
                        else
                        {
                            App.Logger.LogDebug("Mouse left detection radius, resetting proximity timer");
                        }
                    }
                    _proximityTimer = 0;
                }
            }

            // Handle chase cooldown timer
            if (_chaseCooldownActive)
            {
                _chaseCooldownTimer += deltaTime;

                // Check if cooldown has expired
                if (_chaseCooldownTimer >= _chaseCooldownDuration)
                {
                    _chaseCooldownActive = false;
                    _chaseCooldownTimer = 0;
                    App.Logger.LogInformation("Chase cooldown expired. Cat can chase again.");
                }
            }

            // Handle action animations (eating, playing, etc.)
            if (_isPerformingAction)
            {
                _actionTimer += deltaTime;
                if (_actionTimer >= ACTION_DURATION)
                {
                    _isPerformingAction = false;
                    _actionTimer = 0;

                    // Return to idle or random state after action
                    var randomValue = _random.NextDouble();
                    if (randomValue < 0.5)
                    {
                        _animationController.ChangeState(PetState.Idle);
                        _petVelocityX = 0;
                        _petVelocityY = 0;
                        App.Logger.LogDebug("Action completed → Idle (velocity cleared)");
                    }
                    else if (randomValue < 0.75)
                    {
                        _animationController.ChangeState(PetState.Playing);
                        _petVelocityX = 0;
                        _petVelocityY = 0;
                        App.Logger.LogDebug("Action completed → Playing (velocity cleared)");
                    }
                    else
                    {
                        _animationController.ChangeState(PetState.Walking);
                        // Set random velocity for walking
                        _petVelocityX = (_random.NextDouble() - 0.5) * 2 * PET_SPEED;
                        _petVelocityY = (_random.NextDouble() - 0.5) * 2 * PET_SPEED;

                        // Set facing direction
                        if (_petVelocityX > 0)
                            _animationController.ChangeDirection(PetDirection.Right);
                        else if (_petVelocityX < 0)
                            _animationController.ChangeDirection(PetDirection.Left);

                        App.Logger.LogDebug("Action completed → Walking (velocity=({VX:F1}, {VY:F1}))", _petVelocityX, _petVelocityY);
                    }
                }
            }

            // Handle chase movement and duration
            if (_isChasing)
            {
                // Increment chase timer
                _chaseTimer += deltaTime;

                // Check if chase duration has been reached
                if (_chaseTimer >= _chaseDuration)
                {
                    // End chase
                    _isChasing = false;
                    _isAttacking = false;
                    _chaseTimer = 0;
                    _attackTimer = 0;
                    _proximityTimer = 0;
                    _animationController.ChangeState(PetState.Idle);
                    _petVelocityX = 0;
                    _petVelocityY = 0;

                    // Start chase cooldown
                    _chaseCooldownActive = true;
                    _chaseCooldownTimer = 0;
                    _chaseCooldownDuration = CHASE_COOLDOWN_MIN + (_random.NextDouble() * (CHASE_COOLDOWN_MAX - CHASE_COOLDOWN_MIN));

                    App.Logger.LogInformation("Chase ended after {Duration:F1}s. Cooldown started for {Cooldown:F1}s", _chaseDuration, _chaseCooldownDuration);
                }
                else if (_hasMousePosition)
                {
                    // Calculate direction to mouse
                    double petCenterX = _petX + (PetImage.ActualWidth / 2);
                    double petCenterY = _petY + (PetImage.ActualHeight / 2);
                    double deltaX = _mousePosition.X - petCenterX;
                    double deltaY = _mousePosition.Y - petCenterY;
                    double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                    // Handle attack behavior
                    if (_isAttacking)
                    {
                        // Increment attack timer
                        _attackTimer += deltaTime;

                        // Stop movement during attack
                        _petVelocityX = 0;
                        _petVelocityY = 0;

                        // Check if attack duration has been reached
                        if (_attackTimer >= ATTACK_DURATION)
                        {
                            // End attack and return to chasing
                            _isAttacking = false;
                            _attackTimer = 0;
                            _animationController.ChangeState(PetState.Chasing);

                            App.Logger.LogInformation("Attack completed, resuming chase");
                        }
                    }
                    else
                    {
                        // Check if close enough to attack
                        if (distance < ATTACK_DISTANCE)
                        {
                            // Trigger attack!
                            _isAttacking = true;
                            _attackTimer = 0;
                            _animationController.ChangeState(PetState.Attacking);
                            _petVelocityX = 0;
                            _petVelocityY = 0;

                            App.Logger.LogInformation("Attack triggered! Distance: {Distance:F1}px", distance);
                        }
                        else
                        {
                            // Normal chase movement
                            if (distance > 0)
                            {
                                // Calculate chase speed based on distance to mouse
                                double chaseSpeed;
                                if (distance > CHASE_DISTANCE_THRESHOLD_FAR)
                                {
                                    chaseSpeed = CHASE_SPEED_FAR; // 250 px/s for distances > 1200
                                }
                                else if (distance > CHASE_DISTANCE_THRESHOLD_MEDIUM)
                                {
                                    chaseSpeed = CHASE_SPEED_MEDIUM; // 200 px/s for distances 700-1200
                                }
                                else
                                {
                                    chaseSpeed = CHASE_SPEED_BASE; // 150 px/s for distances <= 700
                                }

                                _petVelocityX = (deltaX / distance) * chaseSpeed;
                                _petVelocityY = (deltaY / distance) * chaseSpeed;

                                // Update facing direction based on movement
                                if (_petVelocityX > 0)
                                    _animationController.ChangeDirection(PetDirection.Right);
                                else if (_petVelocityX < 0)
                                    _animationController.ChangeDirection(PetDirection.Left);
                            }
                        }
                    }
                }
            }
            // Don't update wandering behavior if pet is being dragged, performing action, or chasing
            else if (!_isDraggingPet && !_isPerformingAction && !_isChasing)
            {
                // Update wandering behavior timers
                _wanderTimer += deltaTime;
                _idleTimer += deltaTime;

                // Random direction changes during walking
                if (_animationController.CurrentState == PetState.Walking && _wanderTimer >= _wanderInterval)
                {
                    _wanderTimer = 0;
                    _wanderInterval = _random.Next(2, 5); // Random interval 2-5 seconds

                    // Random chance to go idle (30% chance)
                    if (_random.NextDouble() < 0.3)
                    {
                        _animationController.ChangeState(PetState.Idle);
                        _petVelocityX = 0;
                        _petVelocityY = 0;

                        // Reset idle timer and set new interval when transitioning to idle
                        _idleTimer = 0;
                        _idleInterval = _random.Next(3, 8);

                        // App.Logger.LogDebug("Wandering: Walking → Idle (velocity cleared)");
                    }
                    else
                    {
                        // Change direction randomly (including diagonal movement)
                        _petVelocityX = (_random.NextDouble() - 0.5) * 2 * PET_SPEED; // Range: -PET_SPEED to +PET_SPEED
                        _petVelocityY = (_random.NextDouble() - 0.5) * 2 * PET_SPEED; // Range: -PET_SPEED to +PET_SPEED

                        // Set facing direction based on X velocity
                        if (_petVelocityX > 0)
                            _animationController.ChangeDirection(PetDirection.Right);
                        else if (_petVelocityX < 0)
                            _animationController.ChangeDirection(PetDirection.Left);

                        // App.Logger.LogDebug("Wandering: Changed direction (velocity=({VX:F1}, {VY:F1}))", _petVelocityX, _petVelocityY);
                    }
                }

                // Random transitions from idle to walking
                if (_animationController.CurrentState == PetState.Idle && _idleTimer >= _idleInterval)
                {
                    _idleTimer = 0;
                    _idleInterval = _random.Next(3, 8); // Random interval 3-8 seconds

                    // Random chance to start walking (50% chance)
                    if (_random.NextDouble() < 0.5)
                    {
                        _animationController.ChangeState(PetState.Walking);

                        // Random velocity in both X and Y
                        _petVelocityX = (_random.NextDouble() - 0.5) * 2 * PET_SPEED;
                        _petVelocityY = (_random.NextDouble() - 0.5) * 2 * PET_SPEED;

                        // Set facing direction based on X velocity
                        if (_petVelocityX > 0)
                            _animationController.ChangeDirection(PetDirection.Right);
                        else if (_petVelocityX < 0)
                            _animationController.ChangeDirection(PetDirection.Left);

                        // Reset wander timer and set new interval when transitioning to walking
                        _wanderTimer = 0;
                        _wanderInterval = _random.Next(2, 5);

                        // App.Logger.LogDebug("Wandering: Idle → Walking (velocity=({VX:F1}, {VY:F1}))", _petVelocityX, _petVelocityY);
                    }
                }
            }

            // Update pet position based on velocity (for both wandering and chasing)
            if (!_isDraggingPet)
            {
                double newX = _petX + (_petVelocityX * deltaTime);
                double newY = _petY + (_petVelocityY * deltaTime);

                // Boundary checking - keep pet on screen and bounce off edges
                const double MIN_BOUNCE_VELOCITY = 20; // Minimum velocity after bounce

                if (newX < 0)
                {
                    newX = 0;
                    _petVelocityX = Math.Max(Math.Abs(_petVelocityX), MIN_BOUNCE_VELOCITY);
                    _animationController.ChangeDirection(PetDirection.Right);
                    // App.Logger.LogDebug("Pet hit left edge");
                }
                else if (newX > MainCanvas.ActualWidth - PetImage.ActualWidth)
                {
                    newX = MainCanvas.ActualWidth - PetImage.ActualWidth;
                    _petVelocityX = -Math.Max(Math.Abs(_petVelocityX), MIN_BOUNCE_VELOCITY);
                    _animationController.ChangeDirection(PetDirection.Left);
                    // App.Logger.LogDebug("Pet hit right edge");
                }

                if (newY < 0)
                {
                    newY = 0;
                    _petVelocityY = Math.Max(Math.Abs(_petVelocityY), MIN_BOUNCE_VELOCITY);
                }
                else if (newY > MainCanvas.ActualHeight - PetImage.ActualHeight)
                {
                    newY = MainCanvas.ActualHeight - PetImage.ActualHeight;
                    _petVelocityY = -Math.Max(Math.Abs(_petVelocityY), MIN_BOUNCE_VELOCITY);
                }

                UpdatePetPosition(newX, newY);
            }

            // Update animation controller
            _animationController.Update(deltaTime);

            // Get current animation frame and update sprite
            var currentFrame = _animationController.GetCurrentFrame();
            if (currentFrame != null)
            {
                PetImage.Source = currentFrame;
            }

            // Update sprite direction (flip horizontally when facing left)
            UpdateSpriteDirection();

            // Update needs degradation
            _needsTimer += deltaTime;
            if (_needsTimer >= NEEDS_DECAY_INTERVAL)
            {
                _needsTimer = 0;

                // Decay needs over time
                _hunger = Math.Max(0, _hunger - 5);
                _cleanliness = Math.Max(0, _cleanliness - 3);
                _happiness = Math.Max(0, _happiness - 4);

                UpdateNeedsDisplay();
            }

            // Auto-eat from food bowl if hungry
            if (_isFoodBowlFull && _hunger < AUTO_EAT_THRESHOLD)
            {
                // Cat eats from the bowl
                _hunger = Math.Min(100, _hunger + FOOD_BOWL_FILL_AMOUNT);
                _isFoodBowlFull = false;

                // Re-render decorations to show empty bowl
                RenderDecorations();

                UpdateNeedsDisplay();
                App.Logger.LogInformation("Cat ate from food bowl! Hunger restored to {Hunger}", _hunger);
            }
            }
            catch (Exception ex)
            {
                _gameTimer.Stop();
                App.Logger.LogError(ex, "Error in game loop - timer stopped");
            }
        }
    }
}
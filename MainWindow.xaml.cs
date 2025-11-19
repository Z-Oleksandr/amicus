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

        // Needs degradation rates (per hour)
        private const double HUNGER_DECAY_PER_HOUR = 66.67; // 100→0 in 1.5 hours
        private const double CLEANLINESS_DECAY_OUTSIDE_PER_HOUR = 50.0; // 100→0 in 2 hours when outside
        private const double CLEANLINESS_DECAY_INSIDE_PER_HOUR = 0.0; // No decay when inside
        private const double HAPPINESS_DECAY_INSIDE_PER_HOUR = 10.0; // -10 per hour when inside
        private const double HAPPINESS_INCREASE_OUTSIDE_PER_HOUR = 5.0; // +5 per hour when outside

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

        // Left window (exit game trigger)
        private System.Windows.Controls.Image? _leftWindowImage = null;

        // Brush (interactive item)
        private System.Windows.Controls.Image? _brushImage = null;
        private bool _isBrushPickedUp = false;
        private System.Windows.Point _brushOriginalPosition = new System.Windows.Point(85, 150);
        private const double BRUSH_ORIGINAL_SCALE = 0.035; // Brush image is 819x643, scale to ~40x32
        private const double BRUSH_PICKUP_SCALE = 0.05; // Larger when picked up

        // Brushing interaction state
        private bool _isBrushingPet = false;
        private double _brushingStrokeCount = 0;
        private System.Windows.Point _lastBrushPosition = new System.Windows.Point(0, 0);
        private double _brushAwayTimer = 0; // Time since brush left contact area
        private const double BRUSH_CONTACT_DISTANCE = 80.0; // Pixels for brush-pet overlap
        private const double STROKE_DISTANCE_THRESHOLD = 25.0; // Min movement for a stroke (increased for slower rate)
        private const double CLEANLINESS_PER_STROKE = 2.0; // Reduced from 5.0 for balanced progression
        private const double HAPPINESS_PER_STROKE = 1.0; // Reduced from 3.0 for balanced progression
        private const double BRUSH_GRACE_PERIOD = 3.0; // Seconds to wait before ending brushing session

        // Scoop (interactive item)
        private System.Windows.Controls.Image? _scoopImage = null;
        private bool _isScoopPickedUp = false;
        private System.Windows.Point _scoopOriginalPosition = new System.Windows.Point(180, -5);
        private const double SCOOP_ORIGINAL_SCALE = 0.05; // Scale to fit room
        private const double SCOOP_PICKUP_SCALE = 0.08; // Slightly larger when picked up

        // Garbage (animated GIF item - click to animate, no pickup)
        private System.Windows.Controls.Image? _garbageImage = null;
        private List<BitmapFrame> _garbageFrames = new List<BitmapFrame>();
        private int _garbageCurrentFrame = 0;
        private bool _isGarbageAnimating = false;
        private double _garbageAnimationTimer = 0;
        private System.Windows.Point _garbageOriginalPosition = new System.Windows.Point(3, 5);
        private const double GARBAGE_SCALE = 0.37; // Original is 120x120
        private const double GARBAGE_FRAME_DELAY = 0.035; // 28 FPS for smooth animation

        // Poop system
        private class PoopInstance
        {
            public System.Windows.Controls.Image Image { get; set; } = null!;
            public double X { get; set; }
            public double Y { get; set; }
            public DateTime SpawnTime { get; set; }
        }

        private List<PoopInstance> _poopInstances = new List<PoopInstance>();
        private BitmapImage? _poopImage = null;
        private double _poopSpawnTimer = 0;
#if DEBUG
        private double _poopSpawnInterval = 60.0; // 1 minute in debug mode
#else
        private double _poopSpawnInterval = 3600.0; // 1 hour in production
#endif

        // Scoop with poop state
        private bool _isScoopHoldingPoop = false;
        private BitmapImage? _scoopNormalImage = null;
        private BitmapImage? _scoopWithPoopImage = null;

        // Poop interaction constants
        private const double POOP_PICKUP_DISTANCE = 40.0;
        private const double GARBAGE_DISPOSAL_DISTANCE = 50.0;
        private const double CLEANLINESS_LOSS_PER_POOP = 10.0;
        private const double POOP_SCALE = 0.06; // Scale for poop sprite

        // Poop restoration state (saved from LoadGameState, applied after images load)
        private List<Amicus.Data.PoopPositionData>? _savedPoopPositions = null;
        private bool _shouldSpawnTimeAwayPoop = false;

        // Walk to house to eat state
        private bool _isWalkingToHouse = false;
        private bool _shouldEatAfterEntering = false;
        private double _walkToHouseTimer = 0;
        private const double WALK_TO_HOUSE_TIMEOUT = 30.0; // Give up after 30 seconds
        private const double WALK_TO_HOUSE_SPEED = 100.0; // Faster walking when hungry

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
            // Save game state before closing
            SaveGameState();

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

                // Load saved game state first (before setting defaults)
                LoadGameState();

                // Set initial pet position (will be overridden if save data exists)
                UpdatePetPosition(_petX, _petY);

                // Load the default room
                LoadRoom();

                // Restore poops now that images are loaded
                ApplyPoopRestoration();

                // Initialize pet to idle state (or restore from save)
                if (_isPetInRoom)
                {
                    // Pet was in room when saved - restore room state
                    PetImage.Visibility = Visibility.Collapsed;
                    _animationController.ChangeState(PetState.InRoom);

                    // Position pet in room on bed
                    Canvas.SetLeft(PetInRoomImage, 12);
                    Canvas.SetTop(PetInRoomImage, 90);
                    PetInRoomImage.RenderTransform = new ScaleTransform(0.8, 0.8);
                    PetInRoomImage.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                    PetInRoomCanvas.Visibility = Visibility.Visible;

                    App.Logger.LogInformation("Pet restored in room from save");
                }
                else
                {
                    _animationController.ChangeState(PetState.Idle);
                    _animationController.ChangeDirection(PetDirection.Right);
                }

                // Update needs display
                UpdateNeedsDisplay();

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

                // Load message bubble background (food bowl)
                var messageBubbleImage = new BitmapImage();
                messageBubbleImage.BeginInit();
                messageBubbleImage.UriSource = new Uri("Resources/elements/control/message_bubble.png", UriKind.Relative);
                messageBubbleImage.CacheOption = BitmapCacheOption.OnLoad;
                messageBubbleImage.EndInit();
                MessageBubbleImage.Source = messageBubbleImage;

                // Load exit message bubble background (left window)
                var exitMessageBubbleImage = new BitmapImage();
                exitMessageBubbleImage.BeginInit();
                exitMessageBubbleImage.UriSource = new Uri("Resources/elements/control/message_bubble_left.png", UriKind.Relative);
                exitMessageBubbleImage.CacheOption = BitmapCacheOption.OnLoad;
                exitMessageBubbleImage.EndInit();
                ExitMessageBubbleImage.Source = exitMessageBubbleImage;

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

                // Load and place interactive brush (separate from decorations)
                LoadBrush();

                // Load and place interactive scoop (separate from decorations)
                LoadScoop();

                // Load and place animated garbage (click to animate, not pickupable)
                LoadGarbage();

                // Load poop images (for spawning and scoop interaction)
                LoadPoopImages();

                App.Logger.LogInformation("Room loaded successfully");
            }
            catch (Exception ex)
            {
                App.Logger.LogError(ex, "Failed to load room");
            }
        }

        private void LoadBrush()
        {
            try
            {
                App.Logger.LogInformation("Loading interactive brush...");

                // Load brush image
                var brushBitmap = new BitmapImage();
                brushBitmap.BeginInit();
                brushBitmap.UriSource = new Uri("Resources/Sprites/RetroCatsPaid/CatItems/CatToys/brush.png", UriKind.Relative);
                brushBitmap.CacheOption = BitmapCacheOption.OnLoad;
                brushBitmap.EndInit();
                brushBitmap.Freeze(); // Freeze for performance and thread safety

                // Create image element
                _brushImage = new System.Windows.Controls.Image
                {
                    Source = brushBitmap,
                    Stretch = Stretch.None,
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                RenderOptions.SetBitmapScalingMode(_brushImage, BitmapScalingMode.NearestNeighbor);

                // Apply scale transform
                var scaleTransform = new ScaleTransform(BRUSH_ORIGINAL_SCALE, BRUSH_ORIGINAL_SCALE);
                _brushImage.RenderTransform = scaleTransform;
                _brushImage.RenderTransformOrigin = new System.Windows.Point(0, 0);

                // Position on canvas
                Canvas.SetLeft(_brushImage, _brushOriginalPosition.X);
                Canvas.SetTop(_brushImage, _brushOriginalPosition.Y);

                // Add event handlers
                _brushImage.MouseLeftButtonDown += Brush_MouseLeftButtonDown;
                _brushImage.MouseLeftButtonUp += Brush_MouseLeftButtonUp;
                _brushImage.MouseMove += Brush_MouseMove;

                // Add to canvas (on top of all decorations)
                DecorationsCanvas.Children.Add(_brushImage);

                App.Logger.LogInformation("Brush loaded successfully");
            }
            catch (Exception ex)
            {
                App.Logger.LogError(ex, "Failed to load brush");
            }
        }

        private void LoadScoop()
        {
            try
            {
                App.Logger.LogInformation("Loading interactive scoop...");

                // Load scoop image
                var scoopBitmap = new BitmapImage();
                scoopBitmap.BeginInit();
                scoopBitmap.UriSource = new Uri("Resources/Sprites/RetroCatsPaid/CatItems/CatToys/scoop.png", UriKind.Relative);
                scoopBitmap.CacheOption = BitmapCacheOption.OnLoad;
                scoopBitmap.EndInit();
                scoopBitmap.Freeze(); // Freeze for performance and thread safety

                // Save reference to normal scoop image
                _scoopNormalImage = scoopBitmap;

                // Create image element
                _scoopImage = new System.Windows.Controls.Image
                {
                    Source = scoopBitmap,
                    Stretch = Stretch.None,
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                RenderOptions.SetBitmapScalingMode(_scoopImage, BitmapScalingMode.NearestNeighbor);

                // Apply scale transform
                var scaleTransform = new ScaleTransform(SCOOP_ORIGINAL_SCALE, SCOOP_ORIGINAL_SCALE);
                _scoopImage.RenderTransform = scaleTransform;
                _scoopImage.RenderTransformOrigin = new System.Windows.Point(0, 0);

                // Position on canvas
                Canvas.SetLeft(_scoopImage, _scoopOriginalPosition.X);
                Canvas.SetTop(_scoopImage, _scoopOriginalPosition.Y);

                // Add event handlers
                _scoopImage.MouseLeftButtonDown += Scoop_MouseLeftButtonDown;
                _scoopImage.MouseLeftButtonUp += Scoop_MouseLeftButtonUp;
                _scoopImage.MouseMove += Scoop_MouseMove;

                // Add to canvas (on top of all decorations)
                DecorationsCanvas.Children.Add(_scoopImage);

                App.Logger.LogInformation("Scoop loaded successfully");
            }
            catch (Exception ex)
            {
                App.Logger.LogError(ex, "Failed to load scoop");
            }
        }

        private void LoadGarbage()
        {
            try
            {
                App.Logger.LogInformation("Loading garbage GIF...");

                // Load GIF and extract frames using GifBitmapDecoder
                var decoder = new GifBitmapDecoder(
                    new Uri("Resources/Sprites/RetroCatsPaid/CatItems/CatToys/garbage.gif", UriKind.Relative),
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);

                // Extract all frames from the GIF
                foreach (var frame in decoder.Frames)
                {
                    var bitmapFrame = BitmapFrame.Create(frame);
                    bitmapFrame.Freeze(); // Freeze for thread safety and performance
                    _garbageFrames.Add(bitmapFrame);
                }

                App.Logger.LogInformation($"Extracted {_garbageFrames.Count} frames from garbage.gif");

                // Create image element with FIRST FRAME ONLY (idle state)
                _garbageImage = new System.Windows.Controls.Image
                {
                    Source = _garbageFrames.Count > 0 ? _garbageFrames[0] : null,
                    Stretch = Stretch.None,
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                RenderOptions.SetBitmapScalingMode(_garbageImage, BitmapScalingMode.NearestNeighbor);

                // Apply scale transform (no pickup scale - stays at same size)
                var scaleTransform = new ScaleTransform(GARBAGE_SCALE, GARBAGE_SCALE);
                _garbageImage.RenderTransform = scaleTransform;
                _garbageImage.RenderTransformOrigin = new System.Windows.Point(0, 0);

                // Position on canvas
                Canvas.SetLeft(_garbageImage, _garbageOriginalPosition.X);
                Canvas.SetTop(_garbageImage, _garbageOriginalPosition.Y);

                // No event handlers - garbage animates via proximity detection with scoop
                // (not pickupable, not clickable)

                // Add to canvas
                DecorationsCanvas.Children.Add(_garbageImage);

                App.Logger.LogInformation("Garbage loaded successfully at position ({X}, {Y})",
                    _garbageOriginalPosition.X, _garbageOriginalPosition.Y);
            }
            catch (Exception ex)
            {
                App.Logger.LogError(ex, "Failed to load garbage");
            }
        }

        private void LoadPoopImages()
        {
            try
            {
                App.Logger.LogInformation("Loading poop images...");

                // Load poop image
                _poopImage = new BitmapImage();
                _poopImage.BeginInit();
                _poopImage.UriSource = new Uri("Resources/Sprites/RetroCatsPaid/CatItems/CatToys/poop.png", UriKind.Relative);
                _poopImage.CacheOption = BitmapCacheOption.OnLoad;
                _poopImage.EndInit();
                _poopImage.Freeze(); // Freeze for performance and thread safety

                // Load scoop with poop image
                _scoopWithPoopImage = new BitmapImage();
                _scoopWithPoopImage.BeginInit();
                _scoopWithPoopImage.UriSource = new Uri("Resources/Sprites/RetroCatsPaid/CatItems/CatToys/poop_on_scoop.png", UriKind.Relative);
                _scoopWithPoopImage.CacheOption = BitmapCacheOption.OnLoad;
                _scoopWithPoopImage.EndInit();
                _scoopWithPoopImage.Freeze(); // Freeze for performance and thread safety

                App.Logger.LogInformation("Poop images loaded successfully");
            }
            catch (Exception ex)
            {
                App.Logger.LogError(ex, "Failed to load poop images");
            }
        }

        private void SpawnPoop()
        {
            try
            {
                if (_poopImage == null)
                {
                    App.Logger.LogWarning("Cannot spawn poop - poop image not loaded");
                    return;
                }

                App.Logger.LogInformation("Spawning poop at pet position ({X:F1}, {Y:F1})", _petX, _petY);

                // Create poop image
                var poopImageElement = new System.Windows.Controls.Image
                {
                    Source = _poopImage,
                    Stretch = Stretch.None
                };
                RenderOptions.SetBitmapScalingMode(poopImageElement, BitmapScalingMode.NearestNeighbor);

                // Apply scale transform
                var scaleTransform = new ScaleTransform(POOP_SCALE, POOP_SCALE);
                poopImageElement.RenderTransform = scaleTransform;
                poopImageElement.RenderTransformOrigin = new System.Windows.Point(0, 0);

                // Position at current pet location + offset based on facing direction
                // If facing left: 64px right, 64px down
                // If facing right: 64px left, 64px down
                double poopX = _animationController.CurrentDirection == PetDirection.Left
                    ? _petX + 70
                    : _petX - 6;
                double poopY = _petY + 64;
                Canvas.SetLeft(poopImageElement, poopX);
                Canvas.SetTop(poopImageElement, poopY);

                // Add to PetCanvas (below pet, above decorations)
                PetCanvas.Children.Add(poopImageElement);

                // Create poop instance and add to list
                var poopInstance = new PoopInstance
                {
                    Image = poopImageElement,
                    X = poopX,
                    Y = poopY,
                    SpawnTime = DateTime.Now
                };
                _poopInstances.Add(poopInstance);

                // Decrease cleanliness
                _cleanliness = Math.Max(0, _cleanliness - CLEANLINESS_LOSS_PER_POOP);
                App.Logger.LogInformation("Poop spawned! Cleanliness decreased to {Cleanliness:F1}", _cleanliness);
            }
            catch (Exception ex)
            {
                App.Logger.LogError(ex, "Failed to spawn poop");
            }
        }

        private void RestorePoop(double x, double y, DateTime spawnTime)
        {
            try
            {
                if (_poopImage == null)
                {
                    App.Logger.LogWarning("Cannot restore poop - image not loaded");
                    return;
                }

                // Create poop image element
                var poopImageElement = new System.Windows.Controls.Image
                {
                    Source = _poopImage,
                    Stretch = Stretch.None
                };
                RenderOptions.SetBitmapScalingMode(poopImageElement, BitmapScalingMode.NearestNeighbor);

                // Apply scale transform
                var scaleTransform = new ScaleTransform(POOP_SCALE, POOP_SCALE);
                poopImageElement.RenderTransform = scaleTransform;
                poopImageElement.RenderTransformOrigin = new System.Windows.Point(0, 0);

                // Position at saved coordinates
                Canvas.SetLeft(poopImageElement, x);
                Canvas.SetTop(poopImageElement, y);

                // Add to PetCanvas
                PetCanvas.Children.Add(poopImageElement);

                // Add to instances list
                _poopInstances.Add(new PoopInstance
                {
                    Image = poopImageElement,
                    X = x,
                    Y = y,
                    SpawnTime = spawnTime
                });

                App.Logger.LogInformation("Restored poop at ({X:F1}, {Y:F1})", x, y);
            }
            catch (Exception ex)
            {
                App.Logger.LogError(ex, "Failed to restore poop");
            }
        }

        private void SpawnRandomPoop()
        {
            try
            {
                if (_poopImage == null)
                {
                    App.Logger.LogWarning("Cannot spawn random poop - image not loaded");
                    return;
                }

                App.Logger.LogInformation("Spawning random time-away poop");

                // Create poop image element
                var poopImageElement = new System.Windows.Controls.Image
                {
                    Source = _poopImage,
                    Stretch = Stretch.None
                };
                RenderOptions.SetBitmapScalingMode(poopImageElement, BitmapScalingMode.NearestNeighbor);

                // Apply scale transform
                var scaleTransform = new ScaleTransform(POOP_SCALE, POOP_SCALE);
                poopImageElement.RenderTransform = scaleTransform;
                poopImageElement.RenderTransformOrigin = new System.Windows.Point(0, 0);

                // Random position on screen (with margins to avoid edges)
                double margin = 100;
                double poopX = margin + (_random.NextDouble() * (MainCanvas.ActualWidth - 2 * margin));
                double poopY = margin + (_random.NextDouble() * (MainCanvas.ActualHeight - 2 * margin));

                Canvas.SetLeft(poopImageElement, poopX);
                Canvas.SetTop(poopImageElement, poopY);

                // Add to PetCanvas
                PetCanvas.Children.Add(poopImageElement);

                // Create instance and add to list
                var poopInstance = new PoopInstance
                {
                    Image = poopImageElement,
                    X = poopX,
                    Y = poopY,
                    SpawnTime = DateTime.Now
                };
                _poopInstances.Add(poopInstance);

                // Decrease cleanliness by normal amount (-10)
                _cleanliness = Math.Max(0, _cleanliness - CLEANLINESS_LOSS_PER_POOP);

                App.Logger.LogInformation("Random poop spawned at ({X:F1}, {Y:F1}). Cleanliness: {C:F1}",
                    poopX, poopY, _cleanliness);
            }
            catch (Exception ex)
            {
                App.Logger.LogError(ex, "Failed to spawn random poop");
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

                    // Make left window clickable (exit game trigger)
                    if (placed.DecorationName == "window_left")
                    {
                        image.MouseLeftButtonDown += LeftWindow_MouseLeftButtonDown;
                        image.Cursor = System.Windows.Input.Cursors.Hand;
                        _leftWindowImage = image;
                    }

                    // Add to canvas
                    DecorationsCanvas.Children.Add(image);
                }

                // Add brush on top (if exists and not picked up)
                if (_brushImage != null && !_isBrushPickedUp)
                {
                    DecorationsCanvas.Children.Add(_brushImage);
                }

                // Add scoop on top (if exists and not picked up)
                if (_scoopImage != null && !_isScoopPickedUp)
                {
                    DecorationsCanvas.Children.Add(_scoopImage);
                }

                // Add garbage on top (if exists - garbage is never picked up, always stays in place)
                if (_garbageImage != null)
                {
                    DecorationsCanvas.Children.Add(_garbageImage);
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

            // Check if pet entered room to eat from food bowl
            if (_shouldEatAfterEntering && _isFoodBowlFull)
            {
                // Schedule eating to happen after a short delay (1 second)
                var eatTimer = new DispatcherTimer();
                eatTimer.Interval = TimeSpan.FromSeconds(1.0);
                eatTimer.Tick += (s, e) =>
                {
                    eatTimer.Stop();

                    // Only eat if bowl is still full (safety check)
                    if (_isFoodBowlFull)
                    {
                        // Cat eats from the bowl
                        _hunger = Math.Min(100, _hunger + FOOD_BOWL_FILL_AMOUNT);
                        _isFoodBowlFull = false;

                        // Re-render decorations to show empty bowl
                        RenderDecorations();

                        UpdateNeedsDisplay();
                        App.Logger.LogInformation("Cat ate from food bowl after entering room! Hunger restored to {Hunger}", _hunger);
                    }
                };
                eatTimer.Start();

                // Reset the flag
                _shouldEatAfterEntering = false;
            }
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
            // Hide message bubbles when house closes
            FoodBowlMessageCanvas.Visibility = Visibility.Collapsed;
            ExitGameMessageCanvas.Visibility = Visibility.Collapsed;

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

        private void LeftWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Show exit game message bubble
            ExitGameMessageCanvas.Visibility = Visibility.Visible;
            e.Handled = true;
            App.Logger.LogInformation("Left window clicked - showing exit confirmation");
        }

        private void ExitGameYesButton_Click(object sender, RoutedEventArgs e)
        {
            App.Logger.LogInformation("User confirmed exit via left window");

            // Save game state before exiting
            SaveGameState();

            // Exit the application
            _notifyIcon?.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        private void ExitGameNoButton_Click(object sender, RoutedEventArgs e)
        {
            // Just hide the message bubble
            ExitGameMessageCanvas.Visibility = Visibility.Collapsed;
            App.Logger.LogInformation("Exit cancelled");
        }

        #region Brush Interaction Handlers

        private void Brush_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_brushImage == null) return;

            App.Logger.LogInformation("Brush picked up");
            _isBrushPickedUp = true;
            e.Handled = true;

            // Get current position in DecorationsCanvas
            double brushX = Canvas.GetLeft(_brushImage);
            double brushY = Canvas.GetTop(_brushImage);

            // Transform position from DecorationsCanvas to PetCanvas
            // Since they're siblings, we need to use TransformToVisual instead of TransformToAncestor
            var decorationsPoint = new System.Windows.Point(brushX, brushY);
            var transform = DecorationsCanvas.TransformToVisual(PetCanvas);
            var petCanvasPoint = transform.Transform(decorationsPoint);

            // Remove from decorations canvas
            DecorationsCanvas.Children.Remove(_brushImage);

            // Scale up (note: this changes the visual size but not the position)
            _brushImage.RenderTransform = new ScaleTransform(BRUSH_PICKUP_SCALE, BRUSH_PICKUP_SCALE);

            // Set position in PetCanvas coordinates
            Canvas.SetLeft(_brushImage, petCanvasPoint.X);
            Canvas.SetTop(_brushImage, petCanvasPoint.Y);

            // Add to PetCanvas to render on top of everything
            PetCanvas.Children.Add(_brushImage);

            // Capture mouse for smooth dragging
            _brushImage.CaptureMouse();
        }

        private void Brush_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isBrushPickedUp || _brushImage == null) return;

            // Get mouse position relative to PetCanvas
            var mousePos = e.GetPosition(PetCanvas);

            // Center brush on cursor (accounting for scale) with slight offset
            var brushSource = _brushImage.Source as BitmapImage;
            if (brushSource != null)
            {
                double scaledWidth = brushSource.PixelWidth * BRUSH_PICKUP_SCALE;
                double scaledHeight = brushSource.PixelHeight * BRUSH_PICKUP_SCALE;

                // Offset: 5px to the right, 5px higher (subtract for up)
                Canvas.SetLeft(_brushImage, mousePos.X - scaledWidth / 2 + 10);
                Canvas.SetTop(_brushImage, mousePos.Y - scaledHeight / 2 - 15);
            }
        }

        private void Brush_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isBrushPickedUp || _brushImage == null) return;

            App.Logger.LogInformation("Brush released, returning to original position");

            // End brushing session if active
            if (_isBrushingPet)
            {
                EndBrushing();
            }

            // Release mouse capture
            _brushImage.ReleaseMouseCapture();

            // Remove from PetCanvas
            PetCanvas.Children.Remove(_brushImage);

            // Animate back to original position and scale
            AnimateBrushReturn();

            // Only set flag to false AFTER brush is safely back in DecorationsCanvas
            _isBrushPickedUp = false;
        }

        private void AnimateBrushReturn()
        {
            if (_brushImage == null) return;

            // Ensure brush is completely removed from PetCanvas (defensive programming)
            if (PetCanvas.Children.Contains(_brushImage))
            {
                PetCanvas.Children.Remove(_brushImage);
                App.Logger.LogDebug("Removed brush from PetCanvas in AnimateBrushReturn");
            }

            // Reset scale
            _brushImage.RenderTransform = new ScaleTransform(BRUSH_ORIGINAL_SCALE, BRUSH_ORIGINAL_SCALE);

            // Reset position
            Canvas.SetLeft(_brushImage, _brushOriginalPosition.X);
            Canvas.SetTop(_brushImage, _brushOriginalPosition.Y);

            // Only add back to decorations canvas if it's not already there
            if (!DecorationsCanvas.Children.Contains(_brushImage))
            {
                DecorationsCanvas.Children.Add(_brushImage);
                App.Logger.LogInformation("Brush returned to position ({X}, {Y})",
                    _brushOriginalPosition.X, _brushOriginalPosition.Y);
            }
            else
            {
                App.Logger.LogWarning("Brush was already in DecorationsCanvas");
            }
        }

        #endregion

        #region Scoop Interaction Handlers

        private void Scoop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_scoopImage == null) return;

            App.Logger.LogInformation("Scoop picked up");
            _isScoopPickedUp = true;
            e.Handled = true;

            // Get current position in DecorationsCanvas
            double scoopX = Canvas.GetLeft(_scoopImage);
            double scoopY = Canvas.GetTop(_scoopImage);

            // Transform position from DecorationsCanvas to PetCanvas
            // Since they're siblings, we need to use TransformToVisual instead of TransformToAncestor
            var decorationsPoint = new System.Windows.Point(scoopX, scoopY);
            var transform = DecorationsCanvas.TransformToVisual(PetCanvas);
            var petCanvasPoint = transform.Transform(decorationsPoint);

            // Remove from decorations canvas
            DecorationsCanvas.Children.Remove(_scoopImage);

            // Scale up (note: this changes the visual size but not the position)
            _scoopImage.RenderTransform = new ScaleTransform(SCOOP_PICKUP_SCALE, SCOOP_PICKUP_SCALE);

            // Set position in PetCanvas coordinates
            Canvas.SetLeft(_scoopImage, petCanvasPoint.X);
            Canvas.SetTop(_scoopImage, petCanvasPoint.Y);

            // Add to PetCanvas to render on top of everything
            PetCanvas.Children.Add(_scoopImage);

            // Capture mouse for smooth dragging
            _scoopImage.CaptureMouse();
        }

        private void Scoop_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isScoopPickedUp || _scoopImage == null) return;

            // Get mouse position relative to PetCanvas
            var mousePos = e.GetPosition(PetCanvas);

            // Center scoop on cursor (accounting for scale) with slight offset
            var scoopSource = _scoopImage.Source as BitmapImage;
            if (scoopSource != null)
            {
                double scaledWidth = scoopSource.PixelWidth * SCOOP_PICKUP_SCALE;
                double scaledHeight = scoopSource.PixelHeight * SCOOP_PICKUP_SCALE;

                // Offset: 28px to the right, 9px higher (add for cursor up)
                Canvas.SetLeft(_scoopImage, mousePos.X - scaledWidth / 2 - 28);
                Canvas.SetTop(_scoopImage, mousePos.Y - scaledHeight / 2 + 9);
            }
        }

        private void Scoop_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isScoopPickedUp || _scoopImage == null) return;

            App.Logger.LogInformation("Scoop released, returning to original position");

            // Release mouse capture
            _scoopImage.ReleaseMouseCapture();

            // Remove from PetCanvas
            PetCanvas.Children.Remove(_scoopImage);

            // Animate back to original position and scale
            AnimateScoopReturn();

            // Only set flag to false AFTER scoop is safely back in DecorationsCanvas
            _isScoopPickedUp = false;
        }

        private void AnimateScoopReturn()
        {
            if (_scoopImage == null) return;

            // Ensure scoop is completely removed from PetCanvas (defensive programming)
            if (PetCanvas.Children.Contains(_scoopImage))
            {
                PetCanvas.Children.Remove(_scoopImage);
                App.Logger.LogDebug("Removed scoop from PetCanvas in AnimateScoopReturn");
            }

            // Reset scale
            _scoopImage.RenderTransform = new ScaleTransform(SCOOP_ORIGINAL_SCALE, SCOOP_ORIGINAL_SCALE);

            // Reset position
            Canvas.SetLeft(_scoopImage, _scoopOriginalPosition.X);
            Canvas.SetTop(_scoopImage, _scoopOriginalPosition.Y);

            // Only add back to decorations canvas if it's not already there
            if (!DecorationsCanvas.Children.Contains(_scoopImage))
            {
                DecorationsCanvas.Children.Add(_scoopImage);
                App.Logger.LogInformation("Scoop returned to position ({X}, {Y})",
                    _scoopOriginalPosition.X, _scoopOriginalPosition.Y);
            }
            else
            {
                App.Logger.LogWarning("Scoop was already in DecorationsCanvas");
            }
        }

        #endregion

        #region Brushing Interaction Methods

        private void DetectBrushingInteraction(double deltaTime)
        {
            if (_brushImage == null || !_isBrushPickedUp) return;

            // Don't brush if pet is in room or being dragged
            if (_isPetInRoom || _isDraggingPet || _isDraggingPetFromRoom)
            {
                if (_isBrushingPet)
                {
                    EndBrushing();
                }
                return;
            }

            // Get brush position in PetCanvas coordinates
            double brushX = Canvas.GetLeft(_brushImage);
            double brushY = Canvas.GetTop(_brushImage);

            // Get pet center position
            double petCenterX = _petX + (PetImage.ActualWidth / 2);
            double petCenterY = _petY + (PetImage.ActualHeight / 2);

            // Calculate distance between brush and pet center
            double deltaX = brushX - petCenterX;
            double deltaY = brushY - petCenterY;
            double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            // Check if brush is in contact with pet
            if (distance < BRUSH_CONTACT_DISTANCE)
            {
                // Brush is in contact - reset away timer
                _brushAwayTimer = 0;

                if (!_isBrushingPet)
                {
                    // Start brushing session
                    StartBrushing();
                }
                else
                {
                    // Already brushing - detect strokes
                    DetectBrushStroke(brushX, brushY);
                }
            }
            else
            {
                // Brush moved away from pet
                if (_isBrushingPet)
                {
                    // Increment away timer
                    _brushAwayTimer += deltaTime;

                    // Only end brushing after grace period (3 seconds)
                    if (_brushAwayTimer >= BRUSH_GRACE_PERIOD)
                    {
                        EndBrushing();
                    }
                }
            }
        }

        private void StartBrushing()
        {
            _isBrushingPet = true;
            _brushingStrokeCount = 0;
            _brushAwayTimer = 0;
            _lastBrushPosition = new System.Windows.Point(Canvas.GetLeft(_brushImage), Canvas.GetTop(_brushImage));

            // Stop pet movement and enter Happy state (uses Happy.png animation)
            _petVelocityX = 0;
            _petVelocityY = 0;
            _animationController.ChangeState(PetState.Happy);

            // Cancel any ongoing actions or chasing
            _isChasing = false;
            _isAttacking = false;
            _isPerformingAction = false;

            App.Logger.LogInformation("Started brushing the pet");
        }

        private void EndBrushing()
        {
            if (!_isBrushingPet) return;

            _isBrushingPet = false;
            _brushAwayTimer = 0;

            // Return pet to idle state
            _animationController.ChangeState(PetState.Idle);

            App.Logger.LogInformation("Ended brushing session. Total strokes: {Count}", _brushingStrokeCount);
            _brushingStrokeCount = 0;
        }

        private void DetectBrushStroke(double currentBrushX, double currentBrushY)
        {
            // Calculate movement since last frame
            double deltaX = currentBrushX - _lastBrushPosition.X;
            double deltaY = currentBrushY - _lastBrushPosition.Y;
            double movementDistance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            // If brush moved enough, count it as a stroke
            if (movementDistance >= STROKE_DISTANCE_THRESHOLD)
            {
                ApplyBrushStroke();
                _lastBrushPosition = new System.Windows.Point(currentBrushX, currentBrushY);
            }
        }

        private void ApplyBrushStroke()
        {
            _brushingStrokeCount++;

            // Increase cleanliness (capped at 100)
            _cleanliness = Math.Min(100, _cleanliness + CLEANLINESS_PER_STROKE);

            // Increase happiness (capped at 100)
            _happiness = Math.Min(100, _happiness + HAPPINESS_PER_STROKE);

            // Update UI
            UpdateNeedsDisplay();

            App.Logger.LogDebug("Brush stroke #{Count}: Cleanliness = {Clean:F1}, Happiness = {Happy:F1}",
                _brushingStrokeCount, _cleanliness, _happiness);
        }

        private void DetectScoopPoopProximity()
        {
            if (_scoopImage == null || !_isScoopPickedUp || _isScoopHoldingPoop)
                return;

            // Get scoop position in PetCanvas coordinates
            double scoopX = Canvas.GetLeft(_scoopImage);
            double scoopY = Canvas.GetTop(_scoopImage);

            // Get scoop center (approximate based on scoop image size and scale)
            double scoopCenterX = scoopX + ((_scoopImage.ActualWidth * SCOOP_PICKUP_SCALE) / 2);
            double scoopCenterY = scoopY + ((_scoopImage.ActualHeight * SCOOP_PICKUP_SCALE) / 2);

            // Check proximity to each poop instance
            for (int i = _poopInstances.Count - 1; i >= 0; i--)
            {
                var poop = _poopInstances[i];

                // Get poop center
                double poopCenterX = poop.X + ((poop.Image.ActualWidth * POOP_SCALE) / 2);
                double poopCenterY = poop.Y + ((poop.Image.ActualHeight * POOP_SCALE) / 2);

                // Calculate distance
                double deltaX = scoopCenterX - poopCenterX;
                double deltaY = scoopCenterY - poopCenterY;
                double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                // Check if scoop is close enough to pick up poop
                if (distance < POOP_PICKUP_DISTANCE)
                {
                    // Pick up the poop!
                    App.Logger.LogInformation("Scoop picked up poop at ({X:F1}, {Y:F1})", poop.X, poop.Y);

                    // Remove poop image from canvas
                    PetCanvas.Children.Remove(poop.Image);

                    // Remove from instances list
                    _poopInstances.RemoveAt(i);

                    // Mark scoop as holding poop
                    _isScoopHoldingPoop = true;

                    // Change scoop image to scoop with poop
                    if (_scoopWithPoopImage != null)
                    {
                        _scoopImage.Source = _scoopWithPoopImage;
                    }

                    // Only pick up one poop at a time
                    break;
                }
            }
        }

        private void DetectScoopGarbageProximity()
        {
            if (_scoopImage == null || !_isScoopPickedUp || !_isScoopHoldingPoop || _garbageImage == null)
                return;

            // Get scoop position in PetCanvas coordinates
            double scoopX = Canvas.GetLeft(_scoopImage);
            double scoopY = Canvas.GetTop(_scoopImage);

            // Get scoop center
            double scoopCenterX = scoopX + ((_scoopImage.ActualWidth * SCOOP_PICKUP_SCALE) / 2);
            double scoopCenterY = scoopY + ((_scoopImage.ActualHeight * SCOOP_PICKUP_SCALE) / 2);

            // Get garbage position (it's in DecorationsCanvas, need to convert coordinates)
            // Garbage is positioned relative to HousePanel which is at bottom-right
            // We need to get its position in screen/main canvas coordinates
            var garbagePosition = _garbageImage.TransformToAncestor(MainCanvas).Transform(new System.Windows.Point(0, 0));

            // Get garbage center
            double garbageCenterX = garbagePosition.X + ((_garbageImage.ActualWidth * GARBAGE_SCALE) / 2);
            double garbageCenterY = garbagePosition.Y + ((_garbageImage.ActualHeight * GARBAGE_SCALE) / 2);

            // Calculate distance
            double deltaX = scoopCenterX - garbageCenterX;
            double deltaY = scoopCenterY - garbageCenterY;
            double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            // Check if scoop is close enough to dispose poop in garbage
            if (distance < GARBAGE_DISPOSAL_DISTANCE)
            {
                // Dispose of the poop!
                App.Logger.LogInformation("Poop disposed in garbage!");

                // Reset scoop state
                _isScoopHoldingPoop = false;

                // Change scoop image back to normal
                if (_scoopNormalImage != null)
                {
                    _scoopImage.Source = _scoopNormalImage;
                }

                // Trigger garbage animation
                _isGarbageAnimating = true;
                _garbageCurrentFrame = 0;
                _garbageAnimationTimer = 0;

                App.Logger.LogInformation("Garbage animation triggered by poop disposal");
            }
        }

        #endregion

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

            // Detect brushing interaction (if brush is picked up)
            if (_isBrushPickedUp && _brushImage != null)
            {
                DetectBrushingInteraction(deltaTime);
            }

            // Update garbage animation if playing
            if (_isGarbageAnimating && _garbageImage != null && _garbageFrames.Count > 0)
            {
                _garbageAnimationTimer += deltaTime;

                if (_garbageAnimationTimer >= GARBAGE_FRAME_DELAY)
                {
                    _garbageAnimationTimer -= GARBAGE_FRAME_DELAY;
                    _garbageCurrentFrame++;

                    if (_garbageCurrentFrame >= _garbageFrames.Count)
                    {
                        // Animation complete - reset to first frame and stop
                        _garbageCurrentFrame = 0;
                        _isGarbageAnimating = false;
                        App.Logger.LogInformation("Garbage animation completed");
                    }

                    // Update the image source to show current frame
                    _garbageImage.Source = _garbageFrames[_garbageCurrentFrame];
                }
            }

            // Detect scoop interactions (if scoop is picked up)
            if (_isScoopPickedUp && _scoopImage != null)
            {
                // Check for poop pickup (only if not holding poop)
                if (!_isScoopHoldingPoop)
                {
                    DetectScoopPoopProximity();
                }
                // Check for garbage disposal (only if holding poop)
                else
                {
                    DetectScoopGarbageProximity();
                }
            }

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
                if (distanceToMouse < DETECTION_RADIUS && !_isChasing && !_chaseCooldownActive && !_isBrushingPet)
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

            // Handle walk to house to eat behavior
            if (_isWalkingToHouse)
            {
                // Increment walk timer
                _walkToHouseTimer += deltaTime;

                // Check timeout
                if (_walkToHouseTimer >= WALK_TO_HOUSE_TIMEOUT)
                {
                    // Give up walking to house
                    _isWalkingToHouse = false;
                    _shouldEatAfterEntering = false;
                    _walkToHouseTimer = 0;
                    _animationController.ChangeState(PetState.Idle);
                    _petVelocityX = 0;
                    _petVelocityY = 0;
                    App.Logger.LogWarning("Walk to house timeout - giving up");
                }
                else
                {
                    // Calculate target position (center of house area)
                    double targetX = MainCanvas.ActualWidth - 145;
                    double targetY = MainCanvas.ActualHeight - 306;

                    // Calculate direction to house
                    double petCenterX = _petX + (PetImage.ActualWidth / 2);
                    double petCenterY = _petY + (PetImage.ActualHeight / 2);
                    double deltaX = targetX - petCenterX;
                    double deltaY = targetY - petCenterY;
                    double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                    // Check if we've reached the house area
                    if (distance < 50) // Close enough to house
                    {
                        // Reached the house! Stop walking
                        _isWalkingToHouse = false;
                        _walkToHouseTimer = 0;
                        _petVelocityX = 0;
                        _petVelocityY = 0;

                        App.Logger.LogInformation("Reached house area! Opening house panel and entering room.");

                        // Show house panel if not already visible
                        if (HousePanel.Visibility == Visibility.Collapsed)
                        {
                            ShowHousePanel();
                        }

                        // Transition pet into room
                        TransitionPetIntoRoom();
                    }
                    else
                    {
                        // Keep walking toward house
                        _animationController.ChangeState(PetState.Walking);

                        if (distance > 0)
                        {
                            _petVelocityX = (deltaX / distance) * WALK_TO_HOUSE_SPEED;
                            _petVelocityY = (deltaY / distance) * WALK_TO_HOUSE_SPEED;

                            // Update facing direction based on movement
                            if (_petVelocityX > 0)
                                _animationController.ChangeDirection(PetDirection.Right);
                            else if (_petVelocityX < 0)
                                _animationController.ChangeDirection(PetDirection.Left);
                        }
                    }
                }
            }
            // Don't update wandering behavior if pet is being dragged, performing action, chasing, or walking to house
            else if (!_isDraggingPet && !_isPerformingAction && !_isChasing && !_isWalkingToHouse)
            {
                // Update wandering behavior timers
                _wanderTimer += deltaTime;
                _idleTimer += deltaTime;

                // Poop spawning system (only when outside and in Idle/Walking states)
                if ((_animationController.CurrentState == PetState.Idle || _animationController.CurrentState == PetState.Walking))
                {
                    _poopSpawnTimer += deltaTime;

                    if (_poopSpawnTimer >= _poopSpawnInterval)
                    {
                        // 50% random chance to spawn poop
                        if (_random.NextDouble() < 0.5)
                        {
                            SpawnPoop();
                        }

                        // Reset timer for next poop spawn
                        _poopSpawnTimer = 0;

                        // Randomize next interval slightly (±10%)
                        double variance = (_random.NextDouble() - 0.5) * 0.2 * _poopSpawnInterval;
#if DEBUG
                        _poopSpawnInterval = 60.0 + variance;
#else
                        _poopSpawnInterval = 3600.0 + variance;
#endif
                    }
                }

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

            // Update needs degradation (continuous, based on deltaTime)
            // Hunger: Always decays at same rate (66.67/hour = 100→0 in 1.5h)
            double hungerDecay = (HUNGER_DECAY_PER_HOUR / 3600.0) * deltaTime;
            _hunger = Math.Max(0, _hunger - hungerDecay);

            // Cleanliness: Depends on location
            double cleanlinessDecay;
            if (_isPetInRoom)
            {
                // No decay when inside house
                cleanlinessDecay = (CLEANLINESS_DECAY_INSIDE_PER_HOUR / 3600.0) * deltaTime;
            }
            else
            {
                // 50/hour when outside (100→0 in 2h)
                cleanlinessDecay = (CLEANLINESS_DECAY_OUTSIDE_PER_HOUR / 3600.0) * deltaTime;
            }
            _cleanliness = Math.Max(0, _cleanliness - cleanlinessDecay);

            // Happiness: Depends on location
            double happinessChange;
            if (_isPetInRoom)
            {
                // -10/hour when inside (gets bored)
                happinessChange = -(HAPPINESS_DECAY_INSIDE_PER_HOUR / 3600.0) * deltaTime;
            }
            else
            {
                // +5/hour when outside (happy exploring)
                happinessChange = (HAPPINESS_INCREASE_OUTSIDE_PER_HOUR / 3600.0) * deltaTime;
            }
            _happiness = Math.Max(0, Math.Min(100, _happiness + happinessChange));

            UpdateNeedsDisplay();

            // Auto-eat from food bowl if hungry
            if (_isFoodBowlFull && _hunger < AUTO_EAT_THRESHOLD)
            {
                if (_isPetInRoom && !_shouldEatAfterEntering)
                {
                    // Cat is already in room, eat from the bowl
                    // (but not if scheduled eating is pending)
                    _hunger = Math.Min(100, _hunger + FOOD_BOWL_FILL_AMOUNT);
                    _isFoodBowlFull = false;

                    // Re-render decorations to show empty bowl
                    RenderDecorations();

                    UpdateNeedsDisplay();
                    App.Logger.LogInformation("Cat ate from food bowl! Hunger restored to {Hunger}", _hunger);
                }
                else if (!_isWalkingToHouse && !_isPerformingAction && !_isChasing && !_isAttacking)
                {
                    // Cat is not in room and not busy, start walking to house
                    _isWalkingToHouse = true;
                    _shouldEatAfterEntering = true;
                    _walkToHouseTimer = 0;
                    App.Logger.LogInformation("Cat is hungry and food bowl is full. Walking to house to eat.");
                }
            }
            }
            catch (Exception ex)
            {
                _gameTimer.Stop();
                App.Logger.LogError(ex, "Error in game loop - timer stopped");
            }
        }

        #region Persistence

        /// <summary>
        /// Saves the current game state to disk
        /// </summary>
        private void SaveGameState()
        {
            try
            {
                App.Logger.LogInformation("Saving game state...");

                var saveData = new Amicus.Data.SaveData
                {
                    PetState = new Amicus.Data.PetStateData
                    {
                        PositionX = _petX,
                        PositionY = _petY,
                        CurrentState = _animationController.CurrentState.ToString(),
                        IsInRoom = _isPetInRoom,
                        Hunger = _hunger,
                        Cleanliness = _cleanliness,
                        Happiness = _happiness
                    },
                    UserSettings = new Amicus.Data.UserSettingsData
                    {
                        HouseLocked = _isRoomLocked,
                        SoundEnabled = true, // Future feature
                        PetName = "Cat" // Future feature
                    },
                    RoomState = new Amicus.Data.RoomStateData
                    {
                        FoodBowlFull = _isFoodBowlFull,
                        PoopPositions = _poopInstances.Select(p => new Amicus.Data.PoopPositionData
                        {
                            X = p.X,
                            Y = p.Y,
                            SpawnTime = p.SpawnTime
                        }).ToList()
                    },
                    Session = new Amicus.Data.SessionData
                    {
                        LastExitTime = DateTime.UtcNow
                    }
                };

                bool success = Amicus.Data.SaveManager.SaveGame(saveData);
                if (success)
                {
                    App.Logger.LogInformation("Game state saved successfully");
                }
                else
                {
                    App.Logger.LogWarning("Failed to save game state");
                }
            }
            catch (Exception ex)
            {
                App.Logger.LogError(ex, "Error saving game state");
            }
        }

        /// <summary>
        /// Loads the saved game state from disk
        /// </summary>
        private void LoadGameState()
        {
            try
            {
                App.Logger.LogInformation("Loading game state...");

                var saveData = Amicus.Data.SaveManager.LoadGame();
                if (saveData == null)
                {
                    App.Logger.LogInformation("No save data found, using default values");
                    return;
                }

                // Apply time-away degradation first
                ApplyTimeAwayDegradation(saveData);

                // Restore pet position
                _petX = saveData.PetState.PositionX;
                _petY = saveData.PetState.PositionY;

                // Restore needs
                _hunger = Math.Max(0, Math.Min(100, saveData.PetState.Hunger));
                _cleanliness = Math.Max(0, Math.Min(100, saveData.PetState.Cleanliness));
                _happiness = Math.Max(0, Math.Min(100, saveData.PetState.Happiness));

                // Restore room state
                _isPetInRoom = saveData.PetState.IsInRoom;
                _isRoomLocked = saveData.UserSettings.HouseLocked;
                _isFoodBowlFull = saveData.RoomState.FoodBowlFull;

                // Save poop data for restoration after images load
                bool hadPoopsOnExit = saveData.RoomState.PoopPositions != null && saveData.RoomState.PoopPositions.Count > 0;

                if (hadPoopsOnExit && saveData.RoomState.PoopPositions != null)
                {
                    _savedPoopPositions = saveData.RoomState.PoopPositions;
                    App.Logger.LogInformation("Found {Count} poop(s) to restore after images load", saveData.RoomState.PoopPositions.Count);
                }

                // Check if game was off long enough to spawn time-away poop
                TimeSpan timeAway = DateTime.UtcNow - saveData.Session.LastExitTime;

#if DEBUG
                double timeAwayThresholdSeconds = 180.0; // 3 minutes in debug mode
#else
                double timeAwayThresholdSeconds = 10800.0; // 3 hours in production mode
#endif

                if (timeAway.TotalSeconds >= timeAwayThresholdSeconds)
                {
                    App.Logger.LogInformation("Game was off for {Seconds:F0} seconds (threshold: {Threshold}). Will spawn time-away poop after images load.",
                        timeAway.TotalSeconds, timeAwayThresholdSeconds);
                    _shouldSpawnTimeAwayPoop = true;
                }

                // Set cleanliness to 0 if poops existed on exit
                // (Time-away poop cleanliness decrease will be applied when spawned)
                if (hadPoopsOnExit)
                {
                    _cleanliness = 0;
                    App.Logger.LogInformation("Poops existed on exit - cleanliness set to 0");
                }

                App.Logger.LogInformation("Game state loaded successfully");
                App.Logger.LogInformation("Pet position: ({X}, {Y})", _petX, _petY);
                App.Logger.LogInformation("Needs - Hunger: {H}, Cleanliness: {C}, Happiness: {Hp}",
                    _hunger, _cleanliness, _happiness);
                App.Logger.LogInformation("Pet in room: {InRoom}, Locked: {Locked}, Bowl full: {Bowl}",
                    _isPetInRoom, _isRoomLocked, _isFoodBowlFull);
            }
            catch (Exception ex)
            {
                App.Logger.LogError(ex, "Error loading game state");
            }
        }

        /// <summary>
        /// Restores saved poops and spawns time-away poop if needed.
        /// Called after LoadRoom() ensures poop images are loaded.
        /// </summary>
        private void ApplyPoopRestoration()
        {
            try
            {
                // Restore saved poops first
                if (_savedPoopPositions != null && _savedPoopPositions.Count > 0)
                {
                    App.Logger.LogInformation("Restoring {Count} saved poop(s)...", _savedPoopPositions.Count);
                    foreach (var poopData in _savedPoopPositions)
                    {
                        RestorePoop(poopData.X, poopData.Y, poopData.SpawnTime);
                    }
                    App.Logger.LogInformation("Successfully restored {Count} poop(s)", _poopInstances.Count);
                    _savedPoopPositions = null; // Clear after restoration
                }

                // Spawn time-away poop if needed
                if (_shouldSpawnTimeAwayPoop)
                {
                    App.Logger.LogInformation("Spawning time-away poop...");
                    SpawnRandomPoop();
                    _shouldSpawnTimeAwayPoop = false; // Clear flag
                }
            }
            catch (Exception ex)
            {
                App.Logger.LogError(ex, "Error applying poop restoration");
            }
        }

        /// <summary>
        /// Applies needs degradation based on time away from the app
        /// </summary>
        private void ApplyTimeAwayDegradation(Amicus.Data.SaveData saveData)
        {
            try
            {
                DateTime lastExit = saveData.Session.LastExitTime;
                DateTime now = DateTime.UtcNow;
                TimeSpan timeAway = now - lastExit;

                double hoursAway = timeAway.TotalHours;
                App.Logger.LogInformation("Time away: {Hours:F2} hours ({Days:F2} days)",
                    hoursAway, timeAway.TotalDays);

                if (hoursAway <= 0)
                {
                    App.Logger.LogInformation("No time has passed, skipping degradation");
                    return;
                }

                // Degradation rates per hour (time-away)
                const double HUNGER_DECAY_AWAY_PER_HOUR = 66.67; // Same as active gameplay
                const double CLEANLINESS_DECAY_IN_HOUSE_PER_HOUR = 0.333; // 1 point per 3 hours
                const double CLEANLINESS_DECAY_OUTSIDE_HOUSE_PER_HOUR = 50.0; // Same as active
                const double HAPPINESS_DECAY_AWAY_PER_HOUR = 5.0; // Fixed rate when away

                // Calculate hunger degradation (always same rate)
                double hungerLoss = hoursAway * HUNGER_DECAY_AWAY_PER_HOUR;

                // Calculate cleanliness degradation (depends on location)
                double cleanlinessLoss;
                if (saveData.PetState.IsInRoom)
                {
                    // In house: 1 point per 3 hours
                    cleanlinessLoss = hoursAway * CLEANLINESS_DECAY_IN_HOUSE_PER_HOUR;
                }
                else
                {
                    // Outside: 50/hour
                    cleanlinessLoss = hoursAway * CLEANLINESS_DECAY_OUTSIDE_HOUSE_PER_HOUR;
                }

                // Calculate happiness degradation (always -5/hour when away)
                double happinessLoss = hoursAway * HAPPINESS_DECAY_AWAY_PER_HOUR;

                // Apply degradation (with minimum of 0)
                saveData.PetState.Hunger = Math.Max(0, saveData.PetState.Hunger - hungerLoss);
                saveData.PetState.Cleanliness = Math.Max(0, saveData.PetState.Cleanliness - cleanlinessLoss);
                saveData.PetState.Happiness = Math.Max(0, saveData.PetState.Happiness - happinessLoss);

                App.Logger.LogInformation("Applied degradation - Hunger: -{HL:F1}, Cleanliness: -{CL:F1}, Happiness: -{HpL:F1}",
                    hungerLoss, cleanlinessLoss, happinessLoss);
                App.Logger.LogInformation("Pet was {Location}", saveData.PetState.IsInRoom ? "in room" : "outside");
                App.Logger.LogInformation("Needs after degradation - H:{H:F1}, C:{C:F1}, Hp:{Hp:F1}",
                    saveData.PetState.Hunger, saveData.PetState.Cleanliness, saveData.PetState.Happiness);
            }
            catch (Exception ex)
            {
                App.Logger.LogError(ex, "Error applying time-away degradation");
            }
        }

        #endregion
    }
}
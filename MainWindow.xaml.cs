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
using WinForms = System.Windows.Forms;
using AMICUS.Animation;

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

        // Mouse cursor tracking and chasing
        private System.Windows.Point _mousePosition;
        private bool _hasMousePosition = false; // Track if we've received a valid mouse position
        private bool _isChasingMouse = false;
        private bool _wasMouseInRange = false; // Track if mouse was in range last check (to detect ENTERING)
        private double _chaseCheckTimer = 0;
        private const double CHASE_CHECK_INTERVAL = 2.0; // Check every 2 seconds if should chase
        private const double CHASE_TRIGGER_DISTANCE = 200.0; // Trigger chase if mouse within this distance (200px radius)
        private const double CHASE_SPEED = 120; // pixels per second when chasing
        private const double CHASE_DURATION = 15.0; // Chase duration in seconds
        private double _chaseTimeRemaining = 0;
        private const double ATTACK_DISTANCE = 20.0; // Distance to trigger attack animation (20px radius)
        private const double PROXIMITY_DURATION = 2.0; // Must stay within 20px for 2 seconds to attack
        private double _proximityTimer = 0; // Track time within attack distance

        // Direction change management to prevent glitching
        private double _directionChangeTimer = 0;
        private const double DIRECTION_CHANGE_COOLDOWN = 0.2; // Only allow direction change every 0.2 seconds

        // Petting interaction
        private double _timeSinceLastInteraction = 0;
        private const double PET_COOLDOWN = 2.0; // Cooldown between petting in seconds

        // Action animations
        private bool _isPerformingAction = false;
        private double _actionTimer = 0;
        private const double ACTION_DURATION = 3.0; // Duration of action animations in seconds
        private PetState _stateBeforeAction = PetState.Idle;

        // Attack animation
        private double _attackTimer = 0;
        private const double ATTACK_DURATION = 1.5; // Duration of attack animation in seconds

        // System tray icon
        private WinForms.NotifyIcon? _notifyIcon;

        public MainWindow()
        {
            InitializeComponent();
            SetupSystemTray();

            // Initialize animation system
            _animationController = new AnimationController();
            _random = new Random();

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

                // Update needs display
                UpdateNeedsDisplay();

                // Initialize pet to idle state
                _animationController.ChangeState(PetState.Idle);
                _animationController.ChangeDirection(PetDirection.Right);

                App.Logger.LogInformation("Pet initialized, starting game timer...");

                // Start the game timer
                _gameTimer.Start();

                // Set up click-through behavior after window is fully loaded
                this.SourceInitialized += MainWindow_SourceInitialized;

                // Set up mouse tracking for the entire window
                this.MouseMove += Window_MouseMove;
            }
            catch (Exception ex)
            {
                App.Logger.LogError(ex, "Error during window load");
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

        // Mouse tracking for the entire window
        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _mousePosition = e.GetPosition(MainCanvas);
            _hasMousePosition = true;
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

                // Increase happiness for bringing pet home
                _happiness = Math.Min(100, _happiness + 5);
                UpdateNeedsDisplay();
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

            // Stop any chasing behavior
            _isChasingMouse = false;
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

            // Stop any chasing behavior
            _isChasingMouse = false;
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

            // Stop any chasing behavior
            _isChasingMouse = false;
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

        private void UpdateNeedsDisplay()
        {
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

            // Update interaction cooldown timer
            _timeSinceLastInteraction += deltaTime;

            // Update direction change cooldown timer
            _directionChangeTimer += deltaTime;

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

            // Don't update wandering/chasing behavior if pet is being dragged or performing action
            if (!_isDraggingPet && !_isPerformingAction)
            {
                // Only track mouse if we have a valid position (mouse has moved at least once)
                if (_hasMousePosition)
                {
                    // Calculate distance to mouse cursor
                    double petCenterX = _petX + (PetImage.ActualWidth / 2);
                    double petCenterY = _petY + (PetImage.ActualHeight / 2);
                    double deltaX = _mousePosition.X - petCenterX;
                    double deltaY = _mousePosition.Y - petCenterY;
                    double distanceToMouse = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                    // Update chase check timer
                    _chaseCheckTimer += deltaTime;

                    // Check if we should start chasing (only when mouse ENTERS the radius)
                    if (!_isChasingMouse && _chaseCheckTimer >= CHASE_CHECK_INTERVAL)
                    {
                        _chaseCheckTimer = 0;

                        bool isMouseInRange = distanceToMouse < CHASE_TRIGGER_DISTANCE;

                        // Only start chasing if mouse ENTERED the radius (was outside, now inside)
                        if (isMouseInRange && !_wasMouseInRange)
                        {
                            _isChasingMouse = true;
                            _chaseTimeRemaining = CHASE_DURATION;
                            _proximityTimer = 0; // Reset proximity timer
                            _directionChangeTimer = DIRECTION_CHANGE_COOLDOWN; // Allow immediate direction change when starting
                            _animationController.ChangeState(PetState.Chasing);

                            // Set initial direction toward mouse
                            if (deltaX > 0)
                                _animationController.ChangeDirection(PetDirection.Right);
                            else if (deltaX < 0)
                                _animationController.ChangeDirection(PetDirection.Left);

                            App.Logger.LogInformation("Started chasing mouse! Mouse ENTERED {Radius}px radius. Duration: {Duration}s, Distance: {Distance}px",
                                CHASE_TRIGGER_DISTANCE, _chaseTimeRemaining, distanceToMouse);
                        }

                        // Update the range tracking for next check
                        _wasMouseInRange = isMouseInRange;
                    }

                // Handle mouse chasing behavior
                if (_isChasingMouse)
                {
                    _chaseTimeRemaining -= deltaTime;

                    // Handle attack animation
                    if (_animationController.CurrentState == PetState.Attacking)
                    {
                        _attackTimer += deltaTime;

                        // Exit attack after duration - always end chase after attack
                        if (_attackTimer >= ATTACK_DURATION)
                        {
                            _attackTimer = 0;
                            _isChasingMouse = false;
                            _proximityTimer = 0;
                            _petVelocityX = 0;
                            _petVelocityY = 0;
                            _chaseCheckTimer = 0; // Reset check timer to delay next chase trigger
                            _wasMouseInRange = false; // Reset range tracking so mouse must re-enter to trigger again
                            _animationController.ChangeState(PetState.Idle);
                            App.Logger.LogInformation("Attack finished - ending chase, returning to normal behavior");
                        }
                    }
                    // Check if we should start attacking (must stay within 20px for 2 seconds)
                    else if (_animationController.CurrentState == PetState.Chasing)
                    {
                        if (distanceToMouse < ATTACK_DISTANCE)
                        {
                            // Within attack distance - increment proximity timer
                            _proximityTimer += deltaTime;
                            App.Logger.LogDebug("Within attack range: proximity timer = {ProximityTimer:F2}s, distance = {Distance:F1}px",
                                _proximityTimer, distanceToMouse);

                            // Trigger attack if stayed within range for 2 seconds
                            if (_proximityTimer >= PROXIMITY_DURATION)
                            {
                                _animationController.ChangeState(PetState.Attacking);
                                _petVelocityX = 0;
                                _petVelocityY = 0;
                                _attackTimer = 0;
                                App.Logger.LogInformation("Attack triggered! Cat stayed within {Distance}px for {Duration}s",
                                    ATTACK_DISTANCE, PROXIMITY_DURATION);
                            }
                        }
                        else
                        {
                            // Outside attack distance - reset proximity timer
                            if (_proximityTimer > 0)
                            {
                                App.Logger.LogDebug("Left attack range - resetting proximity timer (was {ProximityTimer:F2}s)", _proximityTimer);
                                _proximityTimer = 0;
                            }
                        }
                    }

                    // Move toward mouse if not attacking
                    if (_animationController.CurrentState == PetState.Chasing)
                    {
                        // Calculate direction to mouse and normalize
                        double distanceToTarget = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                        if (distanceToTarget > 0.1) // Prevent division by zero
                        {
                            // Apply deceleration when getting close to prevent oscillation
                            double speed = CHASE_SPEED;
                            const double DECEL_START_DISTANCE = 50.0; // Start slowing down at 50px
                            const double MIN_SPEED = 20.0; // Minimum speed when very close

                            if (distanceToTarget < DECEL_START_DISTANCE)
                            {
                                // Smoothly decelerate as we get closer
                                // At 50px: full speed, at 0px: min speed
                                double speedFactor = distanceToTarget / DECEL_START_DISTANCE;
                                speed = MIN_SPEED + (CHASE_SPEED - MIN_SPEED) * speedFactor;
                            }

                            // Set velocity toward mouse with calculated speed
                            _petVelocityX = (deltaX / distanceToTarget) * speed;
                            _petVelocityY = (deltaY / distanceToTarget) * speed;

                            // Set facing direction with cooldown to prevent rapid flipping
                            if (_directionChangeTimer >= DIRECTION_CHANGE_COOLDOWN)
                            {
                                // Only change direction if X velocity is significant
                                if (Math.Abs(deltaX) > 10) // Horizontal movement must be at least 10 pixels
                                {
                                    if (_petVelocityX > 0 && _animationController.CurrentDirection != PetDirection.Right)
                                    {
                                        _animationController.ChangeDirection(PetDirection.Right);
                                        _directionChangeTimer = 0;
                                    }
                                    else if (_petVelocityX < 0 && _animationController.CurrentDirection != PetDirection.Left)
                                    {
                                        _animationController.ChangeDirection(PetDirection.Left);
                                        _directionChangeTimer = 0;
                                    }
                                }
                            }
                        }
                    }

                    // Check if chase should end (15 second timeout)
                    if (_chaseTimeRemaining <= 0)
                    {
                        _isChasingMouse = false;
                        _proximityTimer = 0;
                        _petVelocityX = 0;
                        _petVelocityY = 0;
                        _chaseCheckTimer = 0; // Reset check timer to delay next chase trigger
                        _wasMouseInRange = false; // Reset range tracking so mouse must re-enter to trigger again

                        // Return to normal behavior - go to Idle
                        _animationController.ChangeState(PetState.Idle);
                        App.Logger.LogInformation("Chase timeout (15s) - returning to normal behavior");

                        // Reset timers for normal wandering
                        _idleTimer = 0;
                        _idleInterval = _random.Next(3, 8);
                    }
                }
                // Normal wandering behavior when not chasing
                else
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

                        App.Logger.LogDebug("Wandering: Walking → Idle (velocity cleared)");
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

                        App.Logger.LogDebug("Wandering: Changed direction (velocity=({VX:F1}, {VY:F1}))", _petVelocityX, _petVelocityY);
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

                        App.Logger.LogDebug("Wandering: Idle → Walking (velocity=({VX:F1}, {VY:F1}))", _petVelocityX, _petVelocityY);
                    }
                }
                } // End of normal wandering else block
                } // End of _hasMousePosition check
                // If no mouse position yet, just do normal wandering behavior
                else
                {
                    // Update wandering behavior timers
                    _wanderTimer += deltaTime;
                    _idleTimer += deltaTime;

                    // Random direction changes during walking
                    if (_animationController.CurrentState == PetState.Walking && _wanderTimer >= _wanderInterval)
                    {
                        _wanderTimer = 0;
                        _wanderInterval = _random.Next(2, 5);

                        // Random chance to go idle (30% chance)
                        if (_random.NextDouble() < 0.3)
                        {
                            _animationController.ChangeState(PetState.Idle);
                            _petVelocityX = 0;
                            _petVelocityY = 0;
                            _idleTimer = 0;
                            _idleInterval = _random.Next(3, 8);
                        }
                        else
                        {
                            // Change direction randomly
                            _petVelocityX = (_random.NextDouble() - 0.5) * 2 * PET_SPEED;
                            _petVelocityY = (_random.NextDouble() - 0.5) * 2 * PET_SPEED;

                            // Set facing direction
                            if (_petVelocityX > 0)
                                _animationController.ChangeDirection(PetDirection.Right);
                            else if (_petVelocityX < 0)
                                _animationController.ChangeDirection(PetDirection.Left);
                        }
                    }

                    // Random transitions from idle to walking
                    if (_animationController.CurrentState == PetState.Idle && _idleTimer >= _idleInterval)
                    {
                        _idleTimer = 0;
                        _idleInterval = _random.Next(3, 8);

                        // Random chance to start walking (50% chance)
                        if (_random.NextDouble() < 0.5)
                        {
                            _animationController.ChangeState(PetState.Walking);
                            _petVelocityX = (_random.NextDouble() - 0.5) * 2 * PET_SPEED;
                            _petVelocityY = (_random.NextDouble() - 0.5) * 2 * PET_SPEED;

                            // Set facing direction
                            if (_petVelocityX > 0)
                                _animationController.ChangeDirection(PetDirection.Right);
                            else if (_petVelocityX < 0)
                                _animationController.ChangeDirection(PetDirection.Left);

                            _wanderTimer = 0;
                            _wanderInterval = _random.Next(2, 5);
                        }
                    }
                }

                // Update pet position based on velocity
                double newX = _petX + (_petVelocityX * deltaTime);
                double newY = _petY + (_petVelocityY * deltaTime);

                // Boundary checking - keep pet on screen and bounce off edges
                const double MIN_BOUNCE_VELOCITY = 20; // Minimum velocity after bounce

                if (newX < 0)
                {
                    newX = 0;
                    if (_isChasingMouse)
                    {
                        // Stop chasing if hit edge
                        _isChasingMouse = false;
                        _proximityTimer = 0;
                        _petVelocityX = 0;
                        _petVelocityY = 0;
                        _chaseCheckTimer = 0; // Reset check timer
                        _wasMouseInRange = false; // Reset range tracking
                        _animationController.ChangeState(PetState.Idle);
                        App.Logger.LogInformation("Chase stopped - hit left edge");
                    }
                    else
                    {
                        _petVelocityX = Math.Max(Math.Abs(_petVelocityX), MIN_BOUNCE_VELOCITY);
                        _animationController.ChangeDirection(PetDirection.Right);
                        App.Logger.LogDebug("Pet hit left edge");
                    }
                }
                else if (newX > MainCanvas.ActualWidth - PetImage.ActualWidth)
                {
                    newX = MainCanvas.ActualWidth - PetImage.ActualWidth;
                    if (_isChasingMouse)
                    {
                        // Stop chasing if hit edge
                        _isChasingMouse = false;
                        _proximityTimer = 0;
                        _petVelocityX = 0;
                        _petVelocityY = 0;
                        _chaseCheckTimer = 0; // Reset check timer
                        _wasMouseInRange = false; // Reset range tracking
                        _animationController.ChangeState(PetState.Idle);
                        App.Logger.LogInformation("Chase stopped - hit right edge");
                    }
                    else
                    {
                        _petVelocityX = -Math.Max(Math.Abs(_petVelocityX), MIN_BOUNCE_VELOCITY);
                        _animationController.ChangeDirection(PetDirection.Left);
                        App.Logger.LogDebug("Pet hit right edge");
                    }
                }

                if (newY < 0)
                {
                    newY = 0;
                    if (_isChasingMouse)
                    {
                        _isChasingMouse = false;
                        _proximityTimer = 0;
                        _petVelocityX = 0;
                        _petVelocityY = 0;
                        _chaseCheckTimer = 0; // Reset check timer
                        _wasMouseInRange = false; // Reset range tracking
                        _animationController.ChangeState(PetState.Idle);
                        App.Logger.LogInformation("Chase stopped - hit top edge");
                    }
                    else
                    {
                        _petVelocityY = Math.Max(Math.Abs(_petVelocityY), MIN_BOUNCE_VELOCITY);
                    }
                }
                else if (newY > MainCanvas.ActualHeight - PetImage.ActualHeight)
                {
                    newY = MainCanvas.ActualHeight - PetImage.ActualHeight;
                    if (_isChasingMouse)
                    {
                        _isChasingMouse = false;
                        _proximityTimer = 0;
                        _petVelocityX = 0;
                        _petVelocityY = 0;
                        _chaseCheckTimer = 0; // Reset check timer
                        _wasMouseInRange = false; // Reset range tracking
                        _animationController.ChangeState(PetState.Idle);
                        App.Logger.LogInformation("Chase stopped - hit bottom edge");
                    }
                    else
                    {
                        _petVelocityY = -Math.Max(Math.Abs(_petVelocityY), MIN_BOUNCE_VELOCITY);
                    }
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
            }
            catch (Exception ex)
            {
                _gameTimer.Stop();
                App.Logger.LogError(ex, "Error in game loop - timer stopped");
            }
        }
    }
}
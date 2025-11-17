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
        private const double CHASE_SPEED = 150.0; // 3x normal speed (50 * 3)
        private const double CHASE_MIN_DURATION = 10.0;
        private const double CHASE_MAX_DURATION = 15.0;
        private const double ATTACK_DISTANCE = 69.0; // Distance to trigger attack animation
        private const double ATTACK_DURATION = 2.0; // Attack animation duration in seconds
        private const double CHASE_CHANCE = 0.42; // 42% chance to start chasing

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

            // Update mouse position every frame
            UpdateMousePosition();

            // Update interaction cooldown timer
            _timeSinceLastInteraction += deltaTime;

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
                if (distanceToMouse < DETECTION_RADIUS && !_isChasing)
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
                    // Mouse is outside detection radius - reset proximity timer
                    if (_proximityTimer > 0)
                    {
                        App.Logger.LogDebug("Mouse left detection radius, resetting proximity timer");
                    }
                    _proximityTimer = 0;
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

                    App.Logger.LogInformation("Chase ended after {Duration:F1}s", _chaseDuration);
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
                                _petVelocityX = (deltaX / distance) * CHASE_SPEED;
                                _petVelocityY = (deltaY / distance) * CHASE_SPEED;

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
            }
            catch (Exception ex)
            {
                _gameTimer.Stop();
                App.Logger.LogError(ex, "Error in game loop - timer stopped");
            }
        }
    }
}
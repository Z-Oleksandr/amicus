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

        // Pet dragging event handlers
        private void PetImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
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
                e.Handled = true;
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
            _hunger = Math.Min(100, _hunger + 25);
            UpdateNeedsDisplay();
            System.Windows.MessageBox.Show("Nom nom! 🍽️", "Amicus");
        }

        private void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            _cleanliness = Math.Min(100, _cleanliness + 25);
            UpdateNeedsDisplay();
            System.Windows.MessageBox.Show("Sparkle sparkle! ✨", "Amicus");
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            _happiness = Math.Min(100, _happiness + 25);
            UpdateNeedsDisplay();
            System.Windows.MessageBox.Show("Purr purr! 😺", "Amicus");
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

        private void GameTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                // Calculate delta time
                DateTime currentTime = DateTime.Now;
                double deltaTime = (currentTime - _lastUpdateTime).TotalSeconds;
                _lastUpdateTime = currentTime;

            // Don't update wandering behavior if pet is being dragged
            if (!_isDraggingPet)
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
                    }
                }

                // Update pet position based on velocity
                double newX = _petX + (_petVelocityX * deltaTime);
                double newY = _petY + (_petVelocityY * deltaTime);

                // Boundary checking - keep pet on screen and bounce off edges
                if (newX < 0)
                {
                    newX = 0;
                    _petVelocityX = Math.Abs(_petVelocityX); // Bounce right
                    _animationController.ChangeDirection(PetDirection.Right);
                }
                else if (newX > MainCanvas.ActualWidth - PetImage.ActualWidth)
                {
                    newX = MainCanvas.ActualWidth - PetImage.ActualWidth;
                    _petVelocityX = -Math.Abs(_petVelocityX); // Bounce left
                    _animationController.ChangeDirection(PetDirection.Left);
                }

                if (newY < 0)
                {
                    newY = 0;
                    _petVelocityY = Math.Abs(_petVelocityY); // Bounce down
                }
                else if (newY > MainCanvas.ActualHeight - PetImage.ActualHeight)
                {
                    newY = MainCanvas.ActualHeight - PetImage.ActualHeight;
                    _petVelocityY = -Math.Abs(_petVelocityY); // Bounce up
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
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
            // Load initial pet sprite
            LoadPetSprite("Idle1");

            // Set initial pet position
            UpdatePetPosition(_petX, _petY);

            // Update needs display
            UpdateNeedsDisplay();

            // Set up click-through behavior after window is fully loaded
            this.SourceInitialized += MainWindow_SourceInitialized;
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

        private void LoadPetSprite(string spriteName)
        {
            try
            {
                string spritePath = $"Resources/Sprites/RetroCatsPaid/Cats/Sprites/{spriteName}.png";
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(spritePath, UriKind.Relative);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                PetImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                // Fallback: create a simple colored rectangle if sprite fails to load
                System.Windows.MessageBox.Show($"Failed to load sprite: {ex.Message}\nUsing placeholder instead.");
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
    }
}
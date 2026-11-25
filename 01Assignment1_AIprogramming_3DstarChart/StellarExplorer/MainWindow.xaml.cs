
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using StellarExplorer.Models;
using StellarExplorer.Services;
using StellarExplorer.Visualization;

namespace StellarExplorer
{
    public partial class MainWindow : Window
    {
        private readonly StarDataService _starDataService;
        private readonly StarVisualisationService _visualisationService;
        private readonly StarLabelService _starLabelService;

        // Stores all stars loaded from the catalogue.
        private List<Star> _stars = new();

        // Stores the currently selected star.
        private Star? _selectedStar;

        // Maximum distance for nearby star detection.
        private const double NearbyStarRadius = 10.0;

        // Camera animation timer.
        private readonly DispatcherTimer _cameraTimer;

        // Camera animation starting values.
        private Point3D _startPosition;
        private Point3D _targetPosition;

        private Vector3D _startLookDirection;
        private Vector3D _targetLookDirection;

        private Vector3D _startUpDirection;
        private Vector3D _targetUpDirection;

        private DateTime _animationStartTime;

        private const double CameraAnimationDuration = 1.5;

        public MainWindow()
        {
            InitializeComponent();

            _starDataService = new StarDataService();
            _visualisationService = new StarVisualisationService();
            _starLabelService = new StarLabelService();

            // Listen for star selection events.
            _visualisationService.StarSelected += OnStarSelected;

            // Configure the camera animation timer.
            _cameraTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };

            _cameraTimer.Tick += CameraTimer_Tick;

            // Load the catalogue when the window opens.
            Loaded += async (sender, e) =>
                await LoadAndDisplayStarsAsync();
        }

        /// <summary>
        /// Loads the star catalogue and displays its stars and labels in 3D.
        /// </summary>
        private async Task LoadAndDisplayStarsAsync()
        {
            try
            {
                string filePath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Data",
                    "stars.csv"
                );

                _stars = await _starDataService.LoadStarsAsync(filePath);

                // Display the star models.
                int displayedCount = _visualisationService.DisplayStars(
                    StarViewport,
                    _stars
                );

                // Display the initial set of star labels.
                int labelCount = _starLabelService.DisplayLabels(
                    StarViewport,
                    _stars
                );

                SelectionStatusText.Text =
                    "Catalogue loaded. Click a star to view its details.";

                System.Diagnostics.Debug.WriteLine(
                    $"STAR VISUALISATION: Displayed {displayedCount} stars."
                );

                System.Diagnostics.Debug.WriteLine(
                    $"STAR LABELS: Displayed {labelCount} labels."
                );

                MessageBox.Show(
                    $"Successfully loaded {_stars.Count} stars!\n" +
                    $"Displayed in 3D: {displayedCount}\n" +
                    $"Visible labels: {labelCount}",
                    "StellarExplorer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to load the star catalogue.\n\n{ex.Message}",
                    "Star Catalogue Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                SelectionStatusText.Text =
                    "Unable to load the star catalogue.";
            }
        }

        /// <summary>
        /// Updates the details panel and labels when a star is selected.
        /// </summary>
        private void OnStarSelected(Star star)
        {
            if (star == null)
                return;

            // Remember the selected star for camera navigation.
            _selectedStar = star;

            // Update the reference star for the label service.
            _starLabelService.SetReferenceStar(star);

            // Display the star's identity.
            StarNameText.Text = GetDisplayValue(star.Name);
            StarIdText.Text = $"Catalogue ID: {star.Id}";

            // Display astronomical classification.
            SpectralTypeText.Text =
                GetDisplayValue(star.SpectralType);

            ConstellationText.Text =
                GetDisplayValue(star.Constellation);

            // Display distance and magnitude information.
            DistanceText.Text =
                FormatNumber(star.Distance) + " light-years";

            ApparentMagnitudeText.Text =
                FormatNumber(star.ApparentMagnitude);

            AbsoluteMagnitudeText.Text =
                FormatNumber(star.AbsoluteMagnitude);

            // Display 3D spatial coordinates.
            XCoordinateText.Text =
                "X: " + FormatNumber(star.X) + " ly";

            YCoordinateText.Text =
                "Y: " + FormatNumber(star.Y) + " ly";

            ZCoordinateText.Text =
                "Z: " + FormatNumber(star.Z) + " ly";

            // Find nearby stars around the selected star.
            UpdateNearbyStars(star);

            // Update the selection status.
            SelectionStatusText.Text =
                $"Currently viewing: {GetDisplayValue(star.Name)}";

            System.Diagnostics.Debug.WriteLine(
                $"DETAILS PANEL UPDATED: {star.Name} (ID: {star.Id})"
            );

            System.Diagnostics.Debug.WriteLine(
                $"STAR LABELS: Updated around {star.Name}."
            );
        }

        /// <summary>
        /// Finds and displays stars within 10 light-years
        /// of the currently selected star.
        /// </summary>
        private void UpdateNearbyStars(Star selectedStar)
        {
            // Clear results from the previously selected star.
            NearbyStarsList.Items.Clear();

            // Store nearby stars and their calculated distances.
            List<(Star Star, double Distance)> nearbyStars = new();

            foreach (Star star in _stars)
            {
                // Exclude the selected star itself.
                if (star.Id == selectedStar.Id)
                    continue;

                // Calculate the difference between their coordinates.
                double dx = star.X - selectedStar.X;
                double dy = star.Y - selectedStar.Y;
                double dz = star.Z - selectedStar.Z;

                // Calculate the three-dimensional distance.
                double distance = Math.Sqrt(
                    dx * dx +
                    dy * dy +
                    dz * dz
                );

                // Include stars within the 10 light-year radius.
                if (double.IsFinite(distance) &&
                    distance <= NearbyStarRadius)
                {
                    nearbyStars.Add((star, distance));
                }
            }

            // Sort nearby stars from nearest to furthest.
            nearbyStars.Sort(
                (a, b) => a.Distance.CompareTo(b.Distance)
            );

            // Display the nearby stars in the interface.
            foreach (var nearby in nearbyStars)
            {
                string starName = GetDisplayValue(nearby.Star.Name);

                NearbyStarsList.Items.Add(
                    $"{starName} — {FormatNumber(nearby.Distance)} ly"
                );
            }

            // Display a message if no nearby stars are found.
            if (nearbyStars.Count == 0)
            {
                NearbyStarsList.Items.Add(
                    "No stars found within 10 light-years."
                );
            }

            System.Diagnostics.Debug.WriteLine(
                $"NEARBY STARS: Found {nearbyStars.Count} stars " +
                $"within {NearbyStarRadius} light-years of " +
                $"{selectedStar.Name}."
            );
        }

        /// <summary>
        /// Starts a smooth camera movement towards the selected star.
        /// </summary>
        private void FlyToSelectedStar_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_selectedStar == null)
            {
                MessageBox.Show(
                    "Please select a star before using Fly to Star.",
                    "No Star Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                return;
            }

            if (StarViewport.Camera is not PerspectiveCamera camera)
            {
                MessageBox.Show(
                    "The 3D viewport does not have a perspective camera.",
                    "Camera Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            // Get the selected star's 3D position.
            Point3D target = new Point3D(
                _selectedStar.X,
                _selectedStar.Y,
                _selectedStar.Z
            );

            // Calculate the direction from the origin to the star.
            Vector3D direction = new Vector3D(
                target.X,
                target.Y,
                target.Z
            );

            // Handle stars positioned at the origin, such as Sol.
            if (direction.Length < 0.001)
            {
                direction = new Vector3D(0, 0, 1);
            }
            else
            {
                direction.Normalize();
            }

            // Position the camera close to the selected star.
            const double viewingDistance = 5.0;

            Point3D destinationPosition =
                target + direction * viewingDistance;

            // Point the camera towards the selected star.
            Vector3D destinationLookDirection =
                target - destinationPosition;

            // Configure the camera's vertical orientation.
            Vector3D destinationUpDirection;

            if (Math.Abs(Vector3D.DotProduct(
                direction,
                new Vector3D(0, 1, 0))) > 0.98)
            {
                destinationUpDirection = new Vector3D(0, 0, 1);
            }
            else
            {
                destinationUpDirection = new Vector3D(0, 1, 0);
            }

            // Start the camera animation.
            StartCameraAnimation(
                camera,
                destinationPosition,
                destinationLookDirection,
                destinationUpDirection
            );

            SelectionStatusText.Text =
                $"Flying to: {GetDisplayValue(_selectedStar.Name)}";

            System.Diagnostics.Debug.WriteLine(
                $"CAMERA: Flying to {_selectedStar.Name}, " +
                $"ID: {_selectedStar.Id}"
            );
        }

        /// <summary>
        /// Restores the camera to its original position smoothly.
        /// </summary>
        private void ResetView_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (StarViewport.Camera is not PerspectiveCamera camera)
                return;

            // Original camera configuration.
            Point3D originalPosition = new Point3D(0, 0, 100);

            Vector3D originalLookDirection =
                new Vector3D(0, 0, -100);

            Vector3D originalUpDirection =
                new Vector3D(0, 1, 0);

            StartCameraAnimation(
                camera,
                originalPosition,
                originalLookDirection,
                originalUpDirection
            );

            camera.FieldOfView = 45;

            SelectionStatusText.Text =
                "Returning to the original star field view.";

            System.Diagnostics.Debug.WriteLine(
                "CAMERA: Resetting view."
            );
        }

        /// <summary>
        /// Prepares and starts a new camera animation.
        /// </summary>
        private void StartCameraAnimation(
            PerspectiveCamera camera,
            Point3D destinationPosition,
            Vector3D destinationLookDirection,
            Vector3D destinationUpDirection)
        {
            // Stop any previous animation.
            _cameraTimer.Stop();

            // Capture the camera's current position and orientation.
            _startPosition = camera.Position;
            _startLookDirection = camera.LookDirection;
            _startUpDirection = camera.UpDirection;

            // Store the destination values.
            _targetPosition = destinationPosition;
            _targetLookDirection = destinationLookDirection;
            _targetUpDirection = destinationUpDirection;

            // Record the animation start time.
            _animationStartTime = DateTime.Now;

            // Start the timer.
            _cameraTimer.Start();
        }

        /// <summary>
        /// Updates camera properties during each animation frame.
        /// </summary>
        private void CameraTimer_Tick(
            object? sender,
            EventArgs e)
        {
            if (StarViewport.Camera is not PerspectiveCamera camera)
            {
                _cameraTimer.Stop();
                return;
            }

            // Calculate animation progress between 0 and 1.
            double elapsedTime =
                (DateTime.Now - _animationStartTime).TotalSeconds;

            double progress = Math.Clamp(
                elapsedTime / CameraAnimationDuration,
                0.0,
                1.0
            );

            // Smooth acceleration and deceleration.
            double easedProgress =
                progress * progress * (3.0 - 2.0 * progress);

            // Interpolate camera position.
            camera.Position = new Point3D(
                Lerp(_startPosition.X, _targetPosition.X, easedProgress),
                Lerp(_startPosition.Y, _targetPosition.Y, easedProgress),
                Lerp(_startPosition.Z, _targetPosition.Z, easedProgress)
            );

            // Interpolate camera viewing direction.
            camera.LookDirection = new Vector3D(
                Lerp(_startLookDirection.X, _targetLookDirection.X, easedProgress),
                Lerp(_startLookDirection.Y, _targetLookDirection.Y, easedProgress),
                Lerp(_startLookDirection.Z, _targetLookDirection.Z, easedProgress)
            );

            // Interpolate camera vertical orientation.
            Vector3D upDirection = new Vector3D(
                Lerp(_startUpDirection.X, _targetUpDirection.X, easedProgress),
                Lerp(_startUpDirection.Y, _targetUpDirection.Y, easedProgress),
                Lerp(_startUpDirection.Z, _targetUpDirection.Z, easedProgress)
            );

            if (upDirection.Length > 0.001)
            {
                upDirection.Normalize();
                camera.UpDirection = upDirection;
            }

            // Finish the animation.
            if (progress >= 1.0)
            {
                camera.Position = _targetPosition;
                camera.LookDirection = _targetLookDirection;
                camera.UpDirection = _targetUpDirection;

                _cameraTimer.Stop();

                System.Diagnostics.Debug.WriteLine(
                    "CAMERA: Animation completed."
                );
            }
        }

        /// <summary>
        /// Performs linear interpolation between two values.
        /// </summary>
        private static double Lerp(
            double start,
            double end,
            double progress)
        {
            return start + (end - start) * progress;
        }

        /// <summary>
        /// Returns a readable value when a string is missing.
        /// </summary>
        private static string GetDisplayValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "Unknown"
                : value;
        }

        /// <summary>
        /// Formats astronomical numerical values.
        /// </summary>
        private static string FormatNumber(double value)
        {
            if (!double.IsFinite(value))
                return "N/A";

            return value.ToString(
                "F2",
                CultureInfo.InvariantCulture
            );
        }

        /// <summary>
        /// Cleans up timers and star labels when the application closes.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            _cameraTimer.Stop();

            // Remove dynamically generated star labels.
            _starLabelService.Clear();

            base.OnClosed(e);
        }
    }
}

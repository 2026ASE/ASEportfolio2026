
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using StellarExplorer.Models;

namespace StellarExplorer.Visualization
{
    /// <summary>
    /// Creates luminous, selectable 3D star objects.
    /// Each star consists of an outer halo, inner glow and core.
    /// </summary>
    public class StarVisualisationService
    {
        // Stores all generated star visuals.
        private readonly List<ModelVisual3D> _starVisuals = new();

        // Connects each 3D model to its original Star object.
        private readonly Dictionary<GeometryModel3D, Star> _modelToStar = new();

        // Stores the original material of each star core.
        private readonly Dictionary<int, Material> _originalMaterials = new();

        // Stores the core geometry associated with each star ID.
        private readonly Dictionary<int, GeometryModel3D> _starModelsById = new();

        // Stores the currently selected star.
        private int? _selectedStarId;

        // Stores the active viewport.
        private HelixViewport3D? _activeViewport;

        // Material used to highlight the selected star.
        private readonly Material _selectionMaterial =
            CreateCoreMaterial(Colors.Cyan);

        // Sphere resolution.
        private const int SphereSlices = 16;
        private const int SphereStacks = 10;

        /// <summary>
        /// Raised when the user clicks a star.
        /// </summary>
        public event Action<Star>? StarSelected;

        /// <summary>
        /// Displays luminous stars inside the HelixToolkit viewport.
        /// Returns the number of successfully displayed stars.
        /// </summary>
        public int DisplayStars(
            HelixViewport3D viewport,
            IEnumerable<Star> stars)
        {
            if (viewport == null)
                throw new ArgumentNullException(nameof(viewport));

            if (stars == null)
                throw new ArgumentNullException(nameof(stars));

            // Remove the previously generated star field.
            ClearStarVisuals();

            _activeViewport = viewport;

            // Listen for mouse clicks on the viewport.
            _activeViewport.PreviewMouseLeftButtonDown +=
                OnViewportPreviewMouseLeftButtonDown;

            int displayedStars = 0;

            foreach (Star star in stars)
            {
                if (star == null)
                    continue;

                // Ignore invalid spatial coordinates.
                if (!double.IsFinite(star.X) ||
                    !double.IsFinite(star.Y) ||
                    !double.IsFinite(star.Z))
                {
                    continue;
                }

                // Avoid duplicate star identifiers.
                if (_starModelsById.ContainsKey(star.Id))
                    continue;

                // Determine the star's spectral colour.
                Color starColour = GetStarColour(star.SpectralType);

                // Calculate the star's visual radius.
                double radius = CalculateStarRadius(
                    star.ApparentMagnitude);

                // Create the star's 3D position.
                Point3D position = new Point3D(
                    star.X,
                    star.Y,
                    star.Z
                );

                // Create a group containing all three visual layers.
                Model3DGroup starGroup = new Model3DGroup();

                // Layer 1: Large, subtle outer halo.
                GeometryModel3D outerHalo = CreateStarLayer(
                    position,
                    radius * 2.4,
                    CreateGlowMaterial(starColour, 20)
                );

                // Layer 2: Smaller, brighter inner glow.
                GeometryModel3D innerGlow = CreateStarLayer(
                    position,
                    radius * 1.5,
                    CreateGlowMaterial(starColour, 65)
                );

                // Layer 3: Bright luminous star core.
                Material coreMaterial =
                    CreateCoreMaterial(starColour);

                GeometryModel3D starCore = CreateStarLayer(
                    position,
                    radius,
                    coreMaterial
                );

                // Add the layers to the star model.
                starGroup.Children.Add(outerHalo);
                starGroup.Children.Add(innerGlow);
                starGroup.Children.Add(starCore);

                // Register all three layers for hit testing.
                _modelToStar.Add(outerHalo, star);
                _modelToStar.Add(innerGlow, star);
                _modelToStar.Add(starCore, star);

                // Store the core material for selection restoration.
                _originalMaterials.Add(star.Id, coreMaterial);

                // Store the core geometry for selection highlighting.
                _starModelsById.Add(star.Id, starCore);

                // Create one visual for the complete star.
                ModelVisual3D starVisual = new ModelVisual3D
                {
                    Content = starGroup
                };

                _starVisuals.Add(starVisual);
                viewport.Children.Add(starVisual);

                displayedStars++;
            }

            Debug.WriteLine(
                $"STAR VISUALISATION: Displayed {displayedStars} luminous stars."
            );

            return displayedStars;
        }

        /// <summary>
        /// Creates a spherical geometry layer for a star.
        /// </summary>
        private static GeometryModel3D CreateStarLayer(
            Point3D position,
            double radius,
            Material material)
        {
            MeshGeometry3D mesh = CreateSphereMesh(
                position,
                radius
            );

            return new GeometryModel3D
            {
                Geometry = mesh,
                Material = material,
                BackMaterial = material
            };
        }

        /// <summary>
        /// Generates a UV sphere mesh around a 3D position.
        /// </summary>
        private static MeshGeometry3D CreateSphereMesh(
            Point3D centre,
            double radius)
        {
            MeshGeometry3D mesh = new MeshGeometry3D();

            // Generate the sphere vertices.
            for (int stack = 0; stack <= SphereStacks; stack++)
            {
                double verticalAngle =
                    Math.PI * stack / SphereStacks;

                double sinVertical = Math.Sin(verticalAngle);
                double cosVertical = Math.Cos(verticalAngle);

                for (int slice = 0; slice <= SphereSlices; slice++)
                {
                    double horizontalAngle =
                        2.0 * Math.PI * slice / SphereSlices;

                    double nx = sinVertical * Math.Cos(horizontalAngle);
                    double ny = cosVertical;
                    double nz = sinVertical * Math.Sin(horizontalAngle);

                    // Add the vertex around the star's centre.
                    mesh.Positions.Add(
                        new Point3D(
                            centre.X + radius * nx,
                            centre.Y + radius * ny,
                            centre.Z + radius * nz
                        )
                    );

                    // Add the sphere's surface normal.
                    mesh.Normals.Add(
                        new Vector3D(nx, ny, nz)
                    );
                }
            }

            // Connect vertices to create triangular faces.
            for (int stack = 0; stack < SphereStacks; stack++)
            {
                for (int slice = 0; slice < SphereSlices; slice++)
                {
                    int first =
                        stack * (SphereSlices + 1) + slice;

                    int second = first + SphereSlices + 1;

                    mesh.TriangleIndices.Add(first);
                    mesh.TriangleIndices.Add(second);
                    mesh.TriangleIndices.Add(first + 1);

                    mesh.TriangleIndices.Add(first + 1);
                    mesh.TriangleIndices.Add(second);
                    mesh.TriangleIndices.Add(second + 1);
                }
            }

            mesh.Freeze();

            return mesh;
        }

        /// <summary>
        /// Creates a luminous material for the star core.
        /// Combines diffuse and emissive materials.
        /// </summary>
        private static Material CreateCoreMaterial(Color colour)
        {
            SolidColorBrush brush = new SolidColorBrush(colour);
            brush.Freeze();

            MaterialGroup materialGroup = new MaterialGroup();

            materialGroup.Children.Add(
                new DiffuseMaterial(brush)
            );

            materialGroup.Children.Add(
                new EmissiveMaterial(brush)
            );

            materialGroup.Freeze();

            return materialGroup;
        }

        /// <summary>
        /// Creates a transparent emissive material for star glow.
        /// </summary>
        private static Material CreateGlowMaterial(
            Color colour,
            byte opacity)
        {
            Color glowColour = Color.FromArgb(
                opacity,
                colour.R,
                colour.G,
                colour.B
            );

            SolidColorBrush brush =
                new SolidColorBrush(glowColour);

            brush.Freeze();

            EmissiveMaterial material =
                new EmissiveMaterial(brush);

            material.Freeze();

            return material;
        }

        /// <summary>
        /// Detects which 3D star model is underneath the mouse pointer.
        /// </summary>
        private void OnViewportPreviewMouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (_activeViewport == null)
                return;

            Point mousePosition = e.GetPosition(_activeViewport);

            Debug.WriteLine(
                $"MOUSE CLICK DETECTED: X={mousePosition.X}, Y={mousePosition.Y}"
            );

            var hits = Viewport3DHelper.FindHits(
                _activeViewport.Viewport,
                mousePosition
            );

            if (hits == null || hits.Count == 0)
            {
                Debug.WriteLine("HIT TEST: No 3D objects detected.");
                return;
            }

            // Check the hit objects for a registered star model.
            foreach (var hit in hits)
            {
                if (hit.Model is GeometryModel3D model &&
                    _modelToStar.TryGetValue(model, out Star? star))
                {
                    Debug.WriteLine(
                        $"STAR HIT: {star.Name}, ID: {star.Id}"
                    );

                    // Highlight the selected star.
                    SelectStar(star.Id);

                    // Notify the rest of the application.
                    StarSelected?.Invoke(star);

                    return;
                }
            }

            Debug.WriteLine(
                "HIT TEST: Objects were detected, but none matched a star."
            );
        }

        /// <summary>
        /// Highlights a star and restores the previous selection.
        /// </summary>
        public void SelectStar(int starId)
        {
            // Restore the previous star's original material.
            if (_selectedStarId.HasValue &&
                _starModelsById.TryGetValue(
                    _selectedStarId.Value,
                    out GeometryModel3D? previousModel) &&
                _originalMaterials.TryGetValue(
                    _selectedStarId.Value,
                    out Material? previousMaterial))
            {
                previousModel.Material = previousMaterial;
                previousModel.BackMaterial = previousMaterial;
            }

            // Find the newly selected star.
            if (!_starModelsById.TryGetValue(
                starId,
                out GeometryModel3D? selectedModel))
            {
                _selectedStarId = null;

                Debug.WriteLine(
                    $"SELECTION: Star ID {starId} was not found."
                );

                return;
            }

            // Apply the cyan selection highlight to the core.
            selectedModel.Material = _selectionMaterial;
            selectedModel.BackMaterial = _selectionMaterial;

            _selectedStarId = starId;

            Debug.WriteLine(
                $"SELECTION: Star ID {starId} highlighted."
            );
        }

        /// <summary>
        /// Removes all previously generated star visuals.
        /// </summary>
        private void ClearStarVisuals()
        {
            // Remove the mouse event from the previous viewport.
            if (_activeViewport != null)
            {
                _activeViewport.PreviewMouseLeftButtonDown -=
                    OnViewportPreviewMouseLeftButtonDown;

                // Remove all previous star visuals.
                foreach (ModelVisual3D visual in _starVisuals)
                {
                    _activeViewport.Children.Remove(visual);
                }

                _activeViewport = null;
            }

            _starVisuals.Clear();
            _modelToStar.Clear();
            _originalMaterials.Clear();
            _starModelsById.Clear();

            _selectedStarId = null;
        }

        /// <summary>
        /// Determines star colour from its spectral classification.
        /// </summary>
        private static Color GetStarColour(string spectralType)
        {
            if (string.IsNullOrWhiteSpace(spectralType))
                return Colors.White;

            string cleanType = spectralType.Trim().ToUpperInvariant();

            // Give white dwarfs a distinctive blue-white appearance.
            if (cleanType.StartsWith("D"))
                return Colors.LightCyan;

            // Search the classification for its primary spectral class.
            foreach (char spectralClass in cleanType)
            {
                switch (spectralClass)
                {
                    case 'O':
                        return Color.FromRgb(120, 170, 255);

                    case 'B':
                        return Color.FromRgb(165, 200, 255);

                    case 'A':
                        return Color.FromRgb(235, 240, 255);

                    case 'F':
                        return Color.FromRgb(255, 248, 220);

                    case 'G':
                        return Color.FromRgb(255, 225, 120);

                    case 'K':
                        return Color.FromRgb(255, 170, 90);

                    case 'M':
                        return Color.FromRgb(255, 105, 95);

                    case 'L':
                    case 'T':
                    case 'Y':
                        return Color.FromRgb(210, 115, 100);
                }
            }

            return Colors.White;
        }

        /// <summary>
        /// Calculates the star's visual radius using apparent magnitude.
        /// Brighter stars receive larger cores.
        /// </summary>
        private static double CalculateStarRadius(double magnitude)
        {
            if (!double.IsFinite(magnitude))
                return 0.15;

            double radius =
                0.15 * Math.Pow(10, -0.08 * magnitude);

            return Math.Clamp(radius, 0.12, 0.55);
        }
    }
}

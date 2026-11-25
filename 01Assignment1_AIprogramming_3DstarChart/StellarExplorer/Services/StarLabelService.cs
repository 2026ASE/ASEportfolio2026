
using HelixToolkit.Wpf;
using StellarExplorer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace StellarExplorer.Services
{
    /// <summary>
    /// Manages a small, readable set of labels around the reference star.
    /// Labels are created dynamically to reduce visual clutter.
    /// </summary>
    public class StarLabelService
    {
        private readonly List<Star> _stars = new();

        private readonly List<BillboardTextVisual3D> _activeLabels = new();

        private HelixViewport3D? _activeViewport;

        private Star? _referenceStar;

        // Maximum distance from the reference star for labels.
        private const double LabelVisibilityLimit = 15.0;

        // Maximum number of labels visible at one time.
        private const int MaximumVisibleLabels = 8;

        // Label appearance.
        private const double LabelHeight = 0.38;
        private const double LabelOffset = 0.8;

        /// <summary>
        /// Initialises the label service and displays labels near the origin.
        /// </summary>
        public int DisplayLabels(
            HelixViewport3D viewport,
            IEnumerable<Star> stars)
        {
            Clear();

            _activeViewport = viewport;

            _stars.AddRange(
                stars.Where(star =>
                    double.IsFinite(star.X) &&
                    double.IsFinite(star.Y) &&
                    double.IsFinite(star.Z))
            );

            // Initially use the origin as the reference position.
            RefreshLabels();

            return _activeLabels.Count;
        }

        /// <summary>
        /// Changes the reference point to the selected star.
        /// </summary>
        public void SetReferenceStar(Star star)
        {
            if (star == null)
                return;

            _referenceStar = star;

            RefreshLabels();
        }

        /// <summary>
        /// Refreshes the visible labels around the reference star.
        /// </summary>
        private void RefreshLabels()
        {
            if (_activeViewport == null)
                return;

            RemoveActiveLabels();

            Point3D referencePosition = _referenceStar == null
                ? new Point3D(0, 0, 0)
                : new Point3D(
                    _referenceStar.X,
                    _referenceStar.Y,
                    _referenceStar.Z
                );

            // Find recognisable stars within the visibility range.
            var candidates = _stars
                .Where(star =>
                    _referenceStar == null ||
                    star.Id != _referenceStar.Id)
                .Select(star => new
                {
                    Star = star,
                    Distance = CalculateDistance(
                        referencePosition,
                        star
                    ),
                    Label = BuildNeighbourLabel(star)
                })
                .Where(item =>
                    item.Label != null &&
                    double.IsFinite(item.Distance) &&
                    item.Distance <= LabelVisibilityLimit)
                .OrderBy(item => item.Distance)
                .Take(
                    _referenceStar == null
                        ? MaximumVisibleLabels
                        : MaximumVisibleLabels - 1
                )
                .ToList();

            // Add the selected star first so it always receives a label.
            if (_referenceStar != null)
            {
                string selectedLabel =
                    BuildSelectedLabel(_referenceStar);

                AddLabel(
                    _referenceStar,
                    selectedLabel,
                    true,
                    0
                );
            }

            // Add the nearest recognisable stars.
            int labelIndex = 1;

            foreach (var candidate in candidates)
            {
                AddLabel(
                    candidate.Star,
                    candidate.Label!,
                    false,
                    labelIndex
                );

                labelIndex++;
            }

            System.Diagnostics.Debug.WriteLine(
                $"STAR LABELS: Displaying {_activeLabels.Count} labels."
            );
        }

        /// <summary>
        /// Creates and adds one label to the 3D viewport.
        /// </summary>
        private void AddLabel(
            Star star,
            string text,
            bool isSelected,
            int labelIndex)
        {
            if (_activeViewport == null ||
                string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            // Distribute labels around their stars using different offsets.
            double angle = labelIndex * 2.399963229728653;

            double offsetX = Math.Cos(angle) * LabelOffset;
            double offsetY = Math.Sin(angle) * LabelOffset;

            Point3D labelPosition = new Point3D(
                star.X + offsetX,
                star.Y + offsetY,
                star.Z
            );

            var label = new BillboardTextVisual3D
            {
                Text = text,

                Position = labelPosition,

                Width = Math.Clamp(
                    text.Length * 0.11,
                    1.5,
                    3.2
                ),

                Height = LabelHeight,

                FontSize = 12,

                FontWeight = isSelected
                    ? FontWeights.Bold
                    : FontWeights.SemiBold,

                FontFamily = new FontFamily("Segoe UI"),

                Foreground = isSelected
                    ? Brushes.Gold
                    : Brushes.LightCyan,

                Background = new SolidColorBrush(
                    Color.FromArgb(145, 8, 16, 28)
                ),

                BorderBrush = Brushes.Transparent,

                BorderThickness = new Thickness(0),

                Padding = new Thickness(2),

                IsRendering = true
            };

            _activeViewport.Children.Add(label);

            _activeLabels.Add(label);
        }

        /// <summary>
        /// Creates a label for a nearby star.
        /// Only proper names and recognised astronomical
        /// designations are used.
        /// </summary>
        private static string? BuildNeighbourLabel(Star star)
        {
            if (HasValue(star.Name))
                return star.Name.Trim();

            string bayer = FormatBayerDesignation(
                star.BayerDesignation,
                star.Constellation
            );

            if (!string.IsNullOrWhiteSpace(bayer))
                return bayer;

            string flamsteed = FormatFlamsteedDesignation(
                star.FlamsteedDesignation,
                star.Constellation
            );

            if (!string.IsNullOrWhiteSpace(flamsteed))
                return flamsteed;

            // Avoid labelling every unnamed catalogue entry.
            return null;
        }

        /// <summary>
        /// Ensures that the selected star always has a label,
        /// even if it has no proper name or Bayer designation.
        /// </summary>
        private static string BuildSelectedLabel(Star star)
        {
            string? label = BuildNeighbourLabel(star);

            if (!string.IsNullOrWhiteSpace(label))
                return label;

            if (HasValue(star.GlieseDesignation))
                return star.GlieseDesignation.Trim();

            if (star.HenryDraperNumber > 0)
                return $"HD {star.HenryDraperNumber}";

            if (star.HipparcosNumber > 0)
                return $"HIP {star.HipparcosNumber}";

            return $"Star {star.Id}";
        }

        /// <summary>
        /// Converts Bayer abbreviations into readable Greek designations.
        /// </summary>
        private static string FormatBayerDesignation(
            string? designation,
            string? constellation)
        {
            if (!HasValue(designation))
                return string.Empty;

            string value = designation!.Trim();

            string abbreviation = value.Length >= 3
                ? value[..3].ToLowerInvariant()
                : value.ToLowerInvariant();

            string greek = abbreviation switch
            {
                "alp" or "alf" => "Alpha",
                "bet" => "Beta",
                "gam" => "Gamma",
                "del" => "Delta",
                "eps" => "Epsilon",
                "zet" => "Zeta",
                "eta" => "Eta",
                "the" => "Theta",
                "iot" => "Iota",
                "kap" => "Kappa",
                "lam" => "Lambda",
                "mu" => "Mu",
                "nu" => "Nu",
                "xi" => "Xi",
                "omi" => "Omicron",
                "pi" => "Pi",
                "rho" => "Rho",
                "sig" => "Sigma",
                "tau" => "Tau",
                "ups" => "Upsilon",
                "phi" => "Phi",
                "chi" => "Chi",
                "psi" => "Psi",
                "ome" => "Omega",
                _ => value
            };

            string suffix = value.Length > 3
                ? value[3..].TrimStart('-', ' ')
                : string.Empty;

            string result = string.IsNullOrWhiteSpace(suffix)
                ? greek
                : $"{greek} {suffix}";

            if (HasValue(constellation))
                result += $" {constellation.Trim()}";

            return result;
        }

        /// <summary>
        /// Formats a Flamsteed designation.
        /// </summary>
        private static string FormatFlamsteedDesignation(
            string? designation,
            string? constellation)
        {
            if (!HasValue(designation))
                return string.Empty;

            string result = designation!.Trim();

            if (HasValue(constellation))
                result += $" {constellation.Trim()}";

            return result;
        }

        /// <summary>
        /// Calculates the distance between the reference point and a star.
        /// </summary>
        private static double CalculateDistance(
            Point3D reference,
            Star star)
        {
            double dx = star.X - reference.X;
            double dy = star.Y - reference.Y;
            double dz = star.Z - reference.Z;

            return Math.Sqrt(
                dx * dx +
                dy * dy +
                dz * dz
            );
        }

        /// <summary>
        /// Checks whether a catalogue string contains a meaningful value.
        /// </summary>
        private static bool HasValue(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   !value.Equals("Unknown", StringComparison.OrdinalIgnoreCase) &&
                   !value.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                   !value.Equals("null", StringComparison.OrdinalIgnoreCase) &&
                   !value.Equals("?", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Removes currently displayed labels from the viewport.
        /// </summary>
        private void RemoveActiveLabels()
        {
            if (_activeViewport != null)
            {
                foreach (BillboardTextVisual3D label in _activeLabels)
                {
                    _activeViewport.Children.Remove(label);
                }
            }

            _activeLabels.Clear();
        }

        /// <summary>
        /// Removes all labels and releases the viewport reference.
        /// </summary>
        public void Clear()
        {
            RemoveActiveLabels();

            _stars.Clear();

            _referenceStar = null;

            _activeViewport = null;
        }
    }
}

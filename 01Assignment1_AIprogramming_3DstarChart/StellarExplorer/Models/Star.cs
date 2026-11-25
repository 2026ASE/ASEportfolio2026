
using System.Collections.Generic;
namespace StellarExplorer.Models
{
    /// <summary>
    /// Represents an individual star in the StellarExplorer universe.
    /// Stores its astronomical properties and 3D position.
    /// </summary>
    public class Star
    {
        // Unique catalogue identifier
        public int Id { get; set; }

        // Star identification
        public string Name { get; set; } = "Unknown";
        public string SpectralType { get; set; } = "Unknown";
        public string Constellation { get; set; } = "Unknown";

        // 3D spatial coordinates
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        // Astronomical measurements
        public double Distance { get; set; }
        public double ApparentMagnitude { get; set; }
        public double AbsoluteMagnitude { get; set; }
        public double Luminosity { get; set; }
        public double ColourIndex { get; set; }

        // Star system information
        public string SystemId { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public List<int> CompanionIds { get; set; } = new();

        // Additional astronomical identifiers
        public string BayerDesignation { get; set; } = string.Empty;
        public string FlamsteedDesignation { get; set; } = string.Empty;
        public string GlieseDesignation { get; set; } = string.Empty;

        public int HenryDraperNumber { get; set; }
        public int HipparcosNumber { get; set; }
    }
}

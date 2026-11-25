
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StellarExplorer.Models;

namespace StellarExplorer.Services
{
    /// <summary>
    /// Loads astronomical star records from a CSV catalogue.
    /// </summary>
    public class StarDataService
    {
        private const double ParsecsToLightYears = 3.26156;

        /// <summary>
        /// Loads stars from a CSV file and returns them as Star objects.
        /// </summary>
        public async Task<List<Star>> LoadStarsAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    "The star catalogue could not be found.",
                    filePath);
            }

            var stars = new List<Star>();

            using var reader = new StreamReader(filePath);

            // Read and process the CSV header.
            string? headerLine = await reader.ReadLineAsync();

            if (string.IsNullOrWhiteSpace(headerLine))
            {
                throw new InvalidDataException(
                    "The CSV file is empty or has no header.");
            }

            List<string> headers = ParseCsvLine(headerLine)
                .Select(h => h.Trim().ToLowerInvariant())
                .ToList();

            // Check that the required columns exist.
            string[] requiredColumns =
            {
                "id", "x", "y", "z"
            };

            foreach (string column in requiredColumns)
            {
                if (!headers.Contains(column))
                {
                    throw new InvalidDataException(
                        $"The CSV catalogue is missing the required column: {column}");
                }
            }

            if (headers.Distinct().Count() != headers.Count)
            {
                throw new InvalidDataException(
                    "The CSV catalogue contains duplicate column names.");
            }

            string? line;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                List<string> values = ParseCsvLine(line);

                // Ignore malformed records.
                if (values.Count != headers.Count)
                    continue;

                var record = headers
                    .Select((header, index) => new
                    {
                        Header = header,
                        Value = values[index].Trim()
                    })
                    .ToDictionary(
                        item => item.Header,
                        item => item.Value);

                Star? star = CreateStar(record);

                if (star != null)
                {
                    stars.Add(star);
                }
            }

            // Connect stars belonging to the same multiple-star system.
            LinkCompanionStars(stars);

            return stars;
        }

        /// <summary>
        /// Converts one CSV record into a Star object.
        /// </summary>
        private Star? CreateStar(
            Dictionary<string, string> record)
        {
            // ID 0 is valid in this catalogue (Sol).
            // Only reject records with an invalid or missing ID.
            if (!TryGetInt(record, "id", out int id))
                return null;

            // The primary component ID identifies the star system.
            bool hasPrimaryId = TryGetInt(
                record, "comp_primary", out int primaryId);

            return new Star
            {
                Id = id,

                Name = GetText(
                    record, "proper", "Unknown"),

                SpectralType = GetText(
                    record, "spect", "Unknown"),

                Constellation = GetText(
                    record, "con", "Unknown"),

                // Convert Cartesian coordinates from parsecs
                // into light-years.
                X = GetDouble(record, "x") * ParsecsToLightYears,
                Y = GetDouble(record, "y") * ParsecsToLightYears,
                Z = GetDouble(record, "z") * ParsecsToLightYears,

                // Convert distance from parsecs into light-years.
                Distance = GetDouble(record, "dist")
                    * ParsecsToLightYears,

                ApparentMagnitude = GetDouble(record, "mag"),
                AbsoluteMagnitude = GetDouble(record, "absmag"),
                Luminosity = GetDouble(record, "lum"),
                ColourIndex = GetDouble(record, "ci"),

                // comp_primary identifies the primary star's ID.
                SystemId = hasPrimaryId
                    ? primaryId.ToString(CultureInfo.InvariantCulture)
                    : string.Empty,

                IsPrimary = hasPrimaryId && primaryId == id,

                BayerDesignation = GetText(
                    record, "bayer", ""),

                FlamsteedDesignation = GetText(
                    record, "flam", ""),

                GlieseDesignation = GetText(
                    record, "gl", ""),

                HenryDraperNumber = GetInt(record, "hd"),
                HipparcosNumber = GetInt(record, "hip")
            };
        }

        /// <summary>
        /// Links stars that belong to the same multiple-star system.
        /// </summary>
        private void LinkCompanionStars(List<Star> stars)
        {
            var systems = stars
                .Where(s => !string.IsNullOrWhiteSpace(s.SystemId))
                .GroupBy(s => s.SystemId);

            foreach (var system in systems)
            {
                List<Star> systemStars = system.ToList();

                foreach (Star star in systemStars)
                {
                    star.CompanionIds = systemStars
                        .Where(other => other.Id != star.Id)
                        .Select(other => other.Id)
                        .ToList();
                }
            }
        }

        /// <summary>
        /// Gets a text value from a CSV record.
        /// </summary>
        private static string GetText(
            Dictionary<string, string> record,
            string column,
            string defaultValue)
        {
            return record.TryGetValue(column, out string? value)
                && !string.IsNullOrWhiteSpace(value)
                    ? value
                    : defaultValue;
        }

        /// <summary>
        /// Attempts to read an integer value from a CSV record.
        /// </summary>
        private static bool TryGetInt(
            Dictionary<string, string> record,
            string column,
            out int result)
        {
            string value = GetText(record, column, "");

            return int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out result);
        }

        /// <summary>
        /// Gets an integer value, returning zero if unavailable.
        /// </summary>
        private static int GetInt(
            Dictionary<string, string> record,
            string column)
        {
            return TryGetInt(record, column, out int result)
                ? result
                : 0;
        }

        /// <summary>
        /// Gets a numeric value from a CSV record.
        /// </summary>
        private static double GetDouble(
            Dictionary<string, string> record,
            string column)
        {
            string value = GetText(record, column, "");

            if (double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double result)
                && double.IsFinite(result))
            {
                return result;
            }

            return 0.0;
        }

        /// <summary>
        /// Splits a CSV line while respecting quoted fields.
        /// </summary>
        private static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var currentField = new StringBuilder();

            bool insideQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char current = line[i];

                if (current == '"')
                {
                    // Handle escaped quotation marks.
                    if (insideQuotes &&
                        i + 1 < line.Length &&
                        line[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        insideQuotes = !insideQuotes;
                    }
                }
                else if (current == ',' && !insideQuotes)
                {
                    fields.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(current);
                }
            }

            fields.Add(currentField.ToString());

            return fields;
        }
    }
}

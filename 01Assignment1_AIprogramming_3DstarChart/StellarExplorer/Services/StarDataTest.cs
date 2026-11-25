
using System;
using System.IO;
using System.Threading.Tasks;

namespace StellarExplorer.Services
{
    public class StarDataTest
    {
        public async Task<string> TestStarCatalogueAsync()
        {
            string filePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Data",
                "stars.csv"
            );

            var dataService = new StarDataService();

            var stars = await dataService.LoadStarsAsync(filePath);

            return $"Successfully loaded {stars.Count} stars!";
        }
    }
}

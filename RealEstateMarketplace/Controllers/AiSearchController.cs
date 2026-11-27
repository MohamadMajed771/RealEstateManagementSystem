using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RealEstateMarketplace.Services.AiSearch;

namespace RealEstateMarketplace.Controllers
{
    /// <summary>
    /// "Describe what you want" search. The sentence becomes normal search
    /// filters and the user lands on the regular results page (map + list).
    /// </summary>
    public class AiSearchController : Controller
    {
        public const string TempDataKey = "AiSearch";
        private readonly AiSearchService _ai;

        public AiSearchController(AiSearchService ai) => _ai = ai;

        [HttpGet]
        public IActionResult Index() => View();

        [HttpGet]
        [EnableRateLimiting("ai-search")]
        public async Task<IActionResult> Search(string? q, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 5)
            {
                TempData["AiError"] = "Please describe the property you are looking for.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _ai.InterpretAsync(q, ct);
            var c = result.Criteria;

            TempData[TempDataKey] = JsonSerializer.Serialize(new
            {
                query = q.Trim(),
                understood = result.Understood,
                keywords = c.Keywords,
                notes = result.Notes,
                source = result.Source
            });

            var inv = CultureInfo.InvariantCulture;
            var route = new Dictionary<string, string?>
            {
                ["listingType"] = c.ListingType?.ToString(),
                ["propertyType"] = c.PropertyType?.ToString(),
                ["city"] = c.City,
                ["minPrice"] = c.MinPrice?.ToString(inv),
                ["maxPrice"] = c.MaxPrice?.ToString(inv),
                ["minBedrooms"] = c.MinBedrooms?.ToString(inv),
                ["minBathrooms"] = c.MinBathrooms?.ToString(inv),
                ["minSize"] = c.MinSize?.ToString(inv),
                ["maxSize"] = c.MaxSize?.ToString(inv),
                ["hasParking"] = c.HasParking == true ? "true" : null,
                ["furnished"] = c.Furnished?.ToString().ToLowerInvariant()
            };

            var query = string.Join("&", route
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value!)}")
                .Concat(c.Keywords.Select(k => $"keywords={Uri.EscapeDataString(k)}")));

            return Redirect(Url.Action("Index", "Properties") + (query.Length > 0 ? "?" + query : ""));
        }
    }
}

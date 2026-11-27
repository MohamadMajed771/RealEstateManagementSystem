using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RealEstateMarketplace.Data;
using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Services.AiSearch
{
    public class AiSearchOptions
    {
        /// <summary>
        /// Any OpenAI-compatible chat endpoint. Free options:
        ///  - Google Gemini free tier: https://generativelanguage.googleapis.com/v1beta/openai/
        ///  - Ollama on this machine:  http://localhost:11434/v1/  (no key)
        /// Leave empty to use only the built-in rule-based parser.
        /// </summary>
        public string? BaseUrl { get; set; }
        public string? ApiKey { get; set; }
        public string Model { get; set; } = "gemini-2.5-flash";
        public int TimeoutSeconds { get; set; } = 20;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(BaseUrl) &&
            (!string.IsNullOrWhiteSpace(ApiKey) || BaseUrl.Contains("localhost") || BaseUrl.Contains("127.0.0.1"));
    }

    /// <summary>
    /// Turns a sentence into search filters. The AI only extracts filters;
    /// the listings always come from our database, so nothing is invented.
    /// Only the search sentence is sent to the AI provider, never personal data.
    /// </summary>
    public class AiSearchService
    {
        private readonly HttpClient _http;
        private readonly AiSearchOptions _options;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AiSearchService> _logger;

        public AiSearchService(HttpClient http, Microsoft.Extensions.Options.IOptions<AiSearchOptions> options,
            ApplicationDbContext context, ILogger<AiSearchService> logger)
        {
            _http = http;
            _options = options.Value;
            _context = context;
            _logger = logger;
            _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 60));
        }

        public async Task<SearchInterpretation> InterpretAsync(string text, CancellationToken ct = default)
        {
            text = text.Trim();
            if (text.Length > 500) text = text[..500];

            var places = await KnownPlacesAsync(ct);

            if (_options.IsConfigured)
            {
                try
                {
                    var answer = await AskModelAsync(text, places, ct);
                    if (answer != null)
                        return InterpretationBuilder.FromAnswer(answer, places, "ai");
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
                {
                    _logger.LogWarning(ex, "AI search provider failed; using the rule-based parser.");
                }
            }

            var result = RuleBasedSearchParser.Parse(text, places);
            if (_options.IsConfigured)
                result.Notes.Add("The AI service did not answer, so a simpler built-in reader was used.");
            return result;
        }

        private async Task<List<string>> KnownPlacesAsync(CancellationToken ct)
        {
            var approved = _context.PropertyListings
                .Where(l => l.Status == ListingStatus.Approved && !l.IsDeleted)
                .Select(l => l.Property);

            var cities = await approved.Select(p => p.City).Distinct().Take(200).ToListAsync(ct);
            var areas = await approved.Select(p => p.Area).Distinct().Take(200).ToListAsync(ct);
            return cities.Concat(areas).Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private async Task<LlmSearchAnswer?> AskModelAsync(string text, List<string> places, CancellationToken ct)
        {
            var system =
                "You convert a property search request into JSON filters for a real-estate marketplace. " +
                "Reply with ONE JSON object only, no prose, with these keys (use null when not stated): " +
                "listingType (\"Sale\" or \"Rent\"), propertyType (\"Apartment\", \"House\", \"Office\", \"Garage\" or \"Land\"), " +
                "city (must be exactly one of the known places below, else null), minPrice, maxPrice (numbers, same currency as stated, " +
                "monthly for rentals), minBedrooms, minBathrooms (integers), minSize, maxSize (square meters), " +
                "hasParking (true/null), furnished (true/false/null), " +
                "keywords (array of at most 6 short lowercase words for soft preferences such as \"quiet\", \"mountain\", \"sea\", \"garden\"; " +
                "use singular words), rentalPeriod (short text like \"summer\" or \"12 months\", else null). " +
                "Never invent properties or values the user did not ask for. " +
                "Known places: " + (places.Count == 0 ? "(none)" : string.Join(", ", places.Take(150)));

            var body = new
            {
                model = _options.Model,
                temperature = 0,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = text }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post,
                new Uri(new Uri(_options.BaseUrl!.TrimEnd('/') + "/"), "chat/completions"))
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AI search provider returned {Status}.", (int)response.StatusCode);
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content)) return null;

            // Some models wrap JSON in ``` fences: keep the outermost object
            int start = content.IndexOf('{'), end = content.LastIndexOf('}');
            if (start < 0 || end <= start) return null;

            return JsonSerializer.Deserialize<LlmSearchAnswer>(content[start..(end + 1)],
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString });
        }
    }
}

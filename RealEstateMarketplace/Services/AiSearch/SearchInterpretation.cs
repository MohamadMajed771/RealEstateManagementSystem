using RealEstateMarketplace.Models;

namespace RealEstateMarketplace.Services.AiSearch
{
    /// <summary>
    /// What the assistant understood from the user's sentence. Only these
    /// fields become database filters, so results are always real listings.
    /// </summary>
    public class SearchInterpretation
    {
        public PropertySearchCriteria Criteria { get; set; } = new();
        public List<string> Understood { get; set; } = new();
        public List<string> Notes { get; set; } = new();
        public string Source { get; set; } = "rules";
    }

    /// <summary>Raw structured answer from the language model (all optional).</summary>
    public class LlmSearchAnswer
    {
        public string? ListingType { get; set; }
        public string? PropertyType { get; set; }
        public string? City { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int? MinBedrooms { get; set; }
        public int? MinBathrooms { get; set; }
        public decimal? MinSize { get; set; }
        public decimal? MaxSize { get; set; }
        public bool? HasParking { get; set; }
        public bool? Furnished { get; set; }
        public List<string>? Keywords { get; set; }
        public string? RentalPeriod { get; set; }
    }

    public static class InterpretationBuilder
    {
        /// <summary>
        /// Validates model output: enums must exist, numbers must be sane,
        /// a city is kept only if a real listing uses it, keywords are
        /// short words used for ranking only.
        /// </summary>
        public static SearchInterpretation FromAnswer(LlmSearchAnswer a, IReadOnlyCollection<string> knownPlaces, string source)
        {
            var c = new PropertySearchCriteria();
            var notes = new List<string>();

            if (Enum.TryParse<ListingType>(a.ListingType, true, out var lt) && Enum.IsDefined(lt)) c.ListingType = lt;
            if (Enum.TryParse<PropertyType>(a.PropertyType, true, out var pt) && Enum.IsDefined(pt)) c.PropertyType = pt;

            if (!string.IsNullOrWhiteSpace(a.City))
            {
                var match = knownPlaces.FirstOrDefault(p => string.Equals(p, a.City.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match != null) c.City = match;
                else
                {
                    notes.Add($"No listings are in “{a.City.Trim()}” yet, so the place was used as a preference only.");
                    (a.Keywords ??= new()).Add(a.City.Trim());
                }
            }

            c.MinPrice = Positive(a.MinPrice);
            c.MaxPrice = Positive(a.MaxPrice);
            if (c.MinPrice > c.MaxPrice) (c.MinPrice, c.MaxPrice) = (c.MaxPrice, c.MinPrice);
            c.MinBedrooms = a.MinBedrooms is >= 0 and <= 50 ? a.MinBedrooms : null;
            c.MinBathrooms = a.MinBathrooms is >= 0 and <= 50 ? a.MinBathrooms : null;
            c.MinSize = Positive(a.MinSize);
            c.MaxSize = Positive(a.MaxSize);
            c.HasParking = a.HasParking == true ? true : null;
            c.Furnished = a.Furnished;

            c.Keywords = (a.Keywords ?? new())
                .Select(k => new string((k ?? "").Where(ch => char.IsLetter(ch) || ch == ' ' || ch == '-').ToArray()).Trim().ToLowerInvariant())
                .Where(k => k.Length is >= 3 and <= 30)
                .Distinct()
                .Take(6)
                .ToList();

            if (!string.IsNullOrWhiteSpace(a.RentalPeriod))
                notes.Add($"Rental period “{a.RentalPeriod.Trim()}”: check the minimum rental period on each listing.");

            return new SearchInterpretation
            {
                Criteria = c,
                Understood = Describe(c),
                Notes = notes,
                Source = source
            };
        }

        public static List<string> Describe(PropertySearchCriteria c)
        {
            var list = new List<string>();
            if (c.ListingType != null) list.Add(c.ListingType == ListingType.Rent ? "For rent" : "For sale");
            if (c.PropertyType != null) list.Add(c.PropertyType.ToString()!);
            if (c.City != null) list.Add("In " + c.City);
            if (c.MinPrice != null && c.MaxPrice != null) list.Add($"Price {c.MinPrice:N0}–{c.MaxPrice:N0}");
            else if (c.MaxPrice != null) list.Add($"Budget up to {c.MaxPrice:N0}" + (c.ListingType == ListingType.Rent ? " / month" : ""));
            else if (c.MinPrice != null) list.Add($"Price from {c.MinPrice:N0}");
            if (c.MinBedrooms != null) list.Add($"At least {c.MinBedrooms} bedroom(s)");
            if (c.MinBathrooms != null) list.Add($"At least {c.MinBathrooms} bathroom(s)");
            if (c.MinSize != null) list.Add($"From {c.MinSize:N0} m²");
            if (c.MaxSize != null) list.Add($"Up to {c.MaxSize:N0} m²");
            if (c.HasParking == true) list.Add("With parking");
            if (c.Furnished == true) list.Add("Furnished");
            if (c.Furnished == false) list.Add("Unfurnished");
            return list;
        }

        private static decimal? Positive(decimal? v) => v is > 0 and < 10_000_000_000m ? v : null;
    }
}

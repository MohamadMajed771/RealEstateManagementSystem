using System.Globalization;
using System.Text.RegularExpressions;

namespace RealEstateMarketplace.Services.AiSearch
{
    /// <summary>
    /// Offline parser used when no AI endpoint is configured or the AI
    /// call fails. Understands common English phrasing.
    /// </summary>
    public static class RuleBasedSearchParser
    {
        private static readonly Dictionary<string, int> NumberWords = new(StringComparer.OrdinalIgnoreCase)
        {
            ["one"] = 1, ["a"] = 1, ["single"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5,
            ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10
        };

        private static readonly (string Word, string Type)[] TypeWords =
        {
            ("apartment", "Apartment"), ("flat", "Apartment"), ("studio", "Apartment"), ("condo", "Apartment"),
            ("house", "House"), ("villa", "House"), ("home", "House"), ("chalet", "House"), ("cottage", "House"),
            ("office", "Office"), ("workspace", "Office"),
            ("garage", "Garage"), ("parking spot", "Garage"),
            ("land", "Land"), ("plot", "Land")
        };

        // Soft preferences: used only to rank listings whose text mentions them
        private static readonly (string Pattern, string Keyword)[] Preferences =
        {
            (@"\bquiet\b|\bcalm\b|\bpeaceful\b", "quiet"),
            (@"\bmountains?\b", "mountain"), (@"\bsea\b|\bseaside\b|\bbeach\b|\bcoast", "sea"),
            (@"\bview\b", "view"), (@"\bgarden\b", "garden"), (@"\bbalcony\b", "balcony"),
            (@"\bterrace\b", "terrace"), (@"\bpool\b", "pool"), (@"\bcity cent(er|re)\b|\bdowntown\b", "center"),
            (@"\bnew\b|\bmodern\b|\brenovated\b", "renovated"), (@"\belevator\b|\blift\b", "elevator"),
            (@"\bsunny\b|\bbright\b", "sunny"), (@"\bfamily\b", "family"), (@"\buniversity\b|\bcampus\b", "university")
        };

        public static SearchInterpretation Parse(string text, IReadOnlyCollection<string> knownPlaces)
        {
            var t = " " + (text ?? "").ToLowerInvariant() + " ";
            var a = new LlmSearchAnswer { Keywords = new() };

            if (Regex.IsMatch(t, @"\b(rent|rental|renting|lease|tenant|per month|/ ?month|monthly|a month)\b")) a.ListingType = "Rent";
            else if (Regex.IsMatch(t, @"\b(buy|buying|purchase|for sale|own|invest)\b")) a.ListingType = "Sale";

            foreach (var (word, type) in TypeWords)
                if (Regex.IsMatch(t, $@"\b{Regex.Escape(word)}s?\b")) { a.PropertyType = type; break; }

            // Budget: "$1,000", "1000 usd", "1.5k", "2 million", "under 800", "between 500 and 900"
            var between = Regex.Match(t, @"between\s+\$?\s*([\d.,]+\s*(k\b|m\b|million\b|thousand\b)?)\s*(\$|usd|dollars)?\s+and\s+\$?\s*([\d.,]+\s*(k\b|m\b|million\b|thousand\b)?)");
            if (between.Success)
            {
                a.MinPrice = Amount(between.Groups[1].Value);
                a.MaxPrice = Amount(between.Groups[4].Value);
            }
            else
            {
                var max = Regex.Match(t, @"(budget(\s+is)?(\s+of)?|under|below|less than|up to|max(imum)?|no more than|at most|around|about)\s*:?\s*\$?\s*([\d.,]+\s*(k\b|m\b|million\b|thousand\b)?)");
                var money = Regex.Match(t, @"\$\s*([\d.,]+\s*(k\b|m\b|million\b|thousand\b)?)|([\d.,]+\s*(k\b|m\b|million\b|thousand\b)?)\s*(\$|usd|dollars)");
                if (max.Success) a.MaxPrice = Amount(max.Groups[5].Value);
                else if (money.Success) a.MaxPrice = Amount(money.Groups[1].Success && money.Groups[1].Value.Length > 0 ? money.Groups[1].Value : money.Groups[3].Value);

                var min = Regex.Match(t, @"(more than|over|above|at least|from|minimum)\s*\$\s*([\d.,]+\s*(k\b|m\b|million\b|thousand\b)?)");
                if (min.Success) a.MinPrice = Amount(min.Groups[2].Value);
            }

            a.MinBedrooms = Count(t, @"bed(room)?s?|br\b");
            a.MinBathrooms = Count(t, @"bath(room)?s?");

            var size = Regex.Match(t, @"(\d{2,6})\s*(m2|m²|sqm|square met(er|re)s?)");
            if (size.Success)
            {
                var value = decimal.Parse(size.Groups[1].Value, CultureInfo.InvariantCulture);
                if (Regex.IsMatch(t, @"(under|less than|up to|max(imum)?|at most)\s*" + size.Groups[1].Value)) a.MaxSize = value;
                else a.MinSize = value;
            }

            if (Regex.IsMatch(t, @"\b(parking|garage space|car space|park my car)\b") && a.PropertyType != "Garage") a.HasParking = true;
            if (Regex.IsMatch(t, @"\bunfurnished\b|\bnot furnished\b|\bempty\b")) a.Furnished = false;
            else if (Regex.IsMatch(t, @"\bfurnished\b")) a.Furnished = true;

            // Place: any city / area that real listings use
            a.City = knownPlaces
                .Where(p => p.Length >= 3)
                .OrderByDescending(p => p.Length)
                .FirstOrDefault(p => Regex.IsMatch(t, $@"\b{Regex.Escape(p.ToLowerInvariant())}\b"));

            foreach (var (pattern, keyword) in Preferences)
                if (Regex.IsMatch(t, pattern)) a.Keywords.Add(keyword);

            var period = Regex.Match(t, @"\b(summer|winter|spring|autumn|fall|short[- ]term|long[- ]term|\d+\s*months?|one year|a year|\d+\s*years?)\b");
            if (period.Success && a.ListingType == "Rent") a.RentalPeriod = period.Value.Trim();

            return InterpretationBuilder.FromAnswer(a, knownPlaces, "rules");
        }

        private static int? Count(string t, string nounPattern)
        {
            var m = Regex.Match(t, $@"(at least|minimum|min|\b)\s*(\d+|one|two|three|four|five|six|seven|eight|nine|ten|single|a)\s*\+?\s*-?\s*({nounPattern})");
            if (!m.Success) return null;
            var raw = m.Groups[2].Value;
            return int.TryParse(raw, out var n) ? n : NumberWords.GetValueOrDefault(raw);
        }

        private static decimal? Amount(string raw)
        {
            raw = raw.Trim().ToLowerInvariant();
            decimal multiplier = 1;
            if (raw.EndsWith("million")) { multiplier = 1_000_000; raw = raw[..^7]; }
            else if (raw.EndsWith("thousand")) { multiplier = 1_000; raw = raw[..^8]; }
            else if (raw.EndsWith("k")) { multiplier = 1_000; raw = raw[..^1]; }
            else if (raw.EndsWith("m")) { multiplier = 1_000_000; raw = raw[..^1]; }

            raw = raw.Trim().TrimEnd('.', ',');
            // "1,000" / "1.000" thousands separators vs "1.5k" decimals
            if (multiplier == 1) raw = raw.Replace(",", "").Replace(".", "");
            else raw = raw.Replace(",", ".");

            return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && v > 0
                ? v * multiplier
                : null;
        }
    }
}

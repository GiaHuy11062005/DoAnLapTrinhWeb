using System.Text.RegularExpressions;

namespace DoAnLapTrinhWeb.Models
{
    public static class GoogleMapsUrlHelper
    {
        private static readonly Regex AtCoordinates = new(
            @"@(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)",
            RegexOptions.Compiled);

        private static readonly Regex DataCoordinates = new(
            @"!3d(-?\d+(?:\.\d+)?).*?!4d(-?\d+(?:\.\d+)?)",
            RegexOptions.Compiled);

        private static readonly Regex QueryCoordinates = new(
            @"[?&](?:q|query)=(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string GetDetailedUrl(string? googleMapsUrl, string fallbackAddress)
        {
            if (string.IsNullOrWhiteSpace(googleMapsUrl))
            {
                return $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(fallbackAddress)}";
            }

            var coordinates = GetCoordinates(googleMapsUrl);
            return string.IsNullOrWhiteSpace(coordinates)
                ? googleMapsUrl
                : $"https://www.google.com/maps?q={coordinates}&z=19";
        }

        public static string GetMapQuery(string? googleMapsUrl, string fallbackAddress)
        {
            return GetCoordinates(googleMapsUrl) ?? fallbackAddress;
        }

        public static string GetEmbedUrl(string? googleMapsUrl, string fallbackAddress)
        {
            var query = Uri.EscapeDataString(GetMapQuery(googleMapsUrl, fallbackAddress));
            return $"https://www.google.com/maps?q={query}&z=19&output=embed";
        }

        public static bool TryGetCoordinates(string? googleMapsUrl, out double latitude, out double longitude)
        {
            latitude = 0;
            longitude = 0;
            if (string.IsNullOrWhiteSpace(googleMapsUrl))
            {
                return false;
            }

            var decodedUrl = Uri.UnescapeDataString(googleMapsUrl);

            // !3d/!4d is normally the marker location; @lat,long may only be the map center.
            var match = DataCoordinates.Match(decodedUrl);
            if (!match.Success) match = QueryCoordinates.Match(decodedUrl);
            if (!match.Success) match = AtCoordinates.Match(decodedUrl);

            return match.Success &&
                   double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out latitude) &&
                   double.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out longitude);
        }

        private static string? GetCoordinates(string? googleMapsUrl)
        {
            return TryGetCoordinates(googleMapsUrl, out var latitude, out var longitude)
                ? $"{latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                : null;
        }
    }
}

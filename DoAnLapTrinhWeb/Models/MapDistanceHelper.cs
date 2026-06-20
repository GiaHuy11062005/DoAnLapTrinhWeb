using System.Globalization;
using System.Text;

namespace DoAnLapTrinhWeb.Models
{
    public static class MapDistanceHelper
    {
        private static readonly Dictionary<string, (double Latitude, double Longitude)> CityCenters = new()
        {
            ["ha noi"] = (21.028511, 105.804817),
            ["thanh pho ha noi"] = (21.028511, 105.804817),
            ["ho chi minh"] = (10.776889, 106.700806),
            ["thanh pho ho chi minh"] = (10.776889, 106.700806),
            ["da nang"] = (16.054407, 108.202164),
            ["hai phong"] = (20.844912, 106.688084),
            ["can tho"] = (10.045162, 105.746857),
            ["hue"] = (16.463713, 107.590866),
            ["thua thien hue"] = (16.463713, 107.590866),
            ["nha trang"] = (12.238791, 109.196749),
            ["khanh hoa"] = (12.238791, 109.196749),
            ["da lat"] = (11.940419, 108.458313),
            ["lam dong"] = (11.940419, 108.458313),
            ["bien hoa"] = (10.957413, 106.842687),
            ["dong nai"] = (10.957413, 106.842687),
            ["vung tau"] = (10.411379, 107.136224),
            ["ba ria vung tau"] = (10.411379, 107.136224),
            ["binh duong"] = (10.980460, 106.651930),
            ["thu dau mot"] = (10.980460, 106.651930),
            ["quy nhon"] = (13.782967, 109.219663),
            ["binh dinh"] = (13.782967, 109.219663),
            ["vinh"] = (18.679585, 105.681335),
            ["nghe an"] = (18.679585, 105.681335),
            ["ha giang"] = (22.823300, 104.983600),
            ["cao bang"] = (22.666700, 106.250000),
            ["bac kan"] = (22.147000, 105.834800),
            ["tuyen quang"] = (21.823600, 105.218100),
            ["lao cai"] = (22.485600, 103.970700),
            ["dien bien"] = (21.386000, 103.023000),
            ["lai chau"] = (22.386200, 103.470300),
            ["son la"] = (21.325600, 103.918800),
            ["yen bai"] = (21.716800, 104.898600),
            ["hoa binh"] = (20.813300, 105.338300),
            ["thai nguyen"] = (21.594200, 105.848200),
            ["lang son"] = (21.853700, 106.761500),
            ["bac giang"] = (21.281900, 106.197500),
            ["phu tho"] = (21.322700, 105.401900),
            ["vinh phuc"] = (21.308900, 105.604900),
            ["bac ninh"] = (21.186100, 106.076300),
            ["quang ninh"] = (20.950000, 107.073400),
            ["hai duong"] = (20.937300, 106.314600),
            ["hung yen"] = (20.646400, 106.051100),
            ["thai binh"] = (20.446300, 106.336600),
            ["ha nam"] = (20.583500, 105.923000),
            ["nam dinh"] = (20.438800, 106.162100),
            ["ninh binh"] = (20.250600, 105.974500),
            ["thanh hoa"] = (19.806700, 105.785200),
            ["ha tinh"] = (18.355900, 105.887700),
            ["quang binh"] = (17.468900, 106.622300),
            ["quang tri"] = (16.816300, 107.100300),
            ["quang nam"] = (15.573600, 108.474000),
            ["quang ngai"] = (15.120500, 108.792300),
            ["phu yen"] = (13.095500, 109.320900),
            ["ninh thuan"] = (11.582600, 108.991200),
            ["binh thuan"] = (10.928900, 108.102100),
            ["kon tum"] = (14.349700, 108.000500),
            ["gia lai"] = (13.983300, 108.000000),
            ["dak lak"] = (12.666700, 108.050000),
            ["dak nong"] = (12.004000, 107.690700),
            ["binh phuoc"] = (11.534400, 106.883200),
            ["tay ninh"] = (11.335200, 106.109900),
            ["long an"] = (10.535000, 106.413700),
            ["tien giang"] = (10.360000, 106.360000),
            ["ben tre"] = (10.241500, 106.375900),
            ["tra vinh"] = (9.934700, 106.345300),
            ["vinh long"] = (10.253700, 105.972200),
            ["dong thap"] = (10.493800, 105.688200),
            ["an giang"] = (10.375900, 105.418500),
            ["kien giang"] = (10.012500, 105.080900),
            ["hau giang"] = (9.784500, 105.470100),
            ["soc trang"] = (9.602500, 105.973900),
            ["bac lieu"] = (9.294000, 105.727800),
            ["ca mau"] = (9.176900, 105.152400)
        };

        private static readonly Dictionary<string, (double Latitude, double Longitude)> DistrictCenters = new()
        {
            ["thanh pho ho chi minh|quan 1"] = (10.775658, 106.700424),
            ["thanh pho ho chi minh|quan 3"] = (10.784369, 106.684408),
            ["thanh pho ho chi minh|quan 4"] = (10.757826, 106.701297),
            ["thanh pho ho chi minh|quan 5"] = (10.754027, 106.663374),
            ["thanh pho ho chi minh|quan 6"] = (10.746903, 106.634495),
            ["thanh pho ho chi minh|quan 7"] = (10.734034, 106.721578),
            ["thanh pho ho chi minh|quan 8"] = (10.724088, 106.628625),
            ["thanh pho ho chi minh|quan 10"] = (10.774596, 106.667954),
            ["thanh pho ho chi minh|quan 11"] = (10.762973, 106.650084),
            ["thanh pho ho chi minh|quan 12"] = (10.867153, 106.641332),
            ["thanh pho ho chi minh|quan binh thanh"] = (10.805493, 106.694821),
            ["thanh pho ho chi minh|quan go vap"] = (10.838678, 106.665290),
            ["thanh pho ho chi minh|quan phu nhuan"] = (10.799194, 106.680263),
            ["thanh pho ho chi minh|quan tan binh"] = (10.801465, 106.652597),
            ["thanh pho ho chi minh|quan tan phu"] = (10.790051, 106.628190),
            ["thanh pho ho chi minh|quan binh tan"] = (10.765258, 106.603853),
            ["thanh pho ho chi minh|thanh pho thu duc"] = (10.849409, 106.753705),
            ["thanh pho ho chi minh|thu duc"] = (10.849409, 106.753705),
            ["thanh pho ho chi minh|huyen binh chanh"] = (10.687392, 106.593854),
            ["thanh pho ho chi minh|huyen can gio"] = (10.411380, 106.954741),
            ["thanh pho ho chi minh|huyen cu chi"] = (10.973602, 106.493392),
            ["thanh pho ho chi minh|huyen hoc mon"] = (10.889814, 106.595382),
            ["thanh pho ho chi minh|huyen nha be"] = (10.695264, 106.704874)
        };

        public static bool TryGetCityCenter(string? cityName, out double latitude, out double longitude)
        {
            latitude = 0;
            longitude = 0;
            if (string.IsNullOrWhiteSpace(cityName)) return false;

            var normalized = Normalize(cityName)
                .Replace("tinh ", "", StringComparison.Ordinal)
                .Replace("thanh pho ", "", StringComparison.Ordinal)
                .Replace("tp ", "", StringComparison.Ordinal);
            if (!CityCenters.TryGetValue(normalized, out var center)) return false;

            latitude = center.Latitude;
            longitude = center.Longitude;
            return true;
        }

        public static bool TryGetDistrictCenter(string? cityName, string? districtName, out double latitude, out double longitude)
        {
            latitude = 0;
            longitude = 0;
            if (string.IsNullOrWhiteSpace(cityName) || string.IsNullOrWhiteSpace(districtName)) return false;

            var cityKey = Normalize(cityName);
            var districtKey = Normalize(districtName);
            var compactDistrictKey = districtKey
                .Replace("quan ", "", StringComparison.Ordinal)
                .Replace("huyen ", "", StringComparison.Ordinal)
                .Replace("thanh pho ", "", StringComparison.Ordinal)
                .Replace("thi xa ", "", StringComparison.Ordinal);

            var keys = new[]
            {
                $"{cityKey}|{districtKey}",
                $"{cityKey}|{compactDistrictKey}"
            };

            foreach (var key in keys)
            {
                if (!DistrictCenters.TryGetValue(key, out var center)) continue;
                latitude = center.Latitude;
                longitude = center.Longitude;
                return true;
            }

            return false;
        }

        public static double CalculateKm(double latitude1, double longitude1, double latitude2, double longitude2)
        {
            const double earthRadiusKm = 6371.0088;
            static double ToRadians(double value) => value * Math.PI / 180;

            var latitudeDelta = ToRadians(latitude2 - latitude1);
            var longitudeDelta = ToRadians(longitude2 - longitude1);
            var a = Math.Pow(Math.Sin(latitudeDelta / 2), 2) +
                    Math.Cos(ToRadians(latitude1)) * Math.Cos(ToRadians(latitude2)) *
                    Math.Pow(Math.Sin(longitudeDelta / 2), 2);
            return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static string Normalize(string value)
        {
            var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();
            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    var normalizedCharacter = character == 'đ' ? 'd' : character;
                    builder.Append(char.IsLetterOrDigit(normalizedCharacter) ? normalizedCharacter : ' ');
                }
            }
            return string.Join(" ", builder.ToString().Normalize(NormalizationForm.FormC)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }
    }
}

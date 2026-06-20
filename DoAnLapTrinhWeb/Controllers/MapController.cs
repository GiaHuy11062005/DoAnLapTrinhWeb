using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DoAnLapTrinhWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DoAnLapTrinhWeb.Controllers
{
    public class MapController : Controller
    {
        private const string UserAgent = "DoAnLapTrinhWeb/1.0 (student rental accommodation project)";
        private static readonly string[] OverpassEndpoints =
        {
            "https://overpass-api.de/api/interpreter",
            "https://overpass.kumi.systems/api/interpreter"
        };
        private static readonly SchoolSeed[] HoChiMinhSchoolSeeds =
        {
            new(
                "Viện Công Nghệ Cao HUTECH",
                10.8407036,
                106.8088695,
                "HUTECH · Khu công nghệ cao TP.HCM",
                "hutech dai hoc cong nghe thanh pho ho chi minh"),
            new(
                "Trường Đại học Hutech - KCN Cao",
                10.8550569,
                106.7853078,
                "HUTECH · Khu công nghệ cao",
                "hutech dai hoc cong nghe thanh pho ho chi minh"),
            new(
                "Trường Ngoại Ngữ Tin Học HUFLIT",
                10.8654655,
                106.6010124,
                "HUFLIT · TP.HCM",
                "huflit dai hoc ngoai ngu tin hoc thanh pho ho chi minh"),
            new(
                "Trường Đại học FPT Thành phố Hồ Chí Minh",
                10.8414168,
                106.8100745,
                "FPT University · Khu công nghệ cao TP.HCM",
                "fpt university dai hoc fpt thanh pho ho chi minh"),
            new(
                "Trường Đại học Khoa học Tự nhiên - ĐHQG TP.HCM - Cơ sở 1",
                10.7625844,
                106.6816948,
                "HCMUS · Nguyễn Văn Cừ",
                "hcmus dai hoc khoa hoc tu nhien dhqg thanh pho ho chi minh")
        };
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;

        public MapController(IHttpClientFactory httpClientFactory, IMemoryCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
        }

        [HttpGet]
        public IActionResult PreviewRoute(
            double originLat,
            double originLon,
            double destinationLat,
            double destinationLon,
            string? destinationName)
        {
            if (!IsValidCoordinate(originLat, originLon) || !IsValidCoordinate(destinationLat, destinationLon))
                return BadRequest("Tọa độ tuyến đường chưa hợp lệ.");

            ViewBag.OriginLat = originLat;
            ViewBag.OriginLon = originLon;
            ViewBag.DestinationLat = destinationLat;
            ViewBag.DestinationLon = destinationLon;
            ViewBag.DestinationName = destinationName ?? "Điểm đến";
            return View();
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> SearchSchool(
            string query,
            double originLat,
            double originLon,
            string? cityName)
        {
            query = query?.Trim() ?? string.Empty;
            if (query.Length < 1)
                return BadRequest(new { success = false, message = "Vui lòng nhập tên trường." });
            if (query.Length > 120)
                return BadRequest(new { success = false, message = "Tên trường quá dài." });
            if (!IsValidCoordinate(originLat, originLon))
                return BadRequest(new { success = false, message = "Vị trí phòng trọ chưa hợp lệ." });

            var centerLatitude = originLat;
            var centerLongitude = originLon;
            if (MapDistanceHelper.TryGetCityCenter(cityName, out var cityLatitude, out var cityLongitude))
            {
                centerLatitude = cityLatitude;
                centerLongitude = cityLongitude;
            }

            const int radius = 50000;
            var normalizedQuery = NormalizeSearchText(query);
            var cacheKey =
                $"osm-school-search:v4:{normalizedQuery}:{Math.Round(centerLatitude, 2)}:{Math.Round(centerLongitude, 2)}:{radius}";
            if (_cache.TryGetValue(cacheKey, out object? cachedPayload) && cachedPayload != null)
                return Ok(cachedPayload);

            try
            {
                var schools = GetSeedSchools(cityName, centerLatitude, centerLongitude, normalizedQuery);

                if (schools.Count == 0 && query.Length >= 3)
                {
                    schools.AddRange(await TryFetchNominatimSchoolsAsync(
                        query,
                        cityName,
                        centerLatitude,
                        centerLongitude,
                        HttpContext.RequestAborted));
                }

                if (schools.Count == 0 && query.Length >= 4)
                {
                    try
                    {
                        schools = await FetchMatchingSchoolsAsync(
                            query,
                            centerLatitude,
                            centerLongitude,
                            radius,
                            HttpContext.RequestAborted);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        schools = new List<SchoolPlace>();
                    }
                }

                var results = schools
                    .GroupBy(school => $"{school.Name}|{Math.Round(school.Latitude, 5)}|{Math.Round(school.Longitude, 5)}")
                    .Select(group => group.First())
                    .Where(school => MatchesSchoolQuery(school.SearchText, normalizedQuery))
                    .Select(school => new MapPlace(
                        school.Name,
                        school.Latitude,
                        school.Longitude,
                        Math.Round(MapDistanceHelper.CalculateKm(
                            originLat,
                            originLon,
                            school.Latitude,
                            school.Longitude), 2),
                        school.Subtitle))
                    .OrderBy(school => school.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(school => school.DistanceKm)
                    .ThenBy(school => school.Name)
                    .ToList();

                var payload = new
                {
                    success = true,
                    results,
                    areaName = cityName,
                    totalMatches = results.Count
                };
                _cache.Set(
                    cacheKey,
                    payload,
                    results.Count > 0 ? TimeSpan.FromMinutes(30) : TimeSpan.FromMinutes(5));
                return Ok(payload);
            }
            catch (OperationCanceledException)
            {
                return new EmptyResult();
            }
            catch (Exception)
            {
                return StatusCode(503, new { success = false, message = "Dịch vụ tìm trường đang bận. Vui lòng thử lại sau." });
            }
        }

        private async Task<List<SchoolPlace>> TryFetchNominatimSchoolsAsync(
            string query,
            string? cityName,
            double centerLatitude,
            double centerLongitude,
            CancellationToken cancellationToken)
        {
            try
            {
                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(TimeSpan.FromSeconds(3));
                return await FetchNominatimSchoolFallbackAsync(
                    query,
                    cityName,
                    centerLatitude,
                    centerLongitude,
                    timeoutSource.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                return new List<SchoolPlace>();
            }
            catch
            {
                return new List<SchoolPlace>();
            }
        }

        private static List<SchoolPlace> GetSeedSchools(
            string? cityName,
            double centerLatitude,
            double centerLongitude,
            string normalizedQuery)
        {
            var normalizedCity = NormalizeSearchText(cityName ?? string.Empty);
            var isHoChiMinh =
                string.IsNullOrWhiteSpace(normalizedCity) ||
                normalizedCity.Contains("ho chi minh", StringComparison.Ordinal) ||
                normalizedCity.Contains("hcm", StringComparison.Ordinal);
            if (!isHoChiMinh) return new List<SchoolPlace>();

            return HoChiMinhSchoolSeeds
                .Where(school => MatchesSchoolQuery(NormalizeSearchText(school.SearchText), normalizedQuery))
                .Select(school => new SchoolPlace(
                    school.Name,
                    school.Latitude,
                    school.Longitude,
                    Math.Round(MapDistanceHelper.CalculateKm(
                        centerLatitude,
                        centerLongitude,
                        school.Latitude,
                        school.Longitude), 2),
                    school.Subtitle,
                    NormalizeSearchText(school.SearchText)))
                .ToList();
        }

        private async Task<List<SchoolPlace>> FetchMatchingSchoolsAsync(
            string query,
            double centerLatitude,
            double centerLongitude,
            int radius,
            CancellationToken cancellationToken)
        {
            var escapedQuery = EscapeOverpassString(Regex.Escape(query).Replace("\\ ", " ", StringComparison.Ordinal));
            var searchableTags = query.Length <= 3
                ? new[] { "name", "short_name" }
                : new[]
                {
                    "name", "name:vi", "name:en", "short_name", "official_name", "alt_name"
                };
            var educationSources = new[]
            {
                ("nwr", "[\"amenity\"~\"school|college|university\"]"),
                ("way", "[\"building\"~\"school|college|university\"]"),
                ("relation", "[\"building\"~\"school|college|university\"]")
            };
            var queryLines = educationSources.SelectMany(source => searchableTags.Select(tag =>
                    $"{source.Item1}{source.Item2}[\"{tag}\"~\"{escapedQuery}\",i]" +
                    $"(around:{radius},{Format(centerLatitude)},{Format(centerLongitude)});"));
            var overpassQuery =
                "[out:json][timeout:20];(" +
                string.Join("", queryLines) +
                ");out center tags;";
            using var document = await GetOverpassDocumentAsync(overpassQuery, cancellationToken);

            return document.RootElement.GetProperty("elements").EnumerateArray()
                .Select(element => CreateSchoolPlace(element, centerLatitude, centerLongitude))
                .OfType<SchoolPlace>()
                .GroupBy(school => $"{school.Name}|{Math.Round(school.Latitude, 5)}|{Math.Round(school.Longitude, 5)}")
                .Select(group => group.First())
                .OrderBy(school => school.DistanceKm)
                .ToList();
        }

        private async Task<JsonDocument> GetOverpassDocumentAsync(
            string overpassQuery,
            CancellationToken cancellationToken)
        {
            Exception? lastError = null;
            foreach (var endpoint in OverpassEndpoints)
            {
                try
                {
                    var client = CreateClient();
                    using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["data"] = overpassQuery });
                    using var response = await client.PostAsync(endpoint, content, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception error)
                {
                    lastError = error;
                }
            }

            throw new HttpRequestException("Không thể kết nối dịch vụ dữ liệu OpenStreetMap.", lastError);
        }

        private async Task<List<SchoolPlace>> FetchNominatimSchoolFallbackAsync(
            string query,
            string? cityName,
            double centerLatitude,
            double centerLongitude,
            CancellationToken cancellationToken)
        {
            var client = CreateClient();
            var searchTexts = BuildNominatimSchoolQueries(query, cityName);
            var schools = new List<SchoolPlace>();

            foreach (var searchText in searchTexts)
            {
                var url =
                    "https://nominatim.openstreetmap.org/search" +
                    $"?q={Uri.EscapeDataString(searchText)}&format=jsonv2&limit=50&dedupe=0&countrycodes=vn&addressdetails=1";
                using var response = await client.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                schools.AddRange(ParseNominatimSchoolResults(document, query, centerLatitude, centerLongitude));

                if (schools.Any(school => MatchesSchoolQuery(school.SearchText, NormalizeSearchText(query))))
                    break;
            }

            return schools
                .GroupBy(school => $"{school.Name}|{Math.Round(school.Latitude, 5)}|{Math.Round(school.Longitude, 5)}")
                .Select(group => group.First())
                .ToList();
        }

        private static string[] BuildNominatimSchoolQueries(string query, string? cityName)
        {
            var location = string.Join(", ", new[] { cityName, "Việt Nam" }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            if (query.Length <= 3)
            {
                return new[]
                {
                    string.Join(", ", new[] { $"{query} [university]", location }
                        .Where(value => !string.IsNullOrWhiteSpace(value))),
                    string.Join(", ", new[] { $"{query} [college]", location }
                        .Where(value => !string.IsNullOrWhiteSpace(value)))
                };
            }

            return new[]
            {
                string.Join(", ", new[] { query, location }
                    .Where(value => !string.IsNullOrWhiteSpace(value))),
                string.Join(", ", new[] { $"{query} [university]", location }
                    .Where(value => !string.IsNullOrWhiteSpace(value)))
            };
        }

        private static List<SchoolPlace> ParseNominatimSchoolResults(
            JsonDocument document,
            string query,
            double centerLatitude,
            double centerLongitude)
        {
            return document.RootElement.EnumerateArray()
                .Select(element =>
                {
                    if (!TryParseCoordinate(element, "lat", out var latitude) ||
                        !TryParseCoordinate(element, "lon", out var longitude))
                        return null;
                    var displayText = element.TryGetProperty("display_name", out var displayName)
                        ? displayName.GetString()
                        : null;
                    var name = element.TryGetProperty("name", out var nameValue)
                        ? nameValue.GetString()
                        : displayText?.Split(',')[0];
                    var normalizedName = NormalizeSearchText(name ?? string.Empty);
                    var normalizedDisplayText = NormalizeSearchText(displayText ?? string.Empty);
                    var category = element.TryGetProperty("category", out var categoryValue)
                        ? categoryValue.GetString()
                        : null;
                    var type = element.TryGetProperty("type", out var typeValue)
                        ? typeValue.GetString()
                        : null;
                    var hasEducationName =
                        normalizedDisplayText.Contains("truong", StringComparison.Ordinal) ||
                        normalizedDisplayText.Contains("dai hoc", StringComparison.Ordinal) ||
                        normalizedDisplayText.Contains("university", StringComparison.Ordinal) ||
                        normalizedDisplayText.Contains("college", StringComparison.Ordinal) ||
                        normalizedDisplayText.Contains("hoc vien", StringComparison.Ordinal);
                    var isEducationPlace =
                        type is "school" or "college" or "university" ||
                        ((category is "amenity" or "building") && hasEducationName);
                    if (string.IsNullOrWhiteSpace(name) ||
                        !isEducationPlace ||
                        !MatchesSchoolQuery(normalizedName, NormalizeSearchText(query)))
                        return null;
                    return new SchoolPlace(
                        name,
                        latitude,
                        longitude,
                        Math.Round(MapDistanceHelper.CalculateKm(
                            centerLatitude,
                            centerLongitude,
                            latitude,
                            longitude), 2),
                        displayText,
                        normalizedName);
                })
                .OfType<SchoolPlace>()
                .ToList();
        }

        private static SchoolPlace? CreateSchoolPlace(
            JsonElement element,
            double centerLatitude,
            double centerLongitude)
        {
            var point = GetElementCoordinate(element);
            if (!point.HasValue || !element.TryGetProperty("tags", out var tags)) return null;

            var name = GetTag(tags, "name") ??
                       GetTag(tags, "official_name") ??
                       GetTag(tags, "short_name") ??
                       GetTag(tags, "operator");
            if (string.IsNullOrWhiteSpace(name)) return null;

            var shortName = GetTag(tags, "short_name");
            var branch = GetTag(tags, "branch") ?? GetTag(tags, "campus");
            var addressParts = new[]
            {
                GetTag(tags, "addr:housenumber"),
                GetTag(tags, "addr:street"),
                GetTag(tags, "addr:suburb"),
                GetTag(tags, "addr:district"),
                GetTag(tags, "addr:city")
            }.Where(value => !string.IsNullOrWhiteSpace(value));
            var address = string.Join(", ", addressParts);
            var subtitle = string.Join(" · ", new[]
                {
                    string.Equals(shortName, name, StringComparison.OrdinalIgnoreCase) ? null : shortName,
                    branch,
                    address
                }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            var searchableValues = new[]
            {
                name,
                GetTag(tags, "name:vi"),
                GetTag(tags, "name:en"),
                shortName,
                GetTag(tags, "official_name"),
                GetTag(tags, "alt_name"),
                GetTag(tags, "operator"),
                GetTag(tags, "brand"),
                GetTag(tags, "ref"),
                branch
            };

            return new SchoolPlace(
                name,
                point.Value.Latitude,
                point.Value.Longitude,
                Math.Round(MapDistanceHelper.CalculateKm(
                    centerLatitude,
                    centerLongitude,
                    point.Value.Latitude,
                    point.Value.Longitude), 2),
                string.IsNullOrWhiteSpace(subtitle) ? null : subtitle,
                NormalizeSearchText(string.Join(" ", searchableValues.Where(value => !string.IsNullOrWhiteSpace(value)))));
        }

        [HttpGet]
        public async Task<IActionResult> Nearby(double latitude, double longitude, string type, int radius = 2500)
        {
            if (!IsValidCoordinate(latitude, longitude))
                return BadRequest(new { success = false, message = "Vị trí phòng trọ chưa hợp lệ." });

            radius = Math.Clamp(radius, 500, 5000);
            var filters = GetOverpassFilters(type);
            if (filters.Count == 0)
                return BadRequest(new { success = false, message = "Loại tiện ích chưa được hỗ trợ." });

            var cacheKey = $"osm-nearby:{Math.Round(latitude, 4)}:{Math.Round(longitude, 4)}:{type}:{radius}";
            if (_cache.TryGetValue(cacheKey, out object? cachedResult) && cachedResult != null)
                return Ok(cachedResult);

            var queryLines = filters.Select(filter => $"nwr{filter}(around:{radius},{Format(latitude)},{Format(longitude)});");
            var overpassQuery = $"[out:json][timeout:20];({string.Join("", queryLines)});out center tags;";

            try
            {
                var client = CreateClient();
                using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["data"] = overpassQuery });
                using var response = await client.PostAsync("https://overpass-api.de/api/interpreter", content);
                response.EnsureSuccessStatusCode();
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

                var results = document.RootElement.GetProperty("elements").EnumerateArray()
                    .Select(element =>
                    {
                        var point = GetElementCoordinate(element);
                        var name = GetElementName(element);
                        if (!point.HasValue || string.IsNullOrWhiteSpace(name)) return null;
                        return new MapPlace(
                            name,
                            point.Value.Latitude,
                            point.Value.Longitude,
                            Math.Round(MapDistanceHelper.CalculateKm(
                                latitude,
                                longitude,
                                point.Value.Latitude,
                                point.Value.Longitude), 2));
                    })
                    .OfType<MapPlace>()
                    .OrderBy(item => item.DistanceKm)
                    .Take(12)
                    .ToList();

                var payload = new { success = true, results };
                _cache.Set(cacheKey, payload, TimeSpan.FromMinutes(30));
                return Ok(payload);
            }
            catch (Exception)
            {
                return StatusCode(503, new { success = false, message = "Dịch vụ tìm tiện ích đang bận. Vui lòng thử lại sau." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Route(
            double originLat,
            double originLon,
            double destinationLat,
            double destinationLon)
        {
            if (!IsValidCoordinate(originLat, originLon) || !IsValidCoordinate(destinationLat, destinationLon))
                return BadRequest(new { success = false, message = "Tọa độ tuyến đường chưa hợp lệ." });

            var cacheKey = $"osm-route:{Math.Round(originLat, 5)}:{Math.Round(originLon, 5)}:{Math.Round(destinationLat, 5)}:{Math.Round(destinationLon, 5)}";
            if (_cache.TryGetValue(cacheKey, out object? cachedResult) && cachedResult != null)
                return Ok(cachedResult);

            try
            {
                var client = CreateClient();
                var url =
                    "https://router.project-osrm.org/route/v1/driving/" +
                    $"{Format(originLon)},{Format(originLat)};{Format(destinationLon)},{Format(destinationLat)}" +
                    "?overview=full&geometries=geojson&steps=false";
                using var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var route = document.RootElement.GetProperty("routes")[0];
                var coordinates = route.GetProperty("geometry").GetProperty("coordinates")
                    .EnumerateArray()
                    .Select(point => new[] { point[1].GetDouble(), point[0].GetDouble() })
                    .ToList();

                var payload = new
                {
                    success = true,
                    distanceKm = Math.Round(route.GetProperty("distance").GetDouble() / 1000d, 2),
                    durationMinutes = (int)Math.Ceiling(route.GetProperty("duration").GetDouble() / 60d),
                    coordinates
                };
                _cache.Set(cacheKey, payload, TimeSpan.FromHours(2));
                return Ok(payload);
            }
            catch (Exception)
            {
                return StatusCode(503, new { success = false, message = "Dịch vụ tính đường đang bận. Vui lòng thử lại sau." });
            }
        }

        private HttpClient CreateClient()
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(25);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            return client;
        }

        private static List<string> GetOverpassFilters(string? type)
        {
            return type?.Trim().ToLowerInvariant() switch
            {
                "supermarket" => new() { "[\"shop\"=\"supermarket\"]", "[\"amenity\"=\"marketplace\"]" },
                "hospital" => new() { "[\"amenity\"~\"hospital|clinic|pharmacy\"]" },
                "bus_stop" => new() { "[\"highway\"=\"bus_stop\"]", "[\"public_transport\"=\"platform\"]" },
                "convenience_store" => new() { "[\"shop\"=\"convenience\"]" },
                "restaurant_cafe" => new() { "[\"amenity\"~\"restaurant|cafe|fast_food\"]" },
                "gym" => new() { "[\"leisure\"=\"fitness_centre\"]", "[\"sport\"=\"fitness\"]" },
                _ => new()
            };
        }

        private static (double Latitude, double Longitude)? GetElementCoordinate(JsonElement element)
        {
            if (element.TryGetProperty("lat", out var lat) && element.TryGetProperty("lon", out var lon))
                return (lat.GetDouble(), lon.GetDouble());
            if (element.TryGetProperty("center", out var center))
                return (center.GetProperty("lat").GetDouble(), center.GetProperty("lon").GetDouble());
            return null;
        }

        private static bool TryParseCoordinate(JsonElement element, string property, out double coordinate)
        {
            coordinate = 0;
            return element.TryGetProperty(property, out var value) &&
                   double.TryParse(
                       value.GetString(),
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out coordinate);
        }

        private static string? GetElementName(JsonElement element)
        {
            if (!element.TryGetProperty("tags", out var tags)) return null;
            if (tags.TryGetProperty("name", out var name)) return name.GetString();
            if (tags.TryGetProperty("brand", out var brand)) return brand.GetString();
            return null;
        }

        private static string? GetTag(JsonElement tags, string property)
        {
            return tags.TryGetProperty(property, out var value) ? value.GetString() : null;
        }

        private static string NormalizeSearchText(string value)
        {
            var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();
            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
                builder.Append(character == 'đ' ? 'd' : character);
            }
            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        private static bool MatchesSchoolQuery(string searchText, string normalizedQuery)
        {
            if (string.IsNullOrWhiteSpace(searchText) || string.IsNullOrWhiteSpace(normalizedQuery))
                return false;

            return searchText.Contains(normalizedQuery, StringComparison.Ordinal);
        }

        private static string EscapeOverpassString(string value)
        {
            return value.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
        }

        private static bool IsValidCoordinate(double latitude, double longitude)
        {
            return latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
        }

        private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);

        private sealed record MapPlace(
            string Name,
            double Latitude,
            double Longitude,
            double? DistanceKm = null,
            string? Subtitle = null);

        private sealed record SchoolPlace(
            string Name,
            double Latitude,
            double Longitude,
            double DistanceKm,
            string? Subtitle,
            string SearchText);

        private sealed record SchoolSeed(
            string Name,
            double Latitude,
            double Longitude,
            string Subtitle,
            string SearchText);
    }
}

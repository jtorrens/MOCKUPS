using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class PhoneSpecsDeviceCatalogProvider : IDeviceCatalogProvider
{
    private const string SourceName = "phone-specs-api";
    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri("https://api-mobilespecs.azharimm.dev/"),
        Timeout = TimeSpan.FromSeconds(12),
    };

    public async Task<IReadOnlyList<DeviceCatalogCandidate>> SearchAsync(
        string manufacturer,
        string modelQuery,
        CancellationToken cancellationToken)
    {
        var query = string.Join(" ", new[] { manufacturer, modelQuery }.Where((part) => !string.IsNullOrWhiteSpace(part))).Trim();
        if (query.Length < 2) return [];

        var root = await GetJsonAsync($"v2/search?query={Uri.EscapeDataString(query)}", cancellationToken)
            ?? await GetJsonAsync($"search?query={Uri.EscapeDataString(query)}", cancellationToken);
        if (root is null) return [];

        return FindPhoneObjects(root)
            .Select(ToCandidate)
            .Where((candidate) => candidate is not null)
            .Cast<DeviceCatalogCandidate>()
            .GroupBy((candidate) => candidate.Id)
            .Select((group) => group.First())
            .Take(40)
            .ToList();
    }

    public async Task<DeviceCatalogDetails?> GetDetailsAsync(
        DeviceCatalogCandidate candidate,
        CancellationToken cancellationToken)
    {
        JsonNode? root = null;
        var detailPath = DetailPath(candidate.DetailUrl);
        if (!string.IsNullOrWhiteSpace(detailPath))
        {
            root = await GetJsonAsync(detailPath, cancellationToken);
        }

        root ??= await GetJsonAsync($"v2/{Uri.EscapeDataString(candidate.Id)}", cancellationToken)
            ?? await GetJsonAsync(Uri.EscapeDataString(candidate.Id), cancellationToken);
        if (root is null) return null;

        var flattened = FlattenText(root).ToList();
        var resolution = FindResolution(flattened);
        if (resolution is null) return null;

        var osFamily = InferOsFamily(candidate.Manufacturer, flattened);
        return new DeviceCatalogDetails(
            candidate.Name,
            candidate.Manufacturer,
            candidate.Model,
            osFamily,
            resolution.Value.Width,
            resolution.Value.Height,
            GuessScale(flattened, osFamily, resolution.Value.Width, resolution.Value.Height),
            SourceName);
    }

    private static async Task<JsonNode?> GetJsonAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
    }

    private static IEnumerable<JsonObject> FindPhoneObjects(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            if (obj.ContainsKey("phone_name") || obj.ContainsKey("phoneName") || obj.ContainsKey("name"))
            {
                yield return obj;
            }

            foreach (var child in obj.Select((pair) => pair.Value).OfType<JsonNode>())
            {
                foreach (var phone in FindPhoneObjects(child))
                {
                    yield return phone;
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array.OfType<JsonNode>())
            {
                foreach (var phone in FindPhoneObjects(child))
                {
                    yield return phone;
                }
            }
        }
    }

    private static DeviceCatalogCandidate? ToCandidate(JsonObject obj)
    {
        var name = FirstString(obj, "phone_name", "phoneName", "name", "title");
        var slug = FirstString(obj, "slug", "phone_slug", "phoneSlug", "id");
        var detail = FirstString(obj, "detail", "detail_url", "detailUrl", "url");
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = SlugFromDetail(detail);
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(slug)) return null;

        var brand = FirstString(obj, "brand", "brand_name", "brandName", "manufacturer");
        if (string.IsNullOrWhiteSpace(brand))
        {
            brand = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        }

        return new DeviceCatalogCandidate(
            slug,
            name,
            brand,
            name,
            detail,
            SourceName);
    }

    private static IEnumerable<string> FlattenText(JsonNode node)
    {
        if (node is JsonValue value)
        {
            var text = value.ToJsonString().Trim('"');
            if (!string.IsNullOrWhiteSpace(text)) yield return text;
            yield break;
        }

        if (node is JsonObject obj)
        {
            foreach (var pair in obj)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key)) yield return pair.Key;
                if (pair.Value is null) continue;
                foreach (var text in FlattenText(pair.Value))
                {
                    yield return text;
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array.OfType<JsonNode>())
            {
                foreach (var text in FlattenText(child))
                {
                    yield return text;
                }
            }
        }
    }

    private static (int Width, int Height)? FindResolution(IReadOnlyList<string> values)
    {
        foreach (var value in values)
        {
            var match = Regex.Match(value, @"(?<w>\d{3,4})\s*x\s*(?<h>\d{3,4})\s*pixels", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                match = Regex.Match(value, @"(?<w>\d{3,4})\s*x\s*(?<h>\d{3,4})", RegexOptions.IgnoreCase);
            }

            if (!match.Success) continue;
            var width = int.Parse(match.Groups["w"].Value, CultureInfo.InvariantCulture);
            var height = int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture);
            if (width <= 0 || height <= 0) continue;
            return width < height ? (width, height) : (height, width);
        }

        return null;
    }

    private static double GuessScale(IReadOnlyList<string> values, string osFamily, int width, int height)
    {
        return DeviceMetricRules.GuessScaleFromText(values, osFamily, width, height);
    }

    private static string InferOsFamily(string manufacturer, IReadOnlyList<string> values)
    {
        if (manufacturer.Contains("apple", StringComparison.OrdinalIgnoreCase)) return "ios";
        var joined = string.Join(" ", values);
        if (joined.Contains("iOS", StringComparison.OrdinalIgnoreCase)) return "ios";
        return "android";
    }

    private static string DetailPath(string detailUrl)
    {
        if (string.IsNullOrWhiteSpace(detailUrl)) return "";
        if (!Uri.TryCreate(detailUrl, UriKind.Absolute, out var uri)) return detailUrl.TrimStart('/');
        return uri.PathAndQuery.TrimStart('/');
    }

    private static string SlugFromDetail(string detailUrl)
    {
        var path = DetailPath(detailUrl);
        if (string.IsNullOrWhiteSpace(path)) return "";
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.LastOrDefault() ?? "";
    }

    private static string FirstString(JsonObject obj, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (obj[key] is JsonValue value && value.TryGetValue<string>(out var text))
            {
                return text;
            }
        }

        return "";
    }
}

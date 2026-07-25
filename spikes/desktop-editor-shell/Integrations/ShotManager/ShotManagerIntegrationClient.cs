using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.Integrations.ShotManager;

internal sealed class ShotManagerIntegrationException : Exception
{
    public ShotManagerIntegrationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

internal sealed class ShotManagerIntegrationClient : IShotManagerIntegrationClient
{
    private const int ApiVersion = 1;
    private static readonly HttpClient SharedHttpClient = CreateHttpClient();
    private readonly string _discoveryPath;
    private readonly HttpClient _httpClient;

    public ShotManagerIntegrationClient(
        string? discoveryPath = null,
        HttpMessageHandler? handler = null)
    {
        _discoveryPath = discoveryPath ?? DefaultDiscoveryPath();
        _httpClient = handler is null
            ? SharedHttpClient
            : CreateHttpClient(handler);
    }

    public async Task<ShotManagerStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var document = await RequestAsync(
                "/health",
                authenticated: false,
                cancellationToken);
            var data = RequiredObject(
                RequiredProperty(document.RootElement, "data", "Shot Manager health"),
                "Shot Manager health data");
            if (!RequiredBoolean(data, "readOnly", "Shot Manager health")
                || RequiredString(data, "service", "Shot Manager health")
                    != "vfx-shot-manager")
            {
                throw InvalidResponse(
                    "Shot Manager health does not describe the read-only integration service.");
            }
            return new ShotManagerStatus(true, "Shot Manager está disponible.");
        }
        catch (ShotManagerIntegrationException exception)
        {
            return new ShotManagerStatus(false, exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ShotManagerStatus(
                false,
                "No se pudo comprobar la conexión local de Shot Manager.");
        }
    }

    public async Task<IReadOnlyList<ShotManagerCatalogProduction>> GetCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        using var document = await RequestAsync(
            "/productions",
            authenticated: true,
            cancellationToken);
        var data = RequiredObject(
            RequiredProperty(document.RootElement, "data", "Shot Manager catalog"),
            "Shot Manager catalog data");
        var productions = RequiredArray(
            RequiredProperty(data, "productions", "Shot Manager catalog"),
            "Shot Manager catalog productions");
        var result = new List<ShotManagerCatalogProduction>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recordValue in productions.EnumerateArray())
        {
            var record = RequiredObject(recordValue, "Shot Manager catalog record");
            if (!RequiredBoolean(record, "available", "Shot Manager catalog record"))
            {
                continue;
            }
            var production = ParseProduction(
                RequiredObject(
                    RequiredProperty(record, "production", "Shot Manager catalog record"),
                    "Shot Manager catalog production"));
            if (!ids.Add(production.Id))
            {
                throw InvalidResponse(
                    "Shot Manager returned duplicate Production identities.");
            }
            result.Add(production);
        }
        return result;
    }

    public async Task<ShotManagerProductionSnapshot> GetSnapshotAsync(
        string productionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productionId))
        {
            throw new ArgumentException(
                "Shot Manager Production identity is required.",
                nameof(productionId));
        }
        using var document = await RequestAsync(
            $"/productions/{Uri.EscapeDataString(productionId)}",
            authenticated: true,
            cancellationToken);
        var data = RequiredObject(
            RequiredProperty(document.RootElement, "data", "Shot Manager snapshot"),
            "Shot Manager snapshot data");
        var production = ParseProduction(RequiredObject(
            RequiredProperty(data, "production", "Shot Manager snapshot"),
            "Shot Manager snapshot production"));
        if (!production.Id.Equals(productionId, StringComparison.Ordinal))
        {
            throw InvalidResponse(
                "Shot Manager returned a different Production than requested.");
        }
        var location = RequiredObject(
            RequiredProperty(data, "location", "Shot Manager snapshot"),
            "Shot Manager snapshot location");
        var rootPath = RequiredString(
            location,
            "rootPath",
            "Shot Manager snapshot location");
        if (!Path.IsPathFullyQualified(rootPath))
        {
            throw InvalidResponse(
                "Shot Manager returned a non-absolute workstation root.");
        }
        var seasons = ParseSeasons(data, production.Id);
        var episodes = ParseEpisodes(data, production.Id, seasons);
        return new ShotManagerProductionSnapshot(
            rootPath,
            production,
            seasons,
            episodes);
    }

    public async Task<ShotManagerExternalShotPlan> PlanShotAsync(
        string productionId,
        string episodeId,
        int shotNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productionId)
            || string.IsNullOrWhiteSpace(episodeId)
            || shotNumber <= 0)
        {
            throw new ArgumentException(
                "An exact Production, Episode and positive Shot number are required.");
        }
        var endpoint =
            $"/productions/{Uri.EscapeDataString(productionId)}/external-shots/plan"
            + $"?episodeId={Uri.EscapeDataString(episodeId)}"
            + $"&shotNumber={shotNumber.ToString(CultureInfo.InvariantCulture)}";
        using var document = await RequestAsync(
            endpoint,
            authenticated: true,
            cancellationToken);
        var data = RequiredObject(
            RequiredProperty(document.RootElement, "data", "Shot Manager Shot plan"),
            "Shot Manager Shot plan data");
        if (RequiredInt32(data, "planVersion", "Shot Manager Shot plan") != 1
            || RequiredBoolean(data, "persisted", "Shot Manager Shot plan")
            || RequiredBoolean(data, "reserved", "Shot Manager Shot plan"))
        {
            throw InvalidResponse(
                "Shot Manager returned a persisted, reserved or unsupported Shot plan.");
        }
        var production = ParsePlanProduction(RequiredObject(
            RequiredProperty(data, "production", "Shot Manager Shot plan"),
            "Shot Manager Shot plan Production"));
        var season = ParsePlanSeason(RequiredObject(
            RequiredProperty(data, "season", "Shot Manager Shot plan"),
            "Shot Manager Shot plan Season"),
            production.Id);
        var episode = ParsePlanEpisode(RequiredObject(
            RequiredProperty(data, "episode", "Shot Manager Shot plan"),
            "Shot Manager Shot plan Episode"),
            production.Id,
            season.Id);
        var returnedNumber = RequiredInt32(
            data,
            "shotNumber",
            "Shot Manager Shot plan");
        if (!production.Id.Equals(productionId, StringComparison.Ordinal)
            || !episode.Id.Equals(episodeId, StringComparison.Ordinal)
            || returnedNumber != shotNumber)
        {
            throw InvalidResponse(
                "Shot Manager returned a Shot plan for a different context.");
        }
        var shotCode = RequiredTechnicalName(
            data,
            "shotCode",
            "Shot Manager Shot plan");
        var fullName = RequiredTechnicalName(
            data,
            "fullName",
            "Shot Manager Shot plan");
        var rootPath = RequiredString(data, "rootPath", "Shot Manager Shot plan");
        if (!Path.IsPathFullyQualified(rootPath))
        {
            throw InvalidResponse(
                "Shot Manager returned a non-absolute Shot plan root.");
        }
        var directories = RequiredArray(
            RequiredProperty(data, "directories", "Shot Manager Shot plan"),
            "Shot Manager Shot plan directories")
            .EnumerateArray()
            .Select((value) => ParseDirectory(
                RequiredObject(value, "Shot Manager Shot plan directory")))
            .ToList();
        var entries = RequiredArray(
            RequiredProperty(data, "structureEntries", "Shot Manager Shot plan"),
            "Shot Manager Shot plan structure entries")
            .EnumerateArray()
            .Select((value) => ParseEntry(
                RequiredObject(value, "Shot Manager Shot plan structure entry")))
            .ToList();
        var shotOwnedDirectories = RequiredArray(
            RequiredProperty(
                data,
                "shotOwnedDirectories",
                "Shot Manager Shot plan"),
            "Shot Manager Shot-owned directories")
            .EnumerateArray()
            .Select((value) => ParseDirectory(
                RequiredObject(
                    value,
                    "Shot Manager Shot-owned directory")))
            .ToList();
        if (directories.Count == 0
            || entries.Count == 0
            || shotOwnedDirectories.Count == 0
            || directories.Select((directory) => directory.RelativePath)
                .Distinct(StringComparer.Ordinal).Count() != directories.Count
            || shotOwnedDirectories.Select((directory) =>
                    directory.RelativePath)
                .Distinct(StringComparer.Ordinal).Count()
                != shotOwnedDirectories.Count
            || entries.Select((entry) => entry.EntryId)
                .Distinct(StringComparer.Ordinal).Count() != entries.Count)
        {
            throw InvalidResponse(
                "Shot Manager returned an empty or ambiguous directory plan.");
        }
        var directoryPaths = directories.Select((directory) => directory.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
        var directoryResolvedPaths = directories.ToDictionary(
            (directory) => directory.RelativePath,
            (directory) => directory.ResolvedPath,
            StringComparer.Ordinal);
        if (entries.Any((entry) => !directoryPaths.Contains(entry.RelativePath))
            || shotOwnedDirectories.Any((directory) =>
                !directoryResolvedPaths.TryGetValue(
                    directory.RelativePath,
                    out var resolvedPath)
                || !resolvedPath.Equals(
                    directory.ResolvedPath,
                    StringComparison.Ordinal)))
        {
            throw InvalidResponse(
                "Shot Manager returned an inconsistent directory ownership or structure mapping.");
        }
        return new ShotManagerExternalShotPlan(
            1,
            production,
            season,
            episode,
            shotNumber,
            shotCode,
            fullName,
            rootPath,
            directories,
            shotOwnedDirectories,
            entries);
    }

    private async Task<JsonDocument> RequestAsync(
        string endpoint,
        bool authenticated,
        CancellationToken cancellationToken)
    {
        var connection = await ReadConnectionAsync(cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(
                connection.BaseUrl.AbsoluteUri.TrimEnd('/')
                + endpoint,
                UriKind.Absolute));
        if (authenticated)
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", connection.Token);
        }
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ShotManagerIntegrationException(
                "SHOT_MANAGER_TIMEOUT",
                "Shot Manager no respondió a tiempo.");
        }
        catch (HttpRequestException)
        {
            throw new ShotManagerIntegrationException(
                "SHOT_MANAGER_UNAVAILABLE",
                "Shot Manager no está disponible en este equipo.");
        }
        using (response)
        {
            JsonDocument document;
            try
            {
                await using var stream =
                    await response.Content.ReadAsStreamAsync(cancellationToken);
                document = await JsonDocument.ParseAsync(
                    stream,
                    cancellationToken: cancellationToken);
            }
            catch (JsonException)
            {
                throw InvalidResponse(
                    "Shot Manager returned a response that MOCKUPS cannot read.");
            }
            var root = RequiredObject(
                document.RootElement,
                "Shot Manager response");
            if (RequiredInt32(root, "apiVersion", "Shot Manager response")
                != ApiVersion)
            {
                document.Dispose();
                throw new ShotManagerIntegrationException(
                    "API_VERSION_UNSUPPORTED",
                    "La versión de Shot Manager no es compatible con MOCKUPS.");
            }
            if (!response.IsSuccessStatusCode)
            {
                var message = "Shot Manager no pudo completar la consulta.";
                if (root.TryGetProperty("error", out var errorValue)
                    && errorValue.ValueKind == JsonValueKind.Object
                    && errorValue.TryGetProperty("message", out var messageValue)
                    && messageValue.ValueKind == JsonValueKind.String)
                {
                    message = messageValue.GetString() ?? message;
                }
                document.Dispose();
                throw new ShotManagerIntegrationException(
                    "SHOT_MANAGER_REQUEST_FAILED",
                    message);
            }
            return document;
        }
    }

    private async Task<Connection> ReadConnectionAsync(
        CancellationToken cancellationToken)
    {
        string json;
        try
        {
            json = await File.ReadAllTextAsync(
                _discoveryPath,
                cancellationToken);
        }
        catch (FileNotFoundException)
        {
            throw new ShotManagerIntegrationException(
                "SHOT_MANAGER_NOT_RUNNING",
                "Abre Shot Manager para conectar esta producción.");
        }
        catch (DirectoryNotFoundException)
        {
            throw new ShotManagerIntegrationException(
                "SHOT_MANAGER_NOT_RUNNING",
                "Abre Shot Manager para conectar esta producción.");
        }
        catch (IOException)
        {
            throw new ShotManagerIntegrationException(
                "DISCOVERY_READ_FAILED",
                "No se pudo leer la conexión local de Shot Manager.");
        }
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = RequiredObject(
                document.RootElement,
                "Shot Manager discovery");
            var keys = root.EnumerateObject()
                .Select((property) => property.Name)
                .ToHashSet(StringComparer.Ordinal);
            var expected = new HashSet<string>(
                ["version", "apiVersion", "baseUrl", "token", "updatedAt"],
                StringComparer.Ordinal);
            if (!keys.SetEquals(expected)
                || RequiredInt32(root, "version", "Shot Manager discovery") != 1
                || RequiredInt32(root, "apiVersion", "Shot Manager discovery")
                    != ApiVersion)
            {
                throw new ShotManagerIntegrationException(
                    "API_VERSION_UNSUPPORTED",
                    "La conexión publicada por Shot Manager no es compatible con MOCKUPS.");
            }
            var token = RequiredString(root, "token", "Shot Manager discovery");
            if (token.Length != 64
                || token.Any((character) =>
                    !(character is >= '0' and <= '9'
                        or >= 'a' and <= 'f')))
            {
                throw InvalidDiscovery();
            }
            var updatedAt = RequiredString(
                root,
                "updatedAt",
                "Shot Manager discovery");
            if (!DateTimeOffset.TryParse(
                updatedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _))
            {
                throw InvalidDiscovery();
            }
            var baseUrlText = RequiredString(
                root,
                "baseUrl",
                "Shot Manager discovery");
            if (!Uri.TryCreate(baseUrlText, UriKind.Absolute, out var baseUrl)
                || baseUrl.Scheme != Uri.UriSchemeHttp
                || baseUrl.Host != "127.0.0.1"
                || baseUrl.IsDefaultPort
                || baseUrl.AbsolutePath.TrimEnd('/') != "/api/v1"
                || !string.IsNullOrEmpty(baseUrl.UserInfo)
                || !string.IsNullOrEmpty(baseUrl.Query)
                || !string.IsNullOrEmpty(baseUrl.Fragment))
            {
                throw InvalidDiscovery();
            }
            return new Connection(
                new Uri(baseUrlText.TrimEnd('/'), UriKind.Absolute),
                token);
        }
        catch (JsonException)
        {
            throw InvalidDiscovery();
        }
    }

    private static IReadOnlyList<ShotManagerSeason> ParseSeasons(
        JsonElement data,
        string productionId)
    {
        var result = new List<ShotManagerSeason>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in RequiredArray(
            RequiredProperty(data, "seasons", "Shot Manager snapshot"),
            "Shot Manager snapshot seasons").EnumerateArray())
        {
            var season = ParsePlanSeason(
                RequiredObject(value, "Shot Manager Season"),
                productionId);
            if (IsArchived(value))
            {
                continue;
            }
            if (!ids.Add(season.Id))
            {
                throw InvalidResponse(
                    "Shot Manager returned duplicate Season identities.");
            }
            result.Add(season);
        }
        return result;
    }

    private static IReadOnlyList<ShotManagerEpisode> ParseEpisodes(
        JsonElement data,
        string productionId,
        IReadOnlyList<ShotManagerSeason> seasons)
    {
        var result = new List<ShotManagerEpisode>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var seasonIds = seasons.Select((season) => season.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var value in RequiredArray(
            RequiredProperty(data, "episodes", "Shot Manager snapshot"),
            "Shot Manager snapshot episodes").EnumerateArray())
        {
            if (IsArchived(value))
            {
                continue;
            }
            var episodeObject = RequiredObject(value, "Shot Manager Episode");
            var seasonId = RequiredString(
                episodeObject,
                "seasonId",
                "Shot Manager Episode");
            var episode = ParsePlanEpisode(
                episodeObject,
                productionId,
                seasonId);
            if (!seasonIds.Contains(seasonId) || !ids.Add(episode.Id))
            {
                throw InvalidResponse(
                    "Shot Manager returned an invalid or duplicate Episode identity.");
            }
            result.Add(episode);
        }
        return result;
    }

    private static ShotManagerCatalogProduction ParseProduction(
        JsonElement value)
    {
        if (IsArchived(value))
        {
            throw InvalidResponse(
                "Shot Manager returned an archived Production as available.");
        }
        return ParsePlanProduction(value);
    }

    private static ShotManagerCatalogProduction ParsePlanProduction(
        JsonElement value)
    {
        return new ShotManagerCatalogProduction(
            RequiredString(value, "id", "Shot Manager Production"),
            RequiredString(value, "name", "Shot Manager Production"),
            RequiredString(value, "code", "Shot Manager Production"),
            RequiredString(value, "productionType", "Shot Manager Production"),
            OptionalString(value, "seriesShotStructure"));
    }

    private static ShotManagerSeason ParsePlanSeason(
        JsonElement value,
        string productionId)
    {
        if (value.TryGetProperty("productionId", out var returnedProduction)
            && RequiredStringValue(
                returnedProduction,
                "Shot Manager Season productionId") != productionId)
        {
            throw InvalidResponse(
                "Shot Manager returned a Season from another Production.");
        }
        var number = RequiredInt32(value, "number", "Shot Manager Season");
        if (number <= 0)
        {
            throw InvalidResponse(
                "Shot Manager returned a non-positive Season number.");
        }
        return new ShotManagerSeason(
            RequiredString(value, "id", "Shot Manager Season"),
            productionId,
            number,
            RequiredString(value, "code", "Shot Manager Season"),
            OptionalString(value, "name"));
    }

    private static ShotManagerEpisode ParsePlanEpisode(
        JsonElement value,
        string productionId,
        string seasonId)
    {
        if (value.TryGetProperty("productionId", out var returnedProduction)
            && RequiredStringValue(
                returnedProduction,
                "Shot Manager Episode productionId") != productionId)
        {
            throw InvalidResponse(
                "Shot Manager returned an Episode from another Production.");
        }
        if (value.TryGetProperty("seasonId", out var returnedSeason)
            && RequiredStringValue(
                returnedSeason,
                "Shot Manager Episode seasonId") != seasonId)
        {
            throw InvalidResponse(
                "Shot Manager returned an Episode from another Season.");
        }
        var number = RequiredInt32(value, "number", "Shot Manager Episode");
        if (number <= 0)
        {
            throw InvalidResponse(
                "Shot Manager returned a non-positive Episode number.");
        }
        return new ShotManagerEpisode(
            RequiredString(value, "id", "Shot Manager Episode"),
            productionId,
            seasonId,
            number,
            RequiredString(value, "code", "Shot Manager Episode"),
            OptionalString(value, "title"));
    }

    private static ShotManagerPlanDirectory ParseDirectory(JsonElement value)
    {
        return new ShotManagerPlanDirectory(
            RequiredString(value, "relativePath", "Shot Manager directory"),
            RequiredString(value, "resolvedPath", "Shot Manager directory"));
    }

    private static ShotManagerPlanEntry ParseEntry(JsonElement value)
    {
        return new ShotManagerPlanEntry(
            RequiredString(value, "entryId", "Shot Manager structure entry"),
            RequiredString(value, "relativePath", "Shot Manager structure entry"),
            RequiredString(value, "resolvedPath", "Shot Manager structure entry"));
    }

    private static bool IsArchived(JsonElement value)
    {
        return value.TryGetProperty("archivedAt", out var archivedAt)
            && archivedAt.ValueKind != JsonValueKind.Null;
    }

    private static JsonElement RequiredProperty(
        JsonElement value,
        string property,
        string context)
    {
        if (!value.TryGetProperty(property, out var result))
        {
            throw InvalidResponse($"{context} requires '{property}'.");
        }
        return result;
    }

    private static JsonElement RequiredObject(
        JsonElement value,
        string context)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw InvalidResponse($"{context} must be an object.");
        }
        return value;
    }

    private static JsonElement RequiredArray(
        JsonElement value,
        string context)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw InvalidResponse($"{context} must be an array.");
        }
        return value;
    }

    private static string RequiredString(
        JsonElement value,
        string property,
        string context)
    {
        return RequiredStringValue(
            RequiredProperty(value, property, context),
            $"{context}.{property}");
    }

    private static string RequiredTechnicalName(
        JsonElement value,
        string property,
        string context)
    {
        var result = RequiredString(value, property, context);
        if (result.Any((character) =>
            !(character is >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '_'
                or '-')))
        {
            throw InvalidResponse(
                $"{context}.{property} is not a portable technical name.");
        }
        return result;
    }

    private static string RequiredStringValue(
        JsonElement value,
        string context)
    {
        if (value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw InvalidResponse($"{context} must be a non-empty string.");
        }
        return value.GetString()!;
    }

    private static string? OptionalString(
        JsonElement value,
        string property)
    {
        if (!value.TryGetProperty(property, out var result)
            || result.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        return RequiredStringValue(result, property);
    }

    private static int RequiredInt32(
        JsonElement value,
        string property,
        string context)
    {
        var result = RequiredProperty(value, property, context);
        if (result.ValueKind != JsonValueKind.Number
            || !result.TryGetInt32(out var number))
        {
            throw InvalidResponse($"{context}.{property} must be an integer.");
        }
        return number;
    }

    private static bool RequiredBoolean(
        JsonElement value,
        string property,
        string context)
    {
        var result = RequiredProperty(value, property, context);
        return result.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw InvalidResponse(
                $"{context}.{property} must be a boolean."),
        };
    }

    private static ShotManagerIntegrationException InvalidResponse(
        string message)
    {
        return new ShotManagerIntegrationException(
            "INVALID_RESPONSE",
            message);
    }

    private static ShotManagerIntegrationException InvalidDiscovery()
    {
        return new ShotManagerIntegrationException(
            "DISCOVERY_INVALID",
            "El archivo de conexión de Shot Manager no es válido.");
    }

    private static string DefaultDiscoveryPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData),
            "VFX Shot Manager",
            "integration-api.json");
    }

    private static HttpClient CreateHttpClient(
        HttpMessageHandler? handler = null)
    {
        var client = handler is null
            ? new HttpClient()
            : new HttpClient(handler);
        client.Timeout = TimeSpan.FromMilliseconds(2500);
        return client;
    }

    private sealed record Connection(Uri BaseUrl, string Token);
}

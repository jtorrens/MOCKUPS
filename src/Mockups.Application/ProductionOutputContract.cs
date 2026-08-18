using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.Data;

public sealed record ProductionOutputSettings(
    string TechnicalCode,
    string SeasonCode,
    string EpisodePrefix,
    string NameSeparator,
    string ShotPrefix,
    int ShotNumberPadding,
    int VersionPadding,
    int FramePadding,
    string RelativeDirectoryTemplate);

public sealed record ProductionOutputShotPlan(
    string ProjectId,
    string ShotId,
    int ShotNumber,
    string ShotCode,
    string TechnicalName,
    string RouteId,
    string RelativeDirectory,
    int VersionPadding,
    int FramePadding);

public static partial class ProductionOutputContract
{
    public const string RouteId = "comp";
    public const string SeasonCodeToken = "{{SEASON_CODE}}";
    public const string EpisodeCodeToken = "{{EPISODE_CODE}}";
    public const string ShotNameToken = "{{SHOT_NAME}}";

    private static readonly string[] RequiredTokens =
    [
        SeasonCodeToken,
        EpisodeCodeToken,
        ShotNameToken,
    ];

    public static ProductionOutputSettings Require(
        ProductionOutputSettings value,
        string context)
    {
        if (!TechnicalCodePattern().IsMatch(value.TechnicalCode))
        {
            throw new InvalidOperationException(
                $"{context}.technicalCode must use A-Z and 0-9.");
        }
        if (!HierarchyCodePattern().IsMatch(value.SeasonCode))
        {
            throw new InvalidOperationException(
                $"{context}.seasonCode must use A-Z, 0-9 and underscore.");
        }
        if (value.NameSeparator is not "_" and not "-" and not "")
        {
            throw new InvalidOperationException(
                $"{context}.nameSeparator must be underscore, hyphen or empty.");
        }
        if (!OptionalTechnicalCodePattern().IsMatch(value.EpisodePrefix))
        {
            throw new InvalidOperationException(
                $"{context}.episodePrefix must be blank or use A-Z and 0-9.");
        }
        if (!OptionalTechnicalCodePattern().IsMatch(value.ShotPrefix))
        {
            throw new InvalidOperationException(
                $"{context}.shotPrefix must be blank or use A-Z and 0-9.");
        }
        RequirePadding(
            value.ShotNumberPadding,
            $"{context}.shotNumberPadding");
        RequirePadding(
            value.VersionPadding,
            $"{context}.versionPadding");
        RequirePadding(
            value.FramePadding,
            $"{context}.framePadding");
        RequireTemplate(value.RelativeDirectoryTemplate, context);
        return value;
    }

    public static ProductionOutputShotPlan Resolve(
        string projectId,
        string shotId,
        int shotNumber,
        string episodeCode,
        string shotCode,
        ProductionOutputSettings settings)
    {
        Require(settings, "Production output settings");
        if (string.IsNullOrWhiteSpace(projectId)
            || string.IsNullOrWhiteSpace(shotId)
            || shotNumber <= 0)
        {
            throw new InvalidOperationException(
                "Production output requires one exact Project, Shot and positive Shot number.");
        }
        var normalizedEpisodeCode = RequireEpisodeCode(
            episodeCode,
            "Production output Episode code");
        var normalizedShotCode = RequireShotCode(
            shotCode,
            "Production output Shot code");
        var technicalName = string.Join(
            settings.NameSeparator,
            settings.TechnicalCode,
            settings.SeasonCode,
            normalizedEpisodeCode,
            normalizedShotCode);
        var relativeDirectory = settings.RelativeDirectoryTemplate
            .Replace(
                SeasonCodeToken,
                settings.SeasonCode,
                StringComparison.Ordinal)
            .Replace(
                EpisodeCodeToken,
                normalizedEpisodeCode,
                StringComparison.Ordinal)
            .Replace(
                ShotNameToken,
                technicalName,
                StringComparison.Ordinal);
        RequirePortableRelativeDirectory(
            relativeDirectory,
            "Resolved Production output directory");
        return new ProductionOutputShotPlan(
            projectId,
            shotId,
            shotNumber,
            normalizedShotCode,
            technicalName,
            RouteId,
            relativeDirectory,
            settings.VersionPadding,
            settings.FramePadding);
    }

    public static string RequireEpisodeCode(
        string value,
        string context)
    {
        var normalized = value.Trim().ToUpperInvariant();
        if (!HierarchyCodePattern().IsMatch(normalized))
        {
            throw new InvalidOperationException(
                $"{context} must use A-Z, 0-9 and underscore.");
        }
        return normalized;
    }

    public static string CreateEpisodeCode(
        string episodePrefix,
        int episodeNumber)
    {
        if (episodeNumber <= 0)
        {
            throw new InvalidOperationException(
                "An Episode requires a positive stable number.");
        }
        var prefix = episodePrefix.Trim().ToUpperInvariant();
        if (!OptionalTechnicalCodePattern().IsMatch(prefix))
        {
            throw new InvalidOperationException(
                "Episode prefix must be blank or use A-Z and 0-9.");
        }
        return string.IsNullOrEmpty(prefix)
            ? episodeNumber.ToString("00")
            : $"{prefix}_{episodeNumber:00}";
    }

    public static string CreateShotCode(
        string shotPrefix,
        int shotNumber,
        int padding)
    {
        RequirePadding(padding, "Shot number padding");
        if (shotNumber <= 0)
        {
            throw new InvalidOperationException(
                "A Shot requires a positive stable number.");
        }
        var prefix = shotPrefix.Trim().ToUpperInvariant();
        if (!OptionalTechnicalCodePattern().IsMatch(prefix))
        {
            throw new InvalidOperationException(
                "Shot prefix must be blank or use A-Z and 0-9.");
        }
        return RequireShotCode(
            $"{prefix}{shotNumber.ToString().PadLeft(padding, '0')}",
            "Generated Shot code");
    }

    public static string RequireShotCode(
        string value,
        string context)
    {
        var normalized = value.Trim();
        if (!ShotCodePattern().IsMatch(normalized))
        {
            throw new InvalidOperationException(
                $"{context} must use letters, numbers, hyphen and underscore.");
        }
        return normalized;
    }

    private static void RequireTemplate(
        string value,
        string context)
    {
        if (RequiredTokens.Any((token) =>
                Count(value, token) != 1))
        {
            throw new InvalidOperationException(
                $"{context}.relativeDirectoryTemplate requires SEASON_CODE, EPISODE_CODE and SHOT_NAME exactly once.");
        }
        var withoutTokens = RequiredTokens.Aggregate(
            value,
            (current, token) => current.Replace(
                token,
                "TOKEN",
                StringComparison.Ordinal));
        if (withoutTokens.Contains('{')
            || withoutTokens.Contains('}'))
        {
            throw new InvalidOperationException(
                $"{context}.relativeDirectoryTemplate contains an unknown token.");
        }
        RequirePortableRelativeDirectory(
            withoutTokens,
            $"{context}.relativeDirectoryTemplate");
    }

    private static void RequirePortableRelativeDirectory(
        string value,
        string context)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.StartsWith("/", StringComparison.Ordinal)
            || value.EndsWith("/", StringComparison.Ordinal)
            || value.Contains('\\')
            || value.Contains(':')
            || value.Split('/').Any((segment) =>
                string.IsNullOrWhiteSpace(segment)
                || segment is "." or ".."
                || !PortableSegmentPattern().IsMatch(segment)))
        {
            throw new InvalidOperationException(
                $"{context} must be a portable relative directory.");
        }
    }

    private static void RequirePadding(
        int value,
        string context)
    {
        if (value is < 1 or > 8)
        {
            throw new InvalidOperationException(
                $"{context} must be between 1 and 8.");
        }
    }

    private static int Count(string value, string term)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(
                   term,
                   index,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += term.Length;
        }
        return count;
    }

    [GeneratedRegex("^[A-Z0-9]{1,32}$")]
    private static partial Regex TechnicalCodePattern();

    [GeneratedRegex("^[A-Z0-9]{0,32}$")]
    private static partial Regex OptionalTechnicalCodePattern();

    [GeneratedRegex("^[A-Z0-9_]{1,32}$")]
    private static partial Regex HierarchyCodePattern();

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex ShotCodePattern();

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex PortableSegmentPattern();
}

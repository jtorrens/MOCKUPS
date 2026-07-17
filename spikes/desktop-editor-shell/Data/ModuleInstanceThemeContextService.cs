using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class ModuleInstanceThemeContextService : IModuleInstanceThemeContextService
{
    private readonly SqliteProjectContext _context;

    public ModuleInstanceThemeContextService(SqliteProjectContext context)
    {
        _context = context;
    }

    public string GetTokensJson(string moduleInstanceId)
    {
        using var connection = _context.OpenConnection();
        var tokensJson = SqliteCommandExecutor.ScalarString(
            connection,
            """
            SELECT t.tokens_json
            FROM module_instances target
            JOIN shots s ON s.id = target.shot_id
            JOIN episodes e ON e.id = s.episode_id
            JOIN actors actor ON actor.id = s.owner_actor_id AND actor.project_id = e.project_id
            JOIN themes t ON t.id = actor.default_theme_id AND t.project_id = actor.project_id
            WHERE target.id = $id
            """,
            ("$id", moduleInstanceId))
            ?? throw new InvalidOperationException(
                $"Module Instance '{moduleInstanceId}' has no resolvable Theme context.");
        JsonPath.ParseRequiredObject(tokensJson, $"Module Instance '{moduleInstanceId}' Theme tokens_json");
        return tokensJson;
    }

    public void RequireShotContext(SqliteConnection connection, string shotId)
    {
        if (HasShotThemeContext(connection, shotId)) return;
        throw new InvalidOperationException(
            "Select a Shot owner Actor with an explicit default Theme before adding a Screen.");
    }

    public void RequireEpisodeActor(SqliteConnection connection, string episodeId, string actorId)
    {
        if (ActorBelongsToEpisodeProject(connection, episodeId, actorId)) return;
        throw new InvalidOperationException(
            "Select an Actor from the same Project before creating the Shot.");
    }

    public void RequireShotOwnerChange(
        SqliteConnection connection,
        string shotId,
        string actorId)
    {
        if (!ActorBelongsToShotProject(connection, shotId, actorId))
        {
            throw new InvalidOperationException(
                "A Shot requires an owner Actor from the same Project.");
        }
        if (!ShotHasModuleInstances(connection, shotId) || HasActorThemeContext(connection, actorId)) return;
        throw new InvalidOperationException(
            "A Shot containing Screens requires an owner Actor with an explicit default Theme.");
    }

    public void RequireActorThemeChange(
        SqliteConnection connection,
        string actorId,
        string themeId)
    {
        if (!ActorOwnsModuleInstances(connection, actorId)) return;
        if (ThemeBelongsToActorProject(connection, actorId, themeId)) return;
        throw new InvalidOperationException(
            "An Actor used as the owner of a Shot containing Screens requires an explicit default Theme.");
    }

    private static bool HasShotThemeContext(SqliteConnection connection, string shotId)
    {
        return SqliteCommandExecutor.ScalarLong(
            connection,
            """
            SELECT COUNT(*)
            FROM shots s
            JOIN episodes e ON e.id = s.episode_id
            JOIN actors actor ON actor.id = s.owner_actor_id AND actor.project_id = e.project_id
            JOIN themes t ON t.id = actor.default_theme_id AND t.project_id = actor.project_id
            WHERE s.id = $shotId
            """,
            ("$shotId", shotId)) == 1;
    }

    private static bool ShotHasModuleInstances(SqliteConnection connection, string shotId)
    {
        return SqliteCommandExecutor.ScalarLong(
            connection,
            "SELECT COUNT(*) FROM module_instances WHERE shot_id = $shotId",
            ("$shotId", shotId)) > 0;
    }

    private static bool ActorBelongsToEpisodeProject(
        SqliteConnection connection,
        string episodeId,
        string actorId)
    {
        return SqliteCommandExecutor.ScalarLong(
            connection,
            """
            SELECT COUNT(*)
            FROM episodes e
            JOIN actors actor ON actor.project_id = e.project_id
            WHERE e.id = $episodeId AND actor.id = $actorId
            """,
            ("$episodeId", episodeId),
            ("$actorId", actorId)) == 1;
    }

    private static bool ActorBelongsToShotProject(
        SqliteConnection connection,
        string shotId,
        string actorId)
    {
        return SqliteCommandExecutor.ScalarLong(
            connection,
            """
            SELECT COUNT(*)
            FROM shots s
            JOIN episodes e ON e.id = s.episode_id
            JOIN actors actor ON actor.project_id = e.project_id
            WHERE s.id = $shotId AND actor.id = $actorId
            """,
            ("$shotId", shotId),
            ("$actorId", actorId)) == 1;
    }

    private static bool HasActorThemeContext(SqliteConnection connection, string actorId)
    {
        return SqliteCommandExecutor.ScalarLong(
            connection,
            """
            SELECT COUNT(*)
            FROM actors actor
            JOIN themes t ON t.id = actor.default_theme_id AND t.project_id = actor.project_id
            WHERE actor.id = $actorId
            """,
            ("$actorId", actorId)) == 1;
    }

    private static bool ActorOwnsModuleInstances(SqliteConnection connection, string actorId)
    {
        return SqliteCommandExecutor.ScalarLong(
            connection,
            """
            SELECT COUNT(*)
            FROM module_instances mi
            JOIN shots s ON s.id = mi.shot_id
            WHERE s.owner_actor_id = $actorId
            """,
            ("$actorId", actorId)) > 0;
    }

    private static bool ThemeBelongsToActorProject(
        SqliteConnection connection,
        string actorId,
        string themeId)
    {
        return SqliteCommandExecutor.ScalarLong(
            connection,
            """
            SELECT COUNT(*)
            FROM actors actor
            JOIN themes t ON t.project_id = actor.project_id
            WHERE actor.id = $actorId AND t.id = $themeId
            """,
            ("$actorId", actorId),
            ("$themeId", themeId)) == 1;
    }
}

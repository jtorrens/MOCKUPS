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
        // TODO(editor-architecture): migrate incomplete seeded Shots to an explicit owner Actor,
        // then remove the project-first Theme fallback and its name/id ordering.
        var tokensJson = SqliteCommandExecutor.ScalarString(
            connection,
            """
            SELECT COALESCE(
              (SELECT t.tokens_json
               FROM module_instances target
               JOIN shots s ON s.id = target.shot_id
               JOIN actors actor ON actor.id = s.owner_actor_id
               JOIN themes t ON t.id = actor.default_theme_id
               WHERE target.id = $id),
              (SELECT t.tokens_json
               FROM module_instances target
               JOIN apps a ON a.id = target.app_id
               JOIN themes t ON t.project_id = a.project_id
               WHERE target.id = $id
               ORDER BY t.name, t.id
               LIMIT 1))
            """,
            ("$id", moduleInstanceId))
            ?? throw new InvalidOperationException(
                $"Module Instance '{moduleInstanceId}' has no resolvable Theme context.");
        JsonPath.ParseRequiredObject(tokensJson, $"Module Instance '{moduleInstanceId}' Theme tokens_json");
        return tokensJson;
    }
}

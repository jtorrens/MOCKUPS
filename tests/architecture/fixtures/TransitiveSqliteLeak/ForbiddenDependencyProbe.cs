using Microsoft.Data.Sqlite;

namespace TransitiveSqliteLeak;

public static class ForbiddenDependencyProbe
{
    public static SqliteConnection Create() =>
        new("Data Source=:memory:");
}

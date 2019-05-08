using System;
using System.Linq;

public static class GameDatabaseExtensions
{
    public static UrlDir GetGameData(this GameDatabase gameDatabase)
    {
        return gameDatabase.root.children.First(dir => dir.type == UrlDir.DirectoryType.GameData && dir.name == "");
    }
}

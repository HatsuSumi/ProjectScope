namespace ProjectScope.Core;

public sealed record FileTypeDefinition(string Name, IReadOnlySet<string> Extensions, string Label, bool IsCode, CommentSyntax CommentSyntax);

public enum CommentSyntax
{
    None,
    CLike,
    Hash,
    Html,
    Sql,
    Ini,
    Batch,
    PowerShell,
    Lua,
    Python
}

public sealed record AssetTypeDefinition(string Name, string Label, IReadOnlySet<string> Extensions);

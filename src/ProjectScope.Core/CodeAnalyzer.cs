using System.Text;
using System.Text.RegularExpressions;

namespace ProjectScope.Core;

public sealed class CodeAnalyzer
{
    public (long Lines, long Characters) AnalyzeRaw(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        long lines = 0;
        long characters = 0;
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lines++;
            characters += line.EnumerateRunes().Count();
        }
        return (lines, characters);
    }

    public (long Lines, long Characters) Analyze(string text, CommentSyntax syntax, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        var stripped = Strip(text, syntax, cancellationToken);
        long lines = 0;
        long characters = 0;
        using var reader = new StringReader(stripped);
        while (reader.ReadLine() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hasContent = false;
            long lineCharacters = 0;
            foreach (var rune in line.EnumerateRunes())
            {
                if ((lineCharacters & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
                hasContent |= !Rune.IsWhiteSpace(rune);
                lineCharacters++;
            }
            if (!hasContent) continue;
            lines++;
            characters += lineCharacters;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return (lines, characters);
    }

    public string Strip(string text, CommentSyntax syntax, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(syntax)) throw new ArgumentOutOfRangeException(nameof(syntax));
        if (syntax == CommentSyntax.None) return text;
        if (syntax == CommentSyntax.Batch)
        {
            var batch = new StringBuilder(text.Length);
            using var reader = new StringReader(text);
            while (reader.ReadLine() is { } line)
            {
                cancellationToken.ThrowIfCancellationRequested();
                batch.AppendLine(Regex.Replace(line, @"(?i)^\s*(?:rem(?:\s.*)?|::.*)$", ""));
            }
            return batch.ToString();
        }
        var output = new StringBuilder(text.Length);
        var position = 0;
        while (position < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tail = text.AsSpan(position);
            if (syntax != CommentSyntax.Html && text[position] is '\'' or '"' or '`')
            {
                CopyString(text, ref position, syntax, output, cancellationToken);
                continue;
            }
            var lineMarker = syntax switch
            {
                CommentSyntax.CLike => "//", CommentSyntax.Sql or CommentSyntax.Lua => "--",
                CommentSyntax.Hash or CommentSyntax.Python or CommentSyntax.PowerShell => "#",
                _ => ""
            };
            var blockStart = syntax switch
            {
                CommentSyntax.CLike or CommentSyntax.Sql => "/*", CommentSyntax.Html => "<!--",
                CommentSyntax.PowerShell => "<#", _ => ""
            };
            var blockEnd = syntax switch
            {
                CommentSyntax.CLike or CommentSyntax.Sql => "*/", CommentSyntax.Html => "-->",
                CommentSyntax.PowerShell => "#>", _ => ""
            };
            if (syntax == CommentSyntax.Lua && tail.StartsWith("--["))
            {
                var match = Regex.Match(text[position..], @"^--\[(=*)\[");
                if (match.Success)
                {
                    SkipBlock(text, ref position, match.Length, "]" + match.Groups[1].Value + "]", output, cancellationToken);
                    continue;
                }
            }
            if (blockStart.Length > 0 && tail.StartsWith(blockStart))
            {
                SkipBlock(text, ref position, blockStart.Length, blockEnd, output, cancellationToken);
                continue;
            }
            if (lineMarker.Length > 0 && tail.StartsWith(lineMarker))
            {
                SkipLine(text, ref position, cancellationToken);
                continue;
            }
            if (syntax == CommentSyntax.Ini && text[position] is '#' or ';')
            {
                var lineStart = text.LastIndexOf('\n', Math.Max(0, position - 1)) + 1;
                if (string.IsNullOrWhiteSpace(text[lineStart..position]))
                {
                    SkipLine(text, ref position, cancellationToken);
                    continue;
                }
            }
            output.Append(text[position++]);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return output.ToString();
    }

    private static void SkipLine(string text, ref int position, CancellationToken token)
    {
        while (position < text.Length && text[position] != '\n')
        {
            token.ThrowIfCancellationRequested();
            position++;
        }
    }

    private static void SkipBlock(string text, ref int position, int openingLength, string end, StringBuilder output, CancellationToken token)
    {
        position += openingLength;
        while (position < text.Length)
        {
            token.ThrowIfCancellationRequested();
            if (text.AsSpan(position).StartsWith(end)) { position += end.Length; return; }
            if (text[position] == '\n') output.Append('\n');
            position++;
        }
    }

    private static void CopyString(string text, ref int position, CommentSyntax syntax, StringBuilder output, CancellationToken token)
    {
        var quote = text[position];
        var delimiter = quote.ToString();
        if (syntax == CommentSyntax.Python && text.AsSpan(position).StartsWith(new string(quote, 3))) delimiter = new string(quote, 3);
        var lineStart = text.LastIndexOf('\n', Math.Max(0, position - 1)) + 1;
        var isDocstring = syntax == CommentSyntax.Python && string.IsNullOrWhiteSpace(text[lineStart..position]);
        if (!isDocstring) output.Append(delimiter);
        position += delimiter.Length;
        while (position < text.Length)
        {
            token.ThrowIfCancellationRequested();
            if (text.AsSpan(position).StartsWith(delimiter))
            {
                if (!isDocstring) output.Append(delimiter);
                position += delimiter.Length;
                return;
            }
            if (text[position] == '\\' && position + 1 < text.Length)
            {
                if (!isDocstring) output.Append(text, position, 2);
                position += 2;
                continue;
            }
            if (!isDocstring) output.Append(text[position]);
            else if (text[position] == '\n') output.Append('\n');
            position++;
        }
    }
}

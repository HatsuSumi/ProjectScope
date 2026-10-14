using System.Text;

namespace ProjectScope.Core;

public static class DirectoryTreeFormatter
{
    public static string Format(string rootName, IReadOnlyList<string> paths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootName);
        ArgumentNullException.ThrowIfNull(paths);
        var root = new Node(rootName);
        foreach (var path in paths)
        {
            var current = root;
            foreach (var name in path.Replace('\\', '/').Split('/'))
            {
                if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException($"Invalid relative path '{path}'.", nameof(paths));
                if (!current.Children.TryGetValue(name, out var child))
                {
                    child = new Node(name);
                    current.Children.Add(name, child);
                }
                current = child;
            }
        }
        var output = new StringBuilder().AppendLine(rootName + "/");
        Append(root, "", output);
        return output.ToString();
    }

    private static void Append(Node node, string prefix, StringBuilder output)
    {
        var children = node.Children.Values.OrderBy(child => child.Children.Count == 0).ThenBy(child => child.Name, StringComparer.Ordinal).ToArray();
        for (var index = 0; index < children.Length; index++)
        {
            var last = index == children.Length - 1;
            var connector = "├── ";
            var next = "│   ";
            if (last) { connector = "└── "; next = "    "; }
            output.Append(prefix).Append(connector).Append(children[index].Name);
            if (children[index].Children.Count > 0) output.Append('/');
            output.AppendLine();
            Append(children[index], prefix + next, output);
        }
    }

    private sealed class Node(string name)
    {
        public string Name { get; } = name;
        public Dictionary<string, Node> Children { get; } = new(StringComparer.Ordinal);
    }
}

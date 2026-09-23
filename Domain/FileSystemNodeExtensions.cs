namespace CloudFileSystem.Domain;

public static class FileSystemNodeExtensions
{
    public static IEnumerable<FileSystemNode> DescendantsAndSelf(this FileSystemNode node)
    {
        yield return node;
        if (node is not DirectoryNode directory) yield break;
        foreach (var child in directory.Children)
            foreach (var descendant in child.DescendantsAndSelf()) yield return descendant;
    }

    public static string Path(this FileSystemNode node, string separator = "/")
    {
        var parts = new Stack<string>();
        for (FileSystemNode? current = node; current is not null; current = current.Parent) parts.Push(current.Name);
        return string.Join(separator, parts);
    }
}

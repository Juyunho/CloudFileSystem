using CloudFileSystem.Domain;
using CloudFileSystem.Managers;
using CloudFileSystem.Models;

namespace CloudFileSystem.Application;

public sealed class FileSystemTreeFactory(IFileManager fileManager, IDirectoryManager directoryManager)
{
    public DirectoryNode Create()
    {
        var directories = directoryManager.getAllDirectories();
        var files = fileManager.getAllFiles();
        if (directories.Count(d => d.parentId == null) != 1)
            throw new InvalidOperationException("Exactly one root directory is required.");
        if (directories.Select(d => d.id).Distinct().Count() != directories.Count || files.Select(f => f.id).Distinct().Count() != files.Count)
            throw new InvalidOperationException("Duplicate node IDs are not allowed within a node type.");
        var directoryIds = directories.Select(d => d.id).ToHashSet();
        if (directories.Any(d => d.parentId != null && !directoryIds.Contains(d.parentId.Value)) || files.Any(f => !directoryIds.Contains(f.directoryId)))
            throw new InvalidOperationException("Every node must belong to an existing directory.");
        var root = Build(directories.Single(d => d.parentId == null), directories, files, []);
        if (root.DescendantsAndSelf().OfType<DirectoryNode>().Count() != directories.Count)
            throw new InvalidOperationException("All directories must be reachable from the root.");
        return root;
    }

    private static DirectoryNode Build(DirectoryModel source, List<DirectoryModel> directories, List<FileModel> files, HashSet<int> ancestors)
    {
        if (!ancestors.Add(source.id)) throw new InvalidOperationException("Directory cycle detected.");
        var node = new DirectoryNode(source.id, source.name, source.createdTime);
        var children = new List<(int Order, CloudFileSystem.Domain.FileSystemNode Node)>();
        foreach (var directory in directories.Where(d => d.parentId == source.id))
            children.Add((directory.displayOrder, Build(directory, directories, files, ancestors)));
        foreach (var file in files.Where(f => f.directoryId == source.id))
            children.Add((file.displayOrder, FromFile(file)));
        foreach (var child in children.OrderBy(item => item.Order)) node.Add(child.Node);
        ancestors.Remove(source.id);
        return node;
    }

    private static FileNode FromFile(FileModel file) => file switch
    {
        WordFile word => new WordFileNode(word.id, word.name, word.createdTime, word.size, word.pageCount),
        ImageFile image => new ImageFileNode(image.id, image.name, image.createdTime, image.size, image.width, image.height),
        TextFile text => new TextFileNode(text.id, text.name, text.createdTime, text.size, text.encoding),
        _ => throw new InvalidOperationException($"Unsupported file type: {file.GetType().Name}")
    };
}

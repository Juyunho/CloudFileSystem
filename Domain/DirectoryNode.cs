namespace CloudFileSystem.Domain;

public sealed class DirectoryNode : FileSystemNode
{
    private readonly List<FileSystemNode> _children = [];

    public DirectoryNode(int id, string name, DateTime createdTime) : base(id, name, createdTime) { }

    public IReadOnlyList<FileSystemNode> Children => _children;
    public override long TotalSize => _children.Sum(child => child.TotalSize);

    public void Add(FileSystemNode child, int? index = null)
    {
        if (child.Parent is not null) throw new InvalidOperationException("A node can belong to only one directory.");
        for (DirectoryNode? current = this; current is not null; current = current.Parent)
            if (ReferenceEquals(current, child))
                throw new InvalidOperationException("A directory cannot contain itself or one of its ancestors.");
        if (index is < 0 || index > _children.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (index.HasValue) _children.Insert(index.Value, child); else _children.Add(child);
        child.Parent = this;
    }

    public int Remove(FileSystemNode child)
    {
        var index = _children.IndexOf(child);
        if (index < 0) throw new InvalidOperationException("The node is not a child of this directory.");
        _children.RemoveAt(index);
        child.Parent = null;
        return index;
    }

    public override FileSystemNode DeepCopy(Func<int> nextDirectoryId, Func<int> nextFileId)
    {
        var copy = new DirectoryNode(nextDirectoryId(), Name, CreatedTime);
        copy.ReplaceTags(Tags);
        foreach (var child in Children) copy.Add(child.DeepCopy(nextDirectoryId, nextFileId));
        return copy;
    }
}

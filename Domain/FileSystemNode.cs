namespace CloudFileSystem.Domain;

public abstract class FileSystemNode
{
    private readonly List<string> _tags = [];

    protected FileSystemNode(int id, string name, DateTime createdTime)
    {
        Id = id;
        Name = name;
        CreatedTime = createdTime;
    }

    public int Id { get; }
    public string Name { get; internal set; }
    public DateTime CreatedTime { get; }
    public DirectoryNode? Parent { get; internal set; }
    public IReadOnlyList<string> Tags => _tags;
    public abstract long TotalSize { get; }

    public void ReplaceTags(IEnumerable<string> tags)
    {
        _tags.Clear();
        _tags.AddRange(tags);
    }

    public abstract FileSystemNode DeepCopy(Func<int> nextDirectoryId, Func<int> nextFileId);
}

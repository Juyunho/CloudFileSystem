namespace CloudFileSystem.Domain;

public abstract class FileNode : FileSystemNode
{
    protected FileNode(int id, string name, DateTime createdTime, long size) : base(id, name, createdTime) => Size = size;
    public long Size { get; }
    public override long TotalSize => Size;
}

public sealed class WordFileNode(int id, string name, DateTime createdTime, long size, int pageCount)
    : FileNode(id, name, createdTime, size)
{
    public int PageCount { get; } = pageCount;
    public override FileSystemNode DeepCopy(Func<int> nextDirectoryId, Func<int> nextFileId)
    {
        var copy = new WordFileNode(nextFileId(), Name, CreatedTime, Size, PageCount); copy.ReplaceTags(Tags); return copy;
    }
}

public sealed class ImageFileNode(int id, string name, DateTime createdTime, long size, int width, int height)
    : FileNode(id, name, createdTime, size)
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    public override FileSystemNode DeepCopy(Func<int> nextDirectoryId, Func<int> nextFileId)
    {
        var copy = new ImageFileNode(nextFileId(), Name, CreatedTime, Size, Width, Height); copy.ReplaceTags(Tags); return copy;
    }
}

public sealed class TextFileNode(int id, string name, DateTime createdTime, long size, string encoding)
    : FileNode(id, name, createdTime, size)
{
    public string Encoding { get; } = encoding;
    public override FileSystemNode DeepCopy(Func<int> nextDirectoryId, Func<int> nextFileId)
    {
        var copy = new TextFileNode(nextFileId(), Name, CreatedTime, Size, Encoding); copy.ReplaceTags(Tags); return copy;
    }
}

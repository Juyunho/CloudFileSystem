using CloudFileSystem.Domain;

namespace CloudFileSystem.Application.Commands;

public sealed class DeleteNodeCommand : IFileSystemCommand
{
    private readonly FileSystemNode _node;
    private readonly DirectoryNode _parent;
    private int _index;

    public DeleteNodeCommand(FileSystemNode node)
    {
        _node = node;
        _parent = node.Parent ?? throw new InvalidOperationException("The root directory cannot be deleted.");
    }

    public void Execute() => _index = _parent.Remove(_node);
    public void Undo() => _parent.Add(_node, _index);
}

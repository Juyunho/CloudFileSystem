using CloudFileSystem.Domain;

namespace CloudFileSystem.Application.Commands;

public sealed class PasteNodeCommand : IFileSystemCommand
{
    private readonly DirectoryNode _target;
    private readonly FileSystemNode _copy;

    public PasteNodeCommand(DirectoryNode target, FileSystemNode copy)
    {
        _target = target;
        _copy = copy;
    }

    public FileSystemNode Copy => _copy;
    public void Execute() => _target.Add(_copy);
    public void Undo() => _target.Remove(_copy);
}

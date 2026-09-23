namespace CloudFileSystem.Application.Commands;

public interface IFileSystemCommand
{
    void Execute();
    void Undo();
}

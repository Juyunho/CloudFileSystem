namespace CloudFileSystem.Application.Commands;

public sealed class FileSystemCommandHistory
{
    private readonly Stack<IFileSystemCommand> _undo = [];
    private readonly Stack<IFileSystemCommand> _redo = [];

    public void Execute(IFileSystemCommand command)
    {
        command.Execute();
        _undo.Push(command);
        _redo.Clear();
    }

    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        var command = _undo.Peek();
        command.Undo();
        _undo.Pop();
        _redo.Push(command);
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        var command = _redo.Peek();
        command.Execute();
        _redo.Pop();
        _undo.Push(command);
        return true;
    }
}

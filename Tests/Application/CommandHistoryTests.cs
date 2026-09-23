using CloudFileSystem.Application.Commands;
using CloudFileSystem.Domain;

namespace CloudFileSystem.Tests.Application;

public class CommandHistoryTests
{
    [Fact]
    public void Delete_undo_and_redo_preserve_original_position()
    {
        var root = new DirectoryNode(1, "Root", DateTime.UnixEpoch);
        var first = new TextFileNode(1, "first.txt", DateTime.UnixEpoch, 1, "UTF-8");
        var second = new TextFileNode(2, "second.txt", DateTime.UnixEpoch, 1, "UTF-8");
        root.Add(first); root.Add(second);
        var history = new FileSystemCommandHistory();

        history.Execute(new DeleteNodeCommand(first));
        Assert.Equal(["second.txt"], root.Children.Select(x => x.Name));
        Assert.True(history.Undo());
        Assert.Equal(["first.txt", "second.txt"], root.Children.Select(x => x.Name));
        Assert.True(history.Redo());
        Assert.Equal(["second.txt"], root.Children.Select(x => x.Name));
    }

    [Fact]
    public void Tag_command_retains_only_old_and_new_tag_values()
    {
        var node = new TextFileNode(1, "a.txt", DateTime.UnixEpoch, 1, "UTF-8");
        node.ReplaceTags(["Work"]);
        var history = new FileSystemCommandHistory();
        history.Execute(new SetTagsCommand(node, ["Urgent", "Personal"]));
        Assert.Equal(["Urgent", "Personal"], node.Tags);
        Assert.True(history.Undo());
        Assert.Equal(["Work"], node.Tags);
        Assert.True(history.Redo());
        Assert.Equal(["Urgent", "Personal"], node.Tags);
    }

    [Fact]
    public void Failed_undo_keeps_command_available_and_history_remains_usable()
    {
        var command = new ThrowOnceCommand(throwOnFirstUndo: true);
        var history = new FileSystemCommandHistory();
        history.Execute(command);

        Assert.Throws<InvalidOperationException>(() => history.Undo());
        Assert.True(history.Undo());
        Assert.True(history.Redo());
    }

    [Fact]
    public void Failed_redo_keeps_command_available_and_history_remains_usable()
    {
        var command = new ThrowOnceCommand(throwOnFirstRedo: true);
        var history = new FileSystemCommandHistory();
        history.Execute(command);
        Assert.True(history.Undo());

        Assert.Throws<InvalidOperationException>(() => history.Redo());
        Assert.True(history.Redo());
        Assert.True(history.Undo());
    }

    private sealed class ThrowOnceCommand(bool throwOnFirstUndo = false, bool throwOnFirstRedo = false) : IFileSystemCommand
    {
        private int _executeCount;
        private bool _undoFailurePending = throwOnFirstUndo;
        private bool _redoFailurePending = throwOnFirstRedo;

        public void Execute()
        {
            _executeCount++;
            if (_executeCount > 1 && _redoFailurePending)
            {
                _redoFailurePending = false;
                throw new InvalidOperationException("Expected redo failure.");
            }
        }

        public void Undo()
        {
            if (_undoFailurePending)
            {
                _undoFailurePending = false;
                throw new InvalidOperationException("Expected undo failure.");
            }
        }
    }
}

using CloudFileSystem.Domain;

namespace CloudFileSystem.Application.Commands;

public sealed class SetTagsCommand : IFileSystemCommand
{
    private readonly FileSystemNode _node;
    private readonly IReadOnlyList<string> _before;
    private readonly IReadOnlyList<string> _after;

    public SetTagsCommand(FileSystemNode node, IEnumerable<string> tags)
    {
        _node = node;
        _before = node.Tags.ToArray();
        _after = tags.ToArray();
    }

    public void Execute() => _node.ReplaceTags(_after);
    public void Undo() => _node.ReplaceTags(_before);
}

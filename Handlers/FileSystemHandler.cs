using CloudFileSystem.Application;
using CloudFileSystem.Application.Commands;
using CloudFileSystem.Domain;
using CloudFileSystem.Managers;
using CloudFileSystem.Models;
using System.Xml;
using System.Xml.Linq;
using DomainNode = CloudFileSystem.Domain.FileSystemNode;
using ApiNode = CloudFileSystem.Models.FileSystemNode;

namespace CloudFileSystem.Handlers;

public sealed class FileSystemHandler : IFileSystemHandler
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase) { "Urgent", "Work", "Personal" };
    private readonly object _gate = new();
    private readonly DirectoryNode _root;
    private readonly FileSystemCommandHistory _history;

    public FileSystemHandler(IFileManager fileManager, IDirectoryManager directoryManager, FileSystemCommandHistory history)
    {
        _root = new FileSystemTreeFactory(fileManager, directoryManager).Create();
        _history = history;
    }

    public ApiNode getFileTree() { lock (_gate) return ToDto(_root); }

    public ProcessResult<DirectorySize> calculateTotalSize(int directoryId)
    {
        lock (_gate)
        {
            var directory = FindDirectory(directoryId);
            var logs = Visit(directory).Select(node => $"Visiting: {node.Path(" -> ")}").ToList();
            logs.ForEach(Console.WriteLine);
            return new ProcessResult<DirectorySize>
            {
                result = new DirectorySize { size = directory.TotalSize, displaySize = FormatFileSize(directory.TotalSize) },
                logs = logs
            };
        }
    }

    public ProcessResult<List<string>> searchByExtension(int directoryId, string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) throw new ArgumentException("An extension is required.", nameof(extension));
        extension = extension.Trim();
        if (!extension.StartsWith('.')) extension = "." + extension;
        lock (_gate)
        {
            var visited = Visit(FindDirectory(directoryId)).ToList();
            var logs = visited.Select(node => $"Visiting: {node.Path(" -> ")}").ToList();
            logs.ForEach(Console.WriteLine);
            var result = visited.OfType<FileNode>()
                .Where(file => System.IO.Path.GetExtension(file.Name).Equals(extension, StringComparison.OrdinalIgnoreCase))
                .Select(file => file.Path()).ToList();
            return new ProcessResult<List<string>> { result = result, logs = logs };
        }
    }

    public string serializeToXml() { lock (_gate) return new XDocument(ToXml(_root)).ToString(); }

    public void Delete(NodeType nodeType, int id) { lock (_gate) _history.Execute(new DeleteNodeCommand(Find(nodeType, id))); }

    public void SetTags(NodeType nodeType, int id, IEnumerable<string> tags)
    {
        var values = tags.Select(tag => tag.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (values.Any(tag => !AllowedTags.Contains(tag))) throw new ArgumentException("Allowed tags are Urgent, Work, and Personal.");
        lock (_gate)
        {
            var node = Find(nodeType, id);
            if (!node.Tags.SequenceEqual(values, StringComparer.OrdinalIgnoreCase))
                _history.Execute(new SetTagsCommand(node, values));
        }
    }

    public ApiNode Paste(NodeType sourceNodeType, int sourceId, int targetDirectoryId)
    {
        lock (_gate)
        {
            var source = Find(sourceNodeType, sourceId);
            if (ReferenceEquals(source, _root)) throw new InvalidOperationException("The root directory cannot be copied.");
            var target = FindDirectory(targetDirectoryId);
            if (source is DirectoryNode directory && IsSelfOrDescendant(directory, target))
                throw new InvalidOperationException("A directory cannot be pasted into itself or its descendant.");
            var nextDirectoryId = _root.DescendantsAndSelf().OfType<DirectoryNode>().Select(node => node.Id).DefaultIfEmpty(0).Max();
            var nextFileId = _root.DescendantsAndSelf().OfType<FileNode>().Select(node => node.Id).DefaultIfEmpty(0).Max();
            var copy = source.DeepCopy(() => ++nextDirectoryId, () => ++nextFileId);
            copy.Name = ResolveCopyName(source.Name, source is FileNode, target.Children);
            var command = new PasteNodeCommand(target, copy);
            _history.Execute(command);
            return ToDto(command.Copy);
        }
    }

    public bool Undo() { lock (_gate) return _history.Undo(); }
    public bool Redo() { lock (_gate) return _history.Redo(); }

    private DirectoryNode FindDirectory(int id) => _root.DescendantsAndSelf().OfType<DirectoryNode>().SingleOrDefault(node => node.Id == id)
        ?? throw new KeyNotFoundException($"Directory {id} not found.");

    private DomainNode Find(NodeType type, int id)
    {
        DomainNode? node = type == NodeType.directory
            ? _root.DescendantsAndSelf().OfType<DirectoryNode>().SingleOrDefault(node => node.Id == id)
            : _root.DescendantsAndSelf().OfType<FileNode>().SingleOrDefault(node => node.Id == id);
        return node ?? throw new KeyNotFoundException("Node not found.");
    }

    private static IEnumerable<DomainNode> Visit(DomainNode node)
    {
        yield return node;
        if (node is not DirectoryNode directory) yield break;
        foreach (var child in directory.Children)
            foreach (var descendant in Visit(child)) yield return descendant;
    }

    private static bool IsSelfOrDescendant(DirectoryNode source, DirectoryNode target)
    {
        for (DirectoryNode? current = target; current is not null; current = current.Parent)
            if (ReferenceEquals(current, source)) return true;
        return false;
    }

    private static string ResolveCopyName(string original, bool isFile, IEnumerable<DomainNode> siblings)
    {
        var extension = isFile ? System.IO.Path.GetExtension(original) : string.Empty;
        var stem = extension.Length == 0 ? original : original[..^extension.Length];
        var names = siblings.Select(child => child.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = $"{stem} - Copy{extension}";
        for (var number = 2; names.Contains(candidate); number++) candidate = $"{stem} - Copy ({number}){extension}";
        return candidate;
    }

    private static ApiNode ToDto(DomainNode node)
    {
        var dto = new ApiNode
        {
            id = node.Id, name = node.Name, createdTime = node.CreatedTime,
            nodeType = node is DirectoryNode ? NodeType.directory : NodeType.file,
            tags = node.Tags.ToList()
        };
        if (node is DirectoryNode directory) dto.children = directory.Children.Select(ToDto).ToList();
        if (node is FileNode file)
        {
            dto.size = file.Size; dto.displaySize = FormatFileSize(file.Size);
            switch (file)
            {
                case WordFileNode word: dto.fileType = FileType.word; dto.pageCount = word.PageCount; break;
                case ImageFileNode image: dto.fileType = FileType.image; dto.width = image.Width; dto.height = image.Height; break;
                case TextFileNode text: dto.fileType = FileType.text; dto.encoding = text.Encoding; break;
            }
        }
        return dto;
    }

    private static XElement ToXml(DomainNode node)
    {
        var element = new XElement(GetXmlElementName(node.Name));
        if (node is DirectoryNode directory)
        {
            foreach (var child in directory.Children) element.Add(ToXml(child));
            return element;
        }
        var file = (FileNode)node;
        var details = file switch
        {
            WordFileNode word => $"頁數: {word.PageCount}, 大小: {FormatFileSize(file.Size).Replace(" ", "")}",
            ImageFileNode image => $"解析度: {image.Width}x{image.Height}, 大小: {FormatFileSize(file.Size).Replace(" ", "")}",
            TextFileNode text => $"編碼: {text.Encoding}, 大小: {FormatFileSize(file.Size).Replace(" ", "")}",
            _ => $"大小: {FormatFileSize(file.Size).Replace(" ", "")}"
        };
        element.Value = details;
        return element;
    }

    private static string GetXmlElementName(string name)
    {
        var displayName = name switch
        {
            "根目錄" => "根目錄_Root",
            "專案文件" => "專案文件_Project_Docs",
            "個人筆記" => "個人筆記_Personal_Notes",
            "2025 備份" => "Archive_2025",
            _ => name.Replace('.', '_')
        };
        return XmlConvert.EncodeLocalName(displayName);
    }

    private static string FormatFileSize(long size)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double displaySize = size; var unit = 0;
        while (displaySize >= 1024 && unit < units.Length - 1) { displaySize /= 1024; unit++; }
        return $"{displaySize:0.##} {units[unit]}";
    }
}

using CloudFileSystem.Managers;
using CloudFileSystem.Managers.Impl;
using CloudFileSystem.Models;
using System.Xml;
using System.Xml.Linq;

namespace CloudFileSystem.Handlers;

public class FileSystemHandler
{
    private readonly object _gate = new();
    private FileSystemNode _root;
    private readonly Stack<FileSystemNode> _undo = new();
    private readonly Stack<FileSystemNode> _redo = new();
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
        { "Urgent", "Work", "Personal" };

    public FileSystemHandler() : this(new FileManager(), new DirectoryManager()) { }

    public FileSystemHandler(IFileManager fileManager, IDirectoryManager directoryManager)
    {
        var directories = directoryManager.getAllDirectories();
        var files = fileManager.getAllFiles();
        if (directories.Count(d => d.parentId == null) != 1)
            throw new InvalidOperationException("Exactly one root directory is required.");
        if (directories.Select(d => d.id).Distinct().Count() != directories.Count ||
            files.Select(f => f.id).Distinct().Count() != files.Count)
            throw new InvalidOperationException("Duplicate node IDs are not allowed within a node type.");
        var directoryIds = directories.Select(d => d.id).ToHashSet();
        if (directories.Any(d => d.parentId != null && !directoryIds.Contains(d.parentId.Value)) ||
            files.Any(f => !directoryIds.Contains(f.directoryId)))
            throw new InvalidOperationException("Every node must belong to an existing directory.");
        _root = buildTree(directories.Single(d => d.parentId == null), directories, files, new HashSet<int>());
        if (CountDirectories(_root) != directories.Count)
            throw new InvalidOperationException("All directories must be reachable from the root.");
    }

    public FileSystemNode getFileTree()
    {
        lock (_gate) return Clone(_root);
    }

    public ProcessResult<DirectorySize> calculateTotalSize(int directoryId)
    {
        lock (_gate)
        {
            var directory = FindDirectory(directoryId);
            var logs = new List<string>();
            var size = calculateNodeSize(directory, logs, "");
            return new ProcessResult<DirectorySize>
            {
                result = new DirectorySize { size = size, displaySize = formatFileSize(size) },
                logs = logs
            };
        }
    }

    public ProcessResult<List<string>> searchByExtension(int directoryId, string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("An extension is required.", nameof(extension));
        extension = extension.Trim();
        if (!extension.StartsWith('.')) extension = "." + extension;
        lock (_gate)
        {
            var result = new List<string>();
            var logs = new List<string>();
            var directory = FindDirectory(directoryId);
            var path = new List<string>();
            FindDirectoryPath(_root, directoryId, path);
            var parentPath = string.Join("/", path.Take(path.Count - 1));
            searchFilesByExtension(directory, extension, parentPath, result, logs);
            return new ProcessResult<List<string>> { result = result, logs = logs };
        }
    }

    private static bool FindDirectoryPath(FileSystemNode node, int id, List<string> path)
    {
        path.Add(node.name);
        if (node.nodeType == NodeType.directory && node.id == id) return true;
        foreach (var child in node.children.Where(c => c.nodeType == NodeType.directory))
            if (FindDirectoryPath(child, id, path)) return true;
        path.RemoveAt(path.Count - 1);
        return false;
    }

    public string serializeToXml()
    {
        lock (_gate) return new XDocument(serializeNodeToXml(_root)).ToString();
    }

    public void Delete(NodeType nodeType, int id)
    {
        lock (_gate)
        {
            if (nodeType == NodeType.directory && id == _root.id)
                throw new InvalidOperationException("The root directory cannot be deleted.");
            var parent = FindParent(_root, nodeType, id) ?? throw new KeyNotFoundException("Node not found.");
            SaveForUndo();
            parent.children.RemoveAll(n => n.nodeType == nodeType && n.id == id);
        }
    }

    public void SetTags(NodeType nodeType, int id, IEnumerable<string> tags)
    {
        var values = tags.Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (values.Any(t => !AllowedTags.Contains(t)))
            throw new ArgumentException("Allowed tags are Urgent, Work, and Personal.");
        lock (_gate)
        {
            var node = Find(_root, nodeType, id) ?? throw new KeyNotFoundException("Node not found.");
            if (node.tags.SequenceEqual(values, StringComparer.OrdinalIgnoreCase)) return;
            SaveForUndo();
            node.tags = values;
        }
    }

    public FileSystemNode Paste(NodeType sourceNodeType, int sourceId, int targetDirectoryId)
    {
        lock (_gate)
        {
            var source = Find(_root, sourceNodeType, sourceId) ?? throw new KeyNotFoundException("Source node not found.");
            var target = Find(_root, NodeType.directory, targetDirectoryId) ?? throw new KeyNotFoundException("Target directory not found.");
            if (source.nodeType == NodeType.directory)
            {
                if (source.id == _root.id) throw new InvalidOperationException("The root directory cannot be copied.");
                if (ContainsDirectory(source, targetDirectoryId))
                    throw new InvalidOperationException("A directory cannot be pasted into itself or its descendant.");
            }

            var nextDirectoryId = MaxId(_root, NodeType.directory);
            var nextFileId = MaxId(_root, NodeType.file);
            var copy = DeepCopyWithNewIds(source, ref nextDirectoryId, ref nextFileId);
            copy.name = ResolveCopyName(source.name, source.nodeType, target.children);
            SaveForUndo();
            target.children.Add(copy);
            return Clone(copy);
        }
    }

    public bool Undo()
    {
        lock (_gate)
        {
            if (_undo.Count == 0) return false;
            _redo.Push(Clone(_root));
            _root = _undo.Pop();
            return true;
        }
    }

    public bool Redo()
    {
        lock (_gate)
        {
            if (_redo.Count == 0) return false;
            _undo.Push(Clone(_root));
            _root = _redo.Pop();
            return true;
        }
    }

    private void SaveForUndo()
    {
        _undo.Push(Clone(_root));
        _redo.Clear();
    }

    private FileSystemNode FindDirectory(int id) =>
        Find(_root, NodeType.directory, id) ?? throw new KeyNotFoundException($"Directory {id} not found.");

    private static FileSystemNode? Find(FileSystemNode node, NodeType type, int id)
    {
        if (node.nodeType == type && node.id == id) return node;
        foreach (var child in node.children)
        {
            var found = Find(child, type, id);
            if (found != null) return found;
        }
        return null;
    }

    private static FileSystemNode? FindParent(FileSystemNode node, NodeType type, int id)
    {
        if (node.children.Any(c => c.nodeType == type && c.id == id)) return node;
        foreach (var child in node.children.Where(c => c.nodeType == NodeType.directory))
        {
            var found = FindParent(child, type, id);
            if (found != null) return found;
        }
        return null;
    }

    private static int CountDirectories(FileSystemNode node) =>
        (node.nodeType == NodeType.directory ? 1 : 0) + node.children.Sum(CountDirectories);

    private static bool ContainsDirectory(FileSystemNode node, int directoryId) =>
        (node.nodeType == NodeType.directory && node.id == directoryId) || node.children.Any(child => ContainsDirectory(child, directoryId));

    private static int MaxId(FileSystemNode node, NodeType type) =>
        Math.Max(node.nodeType == type ? node.id : 0, node.children.Select(child => MaxId(child, type)).DefaultIfEmpty(0).Max());

    private static FileSystemNode DeepCopyWithNewIds(FileSystemNode source, ref int nextDirectoryId, ref int nextFileId)
    {
        var copy = Clone(source);
        copy.id = source.nodeType == NodeType.directory ? ++nextDirectoryId : ++nextFileId;
        copy.children = new List<FileSystemNode>();
        foreach (var child in source.children)
            copy.children.Add(DeepCopyWithNewIds(child, ref nextDirectoryId, ref nextFileId));
        return copy;
    }

    private static string ResolveCopyName(string original, NodeType type, IEnumerable<FileSystemNode> siblings)
    {
        var extension = type == NodeType.file ? Path.GetExtension(original) : string.Empty;
        var stem = extension.Length == 0 ? original : original[..^extension.Length];
        var names = siblings.Select(child => child.name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = $"{stem} - Copy{extension}";
        for (var number = 2; names.Contains(candidate); number++)
            candidate = $"{stem} - Copy ({number}){extension}";
        return candidate;
    }

    private static FileSystemNode Clone(FileSystemNode node) => new()
    {
        id = node.id, name = node.name, createdTime = node.createdTime, nodeType = node.nodeType, size = node.size,
        displaySize = node.displaySize, fileType = node.fileType, pageCount = node.pageCount,
        width = node.width, height = node.height, encoding = node.encoding,
        tags = new List<string>(node.tags), children = node.children.Select(Clone).ToList()
    };

    private static FileSystemNode buildTree(DirectoryModel directory, List<DirectoryModel> directories,
        List<FileModel> files, HashSet<int> ancestors)
    {
        if (!ancestors.Add(directory.id))
            throw new InvalidOperationException("Directory cycle detected.");
        var node = new FileSystemNode { id = directory.id, name = directory.name, createdTime = directory.createdTime, nodeType = NodeType.directory };
        var children = new List<(int order, FileSystemNode node)>();
        foreach (var child in directories.Where(d => d.parentId == directory.id))
            children.Add((child.displayOrder, buildTree(child, directories, files, ancestors)));
        foreach (var file in files.Where(f => f.directoryId == directory.id))
        {
            var item = new FileSystemNode
            {
                id = file.id, name = file.name, createdTime = file.createdTime, nodeType = NodeType.file,
                size = file.size, displaySize = formatFileSize(file.size)
            };
            switch (file)
            {
                case WordFile word: item.fileType = FileType.word; item.pageCount = word.pageCount; break;
                case ImageFile image: item.fileType = FileType.image; item.width = image.width; item.height = image.height; break;
                case TextFile text: item.fileType = FileType.text; item.encoding = text.encoding; break;
                default: throw new InvalidOperationException($"Unsupported file type: {file.GetType().Name}");
            }
            children.Add((file.displayOrder, item));
        }
        node.children = children.OrderBy(child => child.order).Select(child => child.node).ToList();
        ancestors.Remove(directory.id);
        return node;
    }

    private static string formatFileSize(long size)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double displaySize = size;
        int unitIndex = 0;
        while (displaySize >= 1024 && unitIndex < units.Length - 1)
        {
            displaySize /= 1024;
            unitIndex++;
        }
        return $"{displaySize:0.##} {units[unitIndex]}";
    }

    private static long calculateNodeSize(FileSystemNode node, List<string> logs, string parentPath)
    {
        var currentPath = string.IsNullOrEmpty(parentPath) ? node.name : $"{parentPath} -> {node.name}";
        var log = $"Visiting: {currentPath}";
        Console.WriteLine(log);
        logs.Add(log);
        if (node.nodeType == NodeType.file) return node.size ?? 0;
        return node.children.Sum(child => calculateNodeSize(child, logs, currentPath));
    }

    private static void searchFilesByExtension(FileSystemNode node, string extension, string path,
        List<string> result, List<string> logs)
    {
        var currentPath = string.IsNullOrEmpty(path) ? node.name : $"{path}/{node.name}";
        var log = $"Visiting: {currentPath.Replace("/", " -> ")}";
        Console.WriteLine(log);
        logs.Add(log);
        if (node.nodeType == NodeType.file)
        {
            if (Path.GetExtension(node.name).Equals(extension, StringComparison.OrdinalIgnoreCase))
                result.Add(currentPath);
            return;
        }
        foreach (var child in node.children)
            searchFilesByExtension(child, extension, currentPath, result, logs);
    }

    private static XElement serializeNodeToXml(FileSystemNode node)
    {
        var element = new XElement(getXmlElementName(node.name));
        if (node.nodeType == NodeType.directory)
        {
            foreach (var child in node.children) element.Add(serializeNodeToXml(child));
            return element;
        }
        var details = new List<string>();
        if (node.pageCount.HasValue) details.Add($"頁數: {node.pageCount.Value}");
        if (node.width.HasValue && node.height.HasValue)
            details.Add($"解析度: {node.width.Value}x{node.height.Value}");
        if (!string.IsNullOrEmpty(node.encoding)) details.Add($"編碼: {node.encoding}");
        if (node.displaySize != null) details.Add($"大小: {node.displaySize.Replace(" ", "")}");
        element.Value = string.Join(", ", details);
        return element;
    }

    private static string getXmlElementName(string name)
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
}

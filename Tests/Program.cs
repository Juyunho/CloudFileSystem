using CloudFileSystem.Handlers;
using CloudFileSystem.Models;
using CloudFileSystem.Managers;
using System.Xml.Linq;

var tests = new List<(string name, Action run)>
{
    ("sample tree and details", () => {
        var root = new FileSystemHandler().getFileTree();
        Equal("根目錄", root.name);
        Equal(9, Count(root));
        Equal(4, CountDirectories(root));
        Equal("待辦清單.txt", Find(root, NodeType.directory, 3)!.children[0].name);
        Equal("2025 備份", Find(root, NodeType.directory, 3)!.children[1].name);
        var image = Find(root, NodeType.file, 2)!;
        Equal(1920, image.width);
        Equal(1080, image.height);
        Equal(15, Find(root, NodeType.file, 1)!.pageCount);
        True(Find(root, NodeType.file, 1)!.createdTime.HasValue);
        Equal("UTF-8", Find(root, NodeType.file, 3)!.encoding);
    }),
    ("sample sizes and traversal", () => {
        var h = new FileSystemHandler();
        Equal(2815476L, h.calculateTotalSize(1).result!.size);
        Equal(2609152L, h.calculateTotalSize(2).result!.size);
        Equal(205824L, h.calculateTotalSize(3).result!.size);
        var logs = h.calculateTotalSize(1).logs;
        Equal(9, logs.Count);
        Equal("Visiting: 根目錄", logs[0]);
        Equal("Visiting: 根目錄 -> README.txt", logs[^1]);
        Equal("Visiting: 個人筆記 -> 2025 備份 -> 舊會議記錄.docx", h.calculateTotalSize(3).logs[^1]);
    }),
    ("extension paths and logging", () => {
        var h = new FileSystemHandler();
        var found = h.searchByExtension(1, "DOCX");
        Equal(2, found.result!.Count);
        Equal("根目錄/專案文件/需求規格書.docx", found.result[0]);
        Equal("根目錄/個人筆記/2025 備份/舊會議記錄.docx", found.result[1]);
        Equal(9, found.logs.Count);
        Equal(0, h.searchByExtension(1, ".pdf").result!.Count);
        var nested = h.searchByExtension(2, ".docx").result!;
        Equal(1, nested.Count);
        Equal("根目錄/專案文件/需求規格書.docx", nested[0]);
    }),
    ("XML document matches sample", () => {
        var xml = XDocument.Parse(new FileSystemHandler().serializeToXml());
        Equal("根目錄_Root", xml.Root!.Name.LocalName);
        Equal("15", xml.Descendants("需求規格書_docx").Single().Value.Split("頁數: ")[1].Split(',')[0]);
        Equal(5, xml.Descendants().Count(e => !e.HasElements));
    }),
    ("delete, tags, undo and redo", () => {
        var h = new FileSystemHandler();
        h.SetTags(NodeType.file, 1, ["Urgent", "Work"]);
        Equal(2, Find(h.getFileTree(), NodeType.file, 1)!.tags.Count);
        h.Delete(NodeType.file, 1);
        Equal(2303476L, h.calculateTotalSize(1).result!.size);
        Equal(1, h.searchByExtension(1, ".docx").result!.Count);
        True(!h.serializeToXml().Contains("需求規格書_docx"));
        True(h.Undo());
        Equal(2, Find(h.getFileTree(), NodeType.file, 1)!.tags.Count);
        True(h.Redo());
        True(Find(h.getFileTree(), NodeType.file, 1) == null);
        True(h.Undo());
        h.SetTags(NodeType.file, 1, ["Personal"]);
        True(!h.Redo());
    }),
    ("invalid operations", () => {
        var h = new FileSystemHandler();
        Throws<InvalidOperationException>(() => h.Delete(NodeType.directory, 1));
        Throws<KeyNotFoundException>(() => h.Delete(NodeType.file, 999));
        Throws<ArgumentException>(() => h.SetTags(NodeType.file, 1, ["Unknown"]));
        Throws<KeyNotFoundException>(() => h.calculateTotalSize(999));
        Throws<ArgumentException>(() => h.searchByExtension(1, " "));
    }),
    ("directory delete and multiple undo steps", () => {
        var h = new FileSystemHandler();
        h.SetTags(NodeType.directory, 3, ["Personal", "Work"]);
        h.Delete(NodeType.directory, 3);
        Equal(2609652L, h.calculateTotalSize(1).result!.size);
        True(h.Undo());
        Equal(2, Find(h.getFileTree(), NodeType.directory, 3)!.tags.Count);
        True(h.Undo());
        Equal(0, Find(h.getFileTree(), NodeType.directory, 3)!.tags.Count);
        True(h.Redo());
        True(h.Redo());
        True(Find(h.getFileTree(), NodeType.directory, 3) == null);
    }),
    ("empty directory and invalid fixture", () => {
        var folders = new FakeDirectories([
            new DirectoryModel { id = 1, name = "Root" },
            new DirectoryModel { id = 2, parentId = 1, name = "Empty" }
        ]);
        var h = new FileSystemHandler(new FakeFiles([]), folders);
        Equal(0L, h.calculateTotalSize(2).result!.size);
        Equal(0, h.searchByExtension(2, ".txt").result!.Count);
        Equal(2, h.calculateTotalSize(1).logs.Count);
        Throws<InvalidOperationException>(() => new FileSystemHandler(
            new FakeFiles([new TextFile { id = 1, directoryId = 99, name = "orphan.txt" }]), folders));
    }),
    ("file paste preserves domain data and resolves names", () => {
        var h = new FileSystemHandler();
        h.SetTags(NodeType.file, 1, ["Urgent", "Work"]);
        var first = h.Paste(NodeType.file, 1, 2);
        var second = h.Paste(NodeType.file, 1, 2);
        True(first.id != 1 && second.id != first.id);
        Equal(15, first.pageCount); Equal(500L * 1024, first.size); Equal(2, first.tags.Count);
        Equal("需求規格書 - Copy.docx", first.name);
        Equal("需求規格書 - Copy (2).docx", second.name);
        var image = h.Paste(NodeType.file, 2, 3);
        Equal(1920, image.width); Equal(1080, image.height);
        var text = h.Paste(NodeType.file, 3, 2);
        Equal("UTF-8", text.encoding);
        True(Find(h.getFileTree(), NodeType.file, 1) != null);
    }),
    ("directory deep paste rebuilds identity and supports history", () => {
        var h = new FileSystemHandler();
        var original = Find(h.getFileTree(), NodeType.directory, 3)!;
        var copy = h.Paste(NodeType.directory, 3, 2);
        Equal("個人筆記", original.name); Equal("個人筆記 - Copy", copy.name);
        True(copy.id != original.id && copy.children[1].id != original.children[1].id);
        True(copy.children[0].id != original.children[0].id && copy.children[1].children[0].id != original.children[1].children[0].id);
        Equal("UTF-8", copy.children[0].encoding); Equal(5, copy.children[1].children[0].pageCount);
        Equal(13, Count(h.getFileTree()));
        True(h.Undo()); Equal(9, Count(h.getFileTree()));
        True(h.Redo()); Equal(13, Count(h.getFileTree()));
        True(Find(h.getFileTree(), NodeType.directory, 3) != null);
    }),
    ("paste rejects illegal targets", () => {
        var h = new FileSystemHandler();
        Throws<InvalidOperationException>(() => h.Paste(NodeType.directory, 1, 2));
        Throws<InvalidOperationException>(() => h.Paste(NodeType.directory, 3, 3));
        Throws<InvalidOperationException>(() => h.Paste(NodeType.directory, 3, 4));
        Throws<KeyNotFoundException>(() => h.Paste(NodeType.file, 1, 999));
        Throws<KeyNotFoundException>(() => h.Paste(NodeType.file, 999, 2));
        Equal(9, Count(h.getFileTree()));
    })
};

var failed = 0;
foreach (var (name, run) in tests)
{
    try { run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception e) { failed++; Console.Error.WriteLine($"FAIL {name}: {e.Message}"); }
}
Console.WriteLine($"{tests.Count - failed}/{tests.Count} passed");
return failed == 0 ? 0 : 1;

static int Count(FileSystemNode node) => 1 + node.children.Sum(Count);
static int CountDirectories(FileSystemNode node) => (node.nodeType == NodeType.directory ? 1 : 0) + node.children.Sum(CountDirectories);
static FileSystemNode? Find(FileSystemNode node, NodeType type, int id) =>
    node.nodeType == type && node.id == id ? node : node.children.Select(c => Find(c, type, id)).FirstOrDefault(x => x != null);
static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
static void True(bool value) { if (!value) throw new Exception("Expected true"); }
static void Throws<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}");
}

sealed class FakeDirectories(List<DirectoryModel> values) : IDirectoryManager
{
    public List<DirectoryModel> getAllDirectories() => values;
}

sealed class FakeFiles(List<FileModel> values) : IFileManager
{
    public List<FileModel> getAllFiles() => values;
}

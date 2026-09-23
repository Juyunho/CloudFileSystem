using System.Xml.Linq;
using CloudFileSystem.Models;
using CloudFileSystem.Tests.TestSupport;
using CloudFileSystem.Application.Commands;
using CloudFileSystem.Handlers;
using CloudFileSystem.Managers;

namespace CloudFileSystem.Tests.Application;

public class FileSystemHandlerTests
{
    [Fact]
    public void Builds_required_sample_tree_and_type_metadata()
    {
        var root = TestSystem.CreateHandler().getFileTree();
        Assert.Equal("根目錄", root.name);
        Assert.Equal(["專案文件", "個人筆記", "README.txt"], root.children.Select(node => node.name));
        var word = Find(root, NodeType.file, 1); var image = Find(root, NodeType.file, 2); var text = Find(root, NodeType.file, 3);
        Assert.Equal((FileType.word, 15, 500L * 1024), (word.fileType, word.pageCount, word.size));
        Assert.Equal((FileType.image, 1920, 1080, 2L * 1024 * 1024), (image.fileType, image.width, image.height, image.size));
        Assert.Equal((FileType.text, "UTF-8", 1024L), (text.fileType, text.encoding, text.size));
    }

    [Theory]
    [InlineData(1, 2815476, 9)]
    [InlineData(2, 2609152, 3)]
    [InlineData(3, 205824, 4)]
    public void Calculates_recursive_size_and_traversal(int directoryId, long expectedSize, int visited)
    {
        var result = TestSystem.CreateHandler().calculateTotalSize(directoryId);
        Assert.Equal(expectedSize, result.result!.size);
        Assert.Equal(visited, result.logs.Count);
        Assert.All(result.logs, log => Assert.StartsWith("Visiting: ", log));
    }

    [Theory]
    [InlineData("docx")]
    [InlineData(".DOCX")]
    public void Searches_normalized_extension_with_full_paths(string extension)
    {
        var result = TestSystem.CreateHandler().searchByExtension(1, extension);
        Assert.Equal([
            "根目錄/專案文件/需求規格書.docx",
            "根目錄/個人筆記/2025 備份/舊會議記錄.docx"
        ], result.result);
        Assert.Equal(9, result.logs.Count);
    }

    [Fact]
    public void Search_is_scoped_to_selected_directory()
    {
        var result = TestSystem.CreateHandler().searchByExtension(3, ".docx");
        Assert.Equal(["根目錄/個人筆記/2025 備份/舊會議記錄.docx"], result.result);
    }

    [Fact]
    public void Serializes_complete_hierarchy_and_file_metadata()
    {
        var xml = XDocument.Parse(TestSystem.CreateHandler().serializeToXml());
        Assert.Equal("根目錄_Root", xml.Root!.Name.LocalName);
        Assert.Contains("頁數: 15", xml.ToString());
        Assert.Contains("解析度: 1920x1080", xml.ToString());
        Assert.Contains("編碼: ASCII", xml.ToString());
    }

    [Fact]
    public void Delete_undo_and_redo_restore_subtree()
    {
        var handler = TestSystem.CreateHandler();
        handler.Delete(NodeType.directory, 3);
        Assert.Null(TryFind(handler.getFileTree(), NodeType.directory, 3));
        Assert.True(handler.Undo());
        Assert.NotNull(TryFind(handler.getFileTree(), NodeType.file, 4));
        Assert.True(handler.Redo());
        Assert.Null(TryFind(handler.getFileTree(), NodeType.directory, 3));
    }

    [Fact]
    public void Multiple_tags_are_undoable_and_new_edit_clears_redo()
    {
        var handler = TestSystem.CreateHandler();
        handler.SetTags(NodeType.file, 1, ["Urgent", "Work"]);
        Assert.Equal(["Urgent", "Work"], Find(handler.getFileTree(), NodeType.file, 1).tags);
        Assert.True(handler.Undo());
        Assert.Empty(Find(handler.getFileTree(), NodeType.file, 1).tags);
        handler.SetTags(NodeType.file, 1, ["Personal"]);
        Assert.False(handler.Redo());
    }

    [Fact]
    public void File_paste_preserves_metadata_tags_and_assigns_new_identity()
    {
        var handler = TestSystem.CreateHandler();
        handler.SetTags(NodeType.file, 1, ["Urgent", "Work"]);
        var copy = handler.Paste(NodeType.file, 1, 3);
        Assert.NotEqual(1, copy.id);
        Assert.Equal("需求規格書 - Copy.docx", copy.name);
        Assert.Equal((15, 500L * 1024), (copy.pageCount, copy.size));
        Assert.Equal(["Urgent", "Work"], copy.tags);
        Assert.Equal("需求規格書.docx", Find(handler.getFileTree(), NodeType.file, 1).name);
    }

    [Fact]
    public void Directory_paste_is_deep_assigns_new_ids_and_supports_undo_redo()
    {
        var handler = TestSystem.CreateHandler();
        var copy = handler.Paste(NodeType.directory, 3, 2);
        Assert.Equal("個人筆記 - Copy", copy.name);
        Assert.NotEqual(3, copy.id);
        Assert.Equal(2, copy.children.Count);
        Assert.NotEqual(3, copy.children[0].id);
        Assert.NotEqual(4, copy.children[1].children.Single().id);
        Assert.True(handler.Undo());
        Assert.DoesNotContain(handler.getFileTree().children[0].children, node => node.name == "個人筆記 - Copy");
        Assert.True(handler.Redo());
        Assert.Contains(handler.getFileTree().children[0].children, node => node.name == "個人筆記 - Copy");
    }

    [Fact]
    public void Paste_resolves_repeated_name_collisions()
    {
        var handler = TestSystem.CreateHandler();
        Assert.Equal("README - Copy.txt", handler.Paste(NodeType.file, 5, 2).name);
        Assert.Equal("README - Copy (2).txt", handler.Paste(NodeType.file, 5, 2).name);
    }

    [Theory]
    [InlineData(NodeType.directory, 1, 2)]
    [InlineData(NodeType.directory, 3, 3)]
    [InlineData(NodeType.directory, 3, 4)]
    public void Rejects_root_self_and_descendant_paste(NodeType type, int sourceId, int targetId)
    {
        var handler = TestSystem.CreateHandler();
        Assert.Throws<InvalidOperationException>(() => handler.Paste(type, sourceId, targetId));
        Assert.Equal(9, Count(handler.getFileTree()));
    }

    [Fact]
    public void Rejects_root_delete_unknown_nodes_and_invalid_tags()
    {
        var handler = TestSystem.CreateHandler();
        Assert.Throws<InvalidOperationException>(() => handler.Delete(NodeType.directory, 1));
        Assert.Throws<KeyNotFoundException>(() => handler.Delete(NodeType.file, 999));
        Assert.Throws<ArgumentException>(() => handler.SetTags(NodeType.file, 1, ["Unknown"]));
        Assert.Throws<ArgumentException>(() => handler.searchByExtension(1, " "));
    }

    [Fact]
    public void Pastes_empty_directory_when_tree_contains_no_files()
    {
        var directories = new[]
        {
            new DirectoryModel { id = 1, name = "Root" },
            new DirectoryModel { id = 2, parentId = 1, name = "Empty" }
        };
        var handler = new FileSystemHandler(new StubFiles(), new StubDirectories(directories), new FileSystemCommandHistory());

        var copy = handler.Paste(NodeType.directory, 2, 1);

        Assert.Equal("Empty - Copy", copy.name);
        Assert.Empty(copy.children);
    }

    [Fact]
    public void Repeated_directory_paste_keeps_directory_and_file_ids_unique()
    {
        var handler = TestSystem.CreateHandler();
        handler.Paste(NodeType.directory, 3, 2);
        handler.Paste(NodeType.directory, 3, 2);
        var nodes = Flatten(handler.getFileTree()).ToList();

        var directoryIds = nodes.Where(node => node.nodeType == NodeType.directory).Select(node => node.id).ToList();
        var fileIds = nodes.Where(node => node.nodeType == NodeType.file).Select(node => node.id).ToList();
        Assert.Equal(directoryIds.Count, directoryIds.Distinct().Count());
        Assert.Equal(fileIds.Count, fileIds.Distinct().Count());
    }

    private static FileSystemNode Find(FileSystemNode root, NodeType type, int id) => TryFind(root, type, id) ?? throw new Xunit.Sdk.XunitException("Node not found");
    private static FileSystemNode? TryFind(FileSystemNode node, NodeType type, int id) =>
        node.nodeType == type && node.id == id ? node : node.children.Select(child => TryFind(child, type, id)).FirstOrDefault(found => found is not null);
    private static int Count(FileSystemNode node) => 1 + node.children.Sum(Count);
    private static IEnumerable<FileSystemNode> Flatten(FileSystemNode node)
    {
        yield return node;
        foreach (var child in node.children)
            foreach (var descendant in Flatten(child)) yield return descendant;
    }

    private sealed class StubFiles : IFileManager
    {
        public List<FileModel> getAllFiles() => [];
    }

    private sealed class StubDirectories(IEnumerable<DirectoryModel> directories) : IDirectoryManager
    {
        public List<DirectoryModel> getAllDirectories() => directories.ToList();
    }
}

using CloudFileSystem.Domain;

namespace CloudFileSystem.Tests.Domain;

public class CompositeNodeTests
{
    [Fact]
    public void Directory_calculates_size_polymorphically_for_nested_children()
    {
        var root = new DirectoryNode(1, "Root", DateTime.UnixEpoch);
        var nested = new DirectoryNode(2, "Nested", DateTime.UnixEpoch);
        nested.Add(new WordFileNode(1, "a.docx", DateTime.UnixEpoch, 100, 2));
        root.Add(nested);
        root.Add(new TextFileNode(2, "b.txt", DateTime.UnixEpoch, 20, "UTF-8"));

        Assert.Equal(120, root.TotalSize);
        Assert.All(root.Children, child => Assert.Same(root, child.Parent));
        Assert.Same(nested, nested.Children.Single().Parent);
    }

    [Fact]
    public void File_subtypes_expose_only_their_own_metadata()
    {
        FileNode word = new WordFileNode(1, "a.docx", DateTime.UnixEpoch, 100, 15);
        FileNode image = new ImageFileNode(2, "a.png", DateTime.UnixEpoch, 200, 1920, 1080);
        FileNode text = new TextFileNode(3, "a.txt", DateTime.UnixEpoch, 20, "UTF-8");

        Assert.Equal(15, Assert.IsType<WordFileNode>(word).PageCount);
        Assert.Equal((1920, 1080), (Assert.IsType<ImageFileNode>(image).Width, Assert.IsType<ImageFileNode>(image).Height));
        Assert.Equal("UTF-8", Assert.IsType<TextFileNode>(text).Encoding);
    }

    [Fact]
    public void Invalid_insert_index_does_not_modify_parent()
    {
        var parent = new DirectoryNode(1, "Parent", DateTime.UnixEpoch);
        var child = new DirectoryNode(2, "Child", DateTime.UnixEpoch);

        Assert.Throws<ArgumentOutOfRangeException>(() => parent.Add(child, 1));
        Assert.Null(child.Parent);
        Assert.Empty(parent.Children);
    }

    [Fact]
    public void Directory_cannot_add_itself()
    {
        var directory = new DirectoryNode(1, "Directory", DateTime.UnixEpoch);

        Assert.Throws<InvalidOperationException>(() => directory.Add(directory));
        Assert.Null(directory.Parent);
        Assert.Empty(directory.Children);
    }

    [Fact]
    public void Directory_cannot_add_an_ancestor()
    {
        var root = new DirectoryNode(1, "Root", DateTime.UnixEpoch);
        var child = new DirectoryNode(2, "Child", DateTime.UnixEpoch);
        root.Add(child);

        Assert.Throws<InvalidOperationException>(() => child.Add(root));
        Assert.Null(root.Parent);
        Assert.Same(root, child.Parent);
        Assert.Single(root.Children);
        Assert.Empty(child.Children);
    }

    [Fact]
    public void Node_cannot_belong_to_multiple_parents()
    {
        var first = new DirectoryNode(1, "First", DateTime.UnixEpoch);
        var second = new DirectoryNode(2, "Second", DateTime.UnixEpoch);
        var child = new TextFileNode(1, "a.txt", DateTime.UnixEpoch, 1, "UTF-8");
        first.Add(child);

        Assert.Throws<InvalidOperationException>(() => second.Add(child));
        Assert.Same(first, child.Parent);
        Assert.Single(first.Children);
        Assert.Empty(second.Children);
    }

    [Fact]
    public void Adding_the_same_child_twice_is_rejected()
    {
        var parent = new DirectoryNode(1, "Parent", DateTime.UnixEpoch);
        var child = new TextFileNode(1, "a.txt", DateTime.UnixEpoch, 1, "UTF-8");
        parent.Add(child);

        Assert.Throws<InvalidOperationException>(() => parent.Add(child));
        Assert.Same(parent, child.Parent);
        Assert.Single(parent.Children);
    }

    [Fact]
    public void Deep_copy_rebuilds_parent_links_and_owns_independent_tags()
    {
        var original = new DirectoryNode(1, "Original", DateTime.UnixEpoch);
        var nested = new DirectoryNode(2, "Nested", DateTime.UnixEpoch);
        var file = new TextFileNode(1, "a.txt", DateTime.UnixEpoch, 1, "UTF-8");
        file.ReplaceTags(["Work"]);
        nested.Add(file);
        original.Add(nested);
        var nextDirectoryId = 10;
        var nextFileId = 20;

        var copy = Assert.IsType<DirectoryNode>(original.DeepCopy(() => ++nextDirectoryId, () => ++nextFileId));
        var copiedNested = Assert.IsType<DirectoryNode>(copy.Children.Single());
        var copiedFile = Assert.IsType<TextFileNode>(copiedNested.Children.Single());

        Assert.Null(copy.Parent);
        Assert.Same(copy, copiedNested.Parent);
        Assert.Same(copiedNested, copiedFile.Parent);
        copiedFile.ReplaceTags(["Urgent"]);
        Assert.Equal(["Work"], file.Tags);
        Assert.Equal(["Urgent"], copiedFile.Tags);
    }
}

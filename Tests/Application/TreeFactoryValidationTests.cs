using CloudFileSystem.Application;
using CloudFileSystem.Managers;
using CloudFileSystem.Models;

namespace CloudFileSystem.Tests.Application;

public class TreeFactoryValidationTests
{
    [Fact]
    public void Requires_exactly_one_root()
    {
        var directories = new[]
        {
            new DirectoryModel { id = 1, name = "A" },
            new DirectoryModel { id = 2, name = "B" }
        };
        Assert.Throws<InvalidOperationException>(() => Create(directories));
    }

    [Fact]
    public void Rejects_duplicate_directory_identity()
    {
        var directories = new[]
        {
            new DirectoryModel { id = 1, name = "Root" },
            new DirectoryModel { id = 1, parentId = 1, name = "Duplicate" }
        };
        Assert.Throws<InvalidOperationException>(() => Create(directories));
    }

    [Fact]
    public void Rejects_orphan_files()
    {
        var directories = new[] { new DirectoryModel { id = 1, name = "Root" } };
        var files = new FileModel[] { new TextFile { id = 1, directoryId = 99, name = "orphan.txt", encoding = "UTF-8" } };
        Assert.Throws<InvalidOperationException>(() => Create(directories, files));
    }

    private static void Create(IEnumerable<DirectoryModel> directories, IEnumerable<FileModel>? files = null) =>
        new FileSystemTreeFactory(new StubFiles(files ?? []), new StubDirectories(directories)).Create();

    private sealed class StubFiles(IEnumerable<FileModel> files) : IFileManager
    {
        public List<FileModel> getAllFiles() => files.ToList();
    }

    private sealed class StubDirectories(IEnumerable<DirectoryModel> directories) : IDirectoryManager
    {
        public List<DirectoryModel> getAllDirectories() => directories.ToList();
    }
}

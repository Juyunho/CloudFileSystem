using CloudFileSystem.Application.Commands;
using CloudFileSystem.Daos;
using CloudFileSystem.Handlers;
using CloudFileSystem.Managers.Impl;

namespace CloudFileSystem.Tests.TestSupport;

internal static class TestSystem
{
    public static FileSystemHandler CreateHandler() => new(
        new FileManager(new FileDao()),
        new DirectoryManager(new DirectoryDao()),
        new FileSystemCommandHistory());
}

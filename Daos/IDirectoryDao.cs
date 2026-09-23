using CloudFileSystem.Models;

namespace CloudFileSystem.Daos;

public interface IDirectoryDao
{
    List<DirectoryModel> getAllDirectories();
}

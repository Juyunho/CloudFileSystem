using CloudFileSystem.Models;

namespace CloudFileSystem.Daos;

public interface IFileDao
{
    List<FileModel> getAllFiles();
}

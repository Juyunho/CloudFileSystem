using CloudFileSystem.Models;

namespace CloudFileSystem.Managers
{
    public interface IDirectoryManager
    {
        public List<DirectoryModel> getAllDirectories();
    }
}
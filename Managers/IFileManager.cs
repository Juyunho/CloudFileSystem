using CloudFileSystem.Models;

namespace CloudFileSystem.Managers
{
    public interface IFileManager
    {
        public List<FileModel> getAllFiles();
    }
}
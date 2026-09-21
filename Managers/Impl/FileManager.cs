using CloudFileSystem.Daos;
using CloudFileSystem.Models;

namespace CloudFileSystem.Managers.Impl
{
    public class FileManager : IFileManager
    {
        public List<FileModel> getAllFiles()
        {
            return getDao().getAllFiles();
        }

        private FileDao getDao()
        {
            return new FileDao();
        }
    }
}
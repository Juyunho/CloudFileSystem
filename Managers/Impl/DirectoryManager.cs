using CloudFileSystem.Daos;
using CloudFileSystem.Models;

namespace CloudFileSystem.Managers.Impl
{
    public class DirectoryManager : IDirectoryManager
    {
        public List<DirectoryModel> getAllDirectories()
        {
            return getDao().getAllDirectories();
        }

        private DirectoryDao getDao()
        {
            return new DirectoryDao();
        }
    }
}
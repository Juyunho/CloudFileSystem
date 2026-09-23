using CloudFileSystem.Daos;
using CloudFileSystem.Models;

namespace CloudFileSystem.Managers.Impl
{
    public class DirectoryManager : IDirectoryManager
    {
        private readonly IDirectoryDao _dao;

        public DirectoryManager(IDirectoryDao dao) => _dao = dao;

        public List<DirectoryModel> getAllDirectories()
        {
            return _dao.getAllDirectories();
        }
    }
}

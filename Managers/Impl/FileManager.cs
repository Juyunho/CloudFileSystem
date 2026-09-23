using CloudFileSystem.Daos;
using CloudFileSystem.Models;

namespace CloudFileSystem.Managers.Impl
{
    public class FileManager : IFileManager
    {
        private readonly IFileDao _dao;

        public FileManager(IFileDao dao) => _dao = dao;

        public List<FileModel> getAllFiles()
        {
            return _dao.getAllFiles();
        }
    }
}

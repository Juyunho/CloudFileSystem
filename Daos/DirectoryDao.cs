using CloudFileSystem.Models;

namespace CloudFileSystem.Daos
{
    public class DirectoryDao : IDirectoryDao
    {
        public List<DirectoryModel> getAllDirectories()
        {
            return new List<DirectoryModel>()
            {
                new DirectoryModel()
                {
                    id = 1,
                    name = "根目錄",
                    displayOrder = 0,
                    parentId = null,
                },
                new DirectoryModel()
                {
                    id = 2,
                    name = "專案文件",
                    displayOrder = 1,
                    parentId = 1,
                },
                new DirectoryModel()
                {
                    id = 3,
                    name = "個人筆記",
                    displayOrder = 2,
                    parentId = 1,
                },
                new DirectoryModel()
                {
                    id = 4,
                    name = "2025 備份",
                    displayOrder = 2,
                    parentId = 3,
                },
            };
        }
    }
}

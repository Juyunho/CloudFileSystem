using CloudFileSystem.Models;

namespace CloudFileSystem.Daos
{
    public class DirectoryDao
    {
        public List<DirectoryModel> getAllDirectories()
        {
            return new List<DirectoryModel>()
            {
                new DirectoryModel()
                {
                    id = 1,
                    name = "根目錄",
                    parentId = null,
                },
                new DirectoryModel()
                {
                    id = 2,
                    name = "專案文件",
                    parentId = 1,
                },
                new DirectoryModel()
                {
                    id = 3,
                    name = "個人筆記",
                    parentId = 1,
                },
                new DirectoryModel()
                {
                    id = 4,
                    name = "2025 備份",
                    parentId = 3,
                },
            };
        }
    }
}
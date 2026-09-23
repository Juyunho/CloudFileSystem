using CloudFileSystem.Models;

namespace CloudFileSystem.Daos
{
    public class FileDao
    {
        public List<FileModel> getAllFiles()
        {
            return new List<FileModel>()
            {
                new WordFile()
                {   id = 1,
                    directoryId = 2,
                    name = "需求規格書.docx",
                    displayOrder = 1,
                    size = 500 * 1024,
                    pageCount = 15
                },
                new ImageFile()
                {
                    id = 2,
                    directoryId = 2,
                    name = "系統架構圖.png",
                    displayOrder = 2,
                    size = 2 * 1024 * 1024,
                    width = 1920,
                    height = 1080
                },
                new TextFile()
                {
                    id = 3,
                    directoryId = 3,
                    name = "待辦清單.txt",
                    displayOrder = 1,
                    size = 1024,
                    encoding = "UTF-8"
                },
                new WordFile()
                {
                    id = 4,
                    directoryId = 4,
                    name = "舊會議記錄.docx",
                    displayOrder = 1,
                    size = 200 * 1024,
                    pageCount = 5
                },
                new TextFile()
                {
                    id = 5,
                    directoryId = 1,
                    name = "README.txt",
                    displayOrder = 3,
                    size = 500,
                    encoding = "ASCII"
                }
            };
        }
    }
}

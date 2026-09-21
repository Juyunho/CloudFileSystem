using CloudFileSystem.Managers;
using CloudFileSystem.Managers.Impl;
using CloudFileSystem.Models;
using System.Xml.Linq;

namespace CloudFileSystem.Handlers
{
    public class FileSystemHandler
    {
        private readonly IFileManager _fileManager;
        private readonly IDirectoryManager _directoryManager;

        public FileSystemNode getFileTree()
        {
            List<DirectoryModel> directoryList = _directoryManager.getAllDirectories();
            List<FileModel> fileList = _fileManager.getAllFiles();

            DirectoryModel? rootDirectory = directoryList.FirstOrDefault(d => d.parentId == null);
            if (rootDirectory == null)
                throw new Exception("Root directory not found.");

            return buildTree(rootDirectory, directoryList, fileList);
        }

        public ProcessResult<DirectorySize> calculateTotalSize(int directoryId)
        {
            FileSystemNode fileSystemNode = getDirectoryTree(directoryId);

            List<string> logs = new List<string>();
            long size = calculateNodeSize(fileSystemNode, logs);

            DirectorySize directorySize = new DirectorySize
            {
                size = size,
                displaySize = formatFileSize(size)
            };

            return new ProcessResult<DirectorySize>
            {
                result = directorySize,
                logs = logs
            };
        }

        public ProcessResult<List<string>> searchByExtension(int directoryId, string extension)
        {
            FileSystemNode fileSystemNode = getDirectoryTree(directoryId);

            List<string> result = new List<string>();
            List<string> logs = new List<string>();
            searchFilesByExtension(fileSystemNode, extension, "", result, logs);

            return new ProcessResult<List<string>>
            {
                result = result,
                logs = logs
            };
        }

        public string serializeToXml()
        {
            FileSystemNode fileSystemNode = getFileTree();
            XElement xml = serializeNodeToXml(fileSystemNode);
            XDocument document = new XDocument(xml);

            return document.ToString();
        }


        private FileSystemNode buildTree(DirectoryModel rootDirectory, List<DirectoryModel> directoryList, List<FileModel> fileList)
        {
            FileSystemNode node = new FileSystemNode
            {
                id = rootDirectory.id,
                name = rootDirectory.name,
                nodeType = NodeType.directory
            };

            List<DirectoryModel> childDirectories = directoryList
                                                        .Where(dir => dir.parentId == rootDirectory.id)
                                                        .ToList();

            foreach (DirectoryModel childDirectory in childDirectories)
            {
                FileSystemNode childNode = buildTree(childDirectory, directoryList, fileList);
                node.children.Add(childNode);
            }

            List<FileModel> currentFileList = fileList
                                                .Where(file => file.directoryId == rootDirectory.id)
                                                .ToList();

            foreach (FileModel file in currentFileList)
            {
                FileSystemNode fileNode = new FileSystemNode
                {
                    id = file.id,
                    name = file.name,
                    nodeType = NodeType.file,
                    size = file.size,
                    displaySize = formatFileSize(file.size),
                };

                switch (file)
                {
                     case WordFile wordFile:
                        fileNode.fileType = FileType.word;
                        fileNode.pageCount = wordFile.pageCount;
                        break;

                    case ImageFile imageFile:
                        fileNode.fileType = FileType.image;
                        fileNode.width = imageFile.width;
                        fileNode.height = imageFile.height;
                        break;

                    case TextFile textFile:
                        fileNode.fileType = FileType.text;
                        fileNode.encoding = textFile.encoding;
                        break;
                }
                node.children.Add(fileNode);
            }

            return node;
        }

        private string formatFileSize(long size)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double displaySize = size;
            int unitIndex = 0;

            while (displaySize >= 1024 && unitIndex < units.Length - 1)
            {
                displaySize /= 1024;
                unitIndex++;
            }

            return $"{displaySize:0.##} {units[unitIndex]}";
        }

        private long calculateNodeSize(FileSystemNode node, List<string> logs)
        {
            string log = $"Visiting: {node.name}";
            Console.WriteLine(log);
            logs.Add(log);

            if (node.nodeType == NodeType.file)
                return node.size ?? 0;
            
            long totalSize = 0;
            foreach (FileSystemNode child in node.children)
                totalSize += calculateNodeSize(child, logs);

            return totalSize;
        }

        private void searchFilesByExtension(FileSystemNode node, string extension, string currentPath, List<string> result, List<string> logs)
        {
            string currentNodePath = string.IsNullOrEmpty(currentPath) ? node.name : $"{currentPath}/{node.name}";
            string log = $"Visiting: {node.name}";
            Console.WriteLine(log);
            logs.Add(log);

            if (node.nodeType == NodeType.file)
            {
                if (Path.GetExtension(node.name).Equals(extension, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(currentNodePath);
                    Console.WriteLine($"Found: {currentNodePath}");
                }
                    
                return;
            }

            foreach (FileSystemNode child in node.children)
                searchFilesByExtension(child, extension, currentNodePath, result, logs);
        }

        private XElement serializeNodeToXml(FileSystemNode node)
        {
            string elementName = getXmlElementName(node.name);
            XElement element = new XElement(elementName);

            if (node.nodeType == NodeType.directory)
            {
                foreach (FileSystemNode child in node.children)
                {
                    element.Add(serializeNodeToXml(child));
                }
                return element;
            }
            
            List<string> details = new();

            if (node.pageCount.HasValue)
                details.Add($"頁數: {node.pageCount.Value}");

            if (node.width.HasValue && node.height.HasValue)
                details.Add($"解析度: {node.width.Value}x{node.height.Value}");

            if (!string.IsNullOrEmpty(node.encoding))
                details.Add($"編碼: {node.encoding}");

            if (node.displaySize != null)
                    details.Add($"大小： {node.displaySize.Replace(" ", "")}");

            element.Value = string.Join(", ", details);
            return element;
        }

        private string getXmlElementName(string name)
        {
            return name switch
            {
                "我的根目錄" => "根目錄_Root",
                "專案文件" => "專案文件_Project_Docs",
                "個人筆記" => "個人筆記_Personal_Notes",
                "2025 備份" => "Archive_2025",

                _ => name.Replace(".", "_")
            };
        }

        private FileSystemNode getDirectoryTree(int directoryId)
        {
            List<DirectoryModel> directoryList = _directoryManager.getAllDirectories();
            List<FileModel> fileList = _fileManager.getAllFiles();

            DirectoryModel? directory = directoryList.FirstOrDefault(directory => directory.id == directoryId);

            if (directory == null)
                throw new Exception($"Directory with ID {directoryId} not found.");

            return buildTree(directory, directoryList, fileList);
        }

        public FileSystemHandler()
        {
            _fileManager = new FileManager();
            _directoryManager = new DirectoryManager();
        }
    }
        
}
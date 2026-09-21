namespace CloudFileSystem.Models
{
    public class FileSystemNode
    {
        public int id { get; set; }
        public string name { get; set; } = string.Empty;
        public NodeType nodeType { get; set; }
        public long? size { get; set; }
        public string? displaySize { get; set; }
        public FileType? fileType { get; set; }
        public int? pageCount { get; set; }
        public int? width { get; set; }
        public int? height { get; set; }
        public string? encoding { get; set; }
        public List<FileSystemNode> children { get; set; } = new List<FileSystemNode>();

    }
}
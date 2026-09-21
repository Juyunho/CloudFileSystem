using System.ComponentModel.DataAnnotations.Schema;

namespace CloudFileSystem.Models
{
    [Table("File")]
    public class FileModel
    {
        public int id { get; set; }
        public int directoryId { get; set; }
        public string name { get; set; } = string.Empty;
        public long size { get; set; }
        public DateTime createdTime { get; set; } = DateTime.Now;
    }
}
using System.ComponentModel.DataAnnotations.Schema;

namespace CloudFileSystem.Models
{
    [Table("File")]
    public abstract class FileModel
    {
        public int id { get; set; }
        public int directoryId { get; set; }
        public string name { get; set; } = string.Empty;
        public int displayOrder { get; set; }
        public long size { get; set; }
        public DateTime createdTime { get; set; } = DateTime.Now;
    }
}

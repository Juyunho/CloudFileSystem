using System.ComponentModel.DataAnnotations.Schema;

namespace CloudFileSystem.Models
{
    [Table("Directory")]
    public class DirectoryModel
    {
        public int id { get; set; }
        public int? parentId { get; set; }
        public string name { get; set; } = string.Empty;
        public DateTime createdTime { get; set; } = DateTime.Now;
        
    }
}
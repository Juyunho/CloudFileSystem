namespace CloudFileSystem.Models
{
    public class ProcessResult<T>
    {
        public T? result { get; set; }
        public List<string> logs { get; set; } = new List<string>();
    }
}
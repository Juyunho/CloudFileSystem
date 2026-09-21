using Microsoft.AspNetCore.Mvc;
using CloudFileSystem.Handlers;
using CloudFileSystem.Models;

namespace CloudFileSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class FileSystemController : ControllerBase
    {
        [HttpGet]
        public FileSystemNode getFileTree()
        {
            return new FileSystemHandler().getFileTree();
        }

        [HttpGet]
        public ProcessResult<DirectorySize> calculateTotalSize([FromQuery] int directoryId)
        {
            return new FileSystemHandler().calculateTotalSize(directoryId);
        }

        [HttpGet]
        public ProcessResult<List<string>> searchByExtension([FromQuery] int directoryId, [FromQuery] string extension)
        {
            return new FileSystemHandler().searchByExtension(directoryId, extension);
        }

        [HttpGet]
        public string serializeToXml()
        {
            return new FileSystemHandler().serializeToXml();
        }
    }
}
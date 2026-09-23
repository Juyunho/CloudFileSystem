using Microsoft.AspNetCore.Mvc;
using CloudFileSystem.Application;
using CloudFileSystem.Models;

namespace CloudFileSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class FileSystemController : ControllerBase
    {
        private readonly IFileSystemHandler _handler;

        public FileSystemController(IFileSystemHandler handler) => _handler = handler;

        [HttpGet]
        public FileSystemNode getFileTree()
        {
            return _handler.getFileTree();
        }

        [HttpGet]
        public ActionResult<ProcessResult<DirectorySize>> calculateTotalSize([FromQuery] int directoryId)
        {
            try { return Ok(_handler.calculateTotalSize(directoryId)); }
            catch (KeyNotFoundException e) { return NotFound(e.Message); }
        }

        [HttpGet]
        public ActionResult<ProcessResult<List<string>>> searchByExtension([FromQuery] int directoryId, [FromQuery] string extension)
        {
            try { return Ok(_handler.searchByExtension(directoryId, extension)); }
            catch (KeyNotFoundException e) { return NotFound(e.Message); }
            catch (ArgumentException e) { return BadRequest(e.Message); }
        }

        [HttpGet]
        public string serializeToXml()
        {
            return _handler.serializeToXml();
        }

        [HttpDelete]
        public IActionResult deleteNode([FromQuery] NodeType nodeType, [FromQuery] int id)
        {
            try { _handler.Delete(nodeType, id); return NoContent(); }
            catch (KeyNotFoundException e) { return NotFound(e.Message); }
            catch (InvalidOperationException e) { return BadRequest(e.Message); }
        }

        [HttpPut]
        public IActionResult setTags([FromQuery] NodeType nodeType, [FromQuery] int id, [FromBody] List<string>? tags)
        {
            if (tags is null) return BadRequest("Tags are required.");
            try { _handler.SetTags(nodeType, id, tags); return NoContent(); }
            catch (KeyNotFoundException e) { return NotFound(e.Message); }
            catch (ArgumentException e) { return BadRequest(e.Message); }
        }

        [HttpPost]
        public ActionResult<FileSystemNode> pasteNode([FromQuery] NodeType sourceNodeType, [FromQuery] int sourceId,
            [FromQuery] int targetDirectoryId)
        {
            try { return Ok(_handler.Paste(sourceNodeType, sourceId, targetDirectoryId)); }
            catch (KeyNotFoundException e) { return NotFound(e.Message); }
            catch (InvalidOperationException e) { return BadRequest(e.Message); }
        }

        [HttpPost]
        public IActionResult undo() => _handler.Undo() ? NoContent() : Conflict("Nothing to undo.");

        [HttpPost]
        public IActionResult redo() => _handler.Redo() ? NoContent() : Conflict("Nothing to redo.");
    }
}

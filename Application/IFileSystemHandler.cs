using CloudFileSystem.Models;

namespace CloudFileSystem.Application;

public interface IFileSystemHandler
{
    FileSystemNode getFileTree();
    ProcessResult<DirectorySize> calculateTotalSize(int directoryId);
    ProcessResult<List<string>> searchByExtension(int directoryId, string extension);
    string serializeToXml();
    void Delete(NodeType nodeType, int id);
    void SetTags(NodeType nodeType, int id, IEnumerable<string> tags);
    FileSystemNode Paste(NodeType sourceNodeType, int sourceId, int targetDirectoryId);
    bool Undo();
    bool Redo();
}

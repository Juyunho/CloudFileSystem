using System.Net;
using System.Net.Http.Json;
using CloudFileSystem.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text;

namespace CloudFileSystem.Tests.Integration;

public sealed class FileSystemApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public FileSystemApiTests(WebApplicationFactory<Program> factory) => _factory = factory;
    private HttpClient CreateClient() => _factory.WithWebHostBuilder(_ => { }).CreateClient();

    [Fact]
    public async Task Read_endpoints_return_tree_size_search_and_xml()
    {
        using var client = CreateClient();
        var tree = await client.GetFromJsonAsync<FileSystemNode>("/api/FileSystem/getFileTree");
        Assert.Equal("根目錄", tree!.name);
        var size = await client.GetFromJsonAsync<ProcessResult<DirectorySize>>("/api/FileSystem/calculateTotalSize?directoryId=3");
        Assert.Equal(205824, size!.result!.size);
        var search = await client.GetFromJsonAsync<ProcessResult<List<string>>>("/api/FileSystem/searchByExtension?directoryId=1&extension=docx");
        Assert.Equal(2, search!.result!.Count);
        Assert.Contains("根目錄/專案文件/需求規格書.docx", search.result);
        var xml = await client.GetStringAsync("/api/FileSystem/serializeToXml");
        Assert.Contains("根目錄_Root", xml);
    }

    [Fact]
    public async Task Mutation_endpoints_support_tags_delete_paste_and_history()
    {
        using var client = CreateClient();
        var tag = await client.PutAsJsonAsync("/api/FileSystem/setTags?nodeType=2&id=1", new[] { "Urgent", "Work" });
        Assert.Equal(HttpStatusCode.NoContent, tag.StatusCode);
        var paste = await client.PostAsJsonAsync("/api/FileSystem/pasteNode?sourceNodeType=2&sourceId=5&targetDirectoryId=2", new { });
        Assert.Equal(HttpStatusCode.OK, paste.StatusCode);
        var copy = await paste.Content.ReadFromJsonAsync<FileSystemNode>();
        Assert.Equal("README - Copy.txt", copy!.name);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/FileSystem/undo", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/FileSystem/redo", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/FileSystem/deleteNode?nodeType=2&id=1")).StatusCode);
    }

    [Theory]
    [InlineData("/api/FileSystem/deleteNode?nodeType=1&id=1", "DELETE")]
    [InlineData("/api/FileSystem/pasteNode?sourceNodeType=1&sourceId=3&targetDirectoryId=4", "POST")]
    public async Task Invalid_mutations_return_bad_request(string path, string method)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST") request.Content = JsonContent.Create(new { });
        using var client = CreateClient();
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Missing_directory_returns_not_found()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/FileSystem/calculateTotalSize?directoryId=999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Blank_extension_returns_bad_request()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/FileSystem/searchByExtension?directoryId=1&extension=%20");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Null_tags_request_returns_bad_request()
    {
        using var client = CreateClient();
        using var content = new StringContent("null", Encoding.UTF8, "application/json");
        var response = await client.PutAsync("/api/FileSystem/setTags?nodeType=2&id=1", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

"""Run API integration checks against a fresh server instance."""
import json
import subprocess
import time
import urllib.error
import urllib.request
from pathlib import Path

project = Path(__file__).resolve().parents[1]
base = "http://127.0.0.1:5197/api/FileSystem"
server = subprocess.Popen(
    ["dotnet", "run", "--no-build", "--project", str(project / "CloudFileSystem.csproj"),
     "--no-launch-profile", "--urls", "http://127.0.0.1:5197"],
    stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
)

def request(path, method="GET", data=None):
    payload = None if data is None else json.dumps(data).encode()
    req = urllib.request.Request(base + path, data=payload, method=method)
    if payload is not None:
        req.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(req, timeout=5) as response:
        body = response.read().decode()
        return response.status, json.loads(body) if body and response.headers.get_content_type() == "application/json" else body

try:
    for _ in range(50):
        try:
            request("/getFileTree")
            break
        except (urllib.error.URLError, TimeoutError):
            time.sleep(0.2)
    else:
        raise RuntimeError("API did not start")

    _, tree = request("/getFileTree")
    assert tree["name"] == "根目錄" and len(tree["children"]) == 3
    assert [child["name"] for child in tree["children"][1]["children"]] == ["待辦清單.txt", "2025 備份"]
    _, size = request("/calculateTotalSize?directoryId=1")
    assert size["result"]["size"] == 2815476 and len(size["logs"]) == 9
    assert size["logs"][2] == "Visiting: 根目錄 -> 專案文件 -> 需求規格書.docx"
    _, found = request("/searchByExtension?directoryId=1&extension=.docx")
    assert len(found["result"]) == 2 and len(found["logs"]) == 9
    assert found["logs"][7] == "Visiting: 根目錄 -> 個人筆記 -> 2025 備份 -> 舊會議記錄.docx"
    _, nested = request("/searchByExtension?directoryId=2&extension=.docx")
    assert nested["result"] == ["根目錄/專案文件/需求規格書.docx"]
    _, xml = request("/serializeToXml")
    assert "<根目錄_Root>" in xml
    assert request("/setTags?nodeType=2&id=1", "PUT", ["Urgent", "Work"])[0] == 204
    _, tree = request("/getFileTree")
    assert tree["children"][0]["children"][0]["tags"] == ["Urgent", "Work"]
    assert request("/deleteNode?nodeType=2&id=1", "DELETE")[0] == 204
    _, found = request("/searchByExtension?directoryId=1&extension=.docx")
    assert len(found["result"]) == 1
    assert request("/undo", "POST", {})[0] == 204
    _, found = request("/searchByExtension?directoryId=1&extension=.docx")
    assert len(found["result"]) == 2
    assert request("/redo", "POST", {})[0] == 204
    _, found = request("/searchByExtension?directoryId=1&extension=.docx")
    assert len(found["result"]) == 1
    try:
        request("/deleteNode?nodeType=1&id=1", "DELETE")
        raise AssertionError("Expected root deletion to fail")
    except urllib.error.HTTPError as error:
        assert error.code == 400
    status, pasted = request("/pasteNode?sourceNodeType=2&sourceId=5&targetDirectoryId=2", "POST", {})
    assert status == 200 and pasted["id"] != 5 and pasted["name"] == "README - Copy.txt"
    _, tree = request("/getFileTree")
    assert any(child["name"] == "README - Copy.txt" for child in tree["children"][0]["children"])
    assert request("/undo", "POST", {})[0] == 204
    _, tree = request("/getFileTree")
    assert not any(child["name"] == "README - Copy.txt" for child in tree["children"][0]["children"])
    assert request("/redo", "POST", {})[0] == 204
    status, subtree = request("/pasteNode?sourceNodeType=1&sourceId=3&targetDirectoryId=2", "POST", {})
    assert status == 200 and len(subtree["children"]) == 2 and subtree["id"] != 3
    try:
        request("/pasteNode?sourceNodeType=1&sourceId=3&targetDirectoryId=4", "POST", {})
        raise AssertionError("Expected descendant paste to fail")
    except urllib.error.HTTPError as error:
        assert error.code == 400
    print("PASS API integration")
finally:
    server.terminate()
    try:
        server.wait(timeout=5)
    except subprocess.TimeoutExpired:
        server.kill()
        server.wait()

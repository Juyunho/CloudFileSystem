import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { FileSystemService } from './file-system.service';


describe('FileSystemService', () => {
  let service: FileSystemService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(FileSystemService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('requests the sample tree', () => {
    service.getFileTree().subscribe(tree => expect(tree.name).toBe('根目錄'));
    http.expectOne('http://localhost:5182/api/FileSystem/getFileTree').flush({ name: '根目錄' });
  });

  it('passes directory and extension to search', () => {
    service.searchByExtension(3, '.docx').subscribe(result => expect(result.result).toEqual([]));
    const request = http.expectOne(req => req.url.endsWith('/searchByExtension'));
    expect(request.request.params.get('directoryId')).toBe('3');
    expect(request.request.params.get('extension')).toBe('.docx');
    request.flush({ result: [], logs: [] });
  });

  it('sends tags and mutation methods', () => {
    service.setTags(2, 1, ['Urgent', 'Work']).subscribe();
    const tags = http.expectOne(req => req.url.endsWith('/setTags'));
    expect(tags.request.body).toEqual(['Urgent', 'Work']);
    tags.flush(null);
    service.undo().subscribe();
    http.expectOne('http://localhost:5182/api/FileSystem/undo').flush(null);
    service.deleteNode(2, 1).subscribe();
    const deletion = http.expectOne(req => req.url.endsWith('/deleteNode'));
    expect(deletion.request.method).toBe('DELETE');
    expect(deletion.request.params.get('nodeType')).toBe('2');
    expect(deletion.request.params.get('id')).toBe('1');
    deletion.flush(null);
    service.redo().subscribe();
    http.expectOne('http://localhost:5182/api/FileSystem/redo').flush(null);
    service.pasteNode(2, 5, 2).subscribe(node => expect(node.name).toBe('README - Copy.txt'));
    const paste = http.expectOne(req => req.url.endsWith('/pasteNode'));
    expect(paste.request.method).toBe('POST');
    expect(paste.request.params.get('sourceNodeType')).toBe('2');
    expect(paste.request.params.get('sourceId')).toBe('5');
    expect(paste.request.params.get('targetDirectoryId')).toBe('2');
    paste.flush({ id: 6, name: 'README - Copy.txt' });
  });
});

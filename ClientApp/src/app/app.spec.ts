import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { App } from './app';
import { FileSystemComponent } from './file-system/file-system.component';

const sampleTree = {
  id: 1, name: '根目錄', nodeType: 1, tags: [], children: [
    { id: 2, name: '專案文件', nodeType: 1, tags: [], children: [
      { id: 1, name: '需求規格書.docx', createdTime: '2025-01-02T03:04:00', nodeType: 2, fileType: 1, size: 512000, pageCount: 15, displaySize: '500 KB', tags: ['Urgent'], children: [] },
      { id: 2, name: '系統架構圖.png', createdTime: '2025-01-02T03:04:00', nodeType: 2, fileType: 2, size: 2097152, width: 1920, height: 1080, displaySize: '2 MB', tags: [], children: [] }
    ] },
    { id: 3, name: '個人筆記', nodeType: 1, tags: [], children: [
      { id: 3, name: '待辦清單.txt', createdTime: '2025-01-02T03:04:00', nodeType: 2, fileType: 3, size: 1024, displaySize: '1 KB', encoding: 'UTF-8', tags: [], children: [] },
      { id: 4, name: '2025 備份', nodeType: 1, tags: [], children: [
        { id: 4, name: '舊會議記錄.docx', createdTime: '2025-01-02T03:04:00', nodeType: 2, fileType: 1, size: 204800, displaySize: '200 KB', pageCount: 5, tags: [], children: [] }
      ] }
    ] },
    { id: 5, name: 'README.txt', createdTime: '2025-01-02T03:04:00', nodeType: 2, fileType: 3, size: 500, displaySize: '500 B', encoding: 'ASCII', tags: [], children: [] }
  ]
};

describe('App', () => {
  it('renders the file system workspace with sample data', async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('http://localhost:5182/api/FileSystem/getFileTree').flush(sampleTree);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const rows = [...element.querySelectorAll('.tree-row')];
    expect(rows.map(row => row.querySelector('.tree-name')?.textContent?.trim())).toEqual([
      '根目錄', '專案文件', '需求規格書.docx', '系統架構圖.png', '個人筆記', '待辦清單.txt', '2025 備份', '舊會議記錄.docx', 'README.txt'
    ]);
    expect(element.textContent).toContain('pages: 15');
    expect(element.textContent).toContain('res: 1920×1080');
    expect(element.textContent).toContain('enc: UTF-8');
    expect(element.textContent).toContain('500 B');
    expect(element.querySelector('.tag-urgent')?.textContent).toContain('URGENT');
    const component = fixture.debugElement.children[0].componentInstance as FileSystemComponent;
    component.selectDirectory(component.fileSystemItem()[1]);
    component.calculateTotalSize();
    http.expectOne(req => req.url.endsWith('/calculateTotalSize')).flush({ result: { size: 2609152, displaySize: '2.49 MB' }, logs: ['Visiting: 專案文件'] });
    component.extension = '.docx';
    component.searchByExtension();
    http.expectOne(req => req.url.endsWith('/searchByExtension')).flush({ result: ['根目錄/專案文件/需求規格書.docx'], logs: ['Visiting: 專案文件'] });
    fixture.detectChanges();
    expect(element.textContent).toContain('2.49 MB');
    expect(element.textContent).not.toContain('根目錄/專案文件/需求規格書.docx');
    expect(element.querySelectorAll('.tree-row.search-match')).toHaveLength(1);
    expect(element.textContent).toContain('Visiting: 專案文件');
    component.selectDirectory(component.fileSystemItem()[2]);
    fixture.detectChanges();
    expect(element.textContent).toContain('Created Time');
    http.verify();
  });

  it('updates tags, deletes a file, and restores it with undo', async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('http://localhost:5182/api/FileSystem/getFileTree').flush(sampleTree);
    fixture.detectChanges();
    const component = fixture.debugElement.children[0].componentInstance as FileSystemComponent;
    component.selectDirectory(component.fileSystemItem()[2]);
    component.toggleTag('Work');
    const tags = http.expectOne(req => req.url.endsWith('/setTags'));
    expect(tags.request.body).toEqual(['Urgent', 'Work']);
    tags.flush(null);
    http.expectOne(req => req.url.endsWith('/getFileTree')).flush({
      ...sampleTree,
      children: [{ ...sampleTree.children[0], children: [{ ...sampleTree.children[0].children![0], tags: ['Urgent', 'Work'] }] }, sampleTree.children[1]]
    });
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('.tag-badge').length).toBe(2);

    component.deleteSelected();
    http.expectOne(req => req.url.endsWith('/deleteNode') && req.method === 'DELETE').flush(null);
    http.expectOne(req => req.url.endsWith('/getFileTree')).flush({ ...sampleTree, children: [sampleTree.children[1]] });
    fixture.detectChanges();
    expect([...fixture.nativeElement.querySelectorAll('.tree-name')].map((node: Element) => node.textContent)).not.toContain('需求規格書.docx');

    component.undo();
    http.expectOne(req => req.url.endsWith('/undo')).flush(null);
    http.expectOne(req => req.url.endsWith('/getFileTree')).flush(sampleTree);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('需求規格書.docx');
    expect(component.redoCount()).toBe(1);
    component.redo();
    http.expectOne(req => req.url.endsWith('/redo')).flush(null);
    http.expectOne(req => req.url.endsWith('/getFileTree')).flush({ ...sampleTree, children: [sampleTree.children[1], sampleTree.children[2]] });
    fixture.detectChanges();
    expect([...fixture.nativeElement.querySelectorAll('.tree-name')].map((node: Element) => node.textContent)).not.toContain('需求規格書.docx');
    http.verify();
  });

  it('renders an empty search state and readable XML preview', async () => {
    await TestBed.configureTestingModule({
      imports: [App], providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(req => req.url.endsWith('/getFileTree')).flush(sampleTree);
    const component = fixture.debugElement.children[0].componentInstance as FileSystemComponent;
    component.selectDirectory(component.fileSystemItem()[0]);
    component.extension = 'PDF';
    component.searchByExtension();
    const search = http.expectOne(req => req.url.endsWith('/searchByExtension'));
    expect(search.request.params.get('extension')).toBe('PDF');
    search.flush({ result: [], logs: ['Visiting: 根目錄', 'Visiting: 根目錄 -> 專案文件'] });
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('沒有符合的檔案');
    expect(element.textContent).toContain('根目錄 -> 專案文件');

    component.previewXml();
    http.expectOne(req => req.url.endsWith('/serializeToXml')).flush('<根目錄_Root>\n  <README_txt>大小: 500B</README_txt>\n</根目錄_Root>');
    fixture.detectChanges();
    expect(element.querySelector('.xml-preview pre')?.textContent).toContain('<README_txt>');
    http.verify();
  });

  it('sorts name, size, and extension in both directions', async () => {
    await TestBed.configureTestingModule({
      imports: [App], providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(req => req.url.endsWith('/getFileTree')).flush(structuredClone(sampleTree));
    const component = fixture.debugElement.children[0].componentInstance as FileSystemComponent;
    const directChildren = () => component.fileSystemTree()!.children!.map(node => node.name);
    for (const key of ['name', 'size', 'extension'] as const) {
      component.changeSort(key);
      component.changeSortOrder(true);
      const ascending = directChildren();
      component.changeSortOrder(false);
      const descending = directChildren();
      expect(descending).not.toEqual(ascending);
    }
    http.verify();
  });

  it('undoes and redoes a multiple-tag change with button state updates', async () => {
    await TestBed.configureTestingModule({
      imports: [App], providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(req => req.url.endsWith('/getFileTree')).flush(structuredClone(sampleTree));
    const component = fixture.debugElement.children[0].componentInstance as FileSystemComponent;
    component.selectDirectory(component.fileSystemItem()[2]);
    component.toggleTag('Work');
    http.expectOne(req => req.url.endsWith('/setTags')).flush(null);
    http.expectOne(req => req.url.endsWith('/getFileTree')).flush(structuredClone(sampleTree));
    expect(component.undoCount()).toBe(1);
    expect(component.redoCount()).toBe(0);
    component.undo();
    http.expectOne(req => req.url.endsWith('/undo')).flush(null);
    http.expectOne(req => req.url.endsWith('/getFileTree')).flush(structuredClone(sampleTree));
    expect(component.undoCount()).toBe(0);
    expect(component.redoCount()).toBe(1);
    component.redo();
    http.expectOne(req => req.url.endsWith('/redo')).flush(null);
    http.expectOne(req => req.url.endsWith('/getFileTree')).flush(structuredClone(sampleTree));
    expect(component.undoCount()).toBe(1);
    expect(component.redoCount()).toBe(0);
    http.verify();
  });

  it('highlights every search match inline, preserves selected and tags, and clears highlights', async () => {
    await TestBed.configureTestingModule({
      imports: [App], providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(req => req.url.endsWith('/getFileTree')).flush(structuredClone(sampleTree));
    const component = fixture.debugElement.children[0].componentInstance as FileSystemComponent;
    component.extension = '.docx';
    component.searchByExtension();
    http.expectOne(req => req.url.endsWith('/searchByExtension')).flush({
      result: ['根目錄/專案文件/需求規格書.docx', '根目錄/個人筆記/2025 備份/舊會議記錄.docx'],
      logs: ['Visiting: 根目錄', 'Visiting: 需求規格書.docx']
    });
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const matches = [...element.querySelectorAll('.tree-row.search-match')];
    expect(matches).toHaveLength(2);
    expect(matches.every(row => row.querySelector('.tree-name')?.textContent?.endsWith('.docx'))).toBe(true);
    expect(element.querySelector('.tree-row.search-match .tag-urgent')).not.toBeNull();
    expect(element.textContent).not.toContain('根目錄/專案文件/需求規格書.docx');
    expect(component.consoleLines().at(-1)).toBe('[Result] 找到 2 項');

    const selectedMatch = component.fileSystemItem().find(item => item.name === '需求規格書.docx')!;
    component.selectDirectory(selectedMatch);
    fixture.detectChanges();
    const selectedRow = element.querySelector('.tree-row.selected')!;
    expect(selectedRow.classList.contains('search-match')).toBe(true);

    component.clearSearch();
    fixture.detectChanges();
    expect(element.querySelectorAll('.tree-row.search-match')).toHaveLength(0);
    expect(element.querySelectorAll('.tree-row.selected')).toHaveLength(1);
    expect(component.extension).toBe('');

    component.selectDirectory(component.fileSystemItem()[0]);
    component.extension = '.pdf';
    component.searchByExtension();
    http.expectOne(req => req.url.endsWith('/searchByExtension')).flush({ result: [], logs: ['Visiting: 根目錄'] });
    fixture.detectChanges();
    expect(element.querySelectorAll('.tree-row.search-match')).toHaveLength(0);
    expect(element.textContent).toContain('沒有符合的檔案');
    expect(component.consoleLines().at(-1)).toBe('[Result] 找到 0 項');
    http.verify();
  });

  it('copies to frontend clipboard, pastes through the API, and blocks descendant targets', async () => {
    await TestBed.configureTestingModule({ imports: [App], providers: [provideHttpClient(), provideHttpClientTesting()] }).compileComponents();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(req => req.url.endsWith('/getFileTree')).flush(structuredClone(sampleTree));
    const component = fixture.debugElement.children[0].componentInstance as FileSystemComponent;

    component.selectDirectory(component.fileSystemItem().find(item => item.name === 'README.txt')!);
    component.copySelected();
    expect(component.clipboardItem()?.name).toBe('README.txt');
    expect(component.undoCount()).toBe(0);
    component.selectDirectory(component.fileSystemItem().find(item => item.name === '專案文件')!);
    expect(component.canPaste()).toBe(true);
    component.pasteClipboard();
    const paste = http.expectOne(req => req.url.endsWith('/pasteNode'));
    expect(paste.request.params.get('sourceId')).toBe('5');
    expect(paste.request.params.get('targetDirectoryId')).toBe('2');
    paste.flush({ id: 6, name: 'README - Copy.txt', nodeType: 2, tags: [], children: [] });
    http.expectOne(req => req.url.endsWith('/getFileTree')).flush(structuredClone(sampleTree));
    expect(component.undoCount()).toBe(1);
    expect(component.consoleLines().some(line => line.startsWith('[Paste]'))).toBe(true);

    component.selectDirectory(component.fileSystemItem().find(item => item.name === '個人筆記')!);
    component.copySelected();
    component.selectDirectory(component.fileSystemItem().find(item => item.name === '2025 備份')!);
    expect(component.canPaste()).toBe(false);
    http.verify();
  });
});

import { Component, OnInit, signal } from '@angular/core';
import { FileSystemService } from '../service/file-system.service';
import { FileSystemNode } from '../models/file-system-node';
import { FileSystemItem } from '../models/file-system-item';
import { NodeType } from '../models/node-type';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { FileType } from '../models/file-type';

@Component({
  imports: [
    FormsModule,
    DatePipe
  ],
  selector: 'app-file-system',
  styleUrl: './file-system.component.css',
  templateUrl: './file-system.component.html',
})
export class FileSystemComponent implements OnInit {
  fileSystemItem = signal<FileSystemItem[]>([]);
  fileSystemTree = signal<FileSystemNode | null>(null);
  selectedDirectoryId = signal<number | null>(null);
  selectedItem = signal<FileSystemItem | null>(null);
  errorMessage = signal('');
  selectedDirectoryName = signal('');
  extension = '';
  totalSize = signal('');
  searchResults = signal<string[]>([]);
  hasSearched = signal(false);
  processLogs = signal<string[]>([]);
  xmlPreview = signal('');
  sortBy = signal<'name' | 'size' | 'extension' | null>(null);
  sortAscending = signal(true);
  undoCount = signal(0);
  redoCount = signal(0);
  lastOperation = signal('尚未執行操作');
  consoleLines = signal<string[]>(['Cloud File System ready.']);
  processedCount = signal(0);
  operationNodeCount = signal(0);
  clipboardItem = signal<FileSystemItem | null>(null);

  constructor(
    private fileSystemService: FileSystemService
  ) { }

  ngOnInit(): void {
    this.getFileTree();
  }

  getFileTree(): void {
    this.fileSystemService.getFileTree().subscribe({
      next: (result) => {
        this.fileSystemTree.set(result);
        const sortBy = this.sortBy();
        if (sortBy) this.sortTree(result, sortBy, this.sortAscending());

        this.fileSystemItem.set(this.buildFileSystemItem(result));
        const selected = this.selectedItem();
        if (selected) {
          const current = this.fileSystemItem().find(i => i.id === selected.id && i.nodeType === selected.nodeType);
          this.selectedItem.set(current ?? null);
          this.selectedDirectoryId.set(current?.nodeType === NodeType.Directory ? current.id : null);
          this.selectedDirectoryName.set(current?.nodeType === NodeType.Directory ? current.name : '');
        } else {
          const root = this.fileSystemItem()[0];
          if (root) {
            this.selectedItem.set(root);
            this.selectedDirectoryId.set(root.id);
            this.selectedDirectoryName.set(root.name);
          }
        }
        this.errorMessage.set('');
      },
      error: (error) => {
        console.error('Error fetching file tree:', error);
        this.errorMessage.set('無法載入檔案樹。');
      }
    });
  }

  calculateTotalSize(): void {
    const directoryId = this.selectedDirectoryId();
    if (directoryId === null)
      return;

    this.fileSystemService.calculateTotalSize(directoryId).subscribe({
      next: response => {
        this.errorMessage.set('');
        this.totalSize.set(
          response.result.displaySize
        );

        this.processLogs.set(
          response.logs
        );
        this.updateProgress(response.logs.length);
        this.appendConsole([
          `[Operation] Calculate size: ${this.selectedDirectoryName()}`,
          ...response.logs,
          `[Result] Total size: ${response.result.displaySize}`
        ]);
        this.lastOperation.set(`已計算 ${this.selectedDirectoryName()} 的總容量`);
      },
      error: error => {
        console.error('Calculate total size failed:', error);
        this.errorMessage.set('計算大小失敗。');
      }
    });
  }

  searchByExtension(): void {
    const directoryId = this.selectedDirectoryId();
    if (directoryId === null)
      return;

    if (!this.extension.trim())
      return;

    this.fileSystemService.searchByExtension(directoryId, this.extension)
      .subscribe({
        next: (response) => {
          this.errorMessage.set('');
          this.hasSearched.set(true);
          this.searchResults.set(
            response.result
          );

          this.processLogs.set(
            response.logs
          );
          this.updateProgress(response.logs.length);
          this.appendConsole([
            `[Operation] Search extension: ${this.normalizedExtension()}`,
            ...response.logs,
            `[Result] 找到 ${response.result.length} 項`
          ]);
          this.lastOperation.set(`已從 ${this.selectedDirectoryName()} 搜尋 ${this.normalizedExtension()}`);
        },
        error: (error) => {
          console.error('Search failed', error);
          this.errorMessage.set('搜尋失敗。');
        }
      });
  }

  selectDirectory(item: FileSystemItem): void {
    this.selectedItem.set(item);
    this.selectedDirectoryId.set(item.nodeType === NodeType.Directory ? item.id : null);
    this.selectedDirectoryName.set(item.nodeType === NodeType.Directory ? item.name : '');

    this.totalSize.set('');
    this.processLogs.set([]);
    this.processedCount.set(0);
    this.operationNodeCount.set(0);
  }

  deleteSelected(): void {
    const item = this.selectedItem();
    if (!item || (item.nodeType === NodeType.Directory && item.id === this.fileSystemTree()?.id)) return;
    this.fileSystemService.deleteNode(item.nodeType, item.id).subscribe({
      next: () => {
        this.recordEdit(`已刪除 ${item.name}`);
        this.appendConsole([`[Delete] ${item.path}`]);
        this.selectedItem.set(null); this.selectedDirectoryId.set(null); this.selectedDirectoryName.set('');
        this.clearResults(); this.getFileTree();
      },
      error: () => this.errorMessage.set('刪除失敗。')
    });
  }

  copySelected(): void {
    const item = this.selectedItem();
    if (!item || (item.nodeType === NodeType.Directory && item.id === this.fileSystemTree()?.id)) return;
    this.clipboardItem.set({ ...item, tags: [...item.tags] });
    this.appendConsole([`[Copy] ${item.path}`]);
  }

  canPaste(): boolean {
    const source = this.clipboardItem();
    const target = this.selectedItem();
    if (!source || !target || target.nodeType !== NodeType.Directory) return false;
    return source.nodeType !== NodeType.Directory || (target.path !== source.path && !target.path.startsWith(`${source.path}/`));
  }

  pasteClipboard(): void {
    const source = this.clipboardItem();
    const target = this.selectedItem();
    if (!source || !target || !this.canPaste()) return;
    this.fileSystemService.pasteNode(source.nodeType, source.id, target.id).subscribe({
      next: copy => {
        this.recordEdit(`已貼上 ${copy.name}`);
        this.appendConsole([`[Paste] ${source.path} -> ${target.path}/${copy.name}`]);
        this.clearResults(); this.getFileTree();
      },
      error: error => this.errorMessage.set(error?.error || '無法貼上到指定目錄。')
    });
  }

  toggleTag(tag: string): void {
    const item = this.selectedItem();
    if (!item) return;
    const tags = item.tags.includes(tag) ? item.tags.filter(t => t !== tag) : [...item.tags, tag];
    this.fileSystemService.setTags(item.nodeType, item.id, tags).subscribe({
      next: () => {
        this.recordEdit(`已更新 ${item.name} 的標籤`);
        this.appendConsole([`[Tags] ${item.path}: ${tags.length ? tags.join(', ') : 'none'}`]);
        this.getFileTree();
      },
      error: () => this.errorMessage.set('標籤更新失敗。')
    });
  }

  undo(): void {
    if (this.undoCount() === 0) return;
    this.fileSystemService.undo().subscribe({
      next: () => {
        this.undoCount.update(value => value - 1);
        this.redoCount.update(value => value + 1);
        this.lastOperation.set('已復原上一個編輯');
        this.appendConsole(['[Undo] Restored previous state']);
        this.clearResults(); this.getFileTree();
      },
      error: () => this.errorMessage.set('沒有可復原的操作。')
    });
  }

  redo(): void {
    if (this.redoCount() === 0) return;
    this.fileSystemService.redo().subscribe({
      next: () => {
        this.redoCount.update(value => value - 1);
        this.undoCount.update(value => value + 1);
        this.lastOperation.set('已重做上一個編輯');
        this.appendConsole(['[Redo] Reapplied previous state']);
        this.clearResults(); this.getFileTree();
      },
      error: () => this.errorMessage.set('沒有可重做的操作。')
    });
  }

  previewXml(): void {
    this.fileSystemService.serializeToXml().subscribe({
      next: xml => {
        this.errorMessage.set('');
        this.xmlPreview.set(xml);
        this.lastOperation.set('已產生 XML 預覽');
        this.appendConsole(['[Operation] XML serialization complete']);
      },
      error: error => {
        console.error('XML preview failed', error);
        this.errorMessage.set('XML 預覽失敗。');
      }
    });
  }

  downloadXml(): void {
    const download = (xml: string) => {
        const blob = new Blob([xml], { type: 'application/xml'});

        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');

        link.href = url;
        link.download = 'CloudFileSystem.xml';

        link.click();
        window.URL.revokeObjectURL(url);
    };
    if (this.xmlPreview()) {
      download(this.xmlPreview());
      return;
    }
    this.fileSystemService.serializeToXml().subscribe({
      next: xml => { this.xmlPreview.set(xml); download(xml); },
      error: error => {
        console.error('Export XML failed', error);
        this.errorMessage.set('XML 匯出失敗。');
      }
    });
  }

  sortTree(node: FileSystemNode, sortBy: 'name' | 'size' | 'extension', ascending: boolean): void {
    node.children?.sort((a, b) => {
      return this.compareNode(a, b, sortBy, ascending);
    });

    node.children?.forEach(child => {
      if (child.nodeType === NodeType.Directory)
        this.sortTree(child, sortBy, ascending);
    });
  }

  sort(): void {
    const tree = this.fileSystemTree();
    const sortBy = this.sortBy();

    if (tree === null || sortBy === null)
      return;

    this.sortTree(tree, sortBy, this.sortAscending());

    this.fileSystemItem.set(this.buildFileSystemItem(tree));
  }

  changeSort(sortBy: 'name' | 'size' | 'extension'): void {
    if (this.sortBy() === sortBy) this.sortAscending.update(value => !value);
    else { this.sortBy.set(sortBy); this.sortAscending.set(true); }
    this.appendConsole([`[Sort] ${sortBy} ${this.sortAscending() ? 'ascending' : 'descending'}`]);
    this.sort();
  }

  changeSortOrder(ascending: boolean): void {
    this.sortAscending.set(ascending);
    this.sort();
  }

  normalizedExtension(): string {
    const value = this.extension.trim();
    return value.startsWith('.') ? value.toLowerCase() : `.${value.toLowerCase()}`;
  }

  isSearchMatch(item: FileSystemItem): boolean {
    return item.nodeType === NodeType.File && this.searchResults().some(path => path === item.path);
  }

  clearSearch(): void {
    this.extension = '';
    this.searchResults.set([]);
    this.hasSearched.set(false);
  }

  fileTypeName(item: FileSystemItem): string {
    if (item.nodeType === NodeType.Directory) return 'Directory';
    if (item.fileType === FileType.Word) return 'Word';
    if (item.fileType === FileType.Image) return 'Image';
    if (item.fileType === FileType.Text) return 'Text';
    return 'File';
  }

  tagCount(tag: string): number {
    return this.fileSystemItem().filter(item => item.tags.includes(tag)).length;
  }

  progressPercent(): number {
    return this.operationNodeCount() ? Math.round(this.processedCount() / this.operationNodeCount() * 100) : 0;
  }

  removeTagFromTree(event: MouseEvent, item: FileSystemItem, tag: string): void {
    event.stopPropagation();
    this.selectDirectory(item);
    this.toggleTag(tag);
  }

  private updateProgress(count: number): void {
    this.processedCount.set(count);
    this.operationNodeCount.set(count);
  }

  private appendConsole(lines: string[]): void {
    this.consoleLines.update(current => [...current, ...lines]);
  }

  private recordEdit(message: string): void {
    this.undoCount.update(value => value + 1);
    this.redoCount.set(0);
    this.lastOperation.set(message);
    this.errorMessage.set('');
  }

  private clearResults(): void {
    this.totalSize.set('');
    this.searchResults.set([]);
    this.hasSearched.set(false);
    this.processLogs.set([]);
    this.xmlPreview.set('');
    this.processedCount.set(0);
    this.operationNodeCount.set(0);
  }

  private buildFileSystemItem(node: FileSystemNode, level: number = 0, parentPath: string = ''): FileSystemItem[] {
    const items: FileSystemItem[] = [];
    const path = parentPath ? `${parentPath}/${node.name}` : node.name;

    items.push({
      id: node.id,
      name: node.name,
      alias: this.getSampleAlias(node.name),
      path,
      createdTime: node.createdTime,
      icon: this.getNodeIcon(node),
      detail: this.getFileDetail(node),
      level,
      nodeType: node.nodeType,
      size: node.size,
      displaySize: node.displaySize,
      fileType: node.fileType,
      pageCount: node.pageCount,
      width: node.width,
      height: node.height,
      encoding: node.encoding,
      tags: node.tags ?? []
    });

    node.children?.forEach(child => {
      items.push(
        ...this.buildFileSystemItem(child, level + 1, path)
      );
    });
    return items;
  }

  private getNodeIcon(node: FileSystemNode): string {
    if (node.nodeType === NodeType.Directory) 
      return 'bi bi-folder-fill';
    
    switch (node.fileType) {
      case FileType.Word:
        return 'bi bi-file-earmark-word-fill';

      case FileType.Image:
        return 'bi bi-file-earmark-image-fill';

      case FileType.Text:
        return 'bi bi-file-earmark-text-fill';

      default:
        return 'bi bi-file-earmark-fill';
    }
  }

  private getSampleAlias(name: string): string | undefined {
    return ({
      '根目錄': 'Root',
      '專案文件': 'Project_Docs',
      '個人筆記': 'Personal_Notes',
      '2025 備份': 'Archive_2025'
    } as Record<string, string>)[name];
  }
  

  private getFileDetail(node: FileSystemNode): string {
    if (node.nodeType === NodeType.Directory)
      return '';
    
    const details: string[] = [];

    switch (node.fileType) {
      case FileType.Word:
        if (node.displaySize) details.push(node.displaySize);
        if (node.pageCount !== undefined)
          details.push(`pages: ${node.pageCount}`);
        break;

      case FileType.Image:
        if (node.displaySize) details.push(node.displaySize);
        if (node.width !== undefined && node.height !== undefined)
          details.push(`res: ${node.width}×${node.height}`);
        break;

      case FileType.Text:
        if (node.displaySize) details.push(node.displaySize);
        if (node.encoding !== undefined)
          details.push(`enc: ${node.encoding}`);
        break;
    }
    return details.length > 0 ? `(${details.join(', ')})` : '';
  }

  private getExtension(fileName: string): string {
    const index = fileName.lastIndexOf('.');

    if (index === -1)
      return '';

    return fileName.substring(index).toLowerCase();
  }

  private compareNode(a: FileSystemNode, b: FileSystemNode, sortBy: 'name' | 'size' | 'extension', ascending: boolean): number {
    const aIsDirectory = a.nodeType === NodeType.Directory;
    const bIsDirectory = b.nodeType === NodeType.Directory;

    if (aIsDirectory && !bIsDirectory)
      return -1;

    if (!aIsDirectory && bIsDirectory)
      return 1;

    let result = 0;
    if (aIsDirectory && bIsDirectory)
      result = a.name.localeCompare(b.name);

    else if (sortBy === 'name')
      result = a.name.localeCompare(b.name);

    else if (sortBy === 'size')
      result = (a.size ?? 0) - (b.size ?? 0)

    else if (sortBy === 'extension'){
      const extensionA = this.getExtension(a.name);
      const extensionB = this.getExtension(b.name);

      result = extensionA.localeCompare(extensionB);

      if (result === 0)
        result = a.name.localeCompare(b.name);
    }

    return ascending ? result : -result;
  }
}

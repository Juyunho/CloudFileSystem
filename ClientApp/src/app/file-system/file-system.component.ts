import { Component, OnInit, signal } from '@angular/core';
import { FileSystemService } from '../service/file-system.service';
import { FileSystemNode } from '../models/file-system-node';
import { FileSystemItem } from '../models/file-system-item';
import { NodeType } from '../models/node-type';
import { FormsModule } from '@angular/forms';
import { FileType } from '../models/file-type';

@Component({
  imports: [
    FormsModule
  ],
  selector: 'app-file-system',
  styleUrl: './file-system.component.css',
  templateUrl: './file-system.component.html',
})
export class FileSystemComponent implements OnInit {
  fileSystemItem = signal<FileSystemItem[]>([]);
  fileSystemTree = signal<FileSystemNode | null>(null);
  selectedDirectoryId = signal<number | null>(null);
  selectedDirectoryName = signal('');
  extension = '';
  totalSize = signal('');
  searchResults = signal<string[]>([]);
  processLogs = signal<string[]>([]);
  sortBy = signal<'name' | 'size' | 'extension'>('name');
  sortAscending = signal(true);

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
        this.sortBy.set('name');
        this.sortAscending.set(true);

        this.sortTree(result, this.sortBy(), this.sortAscending());

        this.fileSystemItem.set(this.buildFileSystemItem(result));
      },
      error: (error) => {
        console.error('Error fetching file tree:', error);
      }
    });
  }

  calculateTotalSize(): void {
    const directoryId = this.selectedDirectoryId();
    if (directoryId === null)
      return;

    this.fileSystemService.calculateTotalSize(directoryId).subscribe({
      next: response => {
        this.totalSize.set(
          response.result.displaySize
        );

        this.processLogs.set(
          response.logs
        );
      },
      error: error => {
        console.error('Calculate total size failed:', error);
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
          this.searchResults.set(
            response.result
          );

          this.processLogs.set(
            response.logs
          );
        },
        error: (error) => {
          console.error('Search failed', error);
        }
      });
  }

  selectDirectory(item: FileSystemItem): void {
    if (item.nodeType !== NodeType.Directory)
      return;

    this.selectedDirectoryId.set(item.id);
    this.selectedDirectoryName.set(item.name);

    this.totalSize.set('');
    this.searchResults.set([]);
    this.processLogs.set([]);
  }

  exportXml(): void {
    this.fileSystemService.serializeToXml().subscribe({
      next: xml => {
        const blob = new Blob([xml], { type: 'application/xml'});

        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');

        link.href = url;
        link.download = 'CloudFileSystem.xml';

        link.click();
        window.URL.revokeObjectURL(url);
      },
      error: error => {
        console.error('Export XML failed', error);
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

    if (tree === null)
      return;

    this.sortTree(tree, this.sortBy(), this.sortAscending());

    this.fileSystemItem.set(this.buildFileSystemItem(tree));
  }

  changeSort(sortBy: 'name' | 'size' | 'extension'): void {
    if (this.sortBy() === sortBy)
      this.sortAscending.update(value => !value);

    else {
      this.sortBy.set(sortBy);
      this.sortAscending.set(true);
    }

    this.sort();
    console.log('sortBy:', this.sortBy(), 'ascending:', this.sortAscending());
  }

  private buildFileSystemItem(node: FileSystemNode, level: number = 0): FileSystemItem[] {
    const items: FileSystemItem[] = [];

    items.push({
      id: node.id,
      name: node.name,
      icon: this.getNodeIcon(node),
      detail: this.getFileDetail(node),
      level,
      nodeType: node.nodeType
    });

    node.children?.forEach(child => {
      items.push(
        ...this.buildFileSystemItem(child, level + 1)
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
  

  private getFileDetail(node: FileSystemNode): string {
    if (node.nodeType === NodeType.Directory)
      return '';
    
    const details: string[] = [];

    if (node.displaySize)
      details.push(node.displaySize);
    
    switch (node.fileType) {
      case FileType.Word:
        if (node.pageCount !== undefined)
          details.push(`pages: ${node.pageCount}`);
        break;

      case FileType.Image:
        if (node.width !== undefined && node.height !== undefined)
          details.push(`res: ${node.width}x${node.height}`);
        break;

      case FileType.Text:
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

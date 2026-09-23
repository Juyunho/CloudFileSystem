import { NodeType } from './node-type';
import { FileType } from './file-type';

export interface FileSystemItem {
  id: number;
  name: string;
  alias?: string;
  path: string;
  createdTime?: string;
  icon: string;
  detail: string;
  level: number;
  nodeType: NodeType;
  size?: number;
  displaySize?: string;
  fileType?: FileType;
  pageCount?: number;
  width?: number;
  height?: number;
  encoding?: string;
  tags: string[];
}

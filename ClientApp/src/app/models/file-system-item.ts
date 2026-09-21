import { NodeType } from './node-type';

export interface FileSystemItem {
  id: number;
  name: string;
  icon: string;
  detail: string;
  level: number;
  nodeType: NodeType;
}
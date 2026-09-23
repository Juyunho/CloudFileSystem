import { FileType } from "./file-type";
import { NodeType } from "./node-type";

export interface FileSystemNode {
    id: number;
    name: string;
    createdTime?: string;
    nodeType: NodeType;
    size?: number;
    displaySize?: string;
    fileType?: FileType;
    pageCount?: number;
    width?: number;
    height?: number;
    encoding?: string;
    tags: string[];
    children?: FileSystemNode[];
}

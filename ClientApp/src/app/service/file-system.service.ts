import { Injectable } from '@angular/core';     
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

import { FileSystemNode } from '../models/file-system-node';
import { ProcessResult } from '../models/process-result';
import { DirectorySize } from '../models/directory-size';

@Injectable({
    providedIn: 'root'
})

export class FileSystemService {
    private readonly apiUrl = "http://localhost:5182/api/FileSystem";

    constructor(private http: HttpClient) { }
    
    getFileTree(): Observable<FileSystemNode> {
        return this.http.get<FileSystemNode>(`${this.apiUrl}/getFileTree`);
    }

    calculateTotalSize(directoryId: number): Observable<ProcessResult<DirectorySize>> {
        const params = new HttpParams()
                        .set('directoryId', directoryId.toString());
        return this.http.get<ProcessResult<DirectorySize>>(`${this.apiUrl}/calculateTotalSize`, { params });
    }

    searchByExtension(directoryId: number, extension: string): Observable<ProcessResult<string[]>> {
        const params = new HttpParams()
                        .set('directoryId', directoryId.toString())
                        .set('extension', extension);

        return this.http.get<ProcessResult<string[]>>(`${this.apiUrl}/searchByExtension`, { params }
        );
    }

    serializeToXml(): Observable<string> {
        return this.http.get( `${this.apiUrl}/serializeToXml`, { responseType: 'text' });
    }

    deleteNode(nodeType: number, id: number): Observable<void> {
        return this.http.delete<void>(`${this.apiUrl}/deleteNode`, { params: { nodeType, id } });
    }

    setTags(nodeType: number, id: number, tags: string[]): Observable<void> {
        return this.http.put<void>(`${this.apiUrl}/setTags`, tags, { params: { nodeType, id } });
    }

    pasteNode(sourceNodeType: number, sourceId: number, targetDirectoryId: number): Observable<FileSystemNode> {
        return this.http.post<FileSystemNode>(`${this.apiUrl}/pasteNode`, {}, {
            params: { sourceNodeType, sourceId, targetDirectoryId }
        });
    }

    undo(): Observable<void> { return this.http.post<void>(`${this.apiUrl}/undo`, {}); }
    redo(): Observable<void> { return this.http.post<void>(`${this.apiUrl}/redo`, {}); }
}

import { Component, signal } from '@angular/core';
import { FileSystemComponent } from './file-system/file-system.component';

@Component({
  imports: [FileSystemComponent],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  protected readonly title = signal('ClientApp');
}

import { useState } from 'react';
import FileUpload from '@/renderer/components/FileUpload';

function FileUploadDemo() {
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);

  const handleFilesSelected = (files: File[]) => {
    console.log('Files selected:', files);
    setSelectedFiles(prev => [...prev, ...files]);
  };

  return (
    <div style={{ padding: '2rem', maxWidth: '800px', margin: '0 auto' }}>
      <h1 style={{ color: '#e94560', marginBottom: '1rem' }}>
        FileUpload Component Demo
      </h1>
      <p style={{ color: '#a0a0a0', marginBottom: '2rem' }}>
        Try uploading ZIP files using the drag-and-drop zone or browse button.
      </p>
      
      <FileUpload onFilesSelected={handleFilesSelected} />
      
      {selectedFiles.length > 0 && (
        <div style={{ marginTop: '2rem', padding: '1rem', backgroundColor: '#16213e', borderRadius: '8px' }}>
          <h3 style={{ color: '#e94560', marginBottom: '0.5rem' }}>
            Selected Files ({selectedFiles.length})
          </h3>
          <ul style={{ listStyle: 'none', padding: 0 }}>
            {selectedFiles.map((file) => (
              <li
                key={`${file.name}-${file.size}-${file.lastModified}`}
                style={{ color: '#eaeaea', padding: '0.25rem 0' }}
              >
                • {file.name} ({Math.round(file.size / 1024)} KB)
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

export default FileUploadDemo;

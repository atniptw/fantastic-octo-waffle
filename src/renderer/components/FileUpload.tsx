import { useState, useRef, DragEvent, ChangeEvent } from 'react';

/**
 * Represents an uploaded file with its metadata and status
 */
export interface UploadedFile {
  /** Unique identifier for the file */
  id: string;
  /** The File object from the browser */
  file: File;
  /** Display name of the file */
  filename: string;
  /** File size in bytes */
  size: number;
  /** Current upload/processing status */
  status: 'pending' | 'processing' | 'complete' | 'error';
  /** Upload progress percentage (0-100) */
  progress: number;
  /** Error message if status is 'error' */
  errorMessage?: string;
}

/**
 * Props for the FileUpload component
 */
interface FileUploadProps {
  /** Callback fired when valid files are selected */
  onFilesSelected?: (files: File[]) => void;
  /** Whether to allow multiple file selection (default: true) */
  multiple?: boolean;
  /** Accepted file extensions (default: '.zip') */
  accept?: string;
}

/**
 * FileUpload component provides a user interface for uploading files via
 * drag-and-drop or file input button. Supports file type validation and
 * displays upload status for each file.
 * 
 * @example
 * ```tsx
 * <FileUpload 
 *   onFilesSelected={(files) => processFiles(files)}
 *   accept=".zip,.rar"
 *   multiple={true}
 * />
 * ```
 */
function FileUpload({
  onFilesSelected,
  multiple = true,
  accept = '.zip',
}: FileUploadProps) {
  const [uploadedFiles, setUploadedFiles] = useState<UploadedFile[]>([]);
  const [isDragging, setIsDragging] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const formatFileSize = (bytes: number): string => {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB', 'PB'];
    const i = Math.min(Math.floor(Math.log(bytes) / Math.log(k)), sizes.length - 1);
    return `${Math.round((bytes / Math.pow(k, i)) * 100) / 100} ${sizes[i]}`;
  };

  const isValidFileType = (file: File): boolean => {
    const validExtensions = accept.split(',').map(ext => ext.trim().toLowerCase());
    const fileName = file.name;
    const lastDotIndex = fileName.lastIndexOf('.');
    
    // Handle files without extensions
    if (lastDotIndex === -1) {
      return false;
    }
    
    // Handle files that start with a dot (like .htaccess)
    // These have a lastDotIndex of 0, which means the filename is just the extension
    if (lastDotIndex === 0) {
      const fileExtension = fileName.toLowerCase();
      return validExtensions.includes(fileExtension);
    }
    
    const fileExtension = fileName.substring(lastDotIndex).toLowerCase();
    return validExtensions.includes(fileExtension);
  };

  const processFiles = (files: FileList | File[]) => {
    const fileArray = Array.from(files);
    const validFiles: File[] = [];
    const newUploadedFiles: UploadedFile[] = [];
    
    // Get human-readable accepted types for error message
    const acceptedTypes = accept.split(',').map(ext => ext.trim()).join(', ');

    fileArray.forEach(file => {
      if (!isValidFileType(file)) {
        const invalidFile: UploadedFile = {
          id: crypto.randomUUID(),
          file,
          filename: file.name,
          size: file.size,
          status: 'error',
          progress: 0,
          errorMessage: `Invalid file type. Only ${acceptedTypes} files are allowed.`,
        };
        newUploadedFiles.push(invalidFile);
      } else {
        validFiles.push(file);
        const uploadedFile: UploadedFile = {
          id: crypto.randomUUID(),
          file,
          filename: file.name,
          size: file.size,
          status: 'pending',
          progress: 0,
        };
        newUploadedFiles.push(uploadedFile);
      }
    });

    setUploadedFiles(prev => [...prev, ...newUploadedFiles]);

    if (validFiles.length > 0) {
      onFilesSelected?.(validFiles);
    }
  };

  const handleFileInput = (e: ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (files && files.length > 0) {
      processFiles(files);
    }
    // Reset input value to allow re-uploading the same file
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const handleDragEnter = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(true);
  };

  const handleDragLeave = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    // Only set dragging to false if leaving the drop zone itself
    const rect = e.currentTarget.getBoundingClientRect();
    const x = e.clientX;
    const y = e.clientY;
    if (x <= rect.left || x >= rect.right || y <= rect.top || y >= rect.bottom) {
      setIsDragging(false);
    }
  };

  const handleDragOver = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
  };

  const handleDrop = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);

    const files = e.dataTransfer.files;
    if (files && files.length > 0) {
      processFiles(files);
    }
  };

  const handleBrowseClick = () => {
    fileInputRef.current?.click();
  };

  const handleClear = () => {
    setUploadedFiles([]);
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const getStatusIcon = (status: UploadedFile['status']) => {
    switch (status) {
      case 'pending':
        return '⏳';
      case 'processing':
        return '🔄';
      case 'complete':
        return '✅';
      case 'error':
        return '❌';
      default:
        return '📄';
    }
  };

  const getStatusClass = (status: UploadedFile['status']) => {
    switch (status) {
      case 'pending':
        return 'file-item-pending';
      case 'processing':
        return 'file-item-processing';
      case 'complete':
        return 'file-item-complete';
      case 'error':
        return 'file-item-error';
      default:
        return '';
    }
  };

  return (
    <div className="file-upload">
      <div
        className={`drop-zone ${isDragging ? 'drop-zone-active' : ''}`}
        onDragEnter={handleDragEnter}
        onDragLeave={handleDragLeave}
        onDragOver={handleDragOver}
        onDrop={handleDrop}
        role="button"
        tabIndex={0}
        aria-label={`Drop zone for ${accept.split(',').map(ext => ext.trim()).join(', ')} files`}
      >
        <div className="drop-zone-content">
          <div className="drop-zone-icon">📦</div>
          <p className="drop-zone-title">
            {isDragging ? 'Drop ZIP files here' : 'Drag & drop ZIP files here'}
          </p>
          <p className="drop-zone-subtitle">or</p>
          <button
            className="browse-button"
            onClick={handleBrowseClick}
            type="button"
          >
            Browse Files
          </button>
          <input
            ref={fileInputRef}
            type="file"
            accept={accept}
            multiple={multiple}
            onChange={handleFileInput}
            style={{ display: 'none' }}
            aria-label="File input"
          />
        </div>
      </div>

      {uploadedFiles.length > 0 && (
        <div className="upload-list">
          <div className="upload-list-header">
            <h3>Uploaded Files ({uploadedFiles.length})</h3>
            <button
              className="clear-files-button"
              onClick={handleClear}
              type="button"
            >
              Clear All
            </button>
          </div>

          <div className="file-list">
            {uploadedFiles.map(file => (
              <div
                key={file.id}
                className={`file-item ${getStatusClass(file.status)}`}
              >
                <div className="file-item-icon">{getStatusIcon(file.status)}</div>
                <div className="file-item-info">
                  <div className="file-item-name">{file.filename}</div>
                  <div className="file-item-size">
                    {formatFileSize(file.size)}
                  </div>
                  {file.errorMessage && (
                    <div className="file-item-error-message">
                      {file.errorMessage}
                    </div>
                  )}
                </div>
                <div className="file-item-status">
                  {file.status === 'processing' && (
                    <div className="progress-bar">
                      <div
                        className="progress-bar-fill"
                        style={{ width: `${file.progress}%` }}
                      />
                    </div>
                  )}
                  {file.status === 'complete' && (
                    <span className="status-text">Complete</span>
                  )}
                  {file.status === 'pending' && (
                    <span className="status-text">Pending</span>
                  )}
                  {file.status === 'error' && (
                    <span className="status-text">Error</span>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

// Export the component and its props type
export default FileUpload;
export type { FileUploadProps };

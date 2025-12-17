import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import FileUpload from '../FileUpload';

describe('FileUpload', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render the drop zone', () => {
    render(<FileUpload />);
    
    expect(screen.getByText(/drag & drop zip files here/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /browse files/i })).toBeInTheDocument();
  });

  it('should render the drop zone icon', () => {
    render(<FileUpload />);
    
    const dropZone = screen.getByRole('button', { name: /drop zone for .zip files/i });
    expect(dropZone.textContent).toContain('📦');
  });

  it('should have correct default props', () => {
    const onFilesSelected = vi.fn();
    render(<FileUpload onFilesSelected={onFilesSelected} />);
    
    const fileInput = screen.getByLabelText('File input') as HTMLInputElement;
    expect(fileInput.accept).toBe('.zip');
    expect(fileInput.multiple).toBe(true);
  });

  it('should allow custom accept attribute', () => {
    render(<FileUpload accept=".zip,.rar" />);
    
    const fileInput = screen.getByLabelText('File input') as HTMLInputElement;
    expect(fileInput.accept).toBe('.zip,.rar');
  });

  it('should allow single file selection when multiple is false', () => {
    render(<FileUpload multiple={false} />);
    
    const fileInput = screen.getByLabelText('File input') as HTMLInputElement;
    expect(fileInput.multiple).toBe(false);
  });

  it('should trigger file input when browse button is clicked', () => {
    render(<FileUpload />);
    
    const browseButton = screen.getByRole('button', { name: /browse files/i });
    const fileInput = screen.getByLabelText('File input') as HTMLInputElement;
    const clickSpy = vi.spyOn(fileInput, 'click');
    
    fireEvent.click(browseButton);
    
    expect(clickSpy).toHaveBeenCalled();
  });

  it('should handle valid ZIP file selection', async () => {
    const onFilesSelected = vi.fn();
    render(<FileUpload onFilesSelected={onFilesSelected} />);
    
    const fileInput = screen.getByLabelText('File input') as HTMLInputElement;
    const file = new File(['content'], 'test.zip', { type: 'application/zip' });
    
    fireEvent.change(fileInput, { target: { files: [file] } });
    
    await waitFor(() => {
      expect(onFilesSelected).toHaveBeenCalledWith([file]);
      expect(screen.getByText('test.zip')).toBeInTheDocument();
      expect(screen.getByText(/uploaded files \(1\)/i)).toBeInTheDocument();
    });
  });

  it('should handle multiple file selection', async () => {
    const onFilesSelected = vi.fn();
    render(<FileUpload onFilesSelected={onFilesSelected} />);
    
    const fileInput = screen.getByLabelText('File input') as HTMLInputElement;
    const file1 = new File(['content1'], 'test1.zip', { type: 'application/zip' });
    const file2 = new File(['content2'], 'test2.zip', { type: 'application/zip' });
    
    fireEvent.change(fileInput, { target: { files: [file1, file2] } });
    
    await waitFor(() => {
      expect(onFilesSelected).toHaveBeenCalledWith([file1, file2]);
      expect(screen.getByText('test1.zip')).toBeInTheDocument();
      expect(screen.getByText('test2.zip')).toBeInTheDocument();
      expect(screen.getByText(/uploaded files \(2\)/i)).toBeInTheDocument();
    });
  });

  it('should reject invalid file types', async () => {
    const onFilesSelected = vi.fn();
    render(<FileUpload onFilesSelected={onFilesSelected} />);
    
    const fileInput = screen.getByLabelText('File input') as HTMLInputElement;
    const invalidFile = new File(['content'], 'test.txt', { type: 'text/plain' });
    
    fireEvent.change(fileInput, { target: { files: [invalidFile] } });
    
    await waitFor(() => {
      expect(onFilesSelected).not.toHaveBeenCalled();
      expect(screen.getByText('test.txt')).toBeInTheDocument();
      expect(screen.getByText(/invalid file type/i)).toBeInTheDocument();
    });
  });

  it('should show error icon and status for invalid files', async () => {
    render(<FileUpload />);
    
    const fileInput = screen.getByLabelText('File input') as HTMLInputElement;
    const invalidFile = new File(['content'], 'test.pdf', { type: 'application/pdf' });
    
    fireEvent.change(fileInput, { target: { files: [invalidFile] } });
    
    await waitFor(() => {
      const fileItem = screen.getByText('test.pdf').closest('.file-item');
      expect(fileItem).toHaveClass('file-item-error');
      expect(fileItem?.textContent).toContain('❌');
      expect(screen.getByText(/error/i)).toBeInTheDocument();
    });
  });

  it('should allow mix of valid and invalid files', async () => {
    const onFilesSelected = vi.fn();
    render(<FileUpload onFilesSelected={onFilesSelected} />);
    
    const fileInput = screen.getByLabelText('File input') as HTMLInputElement;
    const validFile = new File(['content1'], 'valid.zip', { type: 'application/zip' });
    const invalidFile = new File(['content2'], 'invalid.txt', { type: 'text/plain' });
    
    fireEvent.change(fileInput, { target: { files: [validFile, invalidFile] } });
    
    await waitFor(() => {
      expect(onFilesSelected).toHaveBeenCalledWith([validFile]);
      expect(screen.getByText('valid.zip')).toBeInTheDocument();
      expect(screen.getByText('invalid.txt')).toBeInTheDocument();
      expect(screen.getByText(/uploaded files \(2\)/i)).toBeInTheDocument();
    });
  });

  it('should display file sizes correctly', async () => {
    render(<FileUpload />);
    
    const fileInput = screen.getByLabelText('File input') as HTMLInputElement;
    const file = new File(['a'.repeat(1024)], 'test.zip', { type: 'application/zip' });
    
    Object.defineProperty(file, 'size', { value: 1024 });
    
    fireEvent.change(fileInput, { target: { files: [file] } });
    
    await waitFor(() => {
      expect(screen.getByText('1 KB')).toBeInTheDocument();
    });
  });

  it('should format various file sizes correctly', async () => {
    render(<FileUpload />);
    
    const fileInput = screen.getByLabelText('File input') as HTMLInputElement;
    
    // Test MB size
    const largefile = new File(['x'], 'large.zip', { type: 'application/zip' });
    Object.defineProperty(largefile, 'size', { value: 2 * 1024 * 1024 }); // 2 MB
    
    fireEvent.change(fileInput, { target: { files: [largefile] } });
    
    await waitFor(() => {
      expect(screen.getByText('2 MB')).toBeInTheDocument();
    });
  });

  it('should show pending status for newly uploaded files', async () => {
    render(<FileUpload />);
    
    const fileInput = screen.getByLabelText('File input') as HTMLInputElement;
    const file = new File(['content'], 'test.zip', { type: 'application/zip' });
    
    fireEvent.change(fileInput, { target: { files: [file] } });
    
    await waitFor(() => {
      const fileItem = screen.getByText('test.zip').closest('.file-item');
      expect(fileItem).toHaveClass('file-item-pending');
      expect(fileItem?.textContent).toContain('⏳');
      expect(screen.getByText(/pending/i)).toBeInTheDocument();
    });
  });

  it('should clear all files when clear button is clicked', async () => {
    render(<FileUpload />);
    
    const fileInput = screen.getByLabelText('File input') as HTMLInputElement;
    const file = new File(['content'], 'test.zip', { type: 'application/zip' });
    
    fireEvent.change(fileInput, { target: { files: [file] } });
    
    await waitFor(() => {
      expect(screen.getByText('test.zip')).toBeInTheDocument();
    });
    
    const clearButton = screen.getByRole('button', { name: /clear all/i });
    fireEvent.click(clearButton);
    
    await waitFor(() => {
      expect(screen.queryByText('test.zip')).not.toBeInTheDocument();
      expect(screen.queryByText(/uploaded files/i)).not.toBeInTheDocument();
    });
  });

  it('should handle drag enter event', () => {
    render(<FileUpload />);
    
    const dropZone = screen.getByRole('button', { name: /drop zone for .zip files/i });
    
    fireEvent.dragEnter(dropZone, {
      dataTransfer: { files: [] },
    });
    
    expect(dropZone).toHaveClass('drop-zone-active');
    expect(screen.getByText(/drop zip files here/i)).toBeInTheDocument();
  });

  it('should remove drag state on drop', () => {
    render(<FileUpload />);
    
    const dropZone = screen.getByRole('button', { name: /drop zone for .zip files/i });
    
    // Drag enter
    fireEvent.dragEnter(dropZone, {
      dataTransfer: { files: [] },
    });
    
    expect(dropZone).toHaveClass('drop-zone-active');
    
    // Drop should remove the active state
    const file = new File(['content'], 'test.zip', { type: 'application/zip' });
    fireEvent.drop(dropZone, {
      dataTransfer: { files: [file] },
    });
    
    expect(dropZone).not.toHaveClass('drop-zone-active');
  });

  it('should maintain drag state while dragging over drop zone', () => {
    render(<FileUpload />);
    
    const dropZone = screen.getByRole('button', { name: /drop zone for .zip files/i });
    
    // Drag enter
    fireEvent.dragEnter(dropZone, {
      dataTransfer: { files: [] },
    });
    
    expect(dropZone).toHaveClass('drop-zone-active');
    
    // Drag over should maintain the state
    fireEvent.dragOver(dropZone, {
      dataTransfer: { files: [] },
    });
    
    // Should still be active
    expect(dropZone).toHaveClass('drop-zone-active');
  });

  it('should handle file drop', async () => {
    const onFilesSelected = vi.fn();
    render(<FileUpload onFilesSelected={onFilesSelected} />);
    
    const dropZone = screen.getByRole('button', { name: /drop zone for .zip files/i });
    const file = new File(['content'], 'dropped.zip', { type: 'application/zip' });
    
    fireEvent.drop(dropZone, {
      dataTransfer: { files: [file] },
    });
    
    await waitFor(() => {
      expect(onFilesSelected).toHaveBeenCalledWith([file]);
      expect(screen.getByText('dropped.zip')).toBeInTheDocument();
    });
    
    // Drop zone should no longer be active
    expect(dropZone).not.toHaveClass('drop-zone-active');
  });

  it('should handle multiple files dropped', async () => {
    const onFilesSelected = vi.fn();
    render(<FileUpload onFilesSelected={onFilesSelected} />);
    
    const dropZone = screen.getByRole('button', { name: /drop zone for .zip files/i });
    const file1 = new File(['content1'], 'drop1.zip', { type: 'application/zip' });
    const file2 = new File(['content2'], 'drop2.zip', { type: 'application/zip' });
    
    fireEvent.drop(dropZone, {
      dataTransfer: { files: [file1, file2] },
    });
    
    await waitFor(() => {
      expect(onFilesSelected).toHaveBeenCalledWith([file1, file2]);
      expect(screen.getByText('drop1.zip')).toBeInTheDocument();
      expect(screen.getByText('drop2.zip')).toBeInTheDocument();
    });
  });

  it('should reject invalid files when dropped', async () => {
    const onFilesSelected = vi.fn();
    render(<FileUpload onFilesSelected={onFilesSelected} />);
    
    const dropZone = screen.getByRole('button', { name: /drop zone for .zip files/i });
    const invalidFile = new File(['content'], 'invalid.exe', { type: 'application/x-executable' });
    
    fireEvent.drop(dropZone, {
      dataTransfer: { files: [invalidFile] },
    });
    
    await waitFor(() => {
      expect(onFilesSelected).not.toHaveBeenCalled();
      expect(screen.getByText('invalid.exe')).toBeInTheDocument();
      expect(screen.getByText(/invalid file type/i)).toBeInTheDocument();
    });
  });

  it('should handle drag over event', () => {
    render(<FileUpload />);
    
    const dropZone = screen.getByRole('button', { name: /drop zone for .zip files/i });
    const event = new Event('dragover', { bubbles: true, cancelable: true });
    
    const preventDefaultSpy = vi.spyOn(event, 'preventDefault');
    dropZone.dispatchEvent(event);
    
    expect(preventDefaultSpy).toHaveBeenCalled();
  });

  it('should reset file input value after selection', async () => {
    render(<FileUpload />);
    
    const fileInput = screen.getByLabelText('File input') as HTMLInputElement;
    const file1 = new File(['content1'], 'test1.zip', { type: 'application/zip' });
    
    // First selection
    fireEvent.change(fileInput, { target: { files: [file1] } });
    
    await waitFor(() => {
      expect(screen.getByText('test1.zip')).toBeInTheDocument();
    });
    
    // Input value should be reset
    expect(fileInput.value).toBe('');
    
    // Second selection of same file should work
    const file2 = new File(['content2'], 'test1.zip', { type: 'application/zip' });
    fireEvent.change(fileInput, { target: { files: [file2] } });
    
    await waitFor(() => {
      expect(screen.getByText(/uploaded files \(2\)/i)).toBeInTheDocument();
    });
  });

  it('should handle empty file selection', () => {
    const onFilesSelected = vi.fn();
    render(<FileUpload onFilesSelected={onFilesSelected} />);
    
    const fileInput = screen.getByLabelText('File input') as HTMLInputElement;
    
    fireEvent.change(fileInput, { target: { files: [] } });
    
    expect(onFilesSelected).not.toHaveBeenCalled();
  });

  it('should handle empty file drop', () => {
    const onFilesSelected = vi.fn();
    render(<FileUpload onFilesSelected={onFilesSelected} />);
    
    const dropZone = screen.getByRole('button', { name: /drop zone for .zip files/i });
    
    fireEvent.drop(dropZone, {
      dataTransfer: { files: [] },
    });
    
    expect(onFilesSelected).not.toHaveBeenCalled();
  });

  it('should not show upload list when no files are uploaded', () => {
    render(<FileUpload />);
    
    expect(screen.queryByText(/uploaded files/i)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /clear all/i })).not.toBeInTheDocument();
  });

  it('should accumulate files across multiple uploads', async () => {
    render(<FileUpload />);
    
    const fileInput = screen.getByLabelText('File input') as HTMLInputElement;
    
    // First upload
    const file1 = new File(['content1'], 'test1.zip', { type: 'application/zip' });
    fireEvent.change(fileInput, { target: { files: [file1] } });
    
    await waitFor(() => {
      expect(screen.getByText('test1.zip')).toBeInTheDocument();
    });
    
    // Second upload
    const file2 = new File(['content2'], 'test2.zip', { type: 'application/zip' });
    fireEvent.change(fileInput, { target: { files: [file2] } });
    
    await waitFor(() => {
      expect(screen.getByText('test1.zip')).toBeInTheDocument();
      expect(screen.getByText('test2.zip')).toBeInTheDocument();
      expect(screen.getByText(/uploaded files \(2\)/i)).toBeInTheDocument();
    });
  });

  it('should handle accessibility attributes correctly', () => {
    render(<FileUpload />);
    
    const dropZone = screen.getByRole('button', { name: /drop zone for .zip files/i });
    expect(dropZone).toHaveAttribute('tabIndex', '0');
    
    const fileInput = screen.getByLabelText('File input');
    expect(fileInput).toHaveAttribute('type', 'file');
  });
});

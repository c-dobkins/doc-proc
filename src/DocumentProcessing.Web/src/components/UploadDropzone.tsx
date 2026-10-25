import { useCallback, useState } from 'react';

interface Props {
  onFilesSelected: (files: File[]) => void;
  accept?: string;
  multiple?: boolean;
}

export default function UploadDropzone({
  onFilesSelected,
  accept = '.pdf,.jpg,.jpeg,.png,.tiff,.tif,.bmp',
  multiple = true,
}: Props) {
  const [dragging, setDragging] = useState(false);

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setDragging(false);
      const files = Array.from(e.dataTransfer.files);
      if (files.length) onFilesSelected(files);
    },
    [onFilesSelected],
  );

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragging(true);
  }, []);

  const handleDragLeave = useCallback(() => setDragging(false), []);

  const handleInputChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const files = Array.from(e.target.files ?? []);
      if (files.length) onFilesSelected(files);
      e.target.value = '';
    },
    [onFilesSelected],
  );

  return (
    <div
      onDrop={handleDrop}
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      className={`flex flex-col items-center justify-center rounded-lg border-2 border-dashed p-10 text-center transition ${
        dragging
          ? 'border-indigo-500 bg-indigo-50'
          : 'border-gray-300 bg-white hover:border-gray-400'
      }`}
    >
      <svg
        className="mx-auto mb-3 h-12 w-12 text-gray-400"
        fill="none"
        stroke="currentColor"
        viewBox="0 0 48 48"
      >
        <path
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth={2}
          d="M28 8H12a4 4 0 00-4 4v20m32-12v8m0 0v8a4 4 0 01-4 4H12a4 4 0 01-4-4v-4m32-4l-3.172-3.172a4 4 0 00-5.656 0L28 28M8 32l9.172-9.172a4 4 0 015.656 0L28 28m0 0l4 4m4-24h8m-4-4v8"
        />
      </svg>
      <p className="text-sm text-gray-600">
        Drag & drop files here, or{' '}
        <label className="cursor-pointer font-medium text-indigo-600 hover:text-indigo-500">
          browse
          <input
            type="file"
            className="hidden"
            accept={accept}
            multiple={multiple}
            onChange={handleInputChange}
          />
        </label>
      </p>
      <p className="mt-1 text-xs text-gray-500">
        PDF, JPEG, PNG, TIFF, BMP — up to 50 MB
      </p>
    </div>
  );
}

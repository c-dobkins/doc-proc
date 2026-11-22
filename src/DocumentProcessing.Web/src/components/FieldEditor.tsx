import { useState } from 'react';
import type { DocumentFieldDto } from '../api/types';
import ConfidenceBadge from './ConfidenceBadge';

interface Props {
  field: DocumentFieldDto;
  onSave: (name: string, value: string | null) => void;
  isSelected?: boolean;
  onSelect?: (field: DocumentFieldDto) => void;
}

export default function FieldEditor({ field, onSave, isSelected, onSelect }: Props) {
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(field.value ?? '');

  function handleSave() {
    onSave(field.name, value || null);
    setEditing(false);
  }

  function handleCancel() {
    setValue(field.value ?? '');
    setEditing(false);
  }

  return (
    <div
      className={`flex cursor-pointer items-center gap-3 rounded-lg border p-3 transition-colors ${
        isSelected
          ? 'border-yellow-400 bg-yellow-50 ring-2 ring-yellow-300'
          : 'border-gray-200 bg-white hover:border-indigo-200 hover:bg-indigo-50/30'
      }`}
      onClick={() => onSelect?.(field)}
    >      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium text-gray-700">{field.name}</span>
          <span className="text-xs text-gray-400">{field.dataType}</span>
          <ConfidenceBadge confidence={field.confidence} />
        </div>
        {editing ? (
          <div className="mt-1 flex items-center gap-2">
            <input
              type="text"
              value={value}
              onChange={(e) => setValue(e.target.value)}
              className="block w-full rounded-md border border-gray-300 px-2 py-1 text-sm shadow-sm focus:border-indigo-500 focus:ring-indigo-500"
              autoFocus
            />
            <button
              onClick={handleSave}
              className="rounded bg-indigo-600 px-2 py-1 text-xs text-white hover:bg-indigo-700"
            >
              Save
            </button>
            <button
              onClick={handleCancel}
              className="rounded bg-gray-200 px-2 py-1 text-xs text-gray-700 hover:bg-gray-300"
            >
              Cancel
            </button>
          </div>
        ) : (
          <p
            className="mt-1 cursor-pointer text-sm text-gray-900 hover:text-indigo-600"
            onClick={(e) => { e.stopPropagation(); setEditing(true); }}
          >
            {field.value ?? <span className="italic text-gray-400">—</span>}
          </p>
        )}
      </div>
    </div>
  );
}

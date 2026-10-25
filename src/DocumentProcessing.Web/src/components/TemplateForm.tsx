import { useState } from 'react';
import { useCreateTemplate, useUpdateTemplate } from '../api/hooks';
import type { CreateTemplateRequest, TemplateDto, TemplateFieldDto } from '../api/types';

const DATA_TYPES = [
  'String',
  'Integer',
  'Decimal',
  'Date',
  'Boolean',
  'Currency',
  'Address',
  'PhoneNumber',
  'Email',
];

interface Props {
  template: TemplateDto | null;
  onClose: () => void;
}

export default function TemplateForm({ template, onClose }: Props) {
  const isEdit = !!template;
  const createTemplate = useCreateTemplate();
  const updateTemplate = useUpdateTemplate(template?.id ?? '');

  const [name, setName] = useState(template?.name ?? '');
  const [description, setDescription] = useState(template?.description ?? '');
  const [promptTemplate, setPromptTemplate] = useState(
    template?.promptTemplate ?? 'Extract the following fields from this text:\n{ocrText}\n\nFields: {fieldDefinitions}\n\nReturn as: {outputFormat}',
  );
  const [fields, setFields] = useState<TemplateFieldDto[]>(
    template?.fields ?? [{ name: '', dataType: 'String', isRequired: false, description: null }],
  );

  function addField() {
    setFields([
      ...fields,
      { name: '', dataType: 'String', isRequired: false, description: null },
    ]);
  }

  function removeField(index: number) {
    setFields(fields.filter((_, i) => i !== index));
  }

  function updateField(index: number, updates: Partial<TemplateFieldDto>) {
    setFields(fields.map((f, i) => (i === index ? { ...f, ...updates } : f)));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const request: CreateTemplateRequest = {
      name,
      description: description || null,
      fields,
      promptTemplate,
    };

    if (isEdit) {
      await updateTemplate.mutateAsync(request);
    } else {
      await createTemplate.mutateAsync(request);
    }
    onClose();
  }

  const isPending = createTemplate.isPending || updateTemplate.isPending;

  return (
    <form
      onSubmit={handleSubmit}
      className="mb-6 rounded-lg border border-indigo-200 bg-indigo-50 p-4"
    >
      <h2 className="mb-4 text-lg font-semibold text-gray-900">
        {isEdit ? 'Edit Template' : 'New Template'}
      </h2>

      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className="block text-sm font-medium text-gray-700">Name</label>
          <input
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            maxLength={200}
            className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:ring-indigo-500"
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700">
            Description
          </label>
          <input
            type="text"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:ring-indigo-500"
          />
        </div>
      </div>

      <div className="mt-4">
        <label className="block text-sm font-medium text-gray-700">
          Prompt Template
        </label>
        <textarea
          value={promptTemplate}
          onChange={(e) => setPromptTemplate(e.target.value)}
          required
          rows={4}
          className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 font-mono text-sm shadow-sm focus:border-indigo-500 focus:ring-indigo-500"
        />
        <p className="mt-1 text-xs text-gray-400">
          Variables: {'{ocrText}'}, {'{fieldDefinitions}'}, {'{outputFormat}'}
        </p>
      </div>

      <div className="mt-4">
        <div className="mb-2 flex items-center justify-between">
          <label className="text-sm font-medium text-gray-700">Fields</label>
          <button
            type="button"
            onClick={addField}
            className="text-sm text-indigo-600 hover:text-indigo-700"
          >
            + Add Field
          </button>
        </div>
        <div className="space-y-2">
          {fields.map((f, i) => (
            <div
              key={i}
              className="flex items-center gap-2 rounded border border-gray-200 bg-white p-2"
            >
              <input
                type="text"
                placeholder="Field name"
                value={f.name}
                onChange={(e) => updateField(i, { name: e.target.value })}
                required
                className="flex-1 rounded border border-gray-300 px-2 py-1 text-sm"
              />
              <select
                value={f.dataType}
                onChange={(e) => updateField(i, { dataType: e.target.value })}
                className="rounded border border-gray-300 px-2 py-1 text-sm"
              >
                {DATA_TYPES.map((dt) => (
                  <option key={dt} value={dt}>
                    {dt}
                  </option>
                ))}
              </select>
              <label className="flex items-center gap-1 text-xs text-gray-600">
                <input
                  type="checkbox"
                  checked={f.isRequired}
                  onChange={(e) => updateField(i, { isRequired: e.target.checked })}
                />
                Required
              </label>
              <input
                type="text"
                placeholder="Description"
                value={f.description ?? ''}
                onChange={(e) =>
                  updateField(i, { description: e.target.value || null })
                }
                className="flex-1 rounded border border-gray-300 px-2 py-1 text-sm"
              />
              {fields.length > 1 && (
                <button
                  type="button"
                  onClick={() => removeField(i)}
                  className="text-red-400 hover:text-red-600"
                >
                  ×
                </button>
              )}
            </div>
          ))}
        </div>
      </div>

      <div className="mt-4 flex gap-2">
        <button
          type="submit"
          disabled={isPending}
          className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-indigo-700 disabled:opacity-50"
        >
          {isPending ? 'Saving…' : isEdit ? 'Update' : 'Create'}
        </button>
        <button
          type="button"
          onClick={onClose}
          className="rounded-md bg-gray-200 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-300"
        >
          Cancel
        </button>
      </div>
    </form>
  );
}

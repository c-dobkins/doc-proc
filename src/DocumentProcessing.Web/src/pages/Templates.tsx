import { useState } from 'react';
import {
  useTemplates,
  useDeleteTemplate,
} from '../api/hooks';
import type { TemplateDto } from '../api/types';
import PageState from '../components/PageState';
import TemplateForm from '../components/TemplateForm';

export default function Templates() {
  const { data: templates, isLoading, error } = useTemplates();
  const deleteTemplate = useDeleteTemplate();

  const [editingTemplate, setEditingTemplate] = useState<TemplateDto | null>(null);
  const [showCreate, setShowCreate] = useState(false);

  return (
    <PageState isLoading={isLoading} error={error} data={templates} loadingText="Loading templates…">
      {(templates: TemplateDto[]) => (
        <div>
          <div className="mb-6 flex items-center justify-between">
            <h1 className="text-2xl font-bold text-gray-900">Templates</h1>
            <button
              onClick={() => {
                setShowCreate(true);
                setEditingTemplate(null);
              }}
              className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-indigo-700"
            >
              New Template
            </button>
          </div>

          {(showCreate || editingTemplate) && (
            <TemplateForm
              template={editingTemplate}
              onClose={() => {
                setShowCreate(false);
                setEditingTemplate(null);
              }}
            />
          )}

          <div className="space-y-4">
            {templates.map((t) => (
              <div
                key={t.id}
                className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm"
              >
                <div className="flex items-start justify-between">
                  <div>
                    <h3 className="text-lg font-semibold text-gray-900">{t.name}</h3>
                    {t.description && (
                      <p className="mt-1 text-sm text-gray-500">{t.description}</p>
                    )}
                    <div className="mt-2 flex flex-wrap gap-2">
                      {t.fields.map((f) => (
                        <span
                          key={f.name}
                          className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-700"
                        >
                          {f.name}{' '}
                          <span className="text-gray-400">({f.dataType})</span>
                          {f.isRequired && (
                            <span className="text-red-400"> *</span>
                          )}
                        </span>
                      ))}
                    </div>
                    <p className="mt-2 text-xs text-gray-400">
                      v{t.version} · Created{' '}
                      {new Date(t.createdAt).toLocaleDateString()}
                    </p>
                  </div>
                  <div className="flex gap-2">
                    <button
                      onClick={() => {
                        setEditingTemplate(t);
                        setShowCreate(false);
                      }}
                      className="text-sm text-indigo-600 hover:text-indigo-800"
                    >
                      Edit
                    </button>
                    <button
                      onClick={() => {
                        if (confirm('Delete this template?'))
                          deleteTemplate.mutate(t.id);
                      }}
                      className="text-sm text-red-600 hover:text-red-800"
                    >
                      Delete
                    </button>
                  </div>
                </div>
              </div>
            ))}
            {templates.length === 0 && (
              <p className="text-center text-sm text-gray-500">
                No templates yet. Create one to get started.
              </p>
            )}
          </div>
        </div>
      )}
    </PageState>
  );
}

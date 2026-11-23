import React, { useMemo, useState } from 'react';
import { CapitalizationMode, LanguageProjectState } from '../core/types';
import { buildEngineFromState } from '../core/languageEngine';

interface OrthographyTabProps {
  state: LanguageProjectState;
  onUpdate: (updater: (prev: LanguageProjectState) => LanguageProjectState, label: string) => void;
}

const CAP_MODES: CapitalizationMode[] = ['None', 'SentenceInitial', 'AllWords'];

export const OrthographyTab: React.FC<OrthographyTabProps> = ({ state, onUpdate }) => {
  const [newPhone, setNewPhone] = useState('');
  const [newGrapheme, setNewGrapheme] = useState('');
  const [newPrevContext, setNewPrevContext] = useState('');
  const [newNextContext, setNewNextContext] = useState('');

  const [newExceptionInput, setNewExceptionInput] = useState('');
  const [newExceptionOutput, setNewExceptionOutput] = useState('');

  const [previewInput, setPreviewInput] = useState('ʃika taka');
  const [previewTick, setPreviewTick] = useState(0);

  const previewResult = useMemo(() => {
    void previewTick;
    const engine = buildEngineFromState(state);
    return engine.orthography.renderWithTokens(previewInput);
  }, [state, previewInput, previewTick]);

  const addRule = () => {
    if (!newPhone.trim()) return;
    onUpdate(
      (prev) => ({
        ...prev,
        OrthographyRules: [
          ...prev.OrthographyRules,
          {
            Phone: newPhone.trim(),
            Grapheme: newGrapheme,
            PrevContext: newPrevContext.trim() || null,
            NextContext: newNextContext.trim() || null,
            Enabled: true,
          },
        ],
      }),
      'Orthography rules updated'
    );
    setNewPhone('');
    setNewGrapheme('');
    setNewPrevContext('');
    setNewNextContext('');
  };

  const toggleRule = (index: number) => {
    onUpdate(
      (prev) => ({
        ...prev,
        OrthographyRules: prev.OrthographyRules.map((r, i) =>
          i === index ? { ...r, Enabled: !r.Enabled } : r
        ),
      }),
      'Orthography rules updated'
    );
  };

  const removeRule = (index: number) => {
    onUpdate(
      (prev) => ({
        ...prev,
        OrthographyRules: prev.OrthographyRules.filter((_, i) => i !== index),
      }),
      'Orthography rules updated'
    );
  };

  const addException = () => {
    if (!newExceptionInput.trim()) return;
    onUpdate(
      (prev) => ({
        ...prev,
        OrthographyExceptions: [
          ...prev.OrthographyExceptions,
          {
            Input: newExceptionInput.trim(),
            Output: newExceptionOutput.trim(),
          },
        ],
      }),
      'Orthography exceptions updated'
    );
    setNewExceptionInput('');
    setNewExceptionOutput('');
  };

  const removeException = (index: number) => {
    onUpdate(
      (prev) => ({
        ...prev,
        OrthographyExceptions: prev.OrthographyExceptions.filter((_, i) => i !== index),
      }),
      'Orthography exceptions updated'
    );
  };

  return (
    <div className="p-5 space-y-5">
      <div className="flex items-center gap-3">
        <span className="text-sm font-medium text-slate-700">Capitalization</span>
        <select
          value={state.Capitalization}
          onChange={(e) =>
            onUpdate(
              (prev) => ({ ...prev, Capitalization: e.target.value as CapitalizationMode }),
              'Capitalization updated'
            )
          }
          className="px-2.5 py-1.5 text-sm bg-white border border-slate-300 rounded"
        >
          {CAP_MODES.map((m) => (
            <option key={m} value={m}>
              {m}
            </option>
          ))}
        </select>
      </div>

      <div>
        <div className="border border-slate-300 bg-white rounded max-h-48 overflow-y-auto">
          <table className="w-full text-sm text-left border-collapse">
            <thead className="bg-slate-100 border-b border-slate-300 text-xs text-slate-700 sticky top-0">
              <tr>
                <th className="px-3 py-1.5 font-semibold">Phone</th>
                <th className="px-3 py-1.5 font-semibold">Grapheme</th>
                <th className="px-3 py-1.5 font-semibold">Prev</th>
                <th className="px-3 py-1.5 font-semibold">Next</th>
                <th className="px-3 py-1.5 font-semibold">Enabled</th>
                <th className="px-3 py-1.5 w-20"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {state.OrthographyRules.map((r, idx) => (
                <tr key={`${r.Phone}-${r.Grapheme}-${idx}`} className="hover:bg-slate-50">
                  <td className="px-3 py-1.5 font-mono">{r.Phone}</td>
                  <td className="px-3 py-1.5 font-mono">{r.Grapheme}</td>
                  <td className="px-3 py-1.5 font-mono text-xs text-slate-600">{r.PrevContext ?? ''}</td>
                  <td className="px-3 py-1.5 font-mono text-xs text-slate-600">{r.NextContext ?? ''}</td>
                  <td className="px-3 py-1.5">
                    <input
                      type="checkbox"
                      checked={r.Enabled}
                      onChange={() => toggleRule(idx)}
                    />
                  </td>
                  <td className="px-3 py-1.5 text-right">
                    <button
                      type="button"
                      onClick={() => removeRule(idx)}
                      className="px-2 py-0.5 text-xs bg-slate-100 hover:bg-rose-50 hover:text-rose-700 border border-slate-300 rounded"
                    >
                      Remove
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="flex flex-wrap items-center gap-2 mt-2.5">
          <input
            type="text"
            value={newPhone}
            onChange={(e) => setNewPhone(e.target.value)}
            placeholder="Phone"
            className="w-24 px-2.5 py-1 text-sm bg-white border border-slate-300 rounded font-mono"
          />
          <input
            type="text"
            value={newGrapheme}
            onChange={(e) => setNewGrapheme(e.target.value)}
            placeholder="Grapheme"
            className="w-28 px-2.5 py-1 text-sm bg-white border border-slate-300 rounded font-mono"
          />
          <input
            type="text"
            value={newPrevContext}
            onChange={(e) => setNewPrevContext(e.target.value)}
            placeholder="Prev"
            className="w-24 px-2.5 py-1 text-sm bg-white border border-slate-300 rounded font-mono"
          />
          <input
            type="text"
            value={newNextContext}
            onChange={(e) => setNewNextContext(e.target.value)}
            placeholder="Next"
            className="w-24 px-2.5 py-1 text-sm bg-white border border-slate-300 rounded font-mono"
          />
          <button
            type="button"
            onClick={addRule}
            disabled={!newPhone.trim()}
            className="px-3 py-1 text-sm bg-slate-800 text-white rounded hover:bg-slate-700 disabled:opacity-40"
          >
            Add Mapping
          </button>
        </div>
      </div>

      <div>
        <h3 className="text-sm font-semibold text-slate-800 mb-1.5">Exceptions</h3>
        <div className="border border-slate-300 bg-white rounded max-h-36 overflow-y-auto">
          <table className="w-full text-sm text-left border-collapse">
            <thead className="bg-slate-100 border-b border-slate-300 text-xs text-slate-700 sticky top-0">
              <tr>
                <th className="px-3 py-1.5 font-semibold">Input</th>
                <th className="px-3 py-1.5 font-semibold">Output</th>
                <th className="px-3 py-1.5 w-20"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {state.OrthographyExceptions.length === 0 ? (
                <tr>
                  <td colSpan={3} className="px-3 py-2 text-xs text-slate-400">
                    No word-level exceptions defined.
                  </td>
                </tr>
              ) : (
                state.OrthographyExceptions.map((ex, idx) => (
                  <tr key={`${ex.Input}-${idx}`} className="hover:bg-slate-50">
                    <td className="px-3 py-1.5 font-mono">{ex.Input}</td>
                    <td className="px-3 py-1.5 font-mono">{ex.Output}</td>
                    <td className="px-3 py-1.5 text-right">
                      <button
                        type="button"
                        onClick={() => removeException(idx)}
                        className="px-2 py-0.5 text-xs bg-slate-100 hover:bg-rose-50 hover:text-rose-700 border border-slate-300 rounded"
                      >
                        Remove
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        <div className="flex flex-wrap items-center gap-2 mt-2.5">
          <input
            type="text"
            value={newExceptionInput}
            onChange={(e) => setNewExceptionInput(e.target.value)}
            placeholder="Input word"
            className="w-44 px-2.5 py-1 text-sm bg-white border border-slate-300 rounded font-mono"
          />
          <input
            type="text"
            value={newExceptionOutput}
            onChange={(e) => setNewExceptionOutput(e.target.value)}
            placeholder="Output spelling"
            className="w-44 px-2.5 py-1 text-sm bg-white border border-slate-300 rounded font-mono"
          />
          <button
            type="button"
            onClick={addException}
            disabled={!newExceptionInput.trim()}
            className="px-3 py-1 text-sm bg-slate-800 text-white rounded hover:bg-slate-700 disabled:opacity-40"
          >
            Add Exception
          </button>
        </div>
      </div>

      <div className="space-y-2">
        <div className="text-sm font-semibold text-slate-800">Preview & Layer 1 Abstract Tokens</div>
        <div className="flex flex-wrap items-center gap-2">
          <input
            type="text"
            value={previewInput}
            onChange={(e) => setPreviewInput(e.target.value)}
            className="w-64 px-2.5 py-1 text-sm bg-white border border-slate-300 rounded font-mono"
          />
          <button
            type="button"
            onClick={() => setPreviewTick((n) => n + 1)}
            className="px-3 py-1 text-sm bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded"
          >
            Render
          </button>
        </div>
        <input
          type="text"
          readOnly
          value={previewResult.text}
          className="w-full px-2.5 py-1.5 text-sm font-mono bg-white border border-slate-300 rounded"
        />
        <div className="text-xs font-mono bg-slate-50 border border-slate-300 rounded p-2 text-slate-700">
          Layer 1 Tokens:{' '}
          {previewResult.tokens
            .map((t) => `[${t.id}:${t.positionInWord}]`)
            .join(' ')}
        </div>
      </div>
    </div>
  );
};

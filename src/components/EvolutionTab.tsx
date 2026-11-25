import React, { useMemo, useState } from 'react';
import { DiachronicRule, LanguageProjectState } from '../core/types';
import {
  applyDiachronicEvolutionToProject,
  DIACHRONIC_PRESETS,
  Morphology,
  simulateDiachronicEvolution,
  traceProtoForm,
} from '../core/languageEngine';

interface EvolutionTabProps {
  state: LanguageProjectState;
  onUpdate: (updater: (prev: LanguageProjectState) => LanguageProjectState, label: string) => void;
}

export const EvolutionTab: React.FC<EvolutionTabProps> = ({ state, onUpdate }) => {
  const rules = state.DiachronicRules ?? [];

  const [selectedPresetId, setSelectedPresetId] = useState<string>(DIACHRONIC_PRESETS[0]?.id ?? 'great-vowel-shift');
  const [newName, setNewName] = useState('');
  const [newEra, setNewEra] = useState('Sound Change');
  const [newTarget, setNewTarget] = useState('p');
  const [newReplacement, setNewReplacement] = useState('f');
  const [newPrev, setNewPrev] = useState('');
  const [newNext, setNewNext] = useState('');
  const [newCondition, setNewCondition] = useState('');

  const [protoInput, setProtoInput] = useState('ˈk n iː x t');
  const [daughterName, setDaughterName] = useState('');
  const [preserveHistoricalSpelling, setPreserveHistoricalSpelling] = useState(true);

  const selectedPreset = useMemo(
    () => DIACHRONIC_PRESETS.find((p) => p.id === selectedPresetId) ?? DIACHRONIC_PRESETS[0],
    [selectedPresetId]
  );

  const simulation = useMemo(() => simulateDiachronicEvolution(state), [state]);
  const protoTrace = useMemo(() => traceProtoForm(protoInput, rules, state), [protoInput, rules, state]);

  const loadPresetRules = (mode: 'replace' | 'append') => {
    if (!selectedPreset) return;
    const cloned: DiachronicRule[] = selectedPreset.rules.map((r) => ({ ...r }));
    onUpdate(
      (prev) => ({
        ...prev,
        DiachronicRules: mode === 'replace' ? cloned : [...(prev.DiachronicRules ?? []), ...cloned],
      }),
      `Loaded diachronic preset: ${selectedPreset.name}`
    );
    if (selectedPreset.sampleProtoForms[0]) {
      setProtoInput(selectedPreset.sampleProtoForms[0]);
    }
  };

  const addRule = () => {
    if (!newName.trim() || !newTarget.trim()) return;
    const nextRule: DiachronicRule = {
      Name: newName.trim(),
      Era: newEra.trim() || 'Stage 1',
      Target: newTarget.trim(),
      Replacement: newReplacement.trim(),
      PrevPhone: newPrev.trim() || null,
      NextPhone: newNext.trim() || null,
      Condition: newCondition.trim(),
      Enabled: true,
    };
    onUpdate(
      (prev) => ({
        ...prev,
        DiachronicRules: [...(prev.DiachronicRules ?? []), nextRule],
      }),
      'Added diachronic rule'
    );
    setNewName('');
    setNewCondition('');
  };

  const toggleRule = (idx: number) => {
    onUpdate(
      (prev) => ({
        ...prev,
        DiachronicRules: (prev.DiachronicRules ?? []).map((r, i) =>
          i === idx ? { ...r, Enabled: !r.Enabled } : r
        ),
      }),
      'Toggled diachronic rule'
    );
  };

  const moveRule = (idx: number, delta: -1 | 1) => {
    const target = idx + delta;
    if (target < 0 || target >= rules.length) return;
    onUpdate((prev) => {
      const copy = [...(prev.DiachronicRules ?? [])];
      const [item] = copy.splice(idx, 1);
      copy.splice(target, 0, item);
      return { ...prev, DiachronicRules: copy };
    }, 'Reordered diachronic rule');
  };

  const removeRule = (idx: number) => {
    onUpdate(
      (prev) => ({
        ...prev,
        DiachronicRules: (prev.DiachronicRules ?? []).filter((_, i) => i !== idx),
      }),
      'Removed diachronic rule'
    );
  };

  const applyEvolution = () => {
    if (rules.every((r) => !r.Enabled)) return;
    onUpdate(
      (prev) =>
        applyDiachronicEvolutionToProject(prev, {
          preserveHistoricalSpelling,
          daughterName: daughterName.trim() || undefined,
        }),
      'Evolved project lexicon and phonology'
    );
    setDaughterName('');
  };

  return (
    <div className="p-5 space-y-5">
      <div className="bg-white border border-slate-300 rounded p-4 space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h3 className="text-base font-bold text-slate-900">Historical Sound-Law Presets</h3>
            <p className="text-xs text-slate-600">
              Simulate real diachronic shifts across eras or evolve your entire lexicon into a daughter language.
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <select
              value={selectedPresetId}
              onChange={(e) => setSelectedPresetId(e.target.value)}
              className="px-2.5 py-1.5 text-sm bg-white border border-slate-300 rounded"
            >
              {DIACHRONIC_PRESETS.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
            <button
              type="button"
              onClick={() => loadPresetRules('replace')}
              className="px-3 py-1.5 text-xs font-medium bg-indigo-600 text-white rounded hover:bg-indigo-700"
            >
              Load Preset (Replace)
            </button>
            <button
              type="button"
              onClick={() => loadPresetRules('append')}
              className="px-3 py-1.5 text-xs font-medium bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded"
            >
              Append Rules
            </button>
          </div>
        </div>
        {selectedPreset && (
          <div className="text-xs text-slate-600 flex flex-wrap items-center gap-2">
            <span>{selectedPreset.description}</span>
            <span className="text-slate-400">| Samples:</span>
            {selectedPreset.sampleProtoForms.map((pf) => (
              <button
                key={pf}
                type="button"
                onClick={() => setProtoInput(pf)}
                className="px-1.5 py-0.5 font-mono bg-slate-100 hover:bg-indigo-50 border border-slate-300 rounded text-slate-800"
              >
                *{pf}
              </button>
            ))}
          </div>
        )}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <div className="lg:col-span-2 bg-white border border-slate-300 rounded p-3 space-y-2">
          <div className="flex items-center justify-between">
            <h4 className="text-sm font-bold text-slate-900">Diachronic Sound-Change Sequence</h4>
            <span className="text-xs text-slate-500">{rules.filter((r) => r.Enabled).length} active rule(s)</span>
          </div>
          <div className="border border-slate-200 rounded max-h-60 overflow-y-auto">
            <table className="w-full text-xs text-left border-collapse">
              <thead className="bg-slate-100 border-b border-slate-200 text-slate-700 sticky top-0">
                <tr>
                  <th className="px-2 py-1.5 w-8">On</th>
                  <th className="px-2 py-1.5">Era</th>
                  <th className="px-2 py-1.5">Name</th>
                  <th className="px-2 py-1.5">Rule</th>
                  <th className="px-2 py-1.5">Cond</th>
                  <th className="px-2 py-1.5 w-28"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {rules.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="px-3 py-4 text-center text-slate-400">
                      No diachronic rules defined. Load a preset or add one.
                    </td>
                  </tr>
                ) : (
                  rules.map((r, idx) => (
                    <tr key={`${r.Name}-${idx}`} className="hover:bg-slate-50">
                      <td className="px-2 py-1.5">
                        <input type="checkbox" checked={r.Enabled} onChange={() => toggleRule(idx)} />
                      </td>
                      <td className="px-2 py-1.5 text-indigo-700 font-medium">{r.Era ?? ''}</td>
                      <td className="px-2 py-1.5 font-medium text-slate-800">{r.Name}</td>
                      <td className="px-2 py-1.5 font-mono">
                        {r.Target} → {r.Replacement || '∅'}
                        {(r.PrevPhone || r.NextPhone) && (
                          <span className="text-slate-500">
                            {' '}
                            / {r.PrevPhone ?? ''}__{r.NextPhone ?? ''}
                          </span>
                        )}
                      </td>
                      <td className="px-2 py-1.5 font-mono text-slate-600">{r.Condition ?? ''}</td>
                      <td className="px-2 py-1.5 text-right whitespace-nowrap space-x-1">
                        <button
                          type="button"
                          onClick={() => moveRule(idx, -1)}
                          disabled={idx === 0}
                          className="px-1.5 py-0.5 bg-slate-100 hover:bg-slate-200 border border-slate-300 rounded disabled:opacity-40"
                        >
                          ↑
                        </button>
                        <button
                          type="button"
                          onClick={() => moveRule(idx, 1)}
                          disabled={idx === rules.length - 1}
                          className="px-1.5 py-0.5 bg-slate-100 hover:bg-slate-200 border border-slate-300 rounded disabled:opacity-40"
                        >
                          ↓
                        </button>
                        <button
                          type="button"
                          onClick={() => removeRule(idx)}
                          className="px-2 py-0.5 bg-slate-100 hover:bg-rose-50 hover:text-rose-700 border border-slate-300 rounded"
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
        </div>

        <div className="bg-white border border-slate-300 rounded p-3 space-y-2">
          <h4 className="text-sm font-bold text-slate-900">Add Diachronic Law</h4>
          <input
            type="text"
            value={newName}
            onChange={(e) => setNewName(e.target.value)}
            placeholder="Law name (e.g. Spirantization)"
            className="w-full px-2.5 py-1 text-sm bg-white border border-slate-300 rounded"
          />
          <input
            type="text"
            value={newEra}
            onChange={(e) => setNewEra(e.target.value)}
            placeholder="Era (e.g. Grimm's I)"
            className="w-full px-2.5 py-1 text-sm bg-white border border-slate-300 rounded"
          />
          <div className="grid grid-cols-2 gap-1.5">
            <input
              type="text"
              value={newTarget}
              onChange={(e) => setNewTarget(e.target.value)}
              placeholder="Target (e.g. p, V)"
              className="w-full px-2.5 py-1 text-sm font-mono bg-white border border-slate-300 rounded"
            />
            <input
              type="text"
              value={newReplacement}
              onChange={(e) => setNewReplacement(e.target.value)}
              placeholder="Repl (e.g. f, ∅)"
              className="w-full px-2.5 py-1 text-sm font-mono bg-white border border-slate-300 rounded"
            />
          </div>
          <div className="grid grid-cols-2 gap-1.5">
            <input
              type="text"
              value={newPrev}
              onChange={(e) => setNewPrev(e.target.value)}
              placeholder="Prev (#, V, C)"
              className="w-full px-2.5 py-1 text-sm font-mono bg-white border border-slate-300 rounded"
            />
            <input
              type="text"
              value={newNext}
              onChange={(e) => setNewNext(e.target.value)}
              placeholder="Next (#, V, i)"
              className="w-full px-2.5 py-1 text-sm font-mono bg-white border border-slate-300 rounded"
            />
          </div>
          <input
            type="text"
            value={newCondition}
            onChange={(e) => setNewCondition(e.target.value)}
            placeholder="Condition (e.g. [+stressed])"
            className="w-full px-2.5 py-1 text-sm font-mono bg-white border border-slate-300 rounded"
          />
          <button
            type="button"
            onClick={addRule}
            disabled={!newName.trim() || !newTarget.trim()}
            className="w-full px-3 py-1.5 text-sm bg-slate-800 text-white rounded hover:bg-slate-700 disabled:opacity-40"
          >
            Add Sound Law
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div className="bg-white border border-slate-300 rounded p-3 space-y-2.5">
          <h4 className="text-sm font-bold text-slate-900">Sandbox Proto-Form Era Trace</h4>
          <div className="flex items-center gap-2">
            <span className="text-sm font-mono text-slate-500">*</span>
            <input
              type="text"
              value={protoInput}
              onChange={(e) => setProtoInput(e.target.value)}
              placeholder="Proto IPA phones (e.g. ˈk n iː x t)"
              className="flex-1 px-2.5 py-1.5 text-sm font-mono bg-white border border-slate-300 rounded"
            />
            <span className="text-xs text-slate-500">→</span>
            <span className="px-2.5 py-1 text-sm font-mono font-bold bg-indigo-50 text-indigo-800 border border-indigo-200 rounded">
              /{protoTrace.daughterPhones.join(' ')}/
            </span>
          </div>
          <div className="border border-slate-200 rounded max-h-48 overflow-y-auto divide-y divide-slate-100 text-xs">
            <div className="px-2.5 py-1.5 flex justify-between bg-slate-50">
              <span className="font-semibold text-slate-600">Proto-Form</span>
              <span className="font-mono">*{Morphology.splitPhones(protoInput).join(' ')}</span>
            </div>
            {protoTrace.steps.map((st, idx) => (
              <div
                key={`${st.ruleName}-${idx}`}
                className={`px-2.5 py-1.5 flex justify-between gap-2 ${
                  st.changed ? 'bg-amber-50/70' : 'text-slate-400'
                }`}
              >
                <span className="truncate">
                  {st.era ? `[${st.era}] ` : ''}
                  {st.ruleName}
                </span>
                <span className={`font-mono shrink-0 ${st.changed ? 'font-semibold text-slate-900' : ''}`}>
                  /{st.phones.join(' ')}/
                </span>
              </div>
            ))}
          </div>
        </div>

        <div className="bg-white border border-slate-300 rounded p-3 space-y-3">
          <h4 className="text-sm font-bold text-slate-900">Fork / Commit Daughter Language Evolution</h4>
          <div className="text-xs text-slate-600 space-y-1">
            <div>
              <strong>{simulation.changedCount}</strong> / {simulation.cognates.length} lexeme(s) shift under active laws;{' '}
              <strong>{simulation.affixDiffs.filter((a) => a.changed).length}</strong> affix(es) shift.
            </div>
            <div className="flex flex-wrap gap-3 pt-1">
              <span>
                Emergent phones:{' '}
                <span className="font-mono text-emerald-700">
                  {simulation.emergentPhones.length > 0 ? simulation.emergentPhones.join(', ') : '(none)'}
                </span>
              </span>
              <span>
                Lost phones:{' '}
                <span className="font-mono text-rose-700">
                  {simulation.lostPhones.length > 0 ? simulation.lostPhones.join(', ') : '(none)'}
                </span>
              </span>
            </div>
          </div>

          <input
            type="text"
            value={daughterName}
            onChange={(e) => setDaughterName(e.target.value)}
            placeholder={`Daughter name (default: ${state.Name} (Evolved))`}
            className="w-full px-2.5 py-1.5 text-sm bg-white border border-slate-300 rounded"
          />

          <label className="flex items-center gap-2 text-xs text-slate-700">
            <input
              type="checkbox"
              checked={preserveHistoricalSpelling}
              onChange={(e) => setPreserveHistoricalSpelling(e.target.checked)}
            />
            <span>Preserve historical pre-shift spellings (enables Deep Morphophonemic Orthography)</span>
          </label>

          <button
            type="button"
            onClick={applyEvolution}
            disabled={rules.every((r) => !r.Enabled)}
            className="w-full px-3 py-2 text-sm font-medium bg-indigo-700 text-white rounded hover:bg-indigo-800 disabled:opacity-40"
          >
            Apply Evolution to Project Lexicon & Affixes
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <div className="lg:col-span-2 bg-white border border-slate-300 rounded p-3 space-y-2">
          <h4 className="text-sm font-bold text-slate-900">Lexicon Cognate Correspondence</h4>
          <div className="border border-slate-200 rounded max-h-60 overflow-y-auto">
            <table className="w-full text-xs text-left border-collapse">
              <thead className="bg-slate-100 border-b border-slate-200 text-slate-700 sticky top-0">
                <tr>
                  <th className="px-2.5 py-1.5">Lemma</th>
                  <th className="px-2.5 py-1.5">POS</th>
                  <th className="px-2.5 py-1.5">Proto UR</th>
                  <th className="px-2.5 py-1.5">Daughter UR</th>
                  <th className="px-2.5 py-1.5">Orthography</th>
                  <th className="px-2.5 py-1.5">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {simulation.cognates.map((c) => (
                  <tr key={`${c.lemma}-${c.pos}`} className={c.changed ? 'bg-amber-50/50' : ''}>
                    <td className="px-2.5 py-1.5 font-medium">{c.lemma}</td>
                    <td className="px-2.5 py-1.5 font-mono">{c.pos}</td>
                    <td className="px-2.5 py-1.5 font-mono text-slate-600">*{c.protoUr}</td>
                    <td className="px-2.5 py-1.5 font-mono font-semibold">{c.daughterUr}</td>
                    <td className="px-2.5 py-1.5 font-mono text-indigo-700">{c.daughterWritten}</td>
                    <td className="px-2.5 py-1.5">
                      {c.changed ? (
                        <span className="text-amber-700 font-semibold">Shifted</span>
                      ) : (
                        <span className="text-slate-400">Unchanged</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="bg-white border border-slate-300 rounded p-3 space-y-2">
          <h4 className="text-sm font-bold text-slate-900">Affix Evolution Diff</h4>
          <div className="border border-slate-200 rounded max-h-60 overflow-y-auto divide-y divide-slate-100 text-xs">
            {simulation.affixDiffs.length === 0 ? (
              <div className="p-3 text-slate-400">No affixes in lexicon.</div>
            ) : (
              simulation.affixDiffs.map((a, i) => (
                <div key={`${a.name}-${i}`} className="px-2.5 py-1.5 flex items-center justify-between gap-2">
                  <div>
                    <div className="font-medium text-slate-800">{a.name}</div>
                    <div className="text-[11px] text-slate-500">{a.pos}</div>
                  </div>
                  <div className="font-mono text-right">
                    <span className="text-slate-500">{a.protoAffix}</span>
                    <span className="mx-1 text-slate-400">→</span>
                    <span className={a.changed ? 'font-bold text-amber-700' : 'text-slate-700'}>
                      {a.daughterAffix}
                    </span>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

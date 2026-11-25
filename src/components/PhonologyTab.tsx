import React, { useMemo, useState } from 'react';
import {
  LanguageProjectState,
  Phoneme,
  PhonologicalFeatures,
  RuleSpec,
  StressPattern,
} from '../core/types';
import {
  buildEngineFromState,
  createPhoneme,
  formatRuleSpec,
  Morphology,
} from '../core/languageEngine';

interface PhonologyTabProps {
  state: LanguageProjectState;
  onUpdate: (updater: (prev: LanguageProjectState) => LanguageProjectState, label: string) => void;
}

const VOICING_OPTS = ['', 'voiced', 'voiceless'];
const MANNER_OPTS = ['', 'stop', 'fricative', 'nasal', 'approximant', 'vowel', 'affricate', 'trill', 'tap'];
const PLACE_OPTS = [
  '',
  'bilabial',
  'labiodental',
  'dental',
  'alveolar',
  'postalveolar',
  'palatal',
  'velar',
  'uvular',
  'pharyngeal',
  'glottal',
];
const HEIGHT_OPTS = ['', 'high', 'mid', 'low'];
const BACKNESS_OPTS = ['', 'front', 'central', 'back'];
const ROUND_OPTS = ['', 'rounded', 'unrounded'];

export const PhonologyTab: React.FC<PhonologyTabProps> = ({ state, onUpdate }) => {
  const [newPhoneme, setNewPhoneme] = useState('');
  const [newWeight, setNewWeight] = useState(1);
  const [newAllowOnset, setNewAllowOnset] = useState(true);
  const [newAllowCoda, setNewAllowCoda] = useState(true);
  const [showFeatureEditor, setShowFeatureEditor] = useState(false);

  const [newRuleName, setNewRuleName] = useState('Voicing assimilation');
  const [newRuleTarget, setNewRuleTarget] = useState('s');
  const [newRuleReplacement, setNewRuleReplacement] = useState('z');
  const [newRulePrev, setNewRulePrev] = useState('');
  const [newRuleNext, setNewRuleNext] = useState('');
  const [newRuleCond, setNewRuleCond] = useState('');

  const [derivationInput, setDerivationInput] = useState('a s a');
  const [manualRefreshTick, setManualRefreshTick] = useState(0);

  const [newTemplatePattern, setNewTemplatePattern] = useState('CCV');
  const [newTemplateWeight, setNewTemplateWeight] = useState(1);

  const consonants = useMemo(() => state.Phonemes.filter((p) => p.type === 'C'), [state.Phonemes]);
  const vowels = useMemo(() => state.Phonemes.filter((p) => p.type === 'V'), [state.Phonemes]);

  const derivationPreview = useMemo(() => {
    void manualRefreshTick;
    const engine = buildEngineFromState(state);
    const phones = Morphology.splitPhones(derivationInput);
    const mock = [
      {
        morpheme: { id: 'DEMO', gloss: 'demo', underlyingPhones: phones },
        underlyingPhones: phones,
      },
    ];
    const derived = engine.phonology.derive(mock);
    const sylStr = derived.syllables
      .map((s, idx) => {
        const prefix = idx === derived.stressIndex ? 'ˈ' : '';
        return `${prefix}${s.onset.join('')}${s.nucleus.join('')}${s.coda.join('')}`;
      })
      .join('.');
    return [
      ...derived.steps.map((s) => `${s.label}: ${s.value}`),
      `Syllables: ${sylStr || '(none)'}`,
    ].join('\n');
  }, [state, derivationInput, manualRefreshTick]);

  const syncLegacyLists = (phonemes: Phoneme[]) => ({
    Phonemes: phonemes,
    Consonants: phonemes.filter((p) => p.type === 'C').map((p) => p.symbol),
    Vowels: phonemes.filter((p) => p.type === 'V').map((p) => p.symbol),
  });

  const addPhoneme = (kind: 'C' | 'V') => {
    const sym = newPhoneme.trim();
    if (!sym) return;
    onUpdate((prev) => {
      const existing = prev.Phonemes.find((p) => p.symbol === sym && p.type === kind);
      const next = createPhoneme(sym, kind, {
        weight: Math.max(1, newWeight),
        allowOnset: newAllowOnset,
        allowCoda: newAllowCoda,
        features: existing?.features,
      });
      const updated = [...prev.Phonemes.filter((p) => !(p.symbol === sym && p.type === kind)), next];
      return { ...prev, ...syncLegacyLists(updated) };
    }, 'Phonology inventory updated');
    setNewPhoneme('');
  };

  const removePhoneme = (symbol: string, kind: 'C' | 'V') => {
    onUpdate((prev) => {
      const updated = prev.Phonemes.filter((p) => !(p.symbol === symbol && p.type === kind));
      return { ...prev, ...syncLegacyLists(updated) };
    }, 'Phonology inventory updated');
  };

  const updatePhonemeFeature = (
    symbol: string,
    kind: 'C' | 'V',
    field: keyof PhonologicalFeatures,
    value: string
  ) => {
    onUpdate((prev) => {
      const updated = prev.Phonemes.map((p) =>
        p.symbol === symbol && p.type === kind
          ? { ...p, features: { ...p.features, [field]: value } }
          : p
      );
      return { ...prev, Phonemes: updated };
    }, 'Phoneme features updated');
  };

  const addRule = () => {
    if (!newRuleName.trim()) return;
    onUpdate(
      (prev) => ({
        ...prev,
        PhonologyRules: [
          ...prev.PhonologyRules,
          {
            Name: newRuleName.trim(),
            Target: newRuleTarget.trim() || '∅',
            Replacement: newRuleReplacement.trim() || '∅',
            PrevPhone: newRulePrev.trim() || null,
            NextPhone: newRuleNext.trim() || null,
            Condition: newRuleCond.trim(),
            Enabled: true,
          },
        ],
      }),
      'Phonology rules updated'
    );
  };

  const toggleRule = (index: number) => {
    onUpdate(
      (prev) => ({
        ...prev,
        PhonologyRules: prev.PhonologyRules.map((r, i) =>
          i === index ? { ...r, Enabled: !r.Enabled } : r
        ),
      }),
      'Phonology rules updated'
    );
  };

  const moveRule = (index: number, delta: -1 | 1) => {
    const target = index + delta;
    if (target < 0 || target >= state.PhonologyRules.length) return;
    onUpdate((prev) => {
      const list = [...prev.PhonologyRules];
      const [item] = list.splice(index, 1);
      list.splice(target, 0, item);
      return { ...prev, PhonologyRules: list };
    }, 'Phonology rule reordered');
  };

  const removeRule = (index: number) => {
    onUpdate(
      (prev) => ({
        ...prev,
        PhonologyRules: prev.PhonologyRules.filter((_, i) => i !== index),
      }),
      'Phonology rules updated'
    );
  };

  const addTemplate = () => {
    if (!newTemplatePattern.trim()) return;
    onUpdate(
      (prev) => ({
        ...prev,
        SyllableTemplates: [
          ...prev.SyllableTemplates,
          { pattern: newTemplatePattern.trim(), weight: Math.max(1, newTemplateWeight) },
        ],
      }),
      'Syllable templates updated'
    );
    setNewTemplatePattern('');
  };

  const removeTemplate = (index: number) => {
    onUpdate(
      (prev) => ({
        ...prev,
        SyllableTemplates: prev.SyllableTemplates.filter((_, i) => i !== index),
      }),
      'Syllable templates updated'
    );
  };

  return (
    <div className="p-5 space-y-6">
      {/* Inventory + Feature Matrix */}
      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-bold text-slate-900">Phoneme Inventory</h3>
          <button
            type="button"
            onClick={() => setShowFeatureEditor((v) => !v)}
            className="px-3 py-1 text-xs font-medium bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded"
          >
            {showFeatureEditor ? 'Hide Distinctive Features' : 'Edit Distinctive Features…'}
          </button>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <div className="text-xs font-semibold text-slate-700 mb-1">Consonants ({consonants.length})</div>
            <div className="border border-slate-300 bg-white rounded h-36 overflow-y-auto divide-y divide-slate-100">
              {consonants.map((c) => (
                <div key={c.symbol} className="flex items-center justify-between px-3 py-1.5 text-sm">
                  <div className="flex items-center gap-2">
                    <span className="font-mono font-semibold">{c.symbol}</span>
                    <span className="text-xs text-slate-500">
                      w={c.weight} {c.features.voicing} {c.features.place} {c.features.manner}
                    </span>
                  </div>
                  <button
                    type="button"
                    onClick={() => removePhoneme(c.symbol, 'C')}
                    className="px-2 py-0.5 text-xs bg-slate-100 hover:bg-rose-50 hover:text-rose-700 border border-slate-300 rounded"
                  >
                    Remove
                  </button>
                </div>
              ))}
            </div>
          </div>

          <div>
            <div className="text-xs font-semibold text-slate-700 mb-1">Vowels ({vowels.length})</div>
            <div className="border border-slate-300 bg-white rounded h-36 overflow-y-auto divide-y divide-slate-100">
              {vowels.map((v) => (
                <div key={v.symbol} className="flex items-center justify-between px-3 py-1.5 text-sm">
                  <div className="flex items-center gap-2">
                    <span className="font-mono font-semibold">{v.symbol}</span>
                    <span className="text-xs text-slate-500">
                      w={v.weight} {v.features.height} {v.features.backness} {v.features.roundness}
                    </span>
                  </div>
                  <button
                    type="button"
                    onClick={() => removePhoneme(v.symbol, 'V')}
                    className="px-2 py-0.5 text-xs bg-slate-100 hover:bg-rose-50 hover:text-rose-700 border border-slate-300 rounded"
                  >
                    Remove
                  </button>
                </div>
              ))}
            </div>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-2 text-sm">
          <input
            type="text"
            value={newPhoneme}
            onChange={(e) => setNewPhoneme(e.target.value)}
            placeholder="Symbol (e.g. ŋ, t͡s)"
            className="w-36 px-2.5 py-1 bg-white border border-slate-300 rounded font-mono"
          />
          <label className="flex items-center gap-1 text-xs text-slate-700">
            Weight
            <input
              type="number"
              min={1}
              max={99}
              value={newWeight}
              onChange={(e) => setNewWeight(Math.max(1, Number(e.target.value) || 1))}
              className="w-14 px-2 py-1 bg-white border border-slate-300 rounded"
            />
          </label>
          <label className="flex items-center gap-1 text-xs text-slate-700">
            <input
              type="checkbox"
              checked={newAllowOnset}
              onChange={(e) => setNewAllowOnset(e.target.checked)}
            />
            Onset
          </label>
          <label className="flex items-center gap-1 text-xs text-slate-700">
            <input
              type="checkbox"
              checked={newAllowCoda}
              onChange={(e) => setNewAllowCoda(e.target.checked)}
            />
            Coda
          </label>
          <button
            type="button"
            onClick={() => addPhoneme('C')}
            disabled={!newPhoneme.trim()}
            className="px-3 py-1 text-xs font-medium bg-slate-800 text-white rounded hover:bg-slate-700 disabled:opacity-40"
          >
            Add C
          </button>
          <button
            type="button"
            onClick={() => addPhoneme('V')}
            disabled={!newPhoneme.trim()}
            className="px-3 py-1 text-xs font-medium bg-slate-800 text-white rounded hover:bg-slate-700 disabled:opacity-40"
          >
            Add V
          </button>
        </div>

        {showFeatureEditor && (
          <div className="border border-slate-300 bg-white rounded overflow-x-auto max-h-64">
            <table className="w-full text-xs text-left border-collapse">
              <thead className="bg-slate-100 border-b border-slate-300 text-slate-700 sticky top-0">
                <tr>
                  <th className="px-2 py-1.5 font-semibold">Sym</th>
                  <th className="px-2 py-1.5 font-semibold">Type</th>
                  <th className="px-2 py-1.5 font-semibold">Voicing</th>
                  <th className="px-2 py-1.5 font-semibold">Manner</th>
                  <th className="px-2 py-1.5 font-semibold">Place</th>
                  <th className="px-2 py-1.5 font-semibold">Height</th>
                  <th className="px-2 py-1.5 font-semibold">Backness</th>
                  <th className="px-2 py-1.5 font-semibold">Roundness</th>
                  <th className="px-2 py-1.5 font-semibold">Custom tags</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {state.Phonemes.map((p) => (
                  <tr key={`${p.type}-${p.symbol}`}>
                    <td className="px-2 py-1 font-mono font-semibold">{p.symbol}</td>
                    <td className="px-2 py-1 font-mono">{p.type}</td>
                    <td className="px-1 py-1">
                      <select
                        value={p.features.voicing}
                        onChange={(e) => updatePhonemeFeature(p.symbol, p.type, 'voicing', e.target.value)}
                        className="w-full px-1 py-0.5 bg-white border border-slate-300 rounded"
                      >
                        {VOICING_OPTS.map((o) => (
                          <option key={o} value={o}>{o || '—'}</option>
                        ))}
                      </select>
                    </td>
                    <td className="px-1 py-1">
                      <select
                        value={p.features.manner}
                        onChange={(e) => updatePhonemeFeature(p.symbol, p.type, 'manner', e.target.value)}
                        className="w-full px-1 py-0.5 bg-white border border-slate-300 rounded"
                      >
                        {MANNER_OPTS.map((o) => (
                          <option key={o} value={o}>{o || '—'}</option>
                        ))}
                      </select>
                    </td>
                    <td className="px-1 py-1">
                      <select
                        value={p.features.place}
                        onChange={(e) => updatePhonemeFeature(p.symbol, p.type, 'place', e.target.value)}
                        className="w-full px-1 py-0.5 bg-white border border-slate-300 rounded"
                      >
                        {PLACE_OPTS.map((o) => (
                          <option key={o} value={o}>{o || '—'}</option>
                        ))}
                      </select>
                    </td>
                    <td className="px-1 py-1">
                      <select
                        value={p.features.height}
                        onChange={(e) => updatePhonemeFeature(p.symbol, p.type, 'height', e.target.value)}
                        className="w-full px-1 py-0.5 bg-white border border-slate-300 rounded"
                      >
                        {HEIGHT_OPTS.map((o) => (
                          <option key={o} value={o}>{o || '—'}</option>
                        ))}
                      </select>
                    </td>
                    <td className="px-1 py-1">
                      <select
                        value={p.features.backness}
                        onChange={(e) => updatePhonemeFeature(p.symbol, p.type, 'backness', e.target.value)}
                        className="w-full px-1 py-0.5 bg-white border border-slate-300 rounded"
                      >
                        {BACKNESS_OPTS.map((o) => (
                          <option key={o} value={o}>{o || '—'}</option>
                        ))}
                      </select>
                    </td>
                    <td className="px-1 py-1">
                      <select
                        value={p.features.roundness}
                        onChange={(e) => updatePhonemeFeature(p.symbol, p.type, 'roundness', e.target.value)}
                        className="w-full px-1 py-0.5 bg-white border border-slate-300 rounded"
                      >
                        {ROUND_OPTS.map((o) => (
                          <option key={o} value={o}>{o || '—'}</option>
                        ))}
                      </select>
                    </td>
                    <td className="px-1 py-1">
                      <input
                        type="text"
                        value={p.features.custom}
                        onChange={(e) => updatePhonemeFeature(p.symbol, p.type, 'custom', e.target.value)}
                        className="w-full px-1.5 py-0.5 bg-white border border-slate-300 rounded"
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Syllable Templates & Phonotactics (harvested from MainForm.cs) */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 border-t border-slate-200 pt-4">
        <div className="space-y-2">
          <h3 className="text-sm font-bold text-slate-900">Syllable Templates</h3>
          <div className="border border-slate-300 bg-white rounded max-h-32 overflow-y-auto divide-y divide-slate-100">
            {state.SyllableTemplates.map((t, idx) => (
              <div key={`${t.pattern}-${idx}`} className="flex items-center justify-between px-3 py-1.5 text-sm">
                <span className="font-mono">
                  {t.pattern} <span className="text-xs text-slate-500">(w={t.weight})</span>
                </span>
                <button
                  type="button"
                  onClick={() => removeTemplate(idx)}
                  className="px-2 py-0.5 text-xs bg-slate-100 hover:bg-rose-50 hover:text-rose-700 border border-slate-300 rounded"
                >
                  Remove
                </button>
              </div>
            ))}
          </div>
          <div className="flex items-center gap-2">
            <input
              type="text"
              value={newTemplatePattern}
              onChange={(e) => setNewTemplatePattern(e.target.value)}
              placeholder="Pattern (CV, (C)VC)"
              className="w-36 px-2.5 py-1 text-sm bg-white border border-slate-300 rounded font-mono"
            />
            <input
              type="number"
              min={1}
              max={99}
              value={newTemplateWeight}
              onChange={(e) => setNewTemplateWeight(Math.max(1, Number(e.target.value) || 1))}
              className="w-16 px-2 py-1 text-sm bg-white border border-slate-300 rounded"
            />
            <button
              type="button"
              onClick={addTemplate}
              className="px-3 py-1 text-xs font-medium bg-slate-800 text-white rounded hover:bg-slate-700"
            >
              Add Template
            </button>
          </div>
        </div>

        <div className="space-y-2">
          <h3 className="text-sm font-bold text-slate-900">Phonotactics</h3>
          <div className="grid grid-cols-2 gap-2 text-xs">
            <label className="space-y-1">
              <span className="text-slate-700 font-medium">Allowed Onsets (comma-sep)</span>
              <input
                type="text"
                value={state.Phonotactics.allowedOnsets.join(', ')}
                onChange={(e) =>
                  onUpdate(
                    (prev) => ({
                      ...prev,
                      Phonotactics: {
                        ...prev.Phonotactics,
                        allowedOnsets: e.target.value
                          .split(',')
                          .map((s) => s.trim())
                          .filter(Boolean),
                      },
                    }),
                    'Phonotactics updated'
                  )
                }
                className="w-full px-2 py-1 bg-white border border-slate-300 rounded font-mono"
              />
            </label>
            <label className="space-y-1">
              <span className="text-slate-700 font-medium">Allowed Codas (comma-sep)</span>
              <input
                type="text"
                value={state.Phonotactics.allowedCodas.join(', ')}
                onChange={(e) =>
                  onUpdate(
                    (prev) => ({
                      ...prev,
                      Phonotactics: {
                        ...prev.Phonotactics,
                        allowedCodas: e.target.value
                          .split(',')
                          .map((s) => s.trim())
                          .filter(Boolean),
                      },
                    }),
                    'Phonotactics updated'
                  )
                }
                className="w-full px-2 py-1 bg-white border border-slate-300 rounded font-mono"
              />
            </label>
            <label className="flex items-center gap-2">
              <span className="text-slate-700 font-medium">Max Onset:</span>
              <input
                type="number"
                min={0}
                max={6}
                value={state.Phonotactics.maxOnsetCluster}
                onChange={(e) =>
                  onUpdate(
                    (prev) => ({
                      ...prev,
                      Phonotactics: {
                        ...prev.Phonotactics,
                        maxOnsetCluster: Math.max(0, Number(e.target.value) || 0),
                      },
                    }),
                    'Phonotactics updated'
                  )
                }
                className="w-16 px-2 py-1 bg-white border border-slate-300 rounded"
              />
            </label>
            <label className="flex items-center gap-2">
              <span className="text-slate-700 font-medium">Max Coda:</span>
              <input
                type="number"
                min={0}
                max={6}
                value={state.Phonotactics.maxCodaCluster}
                onChange={(e) =>
                  onUpdate(
                    (prev) => ({
                      ...prev,
                      Phonotactics: {
                        ...prev.Phonotactics,
                        maxCodaCluster: Math.max(0, Number(e.target.value) || 0),
                      },
                    }),
                    'Phonotactics updated'
                  )
                }
                className="w-16 px-2 py-1 bg-white border border-slate-300 rounded"
              />
            </label>
          </div>
          <label className="block text-xs space-y-1">
            <span className="text-slate-700 font-medium">Forbidden Regex Patterns (one per line)</span>
            <textarea
              rows={2}
              value={state.Phonotactics.forbiddenPatterns.join('\n')}
              onChange={(e) =>
                onUpdate(
                  (prev) => ({
                    ...prev,
                    Phonotactics: {
                      ...prev.Phonotactics,
                      forbiddenPatterns: e.target.value
                        .split(/\r?\n/)
                        .map((s) => s.trim())
                        .filter(Boolean),
                    },
                  }),
                  'Phonotactics updated'
                )
              }
              className="w-full px-2 py-1 bg-white border border-slate-300 rounded font-mono"
            />
          </label>
        </div>
      </div>

      {/* Phonological Rules + Stress + Derivation Trace */}
      <div className="border-t border-slate-200 pt-4 space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h3 className="text-sm font-bold text-slate-900">
            Phonological Rules{' '}
            <span className="font-normal text-xs text-slate-500">
              (supports ∅, #, $, V/C, [+voice], [-stressed], [tone=LHL], [αplace], [αback, αround])
            </span>
          </h3>
          <label className="flex items-center gap-2 text-xs">
            <span className="font-medium text-slate-700">Primary Stress:</span>
            <select
              value={state.StressPattern ?? 'None'}
              onChange={(e) =>
                onUpdate(
                  (prev) => ({ ...prev, StressPattern: e.target.value as StressPattern }),
                  'Stress pattern updated'
                )
              }
              className="px-2 py-1 bg-white border border-slate-300 rounded"
            >
              <option value="None">None</option>
              <option value="Initial">Initial Syllable</option>
              <option value="Penultimate">Penultimate Syllable</option>
              <option value="Final">Final Syllable</option>
              <option value="Lexical">Lexical (UR ˈ mark)</option>
            </select>
          </label>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="md:col-span-2 border border-slate-300 bg-white rounded h-52 overflow-y-auto divide-y divide-slate-100">
            {state.PhonologyRules.length === 0 ? (
              <div className="p-3 text-sm text-slate-400">No phonological rules configured.</div>
            ) : (
              state.PhonologyRules.map((rule, idx) => {
                const spec: RuleSpec = {
                  name: rule.Name,
                  target: rule.Target,
                  replacement: rule.Replacement,
                  prevPhone: rule.PrevPhone,
                  nextPhone: rule.NextPhone,
                  condition: rule.Condition,
                  enabled: rule.Enabled,
                };
                return (
                  <div key={`${rule.Name}-${idx}`} className="flex items-center justify-between px-3 py-2 text-sm gap-2">
                    <label className="flex items-center gap-2 min-w-0">
                      <input
                        type="checkbox"
                        checked={rule.Enabled}
                        onChange={() => toggleRule(idx)}
                      />
                      <span className={`font-mono truncate ${rule.Enabled ? 'text-slate-800' : 'text-slate-400 line-through'}`}>
                        {formatRuleSpec(spec)}
                      </span>
                    </label>
                    <div className="flex items-center gap-1 shrink-0">
                      <button
                        type="button"
                        onClick={() => moveRule(idx, -1)}
                        disabled={idx === 0}
                        className="px-1.5 py-0.5 text-xs bg-slate-100 hover:bg-slate-200 border border-slate-300 rounded disabled:opacity-40"
                      >
                        ↑
                      </button>
                      <button
                        type="button"
                        onClick={() => moveRule(idx, 1)}
                        disabled={idx === state.PhonologyRules.length - 1}
                        className="px-1.5 py-0.5 text-xs bg-slate-100 hover:bg-slate-200 border border-slate-300 rounded disabled:opacity-40"
                      >
                        ↓
                      </button>
                      <button
                        type="button"
                        onClick={() => removeRule(idx)}
                        className="px-2 py-0.5 text-xs bg-slate-100 hover:bg-rose-50 hover:text-rose-700 border border-slate-300 rounded"
                      >
                        Remove
                      </button>
                    </div>
                  </div>
                );
              })
            )}
          </div>

          <div className="space-y-1.5">
            <input
              type="text"
              value={newRuleName}
              onChange={(e) => setNewRuleName(e.target.value)}
              placeholder="Rule Name"
              className="w-full px-2.5 py-1 text-sm bg-white border border-slate-300 rounded"
            />
            <div className="grid grid-cols-2 gap-1.5">
              <input
                type="text"
                value={newRuleTarget}
                onChange={(e) => setNewRuleTarget(e.target.value)}
                placeholder="Target (or ∅)"
                className="w-full px-2.5 py-1 text-sm bg-white border border-slate-300 rounded font-mono"
              />
              <input
                type="text"
                value={newRuleReplacement}
                onChange={(e) => setNewRuleReplacement(e.target.value)}
                placeholder="Replace (∅, [αplace], [αback, αround], [tone=LH])"
                className="w-full px-2.5 py-1 text-sm bg-white border border-slate-300 rounded font-mono"
              />
            </div>
            <div className="grid grid-cols-2 gap-1.5">
              <input
                type="text"
                value={newRulePrev}
                onChange={(e) => setNewRulePrev(e.target.value)}
                placeholder="Prev (# or $)"
                className="w-full px-2.5 py-1 text-sm bg-white border border-slate-300 rounded font-mono"
              />
              <input
                type="text"
                value={newRuleNext}
                onChange={(e) => setNewRuleNext(e.target.value)}
                placeholder="Next (#, $, [tone=LHL])"
                className="w-full px-2.5 py-1 text-sm bg-white border border-slate-300 rounded font-mono"
              />
            </div>
            <input
              type="text"
              value={newRuleCond}
              onChange={(e) => setNewRuleCond(e.target.value)}
              placeholder="Condition (e.g. [+voice], [manner=stop], [+high-tone])"
              className="w-full px-2.5 py-1 text-sm bg-white border border-slate-300 rounded font-mono"
            />
            <button
              type="button"
              onClick={addRule}
              disabled={!newRuleName.trim()}
              className="w-full px-3 py-1.5 text-sm bg-slate-800 text-white rounded hover:bg-slate-700 disabled:opacity-40"
            >
              Add Rule
            </button>
          </div>
        </div>
      </div>

      <div className="space-y-2">
        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => setManualRefreshTick((n) => n + 1)}
            className="px-3 py-1.5 text-sm bg-slate-200 hover:bg-slate-300 text-slate-900 border border-slate-300 rounded"
          >
            Show Derivation
          </button>
          <input
            type="text"
            value={derivationInput}
            onChange={(e) => setDerivationInput(e.target.value)}
            placeholder="Test UR (space-separated phones)"
            className="w-56 px-2.5 py-1.5 text-sm bg-white border border-slate-300 rounded font-mono"
          />
        </div>
        <textarea
          readOnly
          value={derivationPreview}
          rows={5}
          className="w-full p-2.5 text-sm font-mono bg-white border border-slate-300 rounded"
        />
      </div>
    </div>
  );
};

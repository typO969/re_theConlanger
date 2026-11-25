import React, { useMemo, useState } from 'react';
import { AffixType, FeatBundle, LanguageProjectState } from '../core/types';
import { buildEngineFromState, Morphology } from '../core/languageEngine';

interface MorphologyTabProps {
  state: LanguageProjectState;
  onUpdate: (updater: (prev: LanguageProjectState) => LanguageProjectState, label: string) => void;
}

const AFFIX_TYPES: AffixType[] = ['Prefix', 'Suffix', 'Infix', 'Circumfix', 'Template'];

export const MorphologyTab: React.FC<MorphologyTabProps> = ({ state, onUpdate }) => {
  const [newLemma, setNewLemma] = useState('');
  const [newPos, setNewPos] = useState('');
  const [newPhonemes, setNewPhonemes] = useState('');
  const [newLexClass, setNewLexClass] = useState('');
  const [newSpelling, setNewSpelling] = useState('');
  const [newIrregulars, setNewIrregulars] = useState('');

  const [newAffixName, setNewAffixName] = useState('');
  const [newAffixPos, setNewAffixPos] = useState('N');
  const [newFeatureName, setNewFeatureName] = useState('Number');
  const [newFeatureValue, setNewFeatureValue] = useState('Pl');
  const [newAffixValue, setNewAffixValue] = useState('-i');
  const [newAffixCondition, setNewAffixCondition] = useState('');
  const [newAffixType, setNewAffixType] = useState<AffixType>('Suffix');

  const [previewLemma, setPreviewLemma] = useState('person');
  const [previewPos, setPreviewPos] = useState('N');
  const [previewFeatureName, setPreviewFeatureName] = useState('Number');
  const [previewFeatureValue, setPreviewFeatureValue] = useState('Pl');
  const [previewTick, setPreviewTick] = useState(0);

  const sortedLexemes = useMemo(
    () => [...state.Lexemes].sort((a, b) => a.Lemma.localeCompare(b.Lemma)),
    [state.Lexemes]
  );

  const previewResult = useMemo(() => {
    void previewTick;
    const engine = buildEngineFromState(state);
    const features = new FeatBundle([{ name: previewFeatureName, value: previewFeatureValue }]);
    return engine.morphology.realizeForPreview(previewLemma, previewPos, features);
  }, [state, previewLemma, previewPos, previewFeatureName, previewFeatureValue, previewTick]);

  const parseIrregularsText = (raw: string): Record<string, string> | undefined => {
    const map: Record<string, string> = {};
    for (const part of raw.split(';')) {
      const idx = part.indexOf(':');
      if (idx <= 0) continue;
      const key = part.slice(0, idx).trim();
      const val = part.slice(idx + 1).trim();
      if (key && val) map[key] = val;
    }
    return Object.keys(map).length > 0 ? map : undefined;
  };

  const formatIrregulars = (irreg?: Record<string, string>): string => {
    if (!irreg) return '';
    return Object.entries(irreg)
      .map(([k, v]) => `${k}: ${v}`)
      .join('; ');
  };

  const addLexeme = () => {
    if (!newLemma.trim()) return;
    const pos = newPos.trim() || 'N';
    const ur = newPhonemes.trim().split(/\s+/).filter(Boolean).join(' ');
    const lexClass = newLexClass.trim();
    const spelling = newSpelling.trim();
    const irregulars = parseIrregularsText(newIrregulars);
    onUpdate(
      (prev) => ({
        ...prev,
        Lexemes: [
          ...prev.Lexemes.filter((l) => l.Lemma !== newLemma.trim()),
          {
            Lemma: newLemma.trim(),
            Pos: pos,
            UR: ur,
            ...(lexClass ? { LexClass: lexClass } : {}),
            ...(spelling ? { Spelling: spelling } : {}),
            ...(irregulars ? { Irregulars: irregulars } : {}),
          },
        ],
      }),
      'Lexicon updated'
    );
    setNewLemma('');
    setNewPos('');
    setNewPhonemes('');
    setNewLexClass('');
    setNewSpelling('');
    setNewIrregulars('');
  };

  const removeLexeme = (lemma: string) => {
    onUpdate(
      (prev) => ({
        ...prev,
        Lexemes: prev.Lexemes.filter((l) => l.Lemma !== lemma),
      }),
      'Lexicon updated'
    );
  };

  const addAffix = () => {
    if (!newAffixName.trim()) return;
    onUpdate(
      (prev) => ({
        ...prev,
        Affixes: [
          ...prev.Affixes,
          {
            Name: newAffixName.trim(),
            Pos: newAffixPos.trim() || 'N',
            FeatureName: newFeatureName.trim(),
            FeatureValue: newFeatureValue.trim(),
            Affix: newAffixValue.trim(),
            IsPrefix: newAffixType === 'Prefix',
            AffixType: newAffixType,
            Condition: newAffixCondition.trim(),
            Enabled: true,
          },
        ],
      }),
      'Morphology rules updated'
    );
    setNewAffixName('');
    setNewAffixCondition('');
  };

  const toggleAffix = (index: number) => {
    onUpdate(
      (prev) => ({
        ...prev,
        Affixes: prev.Affixes.map((a, i) => (i === index ? { ...a, Enabled: !a.Enabled } : a)),
      }),
      'Morphology rules updated'
    );
  };

  const moveAffix = (index: number, delta: -1 | 1) => {
    const target = index + delta;
    if (target < 0 || target >= state.Affixes.length) return;
    onUpdate((prev) => {
      const list = [...prev.Affixes];
      const [item] = list.splice(index, 1);
      list.splice(target, 0, item);
      return { ...prev, Affixes: list };
    }, 'Morphology rule reordered');
  };

  const removeAffix = (index: number) => {
    onUpdate(
      (prev) => ({
        ...prev,
        Affixes: prev.Affixes.filter((_, i) => i !== index),
      }),
      'Morphology rules updated'
    );
  };

  return (
    <div className="p-5 space-y-5">
      <div>
        <h3 className="text-base font-bold text-slate-900 mb-2">Lexicon</h3>
        <div className="border border-slate-300 bg-white rounded max-h-56 overflow-y-auto">
          <table className="w-full text-sm text-left border-collapse">
            <thead className="bg-slate-100 border-b border-slate-300 text-xs text-slate-700 sticky top-0">
              <tr>
                <th className="px-3 py-1.5 font-semibold">Lemma</th>
                <th className="px-3 py-1.5 font-semibold w-16">POS</th>
                <th className="px-3 py-1.5 font-semibold">Class</th>
                <th className="px-3 py-1.5 font-semibold">UR</th>
                <th className="px-3 py-1.5 font-semibold">Spelling</th>
                <th className="px-3 py-1.5 font-semibold">Irregulars</th>
                <th className="px-3 py-1.5 w-20"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {sortedLexemes.map((lex) => (
                <tr key={lex.Lemma} className="hover:bg-slate-50">
                  <td className="px-3 py-1.5 font-medium">{lex.Lemma}</td>
                  <td className="px-3 py-1.5 font-mono text-xs">{lex.Pos}</td>
                  <td className="px-3 py-1.5 font-mono text-xs text-indigo-700">{lex.LexClass ?? ''}</td>
                  <td className="px-3 py-1.5 font-mono text-xs">{lex.UR}</td>
                  <td className="px-3 py-1.5 text-xs text-slate-700">{lex.Spelling ?? ''}</td>
                  <td className="px-3 py-1.5 font-mono text-[11px] text-slate-600">
                    {formatIrregulars(lex.Irregulars)}
                  </td>
                  <td className="px-3 py-1.5 text-right">
                    <button
                      type="button"
                      onClick={() => removeLexeme(lex.Lemma)}
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
            value={newLemma}
            onChange={(e) => setNewLemma(e.target.value)}
            placeholder="Lemma"
            className="w-28 px-2.5 py-1.5 text-sm bg-white border border-slate-300 rounded"
          />
          <input
            type="text"
            value={newPos}
            onChange={(e) => setNewPos(e.target.value)}
            placeholder="POS (N/V/DET)"
            className="w-24 px-2.5 py-1.5 text-sm bg-white border border-slate-300 rounded"
          />
          <input
            type="text"
            value={newLexClass}
            onChange={(e) => setNewLexClass(e.target.value)}
            placeholder="Class (Fem/Class1)"
            className="w-28 px-2.5 py-1.5 text-sm bg-white border border-slate-300 rounded"
          />
          <input
            type="text"
            value={newPhonemes}
            onChange={(e) => setNewPhonemes(e.target.value)}
            placeholder="UR (e.g. ˈk æ t)"
            className="w-40 px-2.5 py-1.5 text-sm bg-white border border-slate-300 rounded font-mono"
          />
          <input
            type="text"
            value={newSpelling}
            onChange={(e) => setNewSpelling(e.target.value)}
            placeholder="Spelling (opt)"
            className="w-28 px-2.5 py-1.5 text-sm bg-white border border-slate-300 rounded"
          />
          <input
            type="text"
            value={newIrregulars}
            onChange={(e) => setNewIrregulars(e.target.value)}
            placeholder="Irregulars (Number=Pl: ˈm aɪ s)"
            className="w-52 px-2.5 py-1.5 text-sm bg-white border border-slate-300 rounded font-mono"
          />
          <button
            type="button"
            onClick={addLexeme}
            disabled={!newLemma.trim()}
            className="px-3 py-1.5 text-sm bg-slate-800 text-white rounded hover:bg-slate-700 disabled:opacity-40"
          >
            Add Lexeme
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <div className="lg:col-span-2 border border-slate-300 bg-white rounded max-h-56 overflow-y-auto">
          <table className="w-full text-sm text-left border-collapse">
            <thead className="bg-slate-100 border-b border-slate-300 text-xs text-slate-700 sticky top-0">
              <tr>
                <th className="px-2 py-1.5 font-semibold w-10">On</th>
                <th className="px-2 py-1.5 font-semibold">Name</th>
                <th className="px-2 py-1.5 font-semibold">POS</th>
                <th className="px-2 py-1.5 font-semibold">Feat</th>
                <th className="px-2 py-1.5 font-semibold">Val</th>
                <th className="px-2 py-1.5 font-semibold">Affix</th>
                <th className="px-2 py-1.5 font-semibold">Condition</th>
                <th className="px-2 py-1.5 font-semibold">Kind</th>
                <th className="px-2 py-1.5 w-28"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {state.Affixes.map((afx, idx) => (
                <tr key={`${afx.Name}-${idx}`} className="hover:bg-slate-50">
                  <td className="px-2 py-1.5">
                    <input
                      type="checkbox"
                      checked={afx.Enabled}
                      onChange={() => toggleAffix(idx)}
                    />
                  </td>
                  <td className="px-2 py-1.5">{afx.Name}</td>
                  <td className="px-2 py-1.5 font-mono text-xs">{afx.Pos}</td>
                  <td className="px-2 py-1.5 text-xs">{afx.FeatureName}</td>
                  <td className="px-2 py-1.5 text-xs">{afx.FeatureValue}</td>
                  <td className="px-2 py-1.5 font-mono text-xs">{afx.Affix}</td>
                  <td className="px-2 py-1.5 font-mono text-[11px] text-slate-600">{afx.Condition ?? ''}</td>
                  <td className="px-2 py-1.5 text-xs text-slate-500">
                    {Morphology.getEffectiveAffixType({ affixType: afx.AffixType, isPrefix: afx.IsPrefix })}
                  </td>
                  <td className="px-2 py-1.5 text-right whitespace-nowrap space-x-1">
                    <button
                      type="button"
                      onClick={() => moveAffix(idx, -1)}
                      disabled={idx === 0}
                      className="px-1.5 py-0.5 text-xs bg-slate-100 hover:bg-slate-200 border border-slate-300 rounded disabled:opacity-40"
                    >
                      ↑
                    </button>
                    <button
                      type="button"
                      onClick={() => moveAffix(idx, 1)}
                      disabled={idx === state.Affixes.length - 1}
                      className="px-1.5 py-0.5 text-xs bg-slate-100 hover:bg-slate-200 border border-slate-300 rounded disabled:opacity-40"
                    >
                      ↓
                    </button>
                    <button
                      type="button"
                      onClick={() => removeAffix(idx)}
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

        <div className="space-y-1.5">
          <div className="text-sm font-semibold text-slate-800">Add Affix / Allomorph Rule</div>
          <input
            type="text"
            value={newAffixName}
            onChange={(e) => setNewAffixName(e.target.value)}
            placeholder="Name (e.g. Plural /s/)"
            className="w-full px-2.5 py-1 text-sm bg-white border border-slate-300 rounded"
          />
          <div className="grid grid-cols-2 gap-1.5">
            <input
              type="text"
              value={newAffixPos}
              onChange={(e) => setNewAffixPos(e.target.value)}
              placeholder="POS"
              className="w-full px-2.5 py-1 text-sm bg-white border border-slate-300 rounded"
            />
            <input
              type="text"
              value={newAffixValue}
              onChange={(e) => setNewAffixValue(e.target.value)}
              placeholder="Affix (-s, um, ge+...+t, C1 a C2 a C3 a)"
              className="w-full px-2.5 py-1 text-sm bg-white border border-slate-300 rounded font-mono"
            />
          </div>
          <div className="grid grid-cols-2 gap-1.5">
            <input
              type="text"
              value={newFeatureName}
              onChange={(e) => setNewFeatureName(e.target.value)}
              placeholder="Feature (Number/Tense/Class)"
              className="w-full px-2.5 py-1 text-sm bg-white border border-slate-300 rounded"
            />
            <input
              type="text"
              value={newFeatureValue}
              onChange={(e) => setNewFeatureValue(e.target.value)}
              placeholder="Feature Value"
              className="w-full px-2.5 py-1 text-sm bg-white border border-slate-300 rounded"
            />
          </div>
          <input
            type="text"
            value={newAffixCondition}
            onChange={(e) => setNewAffixCondition(e.target.value)}
            placeholder="Condition (e.g. [+sibilant], [class=Fem])"
            className="w-full px-2.5 py-1 text-sm bg-white border border-slate-300 rounded font-mono"
          />
          <label className="flex items-center justify-between gap-2 text-xs text-slate-700">
            <span>Morphology Kind:</span>
            <select
              value={newAffixType}
              onChange={(e) => setNewAffixType(e.target.value as AffixType)}
              className="px-2 py-1 bg-white border border-slate-300 rounded text-xs"
            >
              {AFFIX_TYPES.map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </select>
          </label>
          <button
            type="button"
            onClick={addAffix}
            disabled={!newAffixName.trim()}
            className="w-full px-3 py-1.5 text-sm bg-slate-800 text-white rounded hover:bg-slate-700 disabled:opacity-40"
          >
            Add Rule
          </button>
        </div>
      </div>

      <div className="space-y-2">
        <div className="text-sm font-semibold text-slate-800">Preview Realizer</div>
        <div className="flex flex-wrap items-center gap-2">
          <input
            type="text"
            value={previewLemma}
            onChange={(e) => setPreviewLemma(e.target.value)}
            placeholder="Lemma"
            className="w-28 px-2.5 py-1 text-sm bg-white border border-slate-300 rounded"
          />
          <input
            type="text"
            value={previewPos}
            onChange={(e) => setPreviewPos(e.target.value)}
            placeholder="POS"
            className="w-16 px-2.5 py-1 text-sm bg-white border border-slate-300 rounded"
          />
          <input
            type="text"
            value={previewFeatureName}
            onChange={(e) => setPreviewFeatureName(e.target.value)}
            placeholder="Feature"
            className="w-28 px-2.5 py-1 text-sm bg-white border border-slate-300 rounded"
          />
          <input
            type="text"
            value={previewFeatureValue}
            onChange={(e) => setPreviewFeatureValue(e.target.value)}
            placeholder="Value"
            className="w-28 px-2.5 py-1 text-sm bg-white border border-slate-300 rounded"
          />
          <button
            type="button"
            onClick={() => setPreviewTick((n) => n + 1)}
            className="px-3 py-1 text-sm bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded"
          >
            Preview
          </button>
        </div>
        <input
          type="text"
          readOnly
          value={previewResult}
          className="w-full px-2.5 py-1.5 text-sm font-mono bg-white border border-slate-300 rounded"
        />
      </div>
    </div>
  );
};

import React, { useMemo, useState } from 'react';
import { FeatBundle, LanguageProjectState } from '../core/types';
import { buildEngineFromState } from '../core/languageEngine';

interface MorphologyTabProps {
  state: LanguageProjectState;
  onUpdate: (updater: (prev: LanguageProjectState) => LanguageProjectState, label: string) => void;
}

export const MorphologyTab: React.FC<MorphologyTabProps> = ({ state, onUpdate }) => {
  const [newLemma, setNewLemma] = useState('');
  const [newPos, setNewPos] = useState('');
  const [newPhonemes, setNewPhonemes] = useState('');

  const [newAffixName, setNewAffixName] = useState('');
  const [newAffixPos, setNewAffixPos] = useState('N');
  const [newFeatureName, setNewFeatureName] = useState('Number');
  const [newFeatureValue, setNewFeatureValue] = useState('Pl');
  const [newAffixValue, setNewAffixValue] = useState('-i');
  const [newAffixIsPrefix, setNewAffixIsPrefix] = useState(false);

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

  const addLexeme = () => {
    if (!newLemma.trim()) return;
    const pos = newPos.trim() || 'N';
    const ur = newPhonemes.trim().split(/\s+/).filter(Boolean).join(' ');
    onUpdate(
      (prev) => ({
        ...prev,
        Lexemes: [
          ...prev.Lexemes.filter((l) => l.Lemma !== newLemma.trim()),
          { Lemma: newLemma.trim(), Pos: pos, UR: ur },
        ],
      }),
      'Lexicon updated'
    );
    setNewLemma('');
    setNewPos('');
    setNewPhonemes('');
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
            IsPrefix: newAffixIsPrefix,
            Enabled: true,
          },
        ],
      }),
      'Morphology rules updated'
    );
    setNewAffixName('');
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
        <div className="border border-slate-300 bg-white rounded max-h-48 overflow-y-auto">
          <table className="w-full text-sm text-left border-collapse">
            <thead className="bg-slate-100 border-b border-slate-300 text-xs text-slate-700 sticky top-0">
              <tr>
                <th className="px-3 py-1.5 font-semibold">Lemma</th>
                <th className="px-3 py-1.5 font-semibold w-20">POS</th>
                <th className="px-3 py-1.5 font-semibold">UR</th>
                <th className="px-3 py-1.5 w-24"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {sortedLexemes.map((lex) => (
                <tr key={lex.Lemma} className="hover:bg-slate-50">
                  <td className="px-3 py-1.5 font-medium">{lex.Lemma}</td>
                  <td className="px-3 py-1.5 font-mono text-xs">{lex.Pos}</td>
                  <td className="px-3 py-1.5 font-mono text-xs">{lex.UR}</td>
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
            className="w-36 px-2.5 py-1.5 text-sm bg-white border border-slate-300 rounded"
          />
          <input
            type="text"
            value={newPos}
            onChange={(e) => setNewPos(e.target.value)}
            placeholder="POS (N/V/ADJ)"
            className="w-24 px-2.5 py-1.5 text-sm bg-white border border-slate-300 rounded"
          />
          <input
            type="text"
            value={newPhonemes}
            onChange={(e) => setNewPhonemes(e.target.value)}
            placeholder="UR phonemes (e.g. k a t a)"
            className="w-56 px-2.5 py-1.5 text-sm bg-white border border-slate-300 rounded font-mono"
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
        <div className="lg:col-span-2 border border-slate-300 bg-white rounded max-h-48 overflow-y-auto">
          <table className="w-full text-sm text-left border-collapse">
            <thead className="bg-slate-100 border-b border-slate-300 text-xs text-slate-700 sticky top-0">
              <tr>
                <th className="px-3 py-1.5 font-semibold w-12">On</th>
                <th className="px-3 py-1.5 font-semibold">Name</th>
                <th className="px-3 py-1.5 font-semibold">POS</th>
                <th className="px-3 py-1.5 font-semibold">Feat</th>
                <th className="px-3 py-1.5 font-semibold">Val</th>
                <th className="px-3 py-1.5 font-semibold">Affix</th>
                <th className="px-3 py-1.5 font-semibold">Kind</th>
                <th className="px-3 py-1.5 w-20"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {state.Affixes.map((afx, idx) => (
                <tr key={`${afx.Name}-${idx}`} className="hover:bg-slate-50">
                  <td className="px-3 py-1.5">
                    <input
                      type="checkbox"
                      checked={afx.Enabled}
                      onChange={() => toggleAffix(idx)}
                    />
                  </td>
                  <td className="px-3 py-1.5">{afx.Name}</td>
                  <td className="px-3 py-1.5 font-mono text-xs">{afx.Pos}</td>
                  <td className="px-3 py-1.5 text-xs">{afx.FeatureName}</td>
                  <td className="px-3 py-1.5 text-xs">{afx.FeatureValue}</td>
                  <td className="px-3 py-1.5 font-mono text-xs">{afx.Affix}</td>
                  <td className="px-3 py-1.5 text-xs text-slate-500">
                    {afx.IsPrefix ? 'Prefix' : 'Suffix'}
                  </td>
                  <td className="px-3 py-1.5 text-right">
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
          <div className="text-sm font-semibold text-slate-800">Add Affix Rule</div>
          <input
            type="text"
            value={newAffixName}
            onChange={(e) => setNewAffixName(e.target.value)}
            placeholder="Name (e.g. Plural)"
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
              placeholder="Affix (-i)"
              className="w-full px-2.5 py-1 text-sm bg-white border border-slate-300 rounded font-mono"
            />
          </div>
          <div className="grid grid-cols-2 gap-1.5">
            <input
              type="text"
              value={newFeatureName}
              onChange={(e) => setNewFeatureName(e.target.value)}
              placeholder="Feature Name"
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
          <label className="flex items-center gap-2 text-xs text-slate-700">
            <input
              type="checkbox"
              checked={newAffixIsPrefix}
              onChange={(e) => setNewAffixIsPrefix(e.target.checked)}
            />
            Prefix (unchecked = Suffix)
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

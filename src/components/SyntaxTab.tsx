import React, { useMemo, useState } from 'react';
import { ClauseTemplate, LanguageProjectState, WordOrder } from '../core/types';
import { buildEngineFromState, formatSyntaxTree } from '../core/languageEngine';
import { RNG } from '../core/rng';

interface SyntaxTabProps {
  state: LanguageProjectState;
  onUpdate: (updater: (prev: LanguageProjectState) => LanguageProjectState, label: string) => void;
}

const WORD_ORDERS: WordOrder[] = ['SVO', 'SOV', 'VSO', 'VOS', 'OVS', 'OSV'];
const CLAUSE_TEMPLATES: ClauseTemplate[] = ['Intransitive', 'Transitive', 'Copular'];

export const SyntaxTab: React.FC<SyntaxTabProps> = ({ state, onUpdate }) => {
  const [previewSeed, setPreviewSeed] = useState(1);
  const [approxWords, setApproxWords] = useState(4);

  const { linearPreview, treePreview } = useMemo(() => {
    const engine = buildEngineFromState(state);
    const tree = engine.syntax.realize(
      { clauseType: 'decl', approxWords, constraints: { Tense: 'Past' } },
      new RNG(previewSeed)
    );
    const linear = tree
      .linearize()
      .map((n) => `${n.lemmaId}/${n.pos}`)
      .join(' ');
    const treeText = formatSyntaxTree(tree.root, 0, false);
    return { linearPreview: linear, treePreview: treeText };
  }, [state, previewSeed, approxWords]);

  return (
    <div className="p-5 space-y-4">
      <div className="flex flex-wrap items-center gap-4">
        <label className="flex items-center gap-2 text-sm">
          <span className="font-medium text-slate-700">Word Order:</span>
          <select
            value={state.WordOrder}
            onChange={(e) =>
              onUpdate(
                (prev) => ({ ...prev, WordOrder: e.target.value as WordOrder }),
                'Syntax settings updated'
              )
            }
            className="px-2.5 py-1.5 text-sm bg-white border border-slate-300 rounded"
          >
            {WORD_ORDERS.map((wo) => (
              <option key={wo} value={wo}>
                {wo}
              </option>
            ))}
          </select>
        </label>

        <label className="flex items-center gap-2 text-sm">
          <span className="font-medium text-slate-700">Clause:</span>
          <select
            value={state.ClauseTemplate}
            onChange={(e) =>
              onUpdate(
                (prev) => ({ ...prev, ClauseTemplate: e.target.value as ClauseTemplate }),
                'Syntax settings updated'
              )
            }
            className="px-2.5 py-1.5 text-sm bg-white border border-slate-300 rounded"
          >
            {CLAUSE_TEMPLATES.map((ct) => (
              <option key={ct} value={ct}>
                {ct}
              </option>
            ))}
          </select>
        </label>

        <label className="flex items-center gap-2 text-sm">
          <span className="font-medium text-slate-700">Words:</span>
          <input
            type="number"
            min={1}
            max={8}
            value={approxWords}
            onChange={(e) => setApproxWords(Math.max(1, Number(e.target.value) || 3))}
            className="w-16 px-2 py-1 text-sm bg-white border border-slate-300 rounded"
          />
        </label>

        <button
          type="button"
          onClick={() => setPreviewSeed((s) => s + 1)}
          className="px-3 py-1.5 text-sm bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded"
        >
          Sample New Seed (#{previewSeed})
        </button>
      </div>

      <div className="space-y-2">
        <div className="text-sm font-semibold text-slate-800">Linearization Preview</div>
        <input
          type="text"
          readOnly
          value={linearPreview}
          className="w-full px-2.5 py-1.5 text-sm font-mono bg-white border border-slate-300 rounded"
        />
      </div>

      <div className="space-y-2">
        <div className="text-sm font-semibold text-slate-800">Tree Summary</div>
        <textarea
          readOnly
          value={treePreview}
          rows={8}
          className="w-full p-2.5 text-sm font-mono bg-white border border-slate-300 rounded"
        />
      </div>
    </div>
  );
};

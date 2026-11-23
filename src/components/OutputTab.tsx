import React, { useState } from 'react';
import { LanguageProjectState } from '../core/types';
import { buildEngineFromState } from '../core/languageEngine';
import { RNG } from '../core/rng';
import { downloadTextFile } from '../core/download';

interface OutputTabProps {
  state: LanguageProjectState;
}

export const OutputTab: React.FC<OutputTabProps> = ({ state }) => {
  const [sampleCount, setSampleCount] = useState(5);
  const [approxWords, setApproxWords] = useState(6);
  const [seed, setSeed] = useState('');
  const [featureName, setFeatureName] = useState('Tense');
  const [featureValue, setFeatureValue] = useState('Past');
  const [samples, setSamples] = useState<string[]>([]);
  const [outputText, setOutputText] = useState('');

  const generate = () => {
    const engine = buildEngineFromState(state);
    const constraints: Record<string, string> = {};
    if (featureName.trim() && featureValue.trim()) {
      constraints[featureName.trim()] = featureValue.trim();
    }

    const parsedSeed = Number.parseInt(seed, 10);
    const baseSeed = Number.isFinite(parsedSeed) ? parsedSeed : Date.now();
    const count = Math.max(1, sampleCount);
    const nextSamples: string[] = [];

    for (let i = 0; i < count; i++) {
      const rng = new RNG(baseSeed + i);
      const { written } = engine.generateSentence(rng, {
        clauseType: 'decl',
        approxWords: Math.max(1, approxWords),
        constraints,
      });
      nextSamples.push(written);
    }

    setSamples(nextSamples);
    setOutputText(nextSamples.map((s, i) => `${i + 1}. ${s}`).join('\n'));
  };

  const exportFile = (kind: 'txt' | 'csv') => {
    if (samples.length === 0) return;
    if (kind === 'csv') {
      const lines = [
        'Index,Sentence',
        ...samples.map((s, i) => `${i + 1},"${s.replace(/"/g, '""')}"`),
      ];
      downloadTextFile('samples.csv', lines.join('\n'), 'text/csv;charset=utf-8');
    } else {
      downloadTextFile('samples.txt', samples.join('\n'), 'text/plain;charset=utf-8');
    }
  };

  return (
    <div className="p-5 space-y-4">
      <div className="flex flex-wrap items-center gap-2 text-sm">
        <label className="flex items-center gap-1.5">
          <span className="text-slate-700">N</span>
          <input
            type="number"
            min={1}
            value={sampleCount}
            onChange={(e) => setSampleCount(Math.max(1, Number(e.target.value) || 1))}
            className="w-16 px-2 py-1 bg-white border border-slate-300 rounded"
          />
        </label>

        <label className="flex items-center gap-1.5">
          <span className="text-slate-700">Words</span>
          <input
            type="number"
            min={1}
            value={approxWords}
            onChange={(e) => setApproxWords(Math.max(1, Number(e.target.value) || 1))}
            className="w-16 px-2 py-1 bg-white border border-slate-300 rounded"
          />
        </label>

        <label className="flex items-center gap-1.5">
          <span className="text-slate-700">Seed</span>
          <input
            type="text"
            value={seed}
            onChange={(e) => setSeed(e.target.value)}
            placeholder="auto"
            className="w-24 px-2 py-1 bg-white border border-slate-300 rounded"
          />
        </label>

        <label className="flex items-center gap-1.5">
          <span className="text-slate-700">Feature</span>
          <input
            type="text"
            value={featureName}
            onChange={(e) => setFeatureName(e.target.value)}
            className="w-24 px-2 py-1 bg-white border border-slate-300 rounded"
          />
          <input
            type="text"
            value={featureValue}
            onChange={(e) => setFeatureValue(e.target.value)}
            className="w-24 px-2 py-1 bg-white border border-slate-300 rounded"
          />
        </label>

        <button
          type="button"
          onClick={generate}
          className="px-3 py-1 bg-slate-800 text-white rounded hover:bg-slate-700"
        >
          Generate
        </button>
        <button
          type="button"
          onClick={() => exportFile('txt')}
          disabled={samples.length === 0}
          className="px-3 py-1 bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded disabled:opacity-40"
        >
          Export TXT
        </button>
        <button
          type="button"
          onClick={() => exportFile('csv')}
          disabled={samples.length === 0}
          className="px-3 py-1 bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded disabled:opacity-40"
        >
          Export CSV
        </button>
      </div>

      <textarea
        readOnly
        value={outputText}
        rows={6}
        placeholder="Click Generate to synthesize sentences using the active Syntax, Morphology, Phonology, and Orthography pipeline."
        className="w-full p-2.5 text-sm font-mono bg-white border border-slate-300 rounded"
      />

      <div className="border border-slate-300 bg-white rounded min-h-40 max-h-64 overflow-y-auto divide-y divide-slate-100">
        {samples.length === 0 ? (
          <div className="p-3 text-sm text-slate-400">No generated samples yet.</div>
        ) : (
          samples.map((s, idx) => (
            <div key={idx} className="px-3 py-1.5 text-sm font-mono">
              {s}
            </div>
          ))
        )}
      </div>
    </div>
  );
};

import React, { useState } from 'react';
import { DiscourseResult, LanguageProjectState } from '../core/types';
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
  const [moodFilter, setMoodFilter] = useState<'Decl' | 'YNQuestion' | 'Imp'>('Decl');
  const [voiceFilter, setVoiceFilter] = useState<'Auto' | 'Active' | 'Passive'>('Auto');
  const [featureName, setFeatureName] = useState('Tense');
  const [featureValue, setFeatureValue] = useState('Past');
  const [samples, setSamples] = useState<string[]>([]);
  const [outputText, setOutputText] = useState('');

  const [discourseCount, setDiscourseCount] = useState(4);
  const [discourseWords, setDiscourseWords] = useState(6);
  const [discourseTense, setDiscourseTense] = useState<'Pres' | 'Past' | 'Fut'>('Past');
  const [enforceSemantics, setEnforceSemantics] = useState(true);
  const [trackDefiniteness, setTrackDefiniteness] = useState(true);
  const [usePronouns, setUsePronouns] = useState(true);
  const [useConnectives, setUseConnectives] = useState(true);
  const [discourseResult, setDiscourseResult] = useState<DiscourseResult | null>(null);

  const generate = () => {
    const engine = buildEngineFromState(state);
    const constraints: Record<string, string> = {};
    if (moodFilter !== 'Decl') {
      constraints.Mood = moodFilter;
    }
    if (voiceFilter !== 'Auto') {
      constraints.Voice = voiceFilter;
    }
    if (!enforceSemantics) {
      constraints.SemanticFilter = 'false';
    }
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
        clauseType: state.ClauseTemplate,
        approxWords: Math.max(1, approxWords),
        constraints,
      });
      nextSamples.push(written);
    }

    setSamples(nextSamples);
    setOutputText(nextSamples.map((s, i) => `${i + 1}. ${s}`).join('\n'));
  };

  const generateDiscourse = () => {
    const engine = buildEngineFromState(state);
    const parsedSeed = Number.parseInt(seed, 10);
    const baseSeed = Number.isFinite(parsedSeed) ? parsedSeed : Date.now();
    const rng = new RNG(baseSeed);
    const res = engine.generateDiscourse(rng, {
      sentenceCount: discourseCount,
      approxWords: discourseWords,
      enforceSelectionalRestrictions: enforceSemantics,
      trackDefiniteness,
      useAnaphoricPronouns: usePronouns,
      useConnectives,
      constraints: { Tense: discourseTense },
    });
    setDiscourseResult(res);
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
    <div className="p-5 space-y-5">
      <div className="space-y-3">
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <label className="flex items-center gap-1.5">
            <span className="text-slate-700">N</span>
            <input
              type="number"
              min={1}
              value={sampleCount}
              onChange={(e) => setSampleCount(Math.max(1, Number(e.target.value) || 1))}
              className="w-16 px-2 py-1 font-mono tabular-nums bg-white border border-slate-300 rounded"
            />
          </label>

          <label className="flex items-center gap-1.5">
            <span className="text-slate-700">Words</span>
            <input
              type="number"
              min={1}
              value={approxWords}
              onChange={(e) => setApproxWords(Math.max(1, Number(e.target.value) || 1))}
              className="w-16 px-2 py-1 font-mono tabular-nums bg-white border border-slate-300 rounded"
            />
          </label>

          <label className="flex items-center gap-1.5">
            <span className="text-slate-700">Seed</span>
            <input
              type="text"
              value={seed}
              onChange={(e) => setSeed(e.target.value)}
              placeholder="auto"
              className="w-20 px-2 py-1 font-mono tabular-nums bg-white border border-slate-300 rounded"
            />
          </label>

          <label className="flex items-center gap-1.5">
            <span className="text-slate-700">Mood</span>
            <select
              value={moodFilter}
              onChange={(e) => setMoodFilter(e.target.value as 'Decl' | 'YNQuestion' | 'Imp')}
              className="px-2 py-1 bg-white border border-slate-300 rounded"
            >
              <option value="Decl">Decl</option>
              <option value="YNQuestion">YNQuestion</option>
              <option value="Imp">Imp</option>
            </select>
          </label>

          <label className="flex items-center gap-1.5">
            <span className="text-slate-700">Voice</span>
            <select
              value={voiceFilter}
              onChange={(e) => setVoiceFilter(e.target.value as 'Auto' | 'Active' | 'Passive')}
              className="px-2 py-1 bg-white border border-slate-300 rounded"
            >
              <option value="Auto">Auto</option>
              <option value="Active">Active</option>
              <option value="Passive">Passive</option>
            </select>
          </label>

          <label className="flex items-center gap-1.5">
            <span className="text-slate-700">Feature</span>
            <input
              type="text"
              value={featureName}
              onChange={(e) => setFeatureName(e.target.value)}
              className="w-20 px-2 py-1 bg-white border border-slate-300 rounded"
            />
            <input
              type="text"
              value={featureValue}
              onChange={(e) => setFeatureValue(e.target.value)}
              className="w-20 px-2 py-1 bg-white border border-slate-300 rounded"
            />
          </label>

          <button
            type="button"
            onClick={generate}
            className="px-3.5 py-1 bg-slate-800 text-white rounded hover:bg-slate-700 whitespace-nowrap shrink-0"
          >
            Generate
          </button>
          <button
            type="button"
            onClick={() => exportFile('txt')}
            disabled={samples.length === 0}
            className="px-3 py-1 bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded disabled:opacity-40 whitespace-nowrap shrink-0"
          >
            Export TXT
          </button>
          <button
            type="button"
            onClick={() => exportFile('csv')}
            disabled={samples.length === 0}
            className="px-3 py-1 bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded disabled:opacity-40 whitespace-nowrap shrink-0"
          >
            Export CSV
          </button>
        </div>

        <textarea
          readOnly
          value={outputText}
          rows={5}
          placeholder="Click Generate to synthesize sentences using the active Syntax, Morphology, Phonology, and Orthography pipeline."
          className="w-full p-2.5 text-sm font-mono bg-white border border-slate-300 rounded"
        />

        <div className="border border-slate-300 bg-white rounded min-h-32 max-h-52 overflow-y-auto divide-y divide-slate-100">
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

      <div className="bg-white border border-slate-300 rounded p-4 space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h3 className="text-sm font-bold text-slate-900">Discourse & Narrative Paragraph Generator</h3>
            <p className="text-xs text-slate-600">
              Synthesizes multi-sentence connected prose with selectional restrictions, definiteness tracking (a → the), and anaphoric pronouns.
            </p>
          </div>
          <button
            type="button"
            onClick={generateDiscourse}
            className="px-3.5 py-1.5 text-xs font-medium bg-indigo-700 text-white rounded hover:bg-indigo-800 whitespace-nowrap"
          >
            Generate Discourse Paragraph
          </button>
        </div>

        <div className="flex flex-wrap items-center gap-3 text-xs">
          <label className="flex items-center gap-1.5">
            <span className="text-slate-700">Sentences (2-10)</span>
            <input
              type="number"
              min={2}
              max={10}
              value={discourseCount}
              onChange={(e) => setDiscourseCount(Math.max(2, Math.min(10, Number(e.target.value) || 4)))}
              className="w-14 px-2 py-1 font-mono bg-white border border-slate-300 rounded"
            />
          </label>
          <label className="flex items-center gap-1.5">
            <span className="text-slate-700">Words/Clause</span>
            <input
              type="number"
              min={2}
              max={12}
              value={discourseWords}
              onChange={(e) => setDiscourseWords(Math.max(2, Number(e.target.value) || 6))}
              className="w-14 px-2 py-1 font-mono bg-white border border-slate-300 rounded"
            />
          </label>
          <label className="flex items-center gap-1.5">
            <span className="text-slate-700">Tense</span>
            <select
              value={discourseTense}
              onChange={(e) => setDiscourseTense(e.target.value as 'Pres' | 'Past' | 'Fut')}
              className="px-2 py-1 bg-white border border-slate-300 rounded"
            >
              <option value="Pres">Pres</option>
              <option value="Past">Past</option>
              <option value="Fut">Fut</option>
            </select>
          </label>
          <label className="flex items-center gap-1.5 text-slate-700">
            <input
              type="checkbox"
              checked={enforceSemantics}
              onChange={(e) => setEnforceSemantics(e.target.checked)}
            />
            <span>Selectional Restrictions</span>
          </label>
          <label className="flex items-center gap-1.5 text-slate-700">
            <input
              type="checkbox"
              checked={trackDefiniteness}
              onChange={(e) => setTrackDefiniteness(e.target.checked)}
            />
            <span>Definiteness (a → the)</span>
          </label>
          <label className="flex items-center gap-1.5 text-slate-700">
            <input
              type="checkbox"
              checked={usePronouns}
              onChange={(e) => setUsePronouns(e.target.checked)}
            />
            <span>Anaphoric Pronouns</span>
          </label>
          <label className="flex items-center gap-1.5 text-slate-700">
            <input
              type="checkbox"
              checked={useConnectives}
              onChange={(e) => setUseConnectives(e.target.checked)}
            />
            <span>Connectives</span>
          </label>
        </div>

        {discourseResult && (
          <div className="space-y-2">
            <div className="p-3 bg-slate-50 border border-slate-200 rounded text-sm font-serif text-slate-900 leading-relaxed">
              {discourseResult.paragraph}
            </div>
            <div className="text-xs text-slate-600">
              Tracked entities:{' '}
              <span className="font-mono text-indigo-700">
                {discourseResult.trackedEntities.length > 0
                  ? discourseResult.trackedEntities.join(', ')
                  : '(none)'}
              </span>
            </div>
            <div className="border border-slate-200 rounded divide-y divide-slate-100 text-xs">
              {discourseResult.sentences.map((s) => (
                <div key={s.index} className="px-2.5 py-1.5 flex flex-wrap justify-between gap-2">
                  <span className="font-mono text-slate-900">
                    {s.index}. {s.written}
                  </span>
                  <span className="font-mono text-slate-500">/{s.surface}/</span>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

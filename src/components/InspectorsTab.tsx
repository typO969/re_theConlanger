import React, { useMemo, useState } from 'react';
import { FeatBundle, LanguageProjectState } from '../core/types';
import { buildEngineFromState, formatSyntaxTree, Morphology } from '../core/languageEngine';
import { RNG } from '../core/rng';

interface InspectorsTabProps {
  state: LanguageProjectState;
}

export const InspectorsTab: React.FC<InspectorsTabProps> = ({ state }) => {
  const lexemeIds = useMemo(
    () => [...state.Lexemes].sort((a, b) => a.Lemma.localeCompare(b.Lemma)).map((l) => l.Lemma),
    [state.Lexemes]
  );

  const [selectedLexeme, setSelectedLexeme] = useState<string>('person');
  const [refreshTick, setRefreshTick] = useState(0);

  const activeLexeme = lexemeIds.includes(selectedLexeme)
    ? selectedLexeme
    : (lexemeIds[0] ?? 'person');

  const { lexemeInspector, syntaxTreeInspector, phonologyTraceInspector } = useMemo(() => {
    void refreshTick;
    const engine = buildEngineFromState(state);
    const lex = engine.morphology.lexicon.resolve(activeLexeme);
    const ur = lex.rootPhones.UR ?? lex.lemma;

    const baseFeats: Array<{ name: string; value: string }> = lex.lexClass
      ? [{ name: 'Class', value: lex.lexClass }]
      : [];
    const pres = engine.morphology.realizeForPreview(lex.lemma, lex.pos, new FeatBundle(baseFeats));
    const past = engine.morphology.realizeForPreview(
      lex.lemma,
      lex.pos,
      new FeatBundle([...baseFeats, { name: 'Tense', value: 'Past' }])
    );
    const plural = engine.morphology.realizeForPreview(
      lex.lemma,
      lex.pos,
      new FeatBundle([...baseFeats, { name: 'Number', value: 'Pl' }])
    );
    const acc = engine.morphology.realizeForPreview(
      lex.lemma,
      lex.pos,
      new FeatBundle([...baseFeats, { name: 'Case', value: 'Acc' }])
    );
    const sg3 = engine.morphology.realizeForPreview(
      lex.lemma,
      lex.pos,
      new FeatBundle([
        ...baseFeats,
        { name: 'Person', value: '3' },
        { name: 'Number', value: 'Sing' },
        { name: 'Tense', value: 'Pres' },
        { name: 'Agreement', value: '3sg' },
      ])
    );
    const orthPres = engine.orthography.render(pres.replace(/\s+/g, ''));
    const irregSummary = lex.irregulars
      ? Object.entries(lex.irregulars)
          .map(([k, v]) => `${k}=${v}`)
          .join('; ')
      : '(none)';

    const lexText = [
      `Lemma: ${lex.lemma}`,
      `POS: ${lex.pos}`,
      `Class: ${lex.lexClass ?? '(none)'}`,
      `UR: ${ur}`,
      `Spelling: ${lex.spelling ?? '(rule-driven)'}`,
      `Irregulars: ${irregSummary}`,
      `SR(Pres): ${pres}`,
      `SR(3sg): ${sg3}`,
      `SR(Past): ${past}`,
      `SR(Plural): ${plural}`,
      `SR(Acc): ${acc}`,
      `Orth(Pres): ${orthPres}`,
    ].join('\n');

    const tree = engine.syntax.realize(
      { clauseType: 'decl', approxWords: 5, constraints: { Tense: 'Past' } },
      new RNG(3)
    );
    const treeText = formatSyntaxTree(tree.root, 0, true);

    const urPhones = Morphology.splitPhones(ur);
    const derived = engine.phonology.derive([
      {
        morpheme: { id: 'STEM', gloss: lex.lemma, underlyingPhones: urPhones },
        underlyingPhones: urPhones,
      },
    ]);
    const syls = derived.syllables
      .map((s) => `[${s.onset.join('')}|${s.nucleus.join('')}|${s.coda.join('')}]`)
      .join(' . ');
    const traceText = [
      ...derived.steps.map((s) => `${s.label}: ${s.value}`),
      `Syllabified: ${syls}`,
    ].join('\n');

    return {
      lexemeInspector: lexText,
      syntaxTreeInspector: treeText,
      phonologyTraceInspector: traceText,
    };
  }, [state, activeLexeme, refreshTick]);

  return (
    <div className="p-5 space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <label className="flex items-center gap-2 text-sm">
          <span className="font-medium text-slate-700">Lexeme:</span>
          <select
            value={activeLexeme}
            onChange={(e) => setSelectedLexeme(e.target.value)}
            className="w-56 px-2.5 py-1.5 text-sm bg-white border border-slate-300 rounded"
          >
            {lexemeIds.map((id) => (
              <option key={id} value={id}>
                {id}
              </option>
            ))}
          </select>
        </label>
        <button
          type="button"
          onClick={() => setRefreshTick((n) => n + 1)}
          className="px-3 py-1.5 text-sm bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded"
        >
          Refresh Inspectors
        </button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div>
          <div className="text-sm font-semibold text-slate-800 mb-1.5">Lexeme Inspector</div>
          <textarea
            readOnly
            value={lexemeInspector}
            rows={14}
            className="w-full p-2.5 text-sm font-mono bg-white border border-slate-300 rounded"
          />
        </div>

        <div>
          <div className="text-sm font-semibold text-slate-800 mb-1.5">Syntax Tree Viewer</div>
          <textarea
            readOnly
            value={syntaxTreeInspector}
            rows={14}
            className="w-full p-2.5 text-sm font-mono bg-white border border-slate-300 rounded"
          />
        </div>

        <div>
          <div className="text-sm font-semibold text-slate-800 mb-1.5">Phonology & Syllable Trace</div>
          <textarea
            readOnly
            value={phonologyTraceInspector}
            rows={14}
            className="w-full p-2.5 text-sm font-mono bg-white border border-slate-300 rounded"
          />
        </div>
      </div>
    </div>
  );
};

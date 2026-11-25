import React, { useMemo, useState } from 'react';
import {
  AdpositionOrder,
  AlignmentSystem,
  ClauseTemplate,
  LanguageProjectState,
  ModifierOrder,
  QuestionStrategy,
  SentenceMood,
  SyntaxConfig,
  WordOrder,
} from '../core/types';
import {
  buildEngineFromState,
  DEFAULT_SYNTAX_CONFIG,
  formatInterlinearGloss,
  formatSyntaxTree,
  normalizeSyntaxConfig,
} from '../core/languageEngine';
import { RNG } from '../core/rng';

interface SyntaxTabProps {
  state: LanguageProjectState;
  onUpdate: (updater: (prev: LanguageProjectState) => LanguageProjectState, label: string) => void;
}

const WORD_ORDERS: WordOrder[] = ['SVO', 'SOV', 'VSO', 'VOS', 'OVS', 'OSV'];
const CLAUSE_TEMPLATES: ClauseTemplate[] = ['Intransitive', 'Transitive', 'Copular'];
const ALIGNMENTS: AlignmentSystem[] = ['NomAcc', 'ErgAbs', 'Tripartite'];
const MODIFIER_ORDERS: ModifierOrder[] = ['BeforeNoun', 'AfterNoun'];
const ADPOSITION_TYPES: AdpositionOrder[] = ['Preposition', 'Postposition'];
const QUESTION_STRATEGIES: QuestionStrategy[] = ['Inversion', 'Particle'];
const MOOD_OPTIONS: SentenceMood[] = ['Decl', 'YNQuestion', 'Imp'];
const VOICE_OPTIONS = ['Auto', 'Active', 'Passive'] as const;
const TENSE_OPTIONS = ['Pres', 'Past', 'Fut'] as const;

export const SyntaxTab: React.FC<SyntaxTabProps> = ({ state, onUpdate }) => {
  const [previewSeed, setPreviewSeed] = useState(1);
  const [approxWords, setApproxWords] = useState(6);
  const [previewMood, setPreviewMood] = useState<SentenceMood>('Decl');
  const [previewVoice, setPreviewVoice] = useState<'Auto' | 'Active' | 'Passive'>('Auto');
  const [previewTense, setPreviewTense] = useState<'Pres' | 'Past' | 'Fut'>('Past');

  const syntaxCfg = useMemo(() => normalizeSyntaxConfig(state.SyntaxConfig), [state.SyntaxConfig]);

  const updateSyntaxConfig = (patch: Partial<SyntaxConfig>, label: string) => {
    onUpdate(
      (prev) => ({
        ...prev,
        SyntaxConfig: {
          ...normalizeSyntaxConfig(prev.SyntaxConfig),
          ...patch,
        },
      }),
      label
    );
  };

  const { writtenPreview, surfacePreview, linearPreview, igtPreview, treePreview } = useMemo(() => {
    const engine = buildEngineFromState(state);
    const constraints: Record<string, string> = {
      Tense: previewTense,
      Mood: previewMood,
    };
    if (previewVoice !== 'Auto') {
      constraints.Voice = previewVoice;
    }
    const generated = engine.generateSentence(new RNG(previewSeed), {
      clauseType: state.ClauseTemplate,
      approxWords,
      constraints,
    });
    const linear = generated.tree
      .linearize()
      .map((n) => `${n.lemmaId}/${n.pos}`)
      .join(' ');
    const igt = formatInterlinearGloss(engine, generated.tree);
    const treeText = formatSyntaxTree(generated.tree.root, 0, false);
    return {
      writtenPreview: generated.written,
      surfacePreview: generated.surface,
      linearPreview: linear,
      igtPreview: igt,
      treePreview: treeText,
    };
  }, [state, previewSeed, approxWords, previewMood, previewVoice, previewTense]);

  return (
    <div className="p-5 space-y-5">
      {/* Clause & Typological Grammar Parameters */}
      <div className="border border-slate-300 bg-white rounded p-4 space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h3 className="text-sm font-semibold text-slate-900">Clause Architecture & Typology</h3>
          <span className="text-xs text-slate-500">
            Active Generator: {state.Pipeline?.syntaxGeneratorId ?? 'configurable-syntax'}
          </span>
        </div>

        <div className="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-7 gap-3 text-sm">
          <label className="flex flex-col gap-1">
            <span className="text-xs font-medium text-slate-600">Word Order</span>
            <select
              value={state.WordOrder}
              onChange={(e) =>
                onUpdate(
                  (prev) => ({ ...prev, WordOrder: e.target.value as WordOrder }),
                  'Word order updated'
                )
              }
              className="px-2 py-1.5 text-sm bg-white border border-slate-300 rounded"
            >
              {WORD_ORDERS.map((wo) => (
                <option key={wo} value={wo}>
                  {wo}
                </option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1">
            <span className="text-xs font-medium text-slate-600">Clause</span>
            <select
              value={state.ClauseTemplate}
              onChange={(e) =>
                onUpdate(
                  (prev) => ({ ...prev, ClauseTemplate: e.target.value as ClauseTemplate }),
                  'Clause template updated'
                )
              }
              className="px-2 py-1.5 text-sm bg-white border border-slate-300 rounded"
            >
              {CLAUSE_TEMPLATES.map((ct) => (
                <option key={ct} value={ct}>
                  {ct}
                </option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1">
            <span className="text-xs font-medium text-slate-600">Alignment</span>
            <select
              value={syntaxCfg.alignment}
              onChange={(e) =>
                updateSyntaxConfig({ alignment: e.target.value as AlignmentSystem }, 'Alignment updated')
              }
              className="px-2 py-1.5 text-sm bg-white border border-slate-300 rounded"
            >
              {ALIGNMENTS.map((al) => (
                <option key={al} value={al}>
                  {al}
                </option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1">
            <span className="text-xs font-medium text-slate-600">Adjective Order</span>
            <select
              value={syntaxCfg.adjectiveOrder}
              onChange={(e) =>
                updateSyntaxConfig(
                  { adjectiveOrder: e.target.value as ModifierOrder },
                  'Adjective order updated'
                )
              }
              className="px-2 py-1.5 text-sm bg-white border border-slate-300 rounded"
            >
              {MODIFIER_ORDERS.map((m) => (
                <option key={m} value={m}>
                  {m}
                </option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1">
            <span className="text-xs font-medium text-slate-600">Determiner Order</span>
            <select
              value={syntaxCfg.determinerOrder}
              onChange={(e) =>
                updateSyntaxConfig(
                  { determinerOrder: e.target.value as ModifierOrder },
                  'Determiner order updated'
                )
              }
              className="px-2 py-1.5 text-sm bg-white border border-slate-300 rounded"
            >
              {MODIFIER_ORDERS.map((m) => (
                <option key={m} value={m}>
                  {m}
                </option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1">
            <span className="text-xs font-medium text-slate-600">Adposition</span>
            <select
              value={syntaxCfg.adpositionType}
              onChange={(e) =>
                updateSyntaxConfig(
                  { adpositionType: e.target.value as AdpositionOrder },
                  'Adposition order updated'
                )
              }
              className="px-2 py-1.5 text-sm bg-white border border-slate-300 rounded"
            >
              {ADPOSITION_TYPES.map((adp) => (
                <option key={adp} value={adp}>
                  {adp}
                </option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1">
            <span className="text-xs font-medium text-slate-600">Questions</span>
            <select
              value={syntaxCfg.questionStrategy}
              onChange={(e) =>
                updateSyntaxConfig(
                  { questionStrategy: e.target.value as QuestionStrategy },
                  'Question strategy updated'
                )
              }
              className="px-2 py-1.5 text-sm bg-white border border-slate-300 rounded"
            >
              {QUESTION_STRATEGIES.map((qs) => (
                <option key={qs} value={qs}>
                  {qs}
                </option>
              ))}
            </select>
          </label>
        </div>

        <div className="flex flex-wrap items-center gap-x-5 gap-y-2 pt-1 border-t border-slate-200 text-sm">
          <label className="flex items-center gap-1.5 cursor-pointer">
            <input
              type="checkbox"
              checked={syntaxCfg.proDrop}
              onChange={(e) => updateSyntaxConfig({ proDrop: e.target.checked }, 'Pro-drop updated')}
            />
            <span className="text-slate-700">Pro-Drop</span>
          </label>

          <label className="flex items-center gap-1.5 cursor-pointer">
            <input
              type="checkbox"
              checked={syntaxCfg.polypersonalAgreement}
              onChange={(e) =>
                updateSyntaxConfig(
                  { polypersonalAgreement: e.target.checked },
                  'Polypersonal agreement updated'
                )
              }
            />
            <span className="text-slate-700">Polypersonal Agreement</span>
          </label>

          <label className="flex items-center gap-1.5 cursor-pointer">
            <input
              type="checkbox"
              checked={syntaxCfg.allowEmbeddedClauses}
              onChange={(e) =>
                updateSyntaxConfig(
                  { allowEmbeddedClauses: e.target.checked },
                  'Embedded clauses updated'
                )
              }
            />
            <span className="text-slate-700">Complement Clauses (COMP)</span>
          </label>

          <label className="flex items-center gap-1.5 cursor-pointer">
            <input
              type="checkbox"
              checked={syntaxCfg.allowRelativeClauses}
              onChange={(e) =>
                updateSyntaxConfig(
                  { allowRelativeClauses: e.target.checked },
                  'Relative clauses updated'
                )
              }
            />
            <span className="text-slate-700">Relative Clauses (REL)</span>
          </label>

          <label className="flex items-center gap-1.5 cursor-pointer">
            <input
              type="checkbox"
              checked={syntaxCfg.allowPassive}
              onChange={(e) =>
                updateSyntaxConfig({ allowPassive: e.target.checked }, 'Passive voice updated')
              }
            />
            <span className="text-slate-700">Passive Voice</span>
          </label>

          <label className="flex items-center gap-1.5">
            <span className="text-xs font-medium text-slate-600">Max Depth:</span>
            <input
              type="number"
              min={0}
              max={3}
              value={syntaxCfg.maxClauseDepth}
              onChange={(e) =>
                updateSyntaxConfig(
                  { maxClauseDepth: Math.max(0, Math.min(3, Number(e.target.value) || 0)) },
                  'Max clause depth updated'
                )
              }
              className="w-14 px-2 py-0.5 text-sm font-mono tabular-nums bg-white border border-slate-300 rounded"
            />
          </label>
        </div>

        <label className="block space-y-1">
          <span className="text-xs font-medium text-slate-600">
            Custom Phrase-Structure Overrides (optional, one per line e.g. CLAUSE -&gt; S O V CP or NP -&gt; DET N ADJ)
          </span>
          <textarea
            rows={2}
            value={(syntaxCfg.customRules ?? DEFAULT_SYNTAX_CONFIG.customRules).join('\n')}
            onChange={(e) =>
              updateSyntaxConfig(
                {
                  customRules: e.target.value
                    .split(/\r?\n/)
                    .map((line) => line.trimEnd()),
                },
                'Custom CFG rules updated'
              )
            }
            placeholder="NP -> DET N ADJ&#10;PP -> NP POSTP"
            className="w-full px-2.5 py-1.5 text-xs font-mono bg-slate-50 border border-slate-300 rounded"
          />
        </label>
      </div>

      {/* Live Clause Preview Controls */}
      <div className="flex flex-wrap items-center gap-3 text-sm">
        <label className="flex items-center gap-1.5">
          <span className="font-medium text-slate-700">Words</span>
          <input
            type="number"
            min={1}
            max={10}
            value={approxWords}
            onChange={(e) => setApproxWords(Math.max(1, Math.min(10, Number(e.target.value) || 4)))}
            className="w-14 px-2 py-1 font-mono tabular-nums bg-white border border-slate-300 rounded"
          />
        </label>

        <label className="flex items-center gap-1.5">
          <span className="font-medium text-slate-700">Mood</span>
          <select
            value={previewMood}
            onChange={(e) => setPreviewMood(e.target.value as SentenceMood)}
            className="px-2 py-1 bg-white border border-slate-300 rounded"
          >
            {MOOD_OPTIONS.map((m) => (
              <option key={m} value={m}>
                {m}
              </option>
            ))}
          </select>
        </label>

        <label className="flex items-center gap-1.5">
          <span className="font-medium text-slate-700">Voice</span>
          <select
            value={previewVoice}
            onChange={(e) => setPreviewVoice(e.target.value as 'Auto' | 'Active' | 'Passive')}
            className="px-2 py-1 bg-white border border-slate-300 rounded"
          >
            {VOICE_OPTIONS.map((v) => (
              <option key={v} value={v}>
                {v}
              </option>
            ))}
          </select>
        </label>

        <label className="flex items-center gap-1.5">
          <span className="font-medium text-slate-700">Tense</span>
          <select
            value={previewTense}
            onChange={(e) => setPreviewTense(e.target.value as 'Pres' | 'Past' | 'Fut')}
            className="px-2 py-1 bg-white border border-slate-300 rounded"
          >
            {TENSE_OPTIONS.map((t) => (
              <option key={t} value={t}>
                {t}
              </option>
            ))}
          </select>
        </label>

        <button
          type="button"
          onClick={() => setPreviewSeed((s) => s + 1)}
          className="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-white rounded whitespace-nowrap shrink-0 tabular-nums"
        >
          Sample New Seed (#{previewSeed})
        </button>
      </div>

      {/* Rendered Output & Leipzig Interlinear Gloss */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div className="space-y-2">
          <div className="text-sm font-semibold text-slate-800">Orthographic &amp; Phonological Output</div>
          <div className="border border-slate-300 bg-white rounded p-3 space-y-2">
            <div>
              <div className="text-xs text-slate-500">Written</div>
              <div className="text-base font-semibold text-slate-900">{writtenPreview || '(empty)'}</div>
            </div>
            <div>
              <div className="text-xs text-slate-500">Surface Phonology</div>
              <div className="text-sm font-mono text-slate-700">{surfacePreview || '(empty)'}</div>
            </div>
            <div>
              <div className="text-xs text-slate-500">Linearized Leaves</div>
              <div className="text-xs font-mono text-slate-600">{linearPreview}</div>
            </div>
          </div>
        </div>

        <div className="space-y-2">
          <div className="text-sm font-semibold text-slate-800">Interlinear Glossed Text (Leipzig IGT)</div>
          <textarea
            readOnly
            value={igtPreview}
            rows={5}
            className="w-full p-2.5 text-xs font-mono bg-white border border-slate-300 rounded whitespace-pre overflow-x-auto"
          />
        </div>
      </div>

      <div className="space-y-2">
        <div className="text-sm font-semibold text-slate-800">Syntax Tree Hierarchy</div>
        <textarea
          readOnly
          value={treePreview}
          rows={10}
          className="w-full p-2.5 text-sm font-mono bg-white border border-slate-300 rounded"
        />
      </div>
    </div>
  );
};


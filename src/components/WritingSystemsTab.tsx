import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  FlowDirection,
  GlyphDefinition,
  LanguageProjectState,
  WritingSystemConfig,
} from '../core/types';
import { buildEngineFromState, TOP_10_LANGUAGE_PRESETS } from '../core/languageEngine';
import { RNG } from '../core/rng';
import {
  createPresetWritingSystem,
  exportShapedSvg,
  getWritingSystemSampleInput,
  runShapingGraph,
  tokenizeRawInput,
} from '../core/writingSystem';
import { downloadTextFile } from '../core/download';

interface WritingSystemsTabProps {
  state: LanguageProjectState;
  onUpdate: (updater: (prev: LanguageProjectState) => LanguageProjectState, label: string) => void;
}

const FLOW_OPTIONS: Array<{ value: FlowDirection; label: string }> = [
  { value: 'RTL', label: 'Linear Right-to-Left (RTL)' },
  { value: 'LTR', label: 'Linear Left-to-Right (LTR)' },
  { value: 'TTB_LTR', label: 'Vertical Top-to-Bottom, Cols LTR (Mongolian)' },
  { value: 'TTB_RTL', label: 'Vertical Top-to-Bottom, Cols RTL' },
  { value: 'Boustrophedon', label: 'Alternating (Boustrophedon)' },
];

function inferPresetIdFromState(state: LanguageProjectState): string {
  const matched = TOP_10_LANGUAGE_PRESETS.find((p) => p.name === state.Name);
  if (matched) return matched.id;
  const wsName = state.WritingSystem.name.toLowerCase();
  if (wsName.includes('english')) return 'english';
  if (wsName.includes('español') || wsName.includes('spanish')) return 'spanish';
  if (wsName.includes('deutsch') || wsName.includes('german')) return 'german';
  if (wsName.includes('普通话') || wsName.includes('sinographic') || wsName.includes('mandarin')) return 'mandarin';
  if (wsName.includes('العربية') || wsName.includes('arabic')) return 'arabic';
  if (wsName.includes('日本語') || wsName.includes('kana') || wsName.includes('japanese')) return 'japanese';
  if (wsName.includes('türkçe') || wsName.includes('turkish')) return 'turkish';
  if (wsName.includes('kiswahili') || wsName.includes('swahili')) return 'swahili';
  if (wsName.includes('русский') || wsName.includes('cyrillic') || wsName.includes('russian')) return 'russian';
  if (wsName.includes('indonesia') || wsName.includes('nusantara')) return 'indonesian';
  return 'default';
}

export const WritingSystemsTab: React.FC<WritingSystemsTabProps> = ({ state, onUpdate }) => {
  const ws = state.WritingSystem;

  const [inputMode, setInputMode] = useState<'custom' | 'engine'>('custom');
  const initialSample = getWritingSystemSampleInput(inferPresetIdFromState(state));
  const [rawInput, setRawInput] = useState(initialSample);
  const lastAutoSampleRef = useRef<string>(initialSample);
  const [engineSeed, setEngineSeed] = useState(1);
  const [showDebugAnchors, setShowDebugAnchors] = useState(true);
  const [previewVariant, setPreviewVariant] = useState<'default' | 'initial' | 'medial' | 'final'>('default');

  useEffect(() => {
    const nextSample = getWritingSystemSampleInput(inferPresetIdFromState(state));
    setRawInput((prev) => {
      if (prev === lastAutoSampleRef.current || prev === 'shika taka person' || !prev.trim()) {
        lastAutoSampleRef.current = nextSample;
        return nextSample;
      }
      lastAutoSampleRef.current = nextSample;
      return prev;
    });
  }, [state.Name, ws.name]);

  const glyphKeys = useMemo(() => Object.keys(ws.glyphs).sort(), [ws.glyphs]);
  const [selectedGlyphId, setSelectedGlyphId] = useState<string>(glyphKeys[0] ?? 'a');
  const activeGlyphId = ws.glyphs[selectedGlyphId] ? selectedGlyphId : (glyphKeys[0] ?? 'a');
  const activeGlyph: GlyphDefinition | undefined = ws.glyphs[activeGlyphId];

  const [newGlyphId, setNewGlyphId] = useState('');
  const [newRuleName, setNewRuleName] = useState('Inter-word Dot');
  const [newRuleTarget, setNewRuleTarget] = useState('*');
  const [newRuleCtx, setNewRuleCtx] = useState<'any' | 'initial' | 'medial' | 'final'>('initial');
  const [newRulePath, setNewRulePath] = useState('M 48 8 A 3 3 0 1 0 54 8 A 3 3 0 1 0 48 8');

  const layer1Tokens = useMemo(() => {
    if (inputMode === 'engine') {
      const engine = buildEngineFromState(state);
      const generated = engine.generateSentence(new RNG(engineSeed), {
        clauseType: 'decl',
        approxWords: 4,
        constraints: { Tense: 'Past' },
      });
      return {
        sourceLabel: generated.written,
        tokens: generated.tokens,
      };
    }
    return {
      sourceLabel: rawInput,
      tokens: tokenizeRawInput(rawInput, Object.keys(ws.glyphs)),
    };
  }, [inputMode, rawInput, engineSeed, state, ws.glyphs]);

  const shaped = useMemo(
    () => runShapingGraph(layer1Tokens.tokens, ws),
    [layer1Tokens.tokens, ws]
  );

  const updateWs = (updater: (prev: WritingSystemConfig) => WritingSystemConfig, label: string) => {
    onUpdate(
      (prev) => ({ ...prev, WritingSystem: updater(prev.WritingSystem) }),
      label
    );
  };

  const applyPreset = (preset: 'rtl-western' | 'vertical-mongolian' | 'boustrophedon') => {
    const milestoneSample = 'shika taka person';
    if (rawInput === lastAutoSampleRef.current || !rawInput.trim()) {
      lastAutoSampleRef.current = milestoneSample;
      setRawInput(milestoneSample);
    }
    onUpdate(
      (prev) => ({ ...prev, WritingSystem: createPresetWritingSystem(preset) }),
      `Writing System preset (${preset})`
    );
  };

  const updateGlyph = (patch: Partial<GlyphDefinition>) => {
    if (!activeGlyph) return;
    updateWs(
      (prev) => ({
        ...prev,
        glyphs: {
          ...prev.glyphs,
          [activeGlyph.id]: { ...activeGlyph, ...patch },
        },
      }),
      `Edited glyph "${activeGlyph.id}"`
    );
  };

  const addCustomGlyph = () => {
    const id = newGlyphId.trim().toLowerCase();
    if (!id || ws.glyphs[id]) return;
    const isVert = ws.flowDirection === 'TTB_LTR' || ws.flowDirection === 'TTB_RTL';
    const created: GlyphDefinition = {
      id,
      label: id,
      svgPath: isVert
        ? 'M 50 10 L 50 38 L 72 52 L 50 66 L 50 90'
        : 'M 15 55 L 40 30 L 65 70 L 85 55',
      enteringAnchor: isVert ? { x: 50, y: 10 } : { x: 15, y: 55 },
      exitingAnchor: isVert ? { x: 50, y: 90 } : { x: 85, y: 55 },
      advanceWidth: 74,
      advanceHeight: 80,
    };
    updateWs(
      (prev) => ({ ...prev, glyphs: { ...prev.glyphs, [id]: created } }),
      `Added glyph "${id}"`
    );
    setSelectedGlyphId(id);
    setNewGlyphId('');
  };

  const addFeaturicRule = () => {
    if (!newRuleName.trim() || !newRulePath.trim()) return;
    updateWs(
      (prev) => ({
        ...prev,
        featuricRules: [
          ...prev.featuricRules,
          {
            id: `rule-${Date.now()}`,
            name: newRuleName.trim(),
            targetGlyphId: newRuleTarget.trim() || '*',
            contextPosition: newRuleCtx,
            appendSvgPath: newRulePath.trim(),
            offsetX: 0,
            offsetY: 0,
            enabled: true,
          },
        ],
      }),
      'Added featuric assembly rule'
    );
  };

  const toggleFeaturicRule = (id: string) => {
    updateWs(
      (prev) => ({
        ...prev,
        featuricRules: prev.featuricRules.map((r) =>
          r.id === id ? { ...r, enabled: !r.enabled } : r
        ),
      }),
      'Toggled featuric rule'
    );
  };

  const removeFeaturicRule = (id: string) => {
    updateWs(
      (prev) => ({
        ...prev,
        featuricRules: prev.featuricRules.filter((r) => r.id !== id),
      }),
      'Removed featuric rule'
    );
  };

  const scale = ws.glyphScale || 0.72;

  return (
    <div className="p-5 space-y-5">
      {/* Header + Presets */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h3 className="text-base font-bold text-slate-900 flex flex-wrap items-center gap-2">
            <span>Agnostic Text Shaping Engine</span>
            <span className="px-2 py-0.5 text-xs font-medium bg-indigo-50 text-indigo-800 border border-indigo-200 rounded">
              {ws.name}
            </span>
          </h3>
          <p className="text-xs text-slate-600">
            Bypasses OS font/direction sandboxes: Input Matrix → Node-Based Shaping Graph → Vector Canvas.
          </p>
        </div>

        <div className="flex flex-wrap items-center gap-1.5">
          <span className="text-xs font-medium text-slate-600">Milestone Presets:</span>
          <button
            type="button"
            onClick={() => applyPreset('rtl-western')}
            className="px-2.5 py-1 text-xs bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded"
          >
            M1: RTL Western Cursive
          </button>
          <button
            type="button"
            onClick={() => applyPreset('vertical-mongolian')}
            className="px-2.5 py-1 text-xs bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded"
          >
            M2: Vertical Spine (Mongolian)
          </button>
          <button
            type="button"
            onClick={() => applyPreset('boustrophedon')}
            className="px-2.5 py-1 text-xs bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded"
          >
            Boustrophedon
          </button>
        </div>
      </div>

      {/* Layer 1: Input Matrix */}
      <div className="border border-slate-300 bg-white rounded p-3 space-y-2">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="text-xs font-bold uppercase tracking-wide text-slate-700">
            Layer 1 — Input Matrix (Direction-Agnostic Grapheme IDs)
          </div>
          <div className="flex items-center gap-3 text-xs">
            <label className="flex items-center gap-1.5">
              <input
                type="radio"
                checked={inputMode === 'custom'}
                onChange={() => setInputMode('custom')}
              />
              Sandbox Keystrokes
            </label>
            <label className="flex items-center gap-1.5">
              <input
                type="radio"
                checked={inputMode === 'engine'}
                onChange={() => setInputMode('engine')}
              />
              Live LangEngine Output
            </label>
          </div>
        </div>

        {inputMode === 'custom' ? (
          <input
            type="text"
            value={rawInput}
            onChange={(e) => setRawInput(e.target.value)}
            placeholder="Type characters/digraphs (e.g. shika taka person)"
            className="w-full px-2.5 py-1.5 text-sm font-mono bg-slate-50 border border-slate-300 rounded"
          />
        ) : (
          <div className="flex flex-wrap items-center gap-2">
            <input
              type="text"
              readOnly
              value={layer1Tokens.sourceLabel}
              className="flex-1 min-w-56 px-2.5 py-1.5 text-sm font-mono bg-slate-50 border border-slate-300 rounded"
            />
            <button
              type="button"
              onClick={() => setEngineSeed((s) => s + 1)}
              className="px-3 py-1.5 text-xs bg-slate-800 text-white rounded hover:bg-slate-700"
            >
              Next Sentence (#{engineSeed})
            </button>
          </div>
        )}

        <div className="flex flex-wrap gap-1.5 pt-1">
          {layer1Tokens.tokens.map((t, i) => (
            <span
              key={i}
              className="px-1.5 py-0.5 text-xs font-mono bg-slate-100 border border-slate-300 rounded"
            >
              {t.id}
              <span className="text-slate-400">:{t.positionInWord[0]}</span>
            </span>
          ))}
        </div>
      </div>

      {/* Layer 2 & Layer 3 */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-4">
        {/* Layer 2: Node-Based Shaping Graph */}
        <div className="lg:col-span-5 space-y-4">
          <div className="border border-slate-300 bg-white rounded p-3 space-y-2.5">
            <div className="text-xs font-bold uppercase tracking-wide text-slate-700">
              Layer 2 — Flow Vector & Anchor Nodes
            </div>

            <label className="block text-xs space-y-1">
              <span className="font-medium text-slate-700">Flow Vector Node</span>
              <select
                value={ws.flowDirection}
                onChange={(e) =>
                  updateWs(
                    (prev) => ({ ...prev, flowDirection: e.target.value as FlowDirection }),
                    'Changed Flow Vector'
                  )
                }
                className="w-full px-2 py-1.5 text-sm bg-white border border-slate-300 rounded"
              >
                {FLOW_OPTIONS.map((opt) => (
                  <option key={opt.value} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>
            </label>

            <div className="flex flex-wrap gap-4 text-xs pt-1">
              <label className="flex items-center gap-1.5">
                <input
                  type="checkbox"
                  checked={ws.cursiveBonding}
                  onChange={(e) =>
                    updateWs(
                      (prev) => ({ ...prev, cursiveBonding: e.target.checked }),
                      'Toggled Cursive Anchor Bonding'
                    )
                  }
                />
                Bond #entering / #exiting Anchors
              </label>
              <label className="flex items-center gap-1.5">
                <input
                  type="checkbox"
                  checked={ws.contextualVariants}
                  onChange={(e) =>
                    updateWs(
                      (prev) => ({ ...prev, contextualVariants: e.target.checked }),
                      'Toggled Contextual Variants'
                    )
                  }
                />
                Contextual Variants
              </label>
            </div>

            <div className="grid grid-cols-3 gap-2 text-xs">
              <label className="space-y-1">
                <span className="text-slate-600">Scale</span>
                <input
                  type="number"
                  step={0.05}
                  min={0.3}
                  max={1.5}
                  value={ws.glyphScale}
                  onChange={(e) =>
                    updateWs(
                      (prev) => ({ ...prev, glyphScale: Math.max(0.3, Number(e.target.value) || 0.72) }),
                      'Updated glyph scale'
                    )
                  }
                  className="w-full px-2 py-1 border border-slate-300 rounded"
                />
              </label>
              <label className="space-y-1">
                <span className="text-slate-600">Spacing</span>
                <input
                  type="number"
                  min={-20}
                  max={40}
                  value={ws.letterSpacing}
                  onChange={(e) =>
                    updateWs(
                      (prev) => ({ ...prev, letterSpacing: Number(e.target.value) || 0 }),
                      'Updated glyph spacing'
                    )
                  }
                  className="w-full px-2 py-1 border border-slate-300 rounded"
                />
              </label>
              <label className="space-y-1">
                <span className="text-slate-600">Wrap px</span>
                <input
                  type="number"
                  min={200}
                  max={1000}
                  step={20}
                  value={ws.wrapLimit}
                  onChange={(e) =>
                    updateWs(
                      (prev) => ({ ...prev, wrapLimit: Math.max(200, Number(e.target.value) || 480) }),
                      'Updated wrap limit'
                    )
                  }
                  className="w-full px-2 py-1 border border-slate-300 rounded"
                />
              </label>
            </div>
          </div>

          {/* Glyph Geometry & Anchor Editor */}
          <div className="border border-slate-300 bg-white rounded p-3 space-y-2.5">
            <div className="flex items-center justify-between gap-2">
              <div className="text-xs font-bold uppercase tracking-wide text-slate-700">
                Glyph SVG Geometry & Anchors
              </div>
              <div className="flex items-center gap-1">
                <input
                  type="text"
                  value={newGlyphId}
                  onChange={(e) => setNewGlyphId(e.target.value)}
                  placeholder="New ID"
                  className="w-16 px-1.5 py-0.5 text-xs border border-slate-300 rounded font-mono"
                />
                <button
                  type="button"
                  onClick={addCustomGlyph}
                  disabled={!newGlyphId.trim()}
                  className="px-2 py-0.5 text-xs bg-slate-800 text-white rounded disabled:opacity-40"
                >
                  +Glyph
                </button>
              </div>
            </div>

            <div className="flex items-center gap-2 text-xs">
              <span className="font-medium text-slate-700">Select Glyph:</span>
              <select
                value={activeGlyphId}
                onChange={(e) => setSelectedGlyphId(e.target.value)}
                className="px-2 py-1 bg-white border border-slate-300 rounded font-mono"
              >
                {glyphKeys.map((k) => (
                  <option key={k} value={k}>
                    {k} ({ws.glyphs[k].label})
                  </option>
                ))}
              </select>
            </div>

            {activeGlyph && (
              <div className="space-y-2">
                <div className="flex items-start gap-3">
                  <div className="shrink-0 space-y-1">
                    <svg
                      viewBox="0 0 100 100"
                      className="w-20 h-20 border border-slate-300 bg-slate-50 rounded"
                    >
                      <path
                        d={
                          previewVariant === 'initial'
                            ? activeGlyph.initialPath || activeGlyph.svgPath
                            : previewVariant === 'medial'
                              ? activeGlyph.medialPath || activeGlyph.svgPath
                              : previewVariant === 'final'
                                ? activeGlyph.finalPath || activeGlyph.svgPath
                                : activeGlyph.svgPath
                        }
                        fill="none"
                        stroke="#0f172a"
                        strokeWidth="5"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                      />
                      <circle
                        cx={activeGlyph.enteringAnchor.x}
                        cy={activeGlyph.enteringAnchor.y}
                        r="5"
                        fill="#16a34a"
                      />
                      <circle
                        cx={activeGlyph.exitingAnchor.x}
                        cy={activeGlyph.exitingAnchor.y}
                        r="5"
                        fill="#2563eb"
                      />
                    </svg>
                    <div className="grid grid-cols-2 gap-0.5 text-[10px]">
                      {(['default', 'initial', 'medial', 'final'] as const).map((v) => (
                        <button
                          key={v}
                          type="button"
                          onClick={() => setPreviewVariant(v)}
                          className={`px-1 py-0.5 border rounded capitalize ${
                            previewVariant === v
                              ? 'bg-slate-800 text-white border-slate-800'
                              : 'bg-slate-100 text-slate-700 border-slate-300'
                          }`}
                        >
                          {v}
                        </button>
                      ))}
                    </div>
                  </div>

                  <div className="flex-1 space-y-1.5 text-xs">
                    <label className="block space-y-0.5">
                      <span className="text-slate-600">SVG Path (100×100 box)</span>
                      <textarea
                        rows={2}
                        value={activeGlyph.svgPath}
                        onChange={(e) => updateGlyph({ svgPath: e.target.value })}
                        className="w-full px-1.5 py-1 border border-slate-300 rounded font-mono text-[11px]"
                      />
                    </label>
                    <div className="grid grid-cols-3 gap-1">
                      <label>
                        <span className="text-slate-600">Initial Path</span>
                        <input
                          type="text"
                          value={activeGlyph.initialPath ?? ''}
                          placeholder="(default)"
                          onChange={(e) =>
                            updateGlyph({ initialPath: e.target.value.trim() || undefined })
                          }
                          className="w-full px-1 py-0.5 border border-slate-300 rounded font-mono text-[11px]"
                        />
                      </label>
                      <label>
                        <span className="text-slate-600">Medial Path</span>
                        <input
                          type="text"
                          value={activeGlyph.medialPath ?? ''}
                          placeholder="(default)"
                          onChange={(e) =>
                            updateGlyph({ medialPath: e.target.value.trim() || undefined })
                          }
                          className="w-full px-1 py-0.5 border border-slate-300 rounded font-mono text-[11px]"
                        />
                      </label>
                      <label>
                        <span className="text-slate-600">Final Path</span>
                        <input
                          type="text"
                          value={activeGlyph.finalPath ?? ''}
                          placeholder="(default)"
                          onChange={(e) =>
                            updateGlyph({ finalPath: e.target.value.trim() || undefined })
                          }
                          className="w-full px-1 py-0.5 border border-slate-300 rounded font-mono text-[11px]"
                        />
                      </label>
                    </div>
                    <div className="grid grid-cols-3 gap-1">
                      <label>
                        <span className="text-emerald-700">#in X</span>
                        <input
                          type="number"
                          value={activeGlyph.enteringAnchor.x}
                          onChange={(e) =>
                            updateGlyph({
                              enteringAnchor: {
                                ...activeGlyph.enteringAnchor,
                                x: Number(e.target.value) || 0,
                              },
                            })
                          }
                          className="w-full px-1 py-0.5 border border-slate-300 rounded"
                        />
                      </label>
                      <label>
                        <span className="text-emerald-700">#in Y</span>
                        <input
                          type="number"
                          value={activeGlyph.enteringAnchor.y}
                          onChange={(e) =>
                            updateGlyph({
                              enteringAnchor: {
                                ...activeGlyph.enteringAnchor,
                                y: Number(e.target.value) || 0,
                              },
                            })
                          }
                          className="w-full px-1 py-0.5 border border-slate-300 rounded"
                        />
                      </label>
                      <label>
                        <span className="text-slate-700">Adv Width</span>
                        <input
                          type="number"
                          value={activeGlyph.advanceWidth}
                          onChange={(e) =>
                            updateGlyph({
                              advanceWidth: Math.max(10, Number(e.target.value) || 72),
                            })
                          }
                          className="w-full px-1 py-0.5 border border-slate-300 rounded"
                        />
                      </label>
                      <label>
                        <span className="text-blue-700">#out X</span>
                        <input
                          type="number"
                          value={activeGlyph.exitingAnchor.x}
                          onChange={(e) =>
                            updateGlyph({
                              exitingAnchor: {
                                ...activeGlyph.exitingAnchor,
                                x: Number(e.target.value) || 0,
                              },
                            })
                          }
                          className="w-full px-1 py-0.5 border border-slate-300 rounded"
                        />
                      </label>
                      <label>
                        <span className="text-blue-700">#out Y</span>
                        <input
                          type="number"
                          value={activeGlyph.exitingAnchor.y}
                          onChange={(e) =>
                            updateGlyph({
                              exitingAnchor: {
                                ...activeGlyph.exitingAnchor,
                                y: Number(e.target.value) || 0,
                              },
                            })
                          }
                          className="w-full px-1 py-0.5 border border-slate-300 rounded"
                        />
                      </label>
                      <label>
                        <span className="text-slate-700">Adv Height</span>
                        <input
                          type="number"
                          value={activeGlyph.advanceHeight}
                          onChange={(e) =>
                            updateGlyph({
                              advanceHeight: Math.max(10, Number(e.target.value) || 75),
                            })
                          }
                          className="w-full px-1 py-0.5 border border-slate-300 rounded"
                        />
                      </label>
                    </div>
                  </div>
                </div>
              </div>
            )}
          </div>

          {/* Featuric Assembly Rules */}
          <div className="border border-slate-300 bg-white rounded p-3 space-y-2">
            <div className="text-xs font-bold uppercase tracking-wide text-slate-700">
              Featuric Assembly Rules (Loops / Ticks / Diacritics)
            </div>
            <div className="space-y-1 max-h-28 overflow-y-auto">
              {ws.featuricRules.map((rule) => (
                <div key={rule.id} className="flex items-center justify-between text-xs gap-2 border-b border-slate-100 pb-1">
                  <label className="flex items-center gap-1.5 min-w-0">
                    <input
                      type="checkbox"
                      checked={rule.enabled}
                      onChange={() => toggleFeaturicRule(rule.id)}
                    />
                    <span className="truncate font-medium">{rule.name}</span>
                    <span className="font-mono text-slate-500">
                      ({rule.targetGlyphId}@{rule.contextPosition})
                    </span>
                  </label>
                  <button
                    type="button"
                    onClick={() => removeFeaturicRule(rule.id)}
                    className="px-1.5 py-0.5 text-[11px] bg-slate-100 hover:bg-rose-50 hover:text-rose-700 border border-slate-300 rounded"
                  >
                    Remove
                  </button>
                </div>
              ))}
            </div>
            <div className="grid grid-cols-3 gap-1 text-xs">
              <input
                type="text"
                value={newRuleName}
                onChange={(e) => setNewRuleName(e.target.value)}
                placeholder="Rule name"
                className="px-1.5 py-1 border border-slate-300 rounded"
              />
              <input
                type="text"
                value={newRuleTarget}
                onChange={(e) => setNewRuleTarget(e.target.value)}
                placeholder="Glyph (* or a)"
                className="px-1.5 py-1 border border-slate-300 rounded font-mono"
              />
              <select
                value={newRuleCtx}
                onChange={(e) =>
                  setNewRuleCtx(e.target.value as 'any' | 'initial' | 'medial' | 'final')
                }
                className="px-1.5 py-1 border border-slate-300 rounded"
              >
                <option value="any">any</option>
                <option value="initial">initial</option>
                <option value="medial">medial</option>
                <option value="final">final</option>
              </select>
            </div>
            <div className="flex gap-1.5 text-xs">
              <input
                type="text"
                value={newRulePath}
                onChange={(e) => setNewRulePath(e.target.value)}
                placeholder="SVG path component"
                className="flex-1 px-1.5 py-1 border border-slate-300 rounded font-mono"
              />
              <button
                type="button"
                onClick={addFeaturicRule}
                className="px-2.5 py-1 bg-slate-800 text-white rounded hover:bg-slate-700"
              >
                Add
              </button>
            </div>
          </div>
        </div>

        {/* Layer 3: Canvas Vector Renderer */}
        <div className="lg:col-span-7 space-y-3">
          <div className="border border-slate-300 bg-white rounded p-3 space-y-2.5">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div className="text-xs font-bold uppercase tracking-wide text-slate-700">
                Layer 3 — Vector Canvas Renderer ({ws.flowDirection})
              </div>
              <div className="flex items-center gap-3">
                <label className="flex items-center gap-1.5 text-xs text-slate-700">
                  <input
                    type="checkbox"
                    checked={showDebugAnchors}
                    onChange={(e) => setShowDebugAnchors(e.target.checked)}
                  />
                  Show Anchors & Flow Vectors
                </label>
                <button
                  type="button"
                  onClick={() =>
                    downloadTextFile(
                      `${state.Name || 'script'}-shaped.svg`,
                      exportShapedSvg(shaped, ws),
                      'image/svg+xml;charset=utf-8'
                    )
                  }
                  className="px-2.5 py-1 text-xs bg-slate-800 text-white rounded hover:bg-slate-700"
                >
                  Export SVG
                </button>
              </div>
            </div>

            <div className="border border-slate-200 bg-slate-50 rounded overflow-auto max-h-[420px]">
              <svg
                viewBox={`0 0 ${shaped.bounds.width} ${shaped.bounds.height}`}
                className="w-full min-h-[280px] bg-white"
              >
                {showDebugAnchors && (
                  <defs>
                    <pattern id="ws-grid" width="24" height="24" patternUnits="userSpaceOnUse">
                      <path
                        d="M 24 0 L 0 0 0 24"
                        fill="none"
                        stroke="#f1f5f9"
                        strokeWidth="1"
                      />
                    </pattern>
                  </defs>
                )}
                {showDebugAnchors && (
                  <rect width="100%" height="100%" fill="url(#ws-grid)" />
                )}

                {shaped.glyphs.map((g) => (
                  <g key={g.index}>
                    <g transform={`translate(${g.x}, ${g.y}) scale(${scale})`}>
                      <path
                        d={g.svgPath}
                        fill="none"
                        stroke="#0f172a"
                        strokeWidth="4.5"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                      />
                      {g.extraPaths.map((ep, idx) => (
                        <path
                          key={idx}
                          d={ep}
                          fill="none"
                          stroke="#334155"
                          strokeWidth="4"
                          strokeLinecap="round"
                          strokeLinejoin="round"
                        />
                      ))}
                    </g>

                    {showDebugAnchors && (
                      <>
                        <circle
                          cx={g.enteringWorld.x}
                          cy={g.enteringWorld.y}
                          r="3"
                          fill="#16a34a"
                        />
                        <circle
                          cx={g.exitingWorld.x}
                          cy={g.exitingWorld.y}
                          r="3"
                          fill="#2563eb"
                        />
                        <text
                          x={g.x + 6}
                          y={g.y + 10}
                          fontSize="9"
                          fill="#94a3b8"
                          fontFamily="monospace"
                        >
                          {g.token.id}
                        </text>
                      </>
                    )}
                  </g>
                ))}
              </svg>
            </div>

            <div className="text-[11px] text-slate-500 flex items-center gap-4">
              <span className="flex items-center gap-1">
                <span className="inline-block w-2.5 h-2.5 rounded-full bg-emerald-600" />
                #entering anchor
              </span>
              <span className="flex items-center gap-1">
                <span className="inline-block w-2.5 h-2.5 rounded-full bg-blue-600" />
                #exiting anchor
              </span>
              <span>Shaped {shaped.glyphs.length} glyph(s)</span>
            </div>
          </div>

          <div className="border border-slate-300 bg-white rounded p-3 space-y-1.5">
            <div className="text-xs font-bold uppercase tracking-wide text-slate-700">
              Shaping Graph Trace
            </div>
            <div className="max-h-40 overflow-y-auto">
              <table className="w-full text-xs text-left border-collapse">
                <thead className="bg-slate-100 border-b border-slate-200 text-slate-700 sticky top-0">
                  <tr>
                    <th className="px-2 py-1">#</th>
                    <th className="px-2 py-1">Token</th>
                    <th className="px-2 py-1">Variant</th>
                    <th className="px-2 py-1">Anchor Bond</th>
                    <th className="px-2 py-1">Pos</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 font-mono">
                  {shaped.trace.map((tr) => (
                    <tr key={tr.step}>
                      <td className="px-2 py-1">{tr.step}</td>
                      <td className="px-2 py-1 font-semibold">{tr.tokenId}</td>
                      <td className="px-2 py-1">{tr.variant}</td>
                      <td className="px-2 py-1">{tr.anchorMode}</td>
                      <td className="px-2 py-1">{tr.position}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

import React, { useMemo, useState } from 'react';
import { LanguageProjectState } from '../core/types';
import {
  BUILTIN_MORPHOLOGY_MODULES,
  BUILTIN_ORTHOGRAPHY_MODULES,
  BUILTIN_PLUGIN_PHONO_RULES,
  BUILTIN_SYNTAX_MODULES,
  DEFAULT_PIPELINE_SELECTION,
} from '../core/languageEngine';
import { getBuiltInPlugins, PluginOutputFile } from '../core/plugins';
import { downloadTextFile } from '../core/download';

interface ExtensionsTabProps {
  state: LanguageProjectState;
  onUpdate: (updater: (prev: LanguageProjectState) => LanguageProjectState, label: string) => void;
}

export const ExtensionsTab: React.FC<ExtensionsTabProps> = ({ state, onUpdate }) => {
  const { plugins, actions } = useMemo(() => getBuiltInPlugins(), []);
  const pipeline = state.Pipeline ?? DEFAULT_PIPELINE_SELECTION;

  const [selectedPluginId, setSelectedPluginId] = useState<string>(plugins[0]?.id ?? '');
  const [selectedActionId, setSelectedActionId] = useState<string>(actions[0]?.actionId ?? '');
  const [lastResults, setLastResults] = useState<Record<string, string>>({});
  const [logs, setLogs] = useState<string[]>([
    `${new Date().toLocaleTimeString()} Discovered ${plugins.length} unified SDK plugin(s).`,
  ]);
  const [outputFiles, setOutputFiles] = useState<PluginOutputFile[]>([]);

  const [argValues, setArgValues] = useState<Record<string, Record<string, string | boolean>>>(() => {
    const init: Record<string, Record<string, string | boolean>> = {};
    for (const act of actions) {
      const map: Record<string, string | boolean> = {};
      for (const arg of act.arguments) {
        map[arg.name] = arg.type === 'bool' ? arg.defaultValue === 'true' : arg.defaultValue;
      }
      init[act.actionId] = map;
    }
    return init;
  });

  const selectedPlugin = plugins.find((p) => p.id === selectedPluginId) ?? plugins[0];
  const selectedAction = actions.find((a) => a.actionId === selectedActionId) ?? actions[0];

  const appendLog = (msg: string) => {
    const stamp = new Date().toLocaleTimeString();
    setLogs((prev) => [...prev.slice(-199), `${stamp} ${msg}`]);
  };

  const refreshPlugins = () => {
    appendLog(`Refreshed plugin catalog (${plugins.length} loaded, ${actions.length} actions).`);
  };

  const togglePluginPhonoRule = (ruleId: string) => {
    onUpdate((prev) => {
      const cur = prev.Pipeline ?? DEFAULT_PIPELINE_SELECTION;
      const exists = cur.enabledPhonoPluginRules.includes(ruleId);
      const nextRules = exists
        ? cur.enabledPhonoPluginRules.filter((id) => id !== ruleId)
        : [...cur.enabledPhonoPluginRules, ruleId];
      return {
        ...prev,
        Pipeline: { ...cur, enabledPhonoPluginRules: nextRules },
      };
    }, 'Toggled plugin phonology rule');
  };

  const updateArg = (actionId: string, argName: string, val: string | boolean) => {
    setArgValues((prev) => ({
      ...prev,
      [actionId]: {
        ...(prev[actionId] ?? {}),
        [argName]: val,
      },
    }));
  };

  const runAction = (actionId: string) => {
    const act = actions.find((a) => a.actionId === actionId);
    if (!act) return;
    setSelectedActionId(actionId);
    setSelectedPluginId(act.pluginId);

    const args = argValues[actionId] ?? {};
    const result = act.run(state, args);

    setLastResults((prev) => ({ ...prev, [actionId]: result.message }));
    appendLog(`[${act.pluginName}] ${result.message}`);

    if (result.success && result.outputFile) {
      setOutputFiles((prev) => [result.outputFile!, ...prev]);
    }
  };

  return (
    <div className="p-5 space-y-5">
      {/* Phase 4: Runtime Pipeline Stage Swapping */}
      <div className="border border-slate-300 bg-white rounded p-4 space-y-3">
        <div>
          <h3 className="text-base font-bold text-slate-900">Phase 4: Runtime Pipeline Stage Swapper</h3>
          <p className="text-xs text-slate-600">
            Hot-swap active Syntax, Morphology, Orthography, and Plugin Phonology modules consumed by LangEngine.
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-3 text-xs">
          <label className="space-y-1">
            <span className="font-semibold text-slate-700">ISyntaxGenerator</span>
            <select
              value={pipeline.syntaxGeneratorId}
              onChange={(e) =>
                onUpdate(
                  (prev) => ({
                    ...prev,
                    Pipeline: { ...(prev.Pipeline ?? DEFAULT_PIPELINE_SELECTION), syntaxGeneratorId: e.target.value },
                  }),
                  'Swapped syntax generator'
                )
              }
              className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded"
            >
              {BUILTIN_SYNTAX_MODULES.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.name}
                </option>
              ))}
            </select>
          </label>

          <label className="space-y-1">
            <span className="font-semibold text-slate-700">IMorphologyRealizer</span>
            <select
              value={pipeline.morphologyRealizerId}
              onChange={(e) =>
                onUpdate(
                  (prev) => ({
                    ...prev,
                    Pipeline: {
                      ...(prev.Pipeline ?? DEFAULT_PIPELINE_SELECTION),
                      morphologyRealizerId: e.target.value,
                    },
                  }),
                  'Swapped morphology realizer'
                )
              }
              className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded"
            >
              {BUILTIN_MORPHOLOGY_MODULES.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.name}
                </option>
              ))}
            </select>
          </label>

          <label className="space-y-1">
            <span className="font-semibold text-slate-700">IOrthographyRenderer</span>
            <select
              value={pipeline.orthographyRendererId}
              onChange={(e) =>
                onUpdate(
                  (prev) => ({
                    ...prev,
                    Pipeline: {
                      ...(prev.Pipeline ?? DEFAULT_PIPELINE_SELECTION),
                      orthographyRendererId: e.target.value,
                    },
                  }),
                  'Swapped orthography renderer'
                )
              }
              className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded"
            >
              {BUILTIN_ORTHOGRAPHY_MODULES.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.name}
                </option>
              ))}
            </select>
          </label>
        </div>

        <div className="pt-1 space-y-1">
          <div className="text-xs font-semibold text-slate-700">Plugin IPhonoRule Extensions</div>
          <div className="flex flex-wrap gap-4 text-xs">
            {BUILTIN_PLUGIN_PHONO_RULES.map((rule) => (
              <label key={rule.id} className="flex items-center gap-1.5" title={rule.description}>
                <input
                  type="checkbox"
                  checked={pipeline.enabledPhonoPluginRules.includes(rule.id)}
                  onChange={() => togglePluginPhonoRule(rule.id)}
                />
                <span>{rule.name}</span>
              </label>
            ))}
          </div>
        </div>
      </div>

      <div className="flex items-center gap-3">
        <h3 className="text-base font-bold text-slate-900">Action Plugins (Unified SDK)</h3>
        <button
          type="button"
          onClick={refreshPlugins}
          className="px-3 py-1 text-sm bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded"
        >
          Refresh
        </button>
      </div>

      <div className="border border-slate-300 bg-white rounded overflow-hidden">
        <table className="w-full text-sm text-left border-collapse">
          <thead className="bg-slate-100 border-b border-slate-300 text-xs text-slate-700">
            <tr>
              <th className="px-3 py-1.5 font-semibold w-56">Name</th>
              <th className="px-3 py-1.5 font-semibold w-28">Version</th>
              <th className="px-3 py-1.5 font-semibold">Status</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {plugins.map((p) => (
              <tr
                key={p.id}
                onClick={() => setSelectedPluginId(p.id)}
                className={`cursor-pointer ${
                  selectedPlugin?.id === p.id ? 'bg-slate-100 font-medium' : 'hover:bg-slate-50'
                }`}
              >
                <td className="px-3 py-1.5">{p.name}</td>
                <td className="px-3 py-1.5 font-mono text-xs">{p.version}</td>
                <td className="px-3 py-1.5 text-xs text-slate-700">{p.status}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="border border-slate-300 bg-white rounded overflow-hidden">
        <table className="w-full text-sm text-left border-collapse">
          <thead className="bg-slate-100 border-b border-slate-300 text-xs text-slate-700">
            <tr>
              <th className="px-3 py-1.5 font-semibold">Plugin</th>
              <th className="px-3 py-1.5 font-semibold">Category</th>
              <th className="px-3 py-1.5 font-semibold">Action</th>
              <th className="px-3 py-1.5 font-semibold">Last Result</th>
              <th className="px-3 py-1.5 font-semibold w-24">Run</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {actions.map((act) => (
              <tr
                key={act.actionId}
                onClick={() => setSelectedActionId(act.actionId)}
                className={`cursor-pointer ${
                  selectedAction?.actionId === act.actionId ? 'bg-slate-100' : 'hover:bg-slate-50'
                }`}
              >
                <td className="px-3 py-1.5">{act.pluginName}</td>
                <td className="px-3 py-1.5 text-xs">{act.category}</td>
                <td className="px-3 py-1.5 font-medium">{act.label}</td>
                <td className="px-3 py-1.5 text-xs text-slate-600">
                  {lastResults[act.actionId] ?? ''}
                </td>
                <td className="px-3 py-1.5">
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      runAction(act.actionId);
                    }}
                    className="px-3 py-1 text-xs bg-slate-800 text-white rounded hover:bg-slate-700"
                  >
                    Run
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-5 gap-4">
        <div className="lg:col-span-2 border border-slate-300 bg-white rounded p-3 space-y-1 text-xs text-slate-700">
          <div className="text-sm font-semibold text-slate-900 mb-1">Plugin Details</div>
          {selectedPlugin && (
            <>
              <div><span className="font-medium">Name:</span> {selectedPlugin.name}</div>
              <div><span className="font-medium">Version:</span> {selectedPlugin.version}</div>
              <div><span className="font-medium">Isolation:</span> {selectedPlugin.isolationMode}</div>
              <div><span className="font-medium">Author:</span> {selectedPlugin.author}</div>
              <div><span className="font-medium">Description:</span> {selectedPlugin.description}</div>
              <div><span className="font-medium">Capabilities:</span> {selectedPlugin.capabilities.join(', ')}</div>
              <div><span className="font-medium">Website:</span> {selectedPlugin.website}</div>
            </>
          )}
        </div>

        <div className="lg:col-span-3 border border-slate-300 bg-white rounded p-3">
          <div className="text-sm font-semibold text-slate-900 mb-2">
            Action Arguments {selectedAction ? `— ${selectedAction.label}` : ''}
          </div>
          {selectedAction && (
            <table className="w-full text-xs text-left border-collapse">
              <thead className="bg-slate-100 border-b border-slate-300 text-slate-700">
                <tr>
                  <th className="px-2 py-1 font-semibold">Name</th>
                  <th className="px-2 py-1 font-semibold">Type</th>
                  <th className="px-2 py-1 font-semibold">Value</th>
                  <th className="px-2 py-1 font-semibold">Required</th>
                  <th className="px-2 py-1 font-semibold">Description</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {selectedAction.arguments.map((arg) => {
                  const val = argValues[selectedAction.actionId]?.[arg.name] ?? arg.defaultValue;
                  return (
                    <tr key={arg.name}>
                      <td className="px-2 py-1.5 font-medium">{arg.label}</td>
                      <td className="px-2 py-1.5 font-mono">{arg.type}</td>
                      <td className="px-2 py-1.5">
                        {arg.options ? (
                          <select
                            value={String(val)}
                            onChange={(e) =>
                              updateArg(selectedAction.actionId, arg.name, e.target.value)
                            }
                            className="w-full px-1.5 py-1 bg-white border border-slate-300 rounded"
                          >
                            {arg.options.map((opt) => (
                              <option key={opt} value={opt}>
                                {opt}
                              </option>
                            ))}
                          </select>
                        ) : arg.type === 'bool' ? (
                          <input
                            type="checkbox"
                            checked={Boolean(val)}
                            onChange={(e) =>
                              updateArg(selectedAction.actionId, arg.name, e.target.checked)
                            }
                          />
                        ) : (
                          <input
                            type="text"
                            value={String(val)}
                            onChange={(e) =>
                              updateArg(selectedAction.actionId, arg.name, e.target.value)
                            }
                            className="w-full px-1.5 py-1 bg-white border border-slate-300 rounded font-mono"
                          />
                        )}
                      </td>
                      <td className="px-2 py-1.5">{arg.required ? 'True' : 'False'}</td>
                      <td className="px-2 py-1.5 text-slate-600">{arg.description}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      </div>

      {outputFiles.length > 0 && (
        <div className="border border-slate-300 bg-white rounded p-3 space-y-2">
          <div className="text-sm font-semibold text-slate-900">Generated Plugin Outputs</div>
          <div className="space-y-2">
            {outputFiles.map((file, idx) => (
              <div key={`${file.fileName}-${idx}`} className="border border-slate-200 rounded p-2.5 bg-slate-50">
                <div className="flex items-center justify-between mb-1.5">
                  <div className="text-xs font-mono font-semibold text-slate-800">
                    {file.fileName}{' '}
                    <span className="font-sans font-normal text-slate-500">
                      ({file.pluginName} at {file.createdAt})
                    </span>
                  </div>
                  <button
                    type="button"
                    onClick={() => downloadTextFile(file.fileName, file.content)}
                    className="px-2.5 py-1 text-xs bg-slate-800 text-white rounded hover:bg-slate-700"
                  >
                    Download
                  </button>
                </div>
                <pre className="text-xs font-mono bg-white border border-slate-200 rounded p-2 max-h-32 overflow-auto whitespace-pre-wrap">
                  {file.content}
                </pre>
              </div>
            ))}
          </div>
        </div>
      )}

      <div className="text-xs text-slate-600">
        {plugins.length} plugin(s) loaded, {actions.length} action(s) available.
      </div>

      <textarea
        readOnly
        value={logs.join('\n')}
        rows={5}
        className="w-full p-2.5 text-xs font-mono bg-white border border-slate-300 rounded"
      />
    </div>
  );
};

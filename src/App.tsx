import React, { useEffect, useMemo, useRef, useState } from 'react';
import { LanguageProjectState } from './core/types';
import {
  createDefaultProjectState,
  createEnglishPresetProjectState,
  normalizeLoadedProjectState,
  validateProject,
} from './core/languageEngine';
import {
  BackupSnapshot,
  createBackupSnapshot,
  exportLexiconToCsv,
  exportLexiconToJson,
  importLexiconFromText,
  loadAutosavedProject,
  loadBackupSnapshots,
  loadRecentProjects,
  ProjectHistory,
  RecentProjectRecord,
  saveAutosavedProject,
  saveRecentProject,
} from './core/projectHistory';
import { downloadTextFile } from './core/download';
import { PhonologyTab } from './components/PhonologyTab';
import { MorphologyTab } from './components/MorphologyTab';
import { SyntaxTab } from './components/SyntaxTab';
import { OrthographyTab } from './components/OrthographyTab';
import { WritingSystemsTab } from './components/WritingSystemsTab';
import { OutputTab } from './components/OutputTab';
import { InspectorsTab } from './components/InspectorsTab';
import { ExtensionsTab } from './components/ExtensionsTab';

type TabId =
  | 'Phonology'
  | 'Morphology'
  | 'Syntax'
  | 'Orthography'
  | 'Writing Systems'
  | 'Output'
  | 'Inspectors'
  | 'Extensions';

const TABS: TabId[] = [
  'Phonology',
  'Morphology',
  'Syntax',
  'Orthography',
  'Writing Systems',
  'Output',
  'Inspectors',
  'Extensions',
];

export const App: React.FC = () => {
  const [initialLoaded] = useState(() => loadAutosavedProject());
  const [projectState, setProjectState] = useState<LanguageProjectState>(() =>
    initialLoaded ? initialLoaded.state : createDefaultProjectState()
  );
  const [autosaveTime, setAutosaveTime] = useState<string>(initialLoaded?.savedAt ?? '');
  const [currentProjectFile, setCurrentProjectFile] = useState<string>('(unsaved)');
  const [activeTab, setActiveTab] = useState<TabId>('Phonology');
  const [recentProjects, setRecentProjects] = useState<RecentProjectRecord[]>(() =>
    loadRecentProjects()
  );
  const [backups, setBackups] = useState<BackupSnapshot[]>(() => loadBackupSnapshots());
  const [statusMessage, setStatusMessage] = useState<string>('');
  const [historyVersion, setHistoryVersion] = useState(0);
  const [diagnosticsExpanded, setDiagnosticsExpanded] = useState(false);

  const historyRef = useRef<ProjectHistory | null>(null);
  if (historyRef.current === null) {
    const h = new ProjectHistory(60);
    h.push(projectState, 'Initial');
    historyRef.current = h;
  }

  const loadProjectInputRef = useRef<HTMLInputElement>(null);
  const importLexiconInputRef = useRef<HTMLInputElement>(null);

  const diagnostics = useMemo(() => validateProject(projectState), [projectState]);
  const errorCount = diagnostics.filter((d) => d.severity === 'error').length;
  const warningCount = diagnostics.filter((d) => d.severity === 'warning').length;

  useEffect(() => {
    const t = setTimeout(() => {
      const stamp = saveAutosavedProject(projectState);
      setAutosaveTime(stamp);
    }, 350);
    return () => clearTimeout(t);
  }, [projectState]);

  useEffect(() => {
    if (!statusMessage) return;
    const t = setTimeout(() => setStatusMessage(''), 3500);
    return () => clearTimeout(t);
  }, [statusMessage]);

  const updateProject = (
    updater: (prev: LanguageProjectState) => LanguageProjectState,
    label: string
  ) => {
    setProjectState((prev) => {
      const next = updater(prev);
      historyRef.current?.push(next, label);
      setHistoryVersion((v) => v + 1);
      return next;
    });
  };

  const handleReset = () => {
    setBackups(createBackupSnapshot(projectState, 'before-reset'));
    const fresh = createDefaultProjectState();
    setProjectState(fresh);
    setCurrentProjectFile('(unsaved)');
    historyRef.current?.push(fresh, 'Reset to default');
    setHistoryVersion((v) => v + 1);
    setStatusMessage('Reset project to defaults');
  };

  const handleLoadEnglishPreset = () => {
    setBackups(createBackupSnapshot(projectState, 'before-english-preset'));
    const preset = createEnglishPresetProjectState();
    setProjectState(preset);
    setCurrentProjectFile('(unsaved)');
    historyRef.current?.push(preset, 'Loaded General American English preset');
    setHistoryVersion((v) => v + 1);
    setStatusMessage('Loaded General American English showcase preset');
  };

  const handleSave = (saveAs: boolean) => {
    let fileName = currentProjectFile;
    if (saveAs || fileName === '(unsaved)' || !fileName.trim()) {
      const safeName = (projectState.Name || 'My Conlang').trim().replace(/[\\/:*?"<>|]+/g, '_');
      fileName = `${safeName}.clproj.json`;
    }
    const json = JSON.stringify(projectState, null, 2);
    downloadTextFile(fileName, json, 'application/json;charset=utf-8');
    setCurrentProjectFile(fileName);
    setRecentProjects(saveRecentProject(fileName, projectState));
    setBackups(createBackupSnapshot(projectState, 'manual-save'));
    setStatusMessage(`Saved ${fileName}`);
  };

  const handleLoadFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;
    try {
      const text = await file.text();
      const parsed = JSON.parse(text) as Partial<LanguageProjectState>;
      const merged = normalizeLoadedProjectState(parsed);
      setProjectState(merged);
      setCurrentProjectFile(file.name);
      historyRef.current?.push(merged, 'Project loaded');
      setHistoryVersion((v) => v + 1);
      setRecentProjects(saveRecentProject(file.name, merged));
      setStatusMessage(`Loaded ${file.name}`);
    } catch {
      setStatusMessage('Failed to parse project JSON.');
    }
  };

  const handleUndo = () => {
    if (!historyRef.current) return;
    const prev = historyRef.current.undo(projectState);
    if (!prev) return;
    setProjectState(prev);
    setHistoryVersion((v) => v + 1);
  };

  const handleRedo = () => {
    if (!historyRef.current) return;
    const next = historyRef.current.redo();
    if (!next) return;
    setProjectState(next);
    setHistoryVersion((v) => v + 1);
  };

  const handleImportLexicon = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;
    try {
      const text = await file.text();
      const imported = importLexiconFromText(file.name, text);
      if (imported.length === 0) {
        setStatusMessage('No valid lexemes found in file.');
        return;
      }
      updateProject((prev) => ({ ...prev, Lexemes: imported }), 'Lexicon imported');
      setStatusMessage(`Imported ${imported.length} lexeme(s) from ${file.name}`);
    } catch {
      setStatusMessage('Failed to import lexicon.');
    }
  };

  const handleExportLexicon = (format: 'csv' | 'json') => {
    if (format === 'json') {
      downloadTextFile(
        'lexicon.json',
        exportLexiconToJson(projectState.Lexemes),
        'application/json;charset=utf-8'
      );
      setStatusMessage('Exported lexicon.json');
    } else {
      downloadTextFile(
        'lexicon.csv',
        exportLexiconToCsv(projectState.Lexemes),
        'text/csv;charset=utf-8'
      );
      setStatusMessage('Exported lexicon.csv');
    }
  };

  const openRecent = (rec: RecentProjectRecord) => {
    const normalized = normalizeLoadedProjectState(rec.state);
    setProjectState(normalized);
    setCurrentProjectFile(rec.fileName);
    historyRef.current?.push(normalized, 'Project loaded');
    setHistoryVersion((v) => v + 1);
    setStatusMessage(`Opened ${rec.fileName}`);
  };

  void historyVersion;
  const canUndo = historyRef.current.canUndo;
  const canRedo = historyRef.current.canRedo;

  return (
    <div className="min-h-screen flex flex-col bg-slate-100 text-slate-900">
      <header className="bg-white border-b border-slate-300 px-5 py-3 space-y-2.5">
        <div className="flex flex-wrap items-baseline justify-between gap-3">
          <div className="flex flex-wrap items-baseline gap-3">
            <h1 className="text-xl font-bold tracking-tight text-slate-900">Conlang Builder</h1>
            <input
              type="text"
              value={projectState.Name}
              onChange={(e) =>
                updateProject(
                  (prev) => ({ ...prev, Name: e.target.value }),
                  'Project renamed'
                )
              }
              aria-label="Project Name"
              className="px-2 py-0.5 text-sm font-medium bg-slate-50 border border-slate-300 rounded"
            />
            <span className="text-xs text-slate-500 font-mono">{currentProjectFile}</span>
            {autosaveTime && (
              <span className="text-[11px] text-slate-400">Autosaved {autosaveTime}</span>
            )}
          </div>
          {statusMessage && (
            <span className="text-xs font-medium text-emerald-700 bg-emerald-50 border border-emerald-200 px-2.5 py-1 rounded">
              {statusMessage}
            </span>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-1.5">
          <button
            type="button"
            onClick={() => handleSave(false)}
            className="px-3 py-1 text-xs font-medium bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded"
          >
            Save
          </button>
          <button
            type="button"
            onClick={() => handleSave(true)}
            className="px-3 py-1 text-xs font-medium bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded"
          >
            Save As
          </button>
          <button
            type="button"
            onClick={() => loadProjectInputRef.current?.click()}
            className="px-3 py-1 text-xs font-medium bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded"
          >
            Load
          </button>
          <button
            type="button"
            onClick={handleReset}
            className="px-3 py-1 text-xs font-medium bg-slate-100 hover:bg-slate-200 border border-slate-300 rounded text-slate-700"
          >
            New / Reset
          </button>
          <button
            type="button"
            onClick={handleLoadEnglishPreset}
            className="px-3 py-1 text-xs font-medium bg-indigo-50 hover:bg-indigo-100 border border-indigo-300 rounded text-indigo-800"
          >
            English Preset
          </button>
          <input
            ref={loadProjectInputRef}
            type="file"
            accept=".json,.clproj.json"
            onChange={handleLoadFile}
            className="hidden"
          />

          <div className="w-px h-5 bg-slate-300 mx-1" />

          <button
            type="button"
            onClick={handleUndo}
            disabled={!canUndo}
            className="px-3 py-1 text-xs font-medium bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded disabled:opacity-40"
          >
            Undo
          </button>
          <button
            type="button"
            onClick={handleRedo}
            disabled={!canRedo}
            className="px-3 py-1 text-xs font-medium bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded disabled:opacity-40"
          >
            Redo
          </button>

          <div className="w-px h-5 bg-slate-300 mx-1" />

          <button
            type="button"
            onClick={() => importLexiconInputRef.current?.click()}
            className="px-3 py-1 text-xs font-medium bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded"
          >
            Import Lexicon
          </button>
          <input
            ref={importLexiconInputRef}
            type="file"
            accept=".csv,.json"
            onChange={handleImportLexicon}
            className="hidden"
          />
          <button
            type="button"
            onClick={() => handleExportLexicon('csv')}
            className="px-3 py-1 text-xs font-medium bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded"
          >
            Export Lexicon (CSV)
          </button>
          <button
            type="button"
            onClick={() => handleExportLexicon('json')}
            className="px-3 py-1 text-xs font-medium bg-slate-200 hover:bg-slate-300 border border-slate-300 rounded"
          >
            Export Lexicon (JSON)
          </button>
        </div>

        {(recentProjects.length > 0 || backups.length > 0) && (
          <div className="flex flex-wrap items-center gap-3 pt-0.5">
            {recentProjects.length > 0 && (
              <div className="flex flex-wrap items-center gap-1.5">
                <span className="text-xs text-slate-500">Recent:</span>
                {recentProjects.map((rec) => (
                  <button
                    key={rec.id}
                    type="button"
                    onClick={() => openRecent(rec)}
                    className="px-2 py-0.5 text-xs bg-slate-100 hover:bg-slate-200 border border-slate-300 rounded font-mono"
                  >
                    {rec.fileName}
                  </button>
                ))}
              </div>
            )}
            {backups.length > 0 && (
              <label className="flex items-center gap-1.5 text-xs text-slate-600">
                <span>Restore Backup:</span>
                <select
                  value=""
                  onChange={(e) => {
                    const snap = backups.find((b) => b.id === e.target.value);
                    if (!snap) return;
                    const normalized = normalizeLoadedProjectState(snap.state);
                    setProjectState(normalized);
                    historyRef.current?.push(normalized, 'Restored backup');
                    setHistoryVersion((v) => v + 1);
                    setStatusMessage(`Restored backup: ${snap.label}`);
                  }}
                  className="px-2 py-0.5 bg-slate-50 border border-slate-300 rounded text-xs"
                >
                  <option value="">{backups.length} snapshot(s)…</option>
                  {backups.map((b) => (
                    <option key={b.id} value={b.id}>
                      {b.createdAt} — {b.label}
                    </option>
                  ))}
                </select>
              </label>
            )}
          </div>
        )}
      </header>

      <nav className="bg-slate-200 border-b border-slate-300 px-5 pt-2 flex flex-wrap gap-1">
        {TABS.map((tab) => {
          const isActive = activeTab === tab;
          return (
            <button
              key={tab}
              type="button"
              onClick={() => setActiveTab(tab)}
              className={`px-3.5 py-1.5 text-sm font-medium rounded-t border-t border-l border-r ${
                isActive
                  ? 'bg-slate-100 border-slate-300 text-slate-900 -mb-px pb-2'
                  : 'bg-slate-200/70 border-transparent text-slate-600 hover:text-slate-900 hover:bg-slate-200'
              }`}
            >
              {tab}
            </button>
          );
        })}
      </nav>

      <main className="flex-1 max-w-6xl w-full mx-auto pb-4">
        {activeTab === 'Phonology' && (
          <PhonologyTab state={projectState} onUpdate={updateProject} />
        )}
        {activeTab === 'Morphology' && (
          <MorphologyTab state={projectState} onUpdate={updateProject} />
        )}
        {activeTab === 'Syntax' && (
          <SyntaxTab state={projectState} onUpdate={updateProject} />
        )}
        {activeTab === 'Orthography' && (
          <OrthographyTab state={projectState} onUpdate={updateProject} />
        )}
        {activeTab === 'Writing Systems' && (
          <WritingSystemsTab state={projectState} onUpdate={updateProject} />
        )}
        {activeTab === 'Output' && <OutputTab state={projectState} />}
        {activeTab === 'Inspectors' && <InspectorsTab state={projectState} />}
        {activeTab === 'Extensions' && <ExtensionsTab state={projectState} onUpdate={updateProject} />}
      </main>

      {/* Central Diagnostics Bar (Roadmap Phase 0 #3) */}
      <footer className="bg-white border-t border-slate-300 px-5 py-2 text-xs">
        <div className="max-w-6xl mx-auto">
          <div className="flex items-center justify-between">
            <button
              type="button"
              onClick={() => setDiagnosticsExpanded((v) => !v)}
              className="flex items-center gap-3 font-medium text-slate-700 hover:text-slate-900"
            >
              <span>Diagnostics:</span>
              <span className={errorCount > 0 ? 'text-rose-700 font-semibold' : 'text-slate-500'}>
                {errorCount} Error(s)
              </span>
              <span className={warningCount > 0 ? 'text-amber-700 font-semibold' : 'text-slate-500'}>
                {warningCount} Warning(s)
              </span>
              <span className="text-slate-400">
                {diagnosticsExpanded ? '▼ Hide' : '▲ Show details'}
              </span>
            </button>
            <span className="text-slate-400 font-mono">Unified Schema v{projectState.SchemaVersion}</span>
          </div>

          {diagnosticsExpanded && (
            <div className="mt-2 border border-slate-200 rounded bg-slate-50 max-h-36 overflow-y-auto divide-y divide-slate-200">
              {diagnostics.length === 0 ? (
                <div className="px-3 py-2 text-emerald-700">
                  No validation or phonotactic issues detected.
                </div>
              ) : (
                diagnostics.map((d, idx) => (
                  <div key={idx} className="px-3 py-1.5 flex items-center gap-2">
                    <span
                      className={`px-1.5 py-0.5 rounded text-[10px] font-bold uppercase ${
                        d.severity === 'error'
                          ? 'bg-rose-100 text-rose-800'
                          : 'bg-amber-100 text-amber-800'
                      }`}
                    >
                      {d.severity}
                    </span>
                    <span className="font-mono text-slate-600">[{d.subsystem}]</span>
                    <span className="text-slate-800">{d.message}</span>
                  </div>
                ))
              )}
            </div>
          )}
        </div>
      </footer>
    </div>
  );
};

export default App;

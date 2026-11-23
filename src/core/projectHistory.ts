import { LanguageProjectState, LexemeState } from './types';
import { normalizeLoadedProjectState } from './languageEngine';

export interface HistoryEntry {
  state: LanguageProjectState;
  label: string;
}

export class ProjectHistory {
  private readonly maxDepth: number;
  private undoStack: HistoryEntry[] = [];
  private redoStack: HistoryEntry[] = [];

  constructor(maxDepth = 60) {
    this.maxDepth = maxDepth;
  }

  public get canUndo(): boolean {
    return this.undoStack.length > 1;
  }

  public get canRedo(): boolean {
    return this.redoStack.length > 0;
  }

  public push(state: LanguageProjectState, label: string, suppressDuplicate = false): void {
    const clone = structuredClone(state);
    if (suppressDuplicate && this.undoStack.length > 0) {
      const top = this.undoStack[this.undoStack.length - 1];
      if (JSON.stringify(top.state) === JSON.stringify(clone)) {
        return;
      }
    }

    this.undoStack.push({ state: clone, label });
    if (this.undoStack.length > this.maxDepth) {
      this.undoStack.shift();
    }
    this.redoStack = [];
  }

  public undo(currentState: LanguageProjectState): LanguageProjectState | null {
    if (this.undoStack.length <= 1) return null;
    const popped = this.undoStack.pop();
    if (popped) {
      this.redoStack.push({ state: structuredClone(currentState), label: popped.label });
    }
    const target = this.undoStack[this.undoStack.length - 1];
    return target ? structuredClone(target.state) : null;
  }

  public redo(): LanguageProjectState | null {
    const next = this.redoStack.pop();
    if (!next) return null;
    this.undoStack.push({ state: structuredClone(next.state), label: next.label });
    return structuredClone(next.state);
  }
}

export function importLexiconFromText(filename: string, content: string): LexemeState[] {
  const lower = filename.toLowerCase();
  if (lower.endsWith('.json')) {
    const parsed = JSON.parse(content) as Array<{ Lemma?: string; Pos?: string; UR?: string }>;
    if (!Array.isArray(parsed)) return [];
    return parsed
      .filter((r) => typeof r.Lemma === 'string' && r.Lemma.trim().length > 0)
      .map((r) => ({
        Lemma: r.Lemma!.trim(),
        Pos: r.Pos?.trim() || 'N',
        UR: (r.UR ?? '').trim(),
      }));
  }

  const lines = content.split(/\r?\n/).slice(1);
  return lines
    .map((line) => line.split(','))
    .filter((parts) => parts.length >= 3 && parts[0].trim().length > 0)
    .map((parts) => ({
      Lemma: parts[0].trim(),
      Pos: parts[1].trim() || 'N',
      UR: parts[2].trim(),
    }));
}

export function exportLexiconToCsv(lexemes: readonly LexemeState[]): string {
  const lines = ['Lemma,Pos,UR', ...lexemes.map((l) => `${l.Lemma},${l.Pos},${l.UR}`)];
  return lines.join('\n');
}

export function exportLexiconToJson(lexemes: readonly LexemeState[]): string {
  return JSON.stringify(lexemes, null, 2);
}

export interface RecentProjectRecord {
  id: string;
  fileName: string;
  name: string;
  updatedAt: string;
  state: LanguageProjectState;
}

const RECENT_STORAGE_KEY = 'conlang_builder_recent_projects_v2';
const AUTOSAVE_STORAGE_KEY = 'conlang_builder_autosave_v2';

export function loadAutosavedProject(): { state: LanguageProjectState; savedAt: string } | null {
  try {
    const raw = localStorage.getItem(AUTOSAVE_STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as { state?: Partial<LanguageProjectState>; savedAt?: string };
    if (!parsed.state) return null;
    return {
      state: normalizeLoadedProjectState(parsed.state),
      savedAt: parsed.savedAt ?? '',
    };
  } catch {
    return null;
  }
}

export function saveAutosavedProject(state: LanguageProjectState): string {
  const savedAt = new Date().toLocaleTimeString();
  try {
    localStorage.setItem(AUTOSAVE_STORAGE_KEY, JSON.stringify({ state, savedAt }));
  } catch {
    // ignore storage quota errors
  }
  return savedAt;
}

export function loadRecentProjects(): RecentProjectRecord[] {
  try {
    const raw = localStorage.getItem(RECENT_STORAGE_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

export function saveRecentProject(fileName: string, state: LanguageProjectState): RecentProjectRecord[] {
  const existing = loadRecentProjects();
  const id = fileName.toLowerCase();
  const filtered = existing.filter((r) => r.id !== id);
  const next: RecentProjectRecord[] = [
    {
      id,
      fileName,
      name: state.Name,
      updatedAt: new Date().toISOString(),
      state: structuredClone(state),
    },
    ...filtered,
  ].slice(0, 10);

  try {
    localStorage.setItem(RECENT_STORAGE_KEY, JSON.stringify(next));
  } catch {
    // Ignore storage quota errors
  }
  return next;
}

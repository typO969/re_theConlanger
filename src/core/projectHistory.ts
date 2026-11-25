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

function parseCsvRow(line: string): string[] {
  const fields: string[] = [];
  let cur = '';
  let inQuotes = false;
  for (let i = 0; i < line.length; i++) {
    const ch = line[i];
    if (ch === '"') {
      if (inQuotes && line[i + 1] === '"') {
        cur += '"';
        i++;
      } else {
        inQuotes = !inQuotes;
      }
    } else if (ch === ',' && !inQuotes) {
      fields.push(cur);
      cur = '';
    } else {
      cur += ch;
    }
  }
  fields.push(cur);
  return fields;
}

export function importLexiconFromText(filename: string, content: string): LexemeState[] {
  const lower = filename.toLowerCase();
  if (lower.endsWith('.json')) {
    const parsed = JSON.parse(content) as Array<{
      Lemma?: string;
      Pos?: string;
      UR?: string;
      LexClass?: string;
      Spelling?: string;
      Irregulars?: Record<string, string>;
    }>;
    if (!Array.isArray(parsed)) return [];
    return parsed
      .filter((r) => typeof r.Lemma === 'string' && r.Lemma.trim().length > 0)
      .map((r) => ({
        Lemma: r.Lemma!.trim(),
        Pos: r.Pos?.trim() || 'N',
        UR: (r.UR ?? '').trim(),
        ...(r.LexClass?.trim() ? { LexClass: r.LexClass.trim() } : {}),
        ...(r.Spelling?.trim() ? { Spelling: r.Spelling.trim() } : {}),
        ...(r.Irregulars && Object.keys(r.Irregulars).length > 0 ? { Irregulars: r.Irregulars } : {}),
      }));
  }

  const rawLines = content.split(/\r?\n/).filter((l) => l.trim().length > 0);
  if (rawLines.length <= 1) return [];
  const headers = parseCsvRow(rawLines[0]).map((h) => h.trim().toLowerCase());
  const classCol = headers.indexOf('lexclass');
  const lines = rawLines.slice(1);
  return lines
    .map((line) => parseCsvRow(line))
    .filter((parts) => parts.length >= 3 && parts[0].trim().length > 0)
    .map((parts) => {
      const spelling = parts[3]?.trim();
      let irregulars: Record<string, string> | undefined;
      const rawIrreg = (parts[4] ?? '').trim();
      if (rawIrreg) {
        try {
          const parsed = JSON.parse(rawIrreg);
          if (parsed && typeof parsed === 'object') irregulars = parsed;
        } catch {
          // ignore malformed optional irregular column
        }
      }
      const lexClass = (classCol >= 0 ? parts[classCol] : parts[5])?.trim();
      return {
        Lemma: parts[0].trim(),
        Pos: parts[1].trim() || 'N',
        UR: parts[2].trim(),
        ...(lexClass ? { LexClass: lexClass } : {}),
        ...(spelling ? { Spelling: spelling } : {}),
        ...(irregulars && Object.keys(irregulars).length > 0 ? { Irregulars: irregulars } : {}),
      };
    });
}

export function exportLexiconToCsv(lexemes: readonly LexemeState[]): string {
  const hasExtended = lexemes.some(
    (l) =>
      Boolean(l.Spelling) ||
      Boolean(l.LexClass) ||
      (l.Irregulars && Object.keys(l.Irregulars).length > 0)
  );
  if (!hasExtended) {
    const lines = ['Lemma,Pos,UR', ...lexemes.map((l) => `${l.Lemma},${l.Pos},${l.UR}`)];
    return lines.join('\n');
  }
  const lines = [
    'Lemma,Pos,UR,Spelling,Irregulars,LexClass',
    ...lexemes.map((l) => {
      const irreg = l.Irregulars && Object.keys(l.Irregulars).length > 0
        ? `"${JSON.stringify(l.Irregulars).replace(/"/g, '""')}"`
        : '';
      return `${l.Lemma},${l.Pos},${l.UR},${l.Spelling ?? ''},${irreg},${l.LexClass ?? ''}`;
    }),
  ];
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

export interface BackupSnapshot {
  id: string;
  label: string;
  createdAt: string;
  state: LanguageProjectState;
}

const BACKUP_STORAGE_KEY = 'conlang_builder_backups_v2';

export function loadBackupSnapshots(): BackupSnapshot[] {
  try {
    const raw = localStorage.getItem(BACKUP_STORAGE_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

export function createBackupSnapshot(state: LanguageProjectState, label: string): BackupSnapshot[] {
  const existing = loadBackupSnapshots();
  const snap: BackupSnapshot = {
    id: `${Date.now()}`,
    label: `${state.Name || 'Project'} (${label})`,
    createdAt: new Date().toLocaleTimeString(),
    state: structuredClone(state),
  };
  const next = [snap, ...existing].slice(0, 15);
  try {
    localStorage.setItem(BACKUP_STORAGE_KEY, JSON.stringify(next));
  } catch {
    // Ignore storage quota errors
  }
  return next;
}


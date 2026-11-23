import { LanguageProjectState } from './types';
import { WordGenerator } from './languageEngine';

export interface PluginArgumentDefinition {
  name: string;
  label: string;
  type: 'int' | 'string' | 'bool';
  defaultValue: string;
  required: boolean;
  description: string;
  options?: string[];
}

export interface PluginOutputFile {
  fileName: string;
  content: string;
  createdAt: string;
  pluginName: string;
}

export interface PluginActionDescriptor {
  pluginId: string;
  pluginName: string;
  actionId: string;
  label: string;
  category: string;
  arguments: PluginArgumentDefinition[];
  run: (
    project: LanguageProjectState,
    args: Record<string, string | number | boolean>
  ) => { success: boolean; message: string; outputFile?: PluginOutputFile };
}

export interface PluginDescriptor {
  id: string;
  name: string;
  version: string;
  author: string;
  description: string;
  website: string;
  isolationMode: 'OutOfProcess' | 'InProcess';
  capabilities: string[];
  status: string;
}

export function toGraphvizDot(project: LanguageProjectState): string {
  const lines: string[] = [
    'digraph Rules { rankdir=LR; node [shape=box];',
    'INPUT [shape=oval];',
  ];
  let prev = 'INPUT';
  project.PhonologyRules.forEach((r, idx) => {
    const id = `R${idx}`;
    const env = `${r.PrevPhone?.trim() || ''}_${r.NextPhone?.trim() || ''}`;
    const cond = r.Condition?.trim() || '';
    const lbl = `${r.Target || '∅'}→${r.Replacement || '∅'}\\n/${env === '_' ? '' : env}\\n${cond}`.trim();
    lines.push(`${id} [label="${lbl}"${r.Enabled ? '' : ', style=dashed'}]; ${prev} -> ${id};`);
    prev = id;
  });
  lines.push(`${prev} -> OUTPUT; OUTPUT [shape=oval];`);
  lines.push('}');
  return lines.join('\n');
}

export function toFeaturesCsv(project: LanguageProjectState): string {
  const q = (s: string) => `"${(s ?? '').replace(/"/g, '""')}"`;
  const lines = [
    'Symbol,Type,Weight,AllowOnset,AllowCoda,Voicing,Manner,Place,Height,Backness,Roundness,Custom',
  ];
  for (const p of project.Phonemes) {
    const f = p.features;
    lines.push(
      [
        q(p.symbol),
        q(p.type),
        p.weight,
        p.allowOnset,
        p.allowCoda,
        q(f.voicing),
        q(f.manner),
        q(f.place),
        q(f.height),
        q(f.backness),
        q(f.roundness),
        q(f.custom),
      ].join(',')
    );
  }
  return lines.join('\n');
}

export function getBuiltInPlugins(): {
  plugins: PluginDescriptor[];
  actions: PluginActionDescriptor[];
} {
  const plugins: PluginDescriptor[] = [
    {
      id: 'evolution-sim',
      name: 'Evolution Simulator',
      version: '2.0.0',
      author: 'Conlang Builder',
      description: 'Generates sample lexicon candidates directly from the unified LanguageProject.',
      website: 'https://github.com/typO969/theConlanger',
      isolationMode: 'OutOfProcess',
      capabilities: ['OutputFiles', 'UnifiedProjectRead'],
      status: 'Loaded (Unified SDK)',
    },
    {
      id: 'graphviz',
      name: 'Graphviz & Feature Exporter',
      version: '2.0.0',
      author: 'Conlang Builder',
      description: 'Exports phonological rule pipelines (DOT) and phoneme feature matrices (CSV).',
      website: 'https://github.com/typO969/theConlanger',
      isolationMode: 'InProcess',
      capabilities: ['OutputFiles', 'UnifiedProjectRead'],
      status: 'Loaded (Unified SDK)',
    },
  ];

  const actions: PluginActionDescriptor[] = [
    {
      pluginId: 'evolution-sim',
      pluginName: 'Evolution Simulator',
      actionId: 'generate-samples',
      label: 'Generate sample words',
      category: 'Generate',
      arguments: [
        {
          name: 'count',
          label: 'Count',
          type: 'int',
          defaultValue: '10',
          required: true,
          description: 'Number of samples to generate.',
        },
        {
          name: 'syllables',
          label: 'Syllables',
          type: 'int',
          defaultValue: '2',
          required: true,
          description: 'Syllables per word.',
        },
        {
          name: 'fileName',
          label: 'File name',
          type: 'string',
          defaultValue: 'samples.txt',
          required: true,
          description: 'Output file name for the samples.',
        },
        {
          name: 'format',
          label: 'Format',
          type: 'string',
          defaultValue: 'txt',
          required: false,
          description: 'Output format.',
          options: ['txt', 'csv'],
        },
      ],
      run: (project, args) => {
        const hasC = project.Phonemes.some((p) => p.type === 'C');
        const hasV = project.Phonemes.some((p) => p.type === 'V');
        if (!hasC && !hasV) {
          return { success: false, message: 'Inventory has no phonemes to generate words.' };
        }
        const count = Math.max(1, Number(args.count) || 10);
        const syllables = Math.max(1, Number(args.syllables) || 2);
        const fileName = String(args.fileName || 'samples.txt');
        const format = String(args.format || 'txt').toLowerCase();

        const gen = new WordGenerator(project);
        const words = gen.generateWords(count, syllables);
        const content = format === 'csv' ? words.join(',') : words.join('\n');

        return {
          success: true,
          message: `Generated ${words.length} samples to ${fileName}`,
          outputFile: {
            fileName,
            content,
            createdAt: new Date().toLocaleTimeString(),
            pluginName: 'Evolution Simulator',
          },
        };
      },
    },
    {
      pluginId: 'graphviz',
      pluginName: 'Graphviz & Feature Exporter',
      actionId: 'export-dot',
      label: 'Export sound rules as Graphviz',
      category: 'Export',
      arguments: [
        {
          name: 'fileName',
          label: 'File name',
          type: 'string',
          defaultValue: 'sound-rules.dot',
          required: true,
          description: 'Output file name for the DOT graph.',
        },
      ],
      run: (project, args) => {
        const fileName = String(args.fileName || 'sound-rules.dot');
        const dot = toGraphvizDot(project);
        return {
          success: true,
          message: `Exported ${fileName}`,
          outputFile: {
            fileName,
            content: dot,
            createdAt: new Date().toLocaleTimeString(),
            pluginName: 'Graphviz & Feature Exporter',
          },
        };
      },
    },
    {
      pluginId: 'graphviz',
      pluginName: 'Graphviz & Feature Exporter',
      actionId: 'export-features-csv',
      label: 'Export phoneme features as CSV',
      category: 'Export',
      arguments: [
        {
          name: 'fileName',
          label: 'File name',
          type: 'string',
          defaultValue: 'phoneme-features.csv',
          required: true,
          description: 'Output file name for the feature matrix CSV.',
        },
      ],
      run: (project, args) => {
        const fileName = String(args.fileName || 'phoneme-features.csv');
        const csv = toFeaturesCsv(project);
        return {
          success: true,
          message: `Exported ${fileName}`,
          outputFile: {
            fileName,
            content: csv,
            createdAt: new Date().toLocaleTimeString(),
            pluginName: 'Graphviz & Feature Exporter',
          },
        };
      },
    },
  ];

  return { plugins, actions };
}

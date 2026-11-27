import {
  AnchorPoint,
  FeaturicAssemblyRule,
  FlowDirection,
  GlyphDefinition,
  GraphemeToken,
  WritingSystemConfig,
} from './types';

export interface ShapedGlyph {
  index: number;
  token: GraphemeToken;
  x: number;
  y: number;
  svgPath: string;
  extraPaths: string[];
  enteringWorld: AnchorPoint;
  exitingWorld: AnchorPoint;
  variantUsed: 'isolated' | 'initial' | 'medial' | 'final';
  appliedFeatures: string[];
}

export interface ShapingTraceEntry {
  step: number;
  tokenId: string;
  variant: string;
  anchorMode: string;
  position: string;
}

export interface ShapingResult {
  glyphs: ShapedGlyph[];
  trace: ShapingTraceEntry[];
  bounds: { width: number; height: number };
}

function g(
  id: string,
  label: string,
  svgPath: string,
  opts?: Partial<GlyphDefinition>
): GlyphDefinition {
  return {
    id,
    label,
    svgPath,
    enteringAnchor: opts?.enteringAnchor ?? { x: 15, y: 55 },
    exitingAnchor: opts?.exitingAnchor ?? { x: 85, y: 55 },
    advanceWidth: opts?.advanceWidth ?? 72,
    advanceHeight: opts?.advanceHeight ?? 76,
    ...(opts?.initialPath ? { initialPath: opts.initialPath } : {}),
    ...(opts?.medialPath ? { medialPath: opts.medialPath } : {}),
    ...(opts?.finalPath ? { finalPath: opts.finalPath } : {}),
  };
}

// Clean geometric 100x100 glyph paths with natural #entering (left/top) & #exiting (right/bottom) anchors
const DEFAULT_GLYPHS: Record<string, GlyphDefinition> = {
  a: g('a', 'a', 'M 15 55 C 15 30, 55 25, 65 45 L 65 70 C 65 75, 80 75, 85 55', { advanceWidth: 75 }),
  b: g('b', 'b', 'M 15 55 L 30 16 L 30 72 C 68 72, 72 34, 30 44 M 64 58 L 85 55'),
  c: g('c', 'c', 'M 15 55 C 35 30, 70 28, 72 38 M 36 45 C 25 68, 65 72, 85 55', { advanceWidth: 70 }),
  d: g('d', 'd', 'M 15 55 C 22 34, 64 34, 64 56 C 64 74, 24 74, 30 50 M 64 16 L 64 72 L 85 55'),
  e: g('e', 'e', 'M 15 55 L 70 50 C 75 30, 35 25, 30 50 C 25 75, 70 75, 85 55', { advanceWidth: 75 }),
  f: g('f', 'f', 'M 15 55 C 35 35, 50 15, 65 18 L 45 85 M 28 45 L 85 55', { advanceHeight: 80 }),
  g: g('g', 'g', 'M 15 55 C 22 32, 62 32, 62 52 C 62 70, 26 70, 26 52 M 62 38 L 62 82 C 62 92, 32 92, 28 80 L 85 55', { advanceHeight: 82 }),
  h: g('h', 'h', 'M 15 55 L 28 16 L 28 72 M 28 48 C 44 28, 66 32, 66 68 L 85 55'),
  i: g('i', 'i', 'M 15 55 C 35 55, 45 30, 50 25 C 50 45, 55 70, 85 55 M 48 12 A 4 4 0 1 0 56 12 A 4 4 0 1 0 48 12', { advanceWidth: 65 }),
  j: g('j', 'j', 'M 15 55 L 54 34 L 54 80 C 54 92, 28 92, 24 80 M 50 14 A 4 4 0 1 0 58 14 A 4 4 0 1 0 50 14 M 54 56 L 85 55', { advanceWidth: 66, advanceHeight: 82 }),
  k: g('k', 'k', 'M 15 55 L 35 15 L 35 72 M 66 30 L 35 52 L 85 55', { advanceWidth: 75 }),
  l: g('l', 'l', 'M 15 55 C 35 45, 55 12, 48 12 C 40 12, 42 70, 85 55', { advanceWidth: 68 }),
  m: g('m', 'm', 'M 15 55 L 25 32 L 35 65 L 50 32 L 62 65 L 85 55', { advanceWidth: 78 }),
  n: g('n', 'n', 'M 15 55 L 32 30 L 34 68 M 34 45 C 48 25, 65 28, 66 65 L 85 55', { advanceWidth: 75 }),
  o: g('o', 'o', 'M 15 55 C 25 35, 65 30, 65 52 C 65 72, 30 72, 30 52 C 30 42, 60 55, 85 55', { advanceWidth: 75 }),
  p: g('p', 'p', 'M 15 55 L 35 28 L 35 88 M 35 38 C 68 25, 72 60, 35 58 L 85 55', { advanceWidth: 75, advanceHeight: 80 }),
  q: g('q', 'q', 'M 15 55 C 24 32, 64 32, 64 52 C 64 70, 26 70, 26 52 M 64 36 L 64 88 L 85 55', { advanceHeight: 82 }),
  r: g('r', 'r', 'M 15 55 L 35 30 L 35 68 M 35 44 C 48 28, 65 28, 70 40 L 85 55', { advanceWidth: 70 }),
  s: g('s', 's', 'M 15 55 L 42 30 C 65 38, 25 62, 55 70 L 85 55', { advanceWidth: 70 }),
  sh: g('sh', 'sh (ʃ)', 'M 15 55 C 30 22, 48 22, 52 55 C 56 80, 72 80, 85 55 M 32 18 L 68 18', { advanceWidth: 78 }),
  t: g('t', 't', 'M 15 55 L 45 15 L 48 68 C 52 74, 72 70, 85 55 M 28 32 L 66 32', { advanceWidth: 70 }),
  u: g('u', 'u', 'M 15 55 L 32 30 L 35 65 C 40 72, 62 72, 65 30 L 68 65 L 85 55', { advanceWidth: 75 }),
  v: g('v', 'v', 'M 15 55 L 30 32 L 50 70 L 68 32 L 85 55', { advanceWidth: 70 }),
  w: g('w', 'w', 'M 15 55 L 24 32 L 36 70 L 50 40 L 64 70 L 74 32 L 85 55', { advanceWidth: 78 }),
  x: g('x', 'x', 'M 15 55 L 32 30 L 68 70 M 68 30 L 32 70 L 85 55', { advanceWidth: 72 }),
  y: g('y', 'y', 'M 15 55 L 30 32 L 48 62 M 68 32 L 42 88 L 85 55', { advanceHeight: 82 }),
  z: g('z', 'z', 'M 15 55 L 32 32 L 66 32 L 32 68 L 85 55', { advanceWidth: 75 }),
  "'": g("'", "'", 'M 15 55 M 48 16 C 54 24, 52 34, 44 38 M 60 55 L 85 55', { advanceWidth: 42 }),
  '-': g('-', '-', 'M 15 55 L 32 55 M 36 52 L 64 52 M 68 55 L 85 55', { advanceWidth: 48 }),
};

function createLatinPrintGlyphs(): Record<string, GlyphDefinition> {
  const glyphs = structuredClone(DEFAULT_GLYPHS);
  // Crisp, readable print paths (without forced baseline tail connectors when unbonded)
  glyphs.a = g('a', 'a', 'M 32 38 C 46 28, 66 32, 66 52 L 66 72 M 66 50 C 30 48, 28 74, 66 70');
  glyphs.b = g('b', 'b', 'M 30 16 L 30 72 M 30 44 C 72 34, 72 74, 30 70');
  glyphs.c = g('c', 'c', 'M 68 38 C 30 26, 28 76, 68 64');
  glyphs.d = g('d', 'd', 'M 66 16 L 66 72 M 66 44 C 24 34, 24 74, 66 70');
  glyphs.e = g('e', 'e', 'M 30 52 L 68 52 C 70 30, 30 28, 30 52 C 30 74, 62 74, 68 64');
  glyphs.f = g('f', 'f', 'M 64 20 C 48 14, 42 22, 42 74 M 28 42 L 62 42');
  glyphs.g = g('g', 'g', 'M 66 36 C 28 28, 28 70, 66 62 M 66 34 L 66 80 C 66 94, 32 94, 28 80', { advanceHeight: 84 });
  glyphs.h = g('h', 'h', 'M 30 16 L 30 72 M 30 48 C 42 32, 68 32, 68 72');
  glyphs.i = g('i', 'i', 'M 50 34 L 50 72 M 46 16 A 4 4 0 1 0 54 16 A 4 4 0 1 0 46 16', { advanceWidth: 52 });
  glyphs.j = g('j', 'j', 'M 54 34 L 54 80 C 54 94, 30 92, 26 80 M 50 16 A 4 4 0 1 0 58 16 A 4 4 0 1 0 50 16', { advanceWidth: 56, advanceHeight: 84 });
  glyphs.k = g('k', 'k', 'M 32 16 L 32 72 M 68 34 L 32 54 L 68 72');
  glyphs.l = g('l', 'l', 'M 48 16 L 48 68 C 48 74, 58 74, 62 70', { advanceWidth: 52 });
  glyphs.m = g('m', 'm', 'M 22 72 L 22 36 C 34 28, 48 34, 48 72 M 48 44 C 58 28, 74 34, 74 72', { advanceWidth: 78 });
  glyphs.n = g('n', 'n', 'M 30 34 L 30 72 M 30 46 C 44 28, 68 32, 68 72');
  glyphs.o = g('o', 'o', 'M 50 32 C 74 32, 74 72, 50 72 C 26 72, 26 32, 50 32');
  glyphs.p = g('p', 'p', 'M 32 34 L 32 90 M 32 42 C 72 30, 72 72, 32 68', { advanceHeight: 84 });
  glyphs.q = g('q', 'q', 'M 66 36 C 28 28, 28 72, 66 66 M 66 34 L 66 90', { advanceHeight: 84 });
  glyphs.r = g('r', 'r', 'M 34 34 L 34 72 M 34 46 C 44 30, 64 30, 68 38', { advanceWidth: 64 });
  glyphs.s = g('s', 's', 'M 64 38 C 36 26, 26 48, 50 52 C 74 56, 62 78, 30 66', { advanceWidth: 68 });
  glyphs.t = g('t', 't', 'M 46 18 L 46 68 C 46 74, 60 74, 66 68 M 30 34 L 64 34', { advanceWidth: 64 });
  glyphs.u = g('u', 'u', 'M 30 34 L 30 62 C 30 76, 66 76, 66 62 L 66 34');
  glyphs.v = g('v', 'v', 'M 28 34 L 50 72 L 72 34', { advanceWidth: 68 });
  glyphs.w = g('w', 'w', 'M 20 34 L 34 72 L 50 42 L 66 72 L 80 34', { advanceWidth: 78 });
  glyphs.x = g('x', 'x', 'M 28 34 L 70 72 M 70 34 L 28 72', { advanceWidth: 68 });
  glyphs.y = g('y', 'y', 'M 28 34 L 50 66 M 70 34 L 38 90', { advanceHeight: 84 });
  glyphs.z = g('z', 'z', 'M 28 34 L 70 34 L 28 72 L 70 72', { advanceWidth: 68 });
  glyphs["'"] = g("'", "'", 'M 48 16 C 54 24, 52 34, 44 38', { advanceWidth: 34 });
  glyphs['-'] = g('-', '-', 'M 30 52 L 70 52', { advanceWidth: 46 });
  // Digraphs shared across Latin presets
  glyphs.ch = g('ch', 'ch', 'M 44 38 C 18 28, 18 74, 44 66 M 56 18 L 56 72 M 56 48 C 68 34, 84 36, 84 72', { advanceWidth: 82 });
  glyphs.sh = g('sh', 'sh', 'M 44 38 C 20 30, 18 48, 34 52 C 50 56, 42 74, 20 66 M 58 18 L 58 72 M 58 48 C 70 34, 86 36, 86 72', { advanceWidth: 84 });
  glyphs.th = g('th', 'th', 'M 32 20 L 32 70 M 20 36 L 44 36 M 56 18 L 56 72 M 56 48 C 68 34, 84 36, 84 72', { advanceWidth: 82 });
  glyphs.ng = g('ng', 'ng', 'M 20 72 L 20 36 C 32 28, 46 34, 46 72 M 80 38 C 54 30, 54 68, 80 62 M 80 34 L 80 82 C 80 92, 54 92, 52 82', { advanceWidth: 84 });
  glyphs.ee = g('ee', 'ee', 'M 18 52 L 44 52 C 46 34, 18 32, 18 52 C 18 72, 40 72, 44 64 M 56 52 L 82 52 C 84 34, 56 32, 56 52 C 56 72, 78 72, 82 64', { advanceWidth: 84 });
  glyphs.oo = g('oo', 'oo', 'M 32 34 C 48 34, 48 70, 32 70 C 16 70, 16 34, 32 34 M 68 34 C 84 34, 84 70, 68 70 C 52 70, 52 34, 68 34', { advanceWidth: 84 });
  return glyphs;
}

function createSpanishWritingSystem(): WritingSystemConfig {
  const glyphs = structuredClone(DEFAULT_GLYPHS);
  // Add Spanish-specific letters & Romance cursive forms
  glyphs.b = g('b', 'b', 'M 15 55 L 30 16 L 30 70 C 66 70, 68 36, 30 44 L 85 55');
  glyphs.d = g('d', 'd', 'M 15 55 C 24 34, 62 34, 62 56 C 62 72, 24 72, 30 50 M 62 16 L 62 70 L 85 55');
  glyphs.f = g('f', 'f', 'M 15 55 C 35 35, 50 15, 65 18 L 45 85 M 28 45 L 85 55');
  glyphs.g = g('g', 'g', 'M 15 55 C 22 34, 60 34, 60 54 C 60 70, 26 70, 26 52 M 60 36 L 60 82 C 60 92, 32 92, 28 80 L 85 55');
  glyphs.h = g('h', 'h', 'M 15 55 L 28 16 L 28 70 M 28 48 C 44 30, 64 34, 66 68 L 85 55');
  glyphs.ñ = g('ñ', 'ñ', 'M 15 55 L 32 30 L 34 68 M 34 45 C 48 25, 65 28, 66 65 L 85 55 M 28 18 Q 42 10, 50 18 Q 58 26, 72 18', { advanceWidth: 75 });
  glyphs.á = g('á', 'á', `${glyphs.a.svgPath} M 42 20 L 62 10`, { advanceWidth: 75 });
  glyphs.é = g('é', 'é', `${glyphs.e.svgPath} M 42 20 L 62 10`, { advanceWidth: 75 });
  glyphs.í = g('í', 'í', 'M 15 55 C 35 55, 45 30, 50 25 C 50 45, 55 70, 85 55 M 44 18 L 62 8', { advanceWidth: 65 });
  glyphs.ó = g('ó', 'ó', `${glyphs.o.svgPath} M 42 20 L 62 10`, { advanceWidth: 75 });
  glyphs.ú = g('ú', 'ú', `${glyphs.u.svgPath} M 42 20 L 62 10`, { advanceWidth: 75 });
  glyphs.ü = g('ü', 'ü', `${glyphs.u.svgPath} M 36 16 A 3 3 0 1 0 42 16 A 3 3 0 1 0 36 16 M 56 16 A 3 3 0 1 0 62 16 A 3 3 0 1 0 56 16`, { advanceWidth: 75 });
  glyphs.ll = g('ll', 'll', 'M 15 55 C 28 45, 40 14, 34 14 C 28 14, 30 68, 52 55 C 62 45, 72 14, 66 14 C 60 14, 62 68, 85 55', { advanceWidth: 78 });
  glyphs.rr = g('rr', 'rr', 'M 15 55 L 26 32 L 26 68 M 26 44 C 36 30, 48 30, 50 55 L 58 32 L 58 68 M 58 44 C 68 30, 78 30, 85 55', { advanceWidth: 78 });
  glyphs.ch = g('ch', 'ch', 'M 15 55 C 30 34, 48 34, 46 46 C 30 48, 28 70, 52 58 L 58 18 L 58 70 M 58 48 C 68 34, 78 36, 85 55', { advanceWidth: 80 });

  return {
    name: 'Romance Calligraphic Cursive (Español)',
    flowDirection: 'LTR',
    cursiveBonding: true,
    contextualVariants: true,
    glyphScale: 0.68,
    letterSpacing: 3,
    lineSpacing: 86,
    wrapLimit: 680,
    glyphs,
    featuricRules: [
      {
        id: 'spanish-final-sweep',
        name: 'Word-Final Romance Flourish',
        targetGlyphId: '*',
        contextPosition: 'final',
        appendSvgPath: 'M 80 50 Q 94 36 90 68',
        offsetX: 0,
        offsetY: 0,
        enabled: true,
      },
    ],
  };
}

function createGermanWritingSystem(): WritingSystemConfig {
  const glyphs = createLatinPrintGlyphs();
  // Angular Fraktur-inspired accents & German letters (ä, ö, ü, ß, sch)
  glyphs.a = g('a', 'a', 'M 32 38 L 50 28 L 66 38 L 66 72 M 66 48 L 32 54 L 32 72 L 66 68');
  glyphs.e = g('e', 'e', 'M 30 52 L 68 48 L 62 32 L 34 32 L 30 52 L 34 72 L 66 66');
  glyphs.h = g('h', 'h', 'M 30 16 L 30 72 M 30 46 L 52 32 L 68 42 L 64 74');
  glyphs.k = g('k', 'k', 'M 32 16 L 32 72 M 68 32 L 32 52 L 68 72 M 26 20 L 40 16');
  glyphs.s = g('s', 's', 'M 64 36 L 38 30 L 32 46 L 64 56 L 58 72 L 30 66', { advanceWidth: 68 });
  glyphs.ä = g('ä', 'ä', `${glyphs.a.svgPath} M 36 16 L 42 20 L 36 24 L 30 20 M 60 16 L 66 20 L 60 24 L 54 20`);
  glyphs.ö = g('ö', 'ö', `${glyphs.o.svgPath} M 36 16 L 42 20 L 36 24 L 30 20 M 60 16 L 66 20 L 60 24 L 54 20`);
  glyphs.ü = g('ü', 'ü', `${glyphs.u.svgPath} M 36 16 L 42 20 L 36 24 L 30 20 M 60 16 L 66 20 L 60 24 L 54 20`);
  glyphs.ß = g('ß', 'ß', 'M 30 82 L 30 26 C 34 14, 68 14, 64 34 L 46 44 C 74 48, 74 74, 42 70', { advanceWidth: 72, advanceHeight: 82 });
  glyphs.sch = g('sch', 'sch', 'M 34 36 L 16 32 L 14 48 L 34 56 L 26 70 M 58 38 L 40 34 L 40 68 L 58 66 M 68 18 L 68 72 M 68 46 L 86 36 L 86 72', { advanceWidth: 88 });
  glyphs.pf = g('pf', 'pf', 'M 20 34 L 20 88 M 20 40 L 46 34 L 46 64 L 20 64 M 78 20 L 62 18 L 62 78 M 50 42 L 78 42', { advanceWidth: 84 });

  return {
    name: 'Geometric Fraktur-Latin (Deutsch)',
    flowDirection: 'LTR',
    cursiveBonding: false,
    contextualVariants: true,
    glyphScale: 0.68,
    letterSpacing: 4,
    lineSpacing: 86,
    wrapLimit: 680,
    glyphs,
    featuricRules: [
      {
        id: 'fraktur-initial-crest',
        name: 'Initial Blackletter Crest',
        targetGlyphId: '*',
        contextPosition: 'initial',
        appendSvgPath: 'M 14 26 L 26 16 L 34 22',
        offsetX: 0,
        offsetY: 0,
        enabled: true,
      },
    ],
  };
}

function createMandarinWritingSystem(): WritingSystemConfig {
  const glyphs: Record<string, GlyphDefinition> = {};
  const vg = (id: string, label: string, svgPath: string) =>
    g(id, label, svgPath, {
      enteringAnchor: { x: 50, y: 10 },
      exitingAnchor: { x: 50, y: 90 },
      advanceWidth: 76,
      advanceHeight: 80,
    });

  // Square Sinographic / brush-stroke blocks with vertical top-to-bottom anchors
  const baseCons: Array<[string, string]> = [
    ['b', 'M 24 26 L 76 26 L 72 74 L 28 74 L 24 26 M 50 18 L 50 82'],
    ['p', 'M 24 24 L 76 24 M 36 24 L 36 78 M 64 24 L 64 78 M 22 52 L 78 52'],
    ['m', 'M 22 24 L 78 24 M 28 24 L 28 78 M 50 24 L 50 78 M 72 24 L 72 78'],
    ['f', 'M 24 28 L 76 28 M 50 18 L 50 82 M 26 54 L 74 54'],
    ['d', 'M 24 26 L 76 26 M 50 18 L 50 80 L 38 72'],
    ['t', 'M 20 38 L 80 38 M 50 16 L 50 82 M 28 60 L 72 78'],
    ['n', 'M 28 24 L 28 78 M 28 46 L 72 46 L 72 78'],
    ['l', 'M 34 20 L 34 76 M 66 20 L 66 80 L 54 70'],
    ['g', 'M 26 24 L 74 24 M 50 24 L 32 76 M 44 50 L 74 50 L 74 78'],
    ['k', 'M 28 22 L 28 80 M 28 48 L 72 26 M 46 42 L 76 78'],
    ['h', 'M 22 28 L 78 28 M 36 28 L 28 78 M 64 28 L 74 78 M 28 56 L 72 56'],
    ['j', 'M 26 28 L 74 28 M 50 18 L 50 78 C 50 86, 32 84, 28 74'],
    ['q', 'M 24 26 L 76 26 L 70 64 M 50 18 L 50 82 M 26 52 L 74 52'],
    ['x', 'M 26 28 L 74 76 M 74 28 L 26 76 M 24 52 L 76 52'],
    ['zh', 'M 24 24 L 76 24 L 76 52 L 24 52 L 24 78 L 76 78 M 50 16 L 50 84'],
    ['ch', 'M 22 32 L 78 32 M 36 20 L 36 74 M 64 20 L 64 74 M 24 68 L 76 68'],
    ['sh', 'M 24 22 L 24 78 M 50 22 L 50 78 M 76 22 L 76 78 M 22 76 L 78 76'],
    ['r', 'M 28 24 L 72 24 L 72 76 L 28 76 L 28 24 M 28 50 L 72 50'],
    ['z', 'M 26 26 L 74 26 L 26 74 L 74 74'],
    ['c', 'M 26 28 L 74 28 L 70 74 L 26 74'],
    ['s', 'M 32 24 L 72 24 L 50 52 L 74 78 L 28 78'],
    ['w', 'M 22 28 L 40 76 L 50 44 L 60 76 L 78 28'],
    ['y', 'M 26 24 L 50 52 M 74 24 L 50 52 L 50 82'],
  ];
  for (const [id, path] of baseCons) glyphs[id] = vg(id, id, path);

  const toneMarks: Record<string, string> = {
    '': '',
    '1': 'M 30 14 L 70 14',
    '2': 'M 34 18 L 68 10',
    '3': 'M 32 10 L 50 20 L 68 10',
    '4': 'M 32 10 L 68 18',
  };
  const baseVowels: Array<[string, string, Array<[string, string]>]> = [
    ['a', 'M 28 32 L 72 32 M 50 24 L 50 80 M 24 58 L 76 58', [['a', ''], ['ā', '1'], ['á', '2'], ['ǎ', '3'], ['à', '4']]],
    ['o', 'M 30 30 L 70 30 L 70 74 L 30 74 L 30 30', [['o', ''], ['ō', '1'], ['ó', '2'], ['ǒ', '3'], ['ò', '4']]],
    ['e', 'M 26 32 L 74 32 M 26 52 L 70 52 M 26 74 L 74 74 M 36 32 L 36 74', [['e', ''], ['ē', '1'], ['é', '2'], ['ě', '3'], ['è', '4']]],
    ['i', 'M 50 24 L 50 80 M 30 78 L 70 78', [['i', ''], ['ī', '1'], ['í', '2'], ['ǐ', '3'], ['ì', '4']]],
    ['u', 'M 28 32 L 28 74 L 72 74 L 72 32', [['u', ''], ['ū', '1'], ['ú', '2'], ['ǔ', '3'], ['ù', '4']]],
  ];
  for (const [, basePath, forms] of baseVowels) {
    for (const [charId, tone] of forms) {
      const mark = toneMarks[tone];
      glyphs[charId] = vg(charId, charId, mark ? `${basePath} ${mark}` : basePath);
    }
  }
  glyphs.ü = vg('ü', 'ü', 'M 28 32 L 28 74 L 72 74 L 72 32 M 38 20 A 3 3 0 1 0 44 20 A 3 3 0 1 0 38 20 M 56 20 A 3 3 0 1 0 62 20 A 3 3 0 1 0 56 20');
  glyphs.ɤ = vg('ɤ', 'ɤ', 'M 26 32 L 74 32 M 26 52 L 70 52 M 26 74 L 74 74');
  glyphs['-'] = vg('-', '-', 'M 50 28 L 50 72');

  return {
    name: 'Vertical Sinographic Grid (普通话)',
    flowDirection: 'TTB_RTL',
    cursiveBonding: false,
    contextualVariants: true,
    glyphScale: 0.68,
    letterSpacing: 8,
    lineSpacing: 86,
    wrapLimit: 380,
    glyphs,
    featuricRules: [
      {
        id: 'sinographic-hook',
        name: 'Brush Hook Terminal',
        targetGlyphId: '*',
        contextPosition: 'final',
        appendSvgPath: 'M 72 74 L 84 64',
        offsetX: 0,
        offsetY: 0,
        enabled: true,
      },
    ],
  };
}

function createArabicWritingSystem(): WritingSystemConfig {
  const glyphs: Record<string, GlyphDefinition> = {};
  const ar = (
    id: string,
    label: string,
    iso: string,
    init?: string,
    med?: string,
    fin?: string
  ) =>
    g(id, label, iso, {
      initialPath: init,
      medialPath: med,
      finalPath: fin,
      enteringAnchor: { x: 15, y: 58 },
      exitingAnchor: { x: 85, y: 58 },
      advanceWidth: 70,
      advanceHeight: 80,
    });

  glyphs.a = ar('a', 'ا (a)', 'M 50 14 L 50 72', 'M 50 14 L 50 72', 'M 50 14 L 50 58 L 85 58', 'M 50 14 L 50 58 L 85 58');
  glyphs.aa = ar('aa', 'آ (aa)', 'M 50 16 L 50 74 M 36 10 Q 50 4 64 10', undefined, 'M 50 16 L 50 58 L 85 58', 'M 50 16 L 50 58 L 85 58');
  glyphs.b = ar(
    'b',
    'ب (b)',
    'M 22 44 C 22 72, 78 72, 78 44 M 48 80 A 3 3 0 1 0 54 80 A 3 3 0 1 0 48 80',
    'M 15 58 L 58 58 L 62 36 M 56 76 A 3 3 0 1 0 62 76 A 3 3 0 1 0 56 76',
    'M 15 58 L 50 58 L 52 38 L 54 58 L 85 58 M 48 76 A 3 3 0 1 0 54 76 A 3 3 0 1 0 48 76',
    'M 20 44 C 22 72, 72 72, 74 58 L 85 58 M 48 80 A 3 3 0 1 0 54 80 A 3 3 0 1 0 48 80'
  );
  glyphs.t = ar(
    't',
    'ت (t)',
    'M 22 44 C 22 72, 78 72, 78 44 M 40 34 A 3 3 0 1 0 46 34 A 3 3 0 1 0 40 34 M 54 34 A 3 3 0 1 0 60 34 A 3 3 0 1 0 54 34',
    'M 15 58 L 58 58 L 62 36 M 52 24 A 3 3 0 1 0 58 24 A 3 3 0 1 0 52 24 M 64 24 A 3 3 0 1 0 70 24 A 3 3 0 1 0 64 24',
    'M 15 58 L 50 58 L 52 38 L 54 58 L 85 58 M 44 26 A 3 3 0 1 0 50 26 A 3 3 0 1 0 44 26 M 56 26 A 3 3 0 1 0 62 26 A 3 3 0 1 0 56 26',
    'M 20 44 C 22 72, 72 72, 74 58 L 85 58 M 40 34 A 3 3 0 1 0 46 34 A 3 3 0 1 0 40 34 M 54 34 A 3 3 0 1 0 60 34 A 3 3 0 1 0 54 34'
  );
  glyphs.th = ar(
    'th',
    'ث (th)',
    'M 22 44 C 22 72, 78 72, 78 44 M 40 36 A 3 3 0 1 0 46 36 A 3 3 0 1 0 40 36 M 54 36 A 3 3 0 1 0 60 36 A 3 3 0 1 0 54 36 M 47 26 A 3 3 0 1 0 53 26 A 3 3 0 1 0 47 26'
  );
  glyphs.j = ar(
    'j',
    'ج (j)',
    'M 34 32 L 72 32 C 30 48, 28 84, 72 78 M 48 56 A 3 3 0 1 0 54 56 A 3 3 0 1 0 48 56',
    'M 15 58 L 48 58 L 68 38 L 40 38 M 48 72 A 3 3 0 1 0 54 72 A 3 3 0 1 0 48 72',
    'M 15 58 L 85 58 M 42 42 L 66 42 L 46 58 M 48 72 A 3 3 0 1 0 54 72 A 3 3 0 1 0 48 72'
  );
  glyphs.h = ar('h', 'ح/ه (h)', 'M 36 32 L 72 32 C 30 48, 28 84, 72 78', 'M 15 58 L 52 58 L 68 38 L 40 38', 'M 15 58 L 85 58 M 42 42 L 66 42 L 46 58');
  glyphs.kh = ar('kh', 'خ (kh)', 'M 36 36 L 72 36 C 30 50, 28 84, 72 78 M 50 20 A 3 3 0 1 0 56 20 A 3 3 0 1 0 50 20', 'M 15 58 L 52 58 L 68 38 L 40 38 M 50 22 A 3 3 0 1 0 56 22 A 3 3 0 1 0 50 22');
  glyphs.d = ar('d', 'د (d)', 'M 42 32 L 66 56 L 34 68', undefined, 'M 44 36 L 66 58 L 85 58', 'M 44 36 L 66 58 L 85 58');
  glyphs.dh = ar('dh', 'ذ (dh)', 'M 42 34 L 66 56 L 34 68 M 48 20 A 3 3 0 1 0 54 20 A 3 3 0 1 0 48 20');
  glyphs.r = ar('r', 'ر (r)', 'M 62 42 C 62 66, 44 82, 26 84', undefined, 'M 85 58 L 64 58 C 56 76, 40 84, 24 84', 'M 85 58 L 64 58 C 56 76, 40 84, 24 84');
  glyphs.z = ar('z', 'ز (z)', 'M 62 42 C 62 66, 44 82, 26 84 M 56 24 A 3 3 0 1 0 62 24 A 3 3 0 1 0 56 24');
  glyphs.s = ar(
    's',
    'س (s)',
    'M 78 42 L 72 56 L 62 42 L 56 56 L 46 42 C 42 74, 20 74, 20 50',
    'M 15 58 L 46 58 L 54 40 L 62 58 L 70 40 L 78 58',
    'M 15 58 L 42 58 L 50 40 L 58 58 L 66 40 L 74 58 L 85 58'
  );
  glyphs.sh = ar(
    'sh',
    'ش (sh)',
    'M 78 42 L 72 56 L 62 42 L 56 56 L 46 42 C 42 74, 20 74, 20 50 M 54 24 A 3 3 0 1 0 60 24 A 3 3 0 1 0 54 24 M 68 24 A 3 3 0 1 0 74 24 A 3 3 0 1 0 68 24 M 61 14 A 3 3 0 1 0 67 14 A 3 3 0 1 0 61 14'
  );
  glyphs.gh = ar('gh', 'غ (gh)', 'M 66 36 C 46 30, 44 48, 66 52 C 30 62, 32 88, 72 82 M 52 20 A 3 3 0 1 0 58 20 A 3 3 0 1 0 52 20');
  glyphs.f = ar('f', 'ف (f)', 'M 68 38 C 52 28, 48 52, 68 50 L 72 64 C 50 72, 24 68, 22 48 M 56 20 A 3 3 0 1 0 62 20 A 3 3 0 1 0 56 20', 'M 15 58 L 68 58 C 72 34, 52 34, 54 58 M 56 22 A 3 3 0 1 0 62 22 A 3 3 0 1 0 56 22');
  glyphs.q = ar('q', 'ق (q)', 'M 68 38 C 52 28, 48 52, 68 50 L 70 68 C 50 88, 22 82, 22 52 M 48 20 A 3 3 0 1 0 54 20 A 3 3 0 1 0 48 20 M 62 20 A 3 3 0 1 0 68 20 A 3 3 0 1 0 62 20');
  glyphs.k = ar('k', 'ك (k)', 'M 68 20 L 68 68 L 24 68 L 24 54 M 44 38 L 54 46 L 44 52', 'M 15 58 L 58 58 L 46 38 L 68 22');
  glyphs.l = ar('l', 'ل (l)', 'M 64 16 L 64 68 C 60 88, 24 88, 24 58', 'M 15 58 L 62 58 L 62 16', 'M 15 58 L 54 58 L 54 16 L 54 58 L 85 58');
  glyphs.m = ar('m', 'م (m)', 'M 54 46 C 72 34, 74 62, 54 58 L 32 60 L 32 88', 'M 15 58 L 52 58 C 68 42, 68 62, 52 58', 'M 15 58 L 52 58 C 66 42, 66 62, 52 58 L 85 58');
  glyphs.n = ar('n', 'ن (n)', 'M 72 44 C 72 82, 26 82, 26 44 M 48 32 A 3 3 0 1 0 54 32 A 3 3 0 1 0 48 32', 'M 15 58 L 58 58 L 62 36 M 56 22 A 3 3 0 1 0 62 22 A 3 3 0 1 0 56 22', 'M 15 58 L 50 58 L 52 38 L 54 58 L 85 58 M 48 24 A 3 3 0 1 0 54 24 A 3 3 0 1 0 48 24');
  glyphs.w = ar('w', 'و (w)', 'M 60 40 C 44 34, 42 56, 62 54 C 62 74, 40 84, 24 80', undefined, 'M 85 58 L 62 58 C 46 42, 46 58, 62 58 C 58 76, 38 84, 24 80');
  glyphs.y = ar('y', 'ي (y)', 'M 66 40 C 46 38, 46 54, 68 58 C 68 82, 24 84, 24 56 M 42 84 A 3 3 0 1 0 48 84 A 3 3 0 1 0 42 84 M 56 84 A 3 3 0 1 0 62 84 A 3 3 0 1 0 56 84');
  glyphs.i = ar('i', 'ي/i', glyphs.y.svgPath, 'M 15 58 L 58 58 L 62 36 M 52 76 A 3 3 0 1 0 58 76 A 3 3 0 1 0 52 76 M 64 76 A 3 3 0 1 0 70 76 A 3 3 0 1 0 64 76');
  glyphs.ii = ar('ii', 'ي (ii)', glyphs.i.svgPath, glyphs.i.initialPath);
  glyphs.u = ar('u', 'و/u', glyphs.w.svgPath, undefined, glyphs.w.medialPath);
  glyphs.uu = ar('uu', 'و (uu)', glyphs.w.svgPath, undefined, glyphs.w.medialPath);
  glyphs.e = ar('e', 'e', 'M 15 58 L 85 58 M 42 32 L 62 24');
  glyphs.o = ar('o', 'o', 'M 15 58 L 85 58 M 50 28 C 60 28, 60 40, 50 40 L 42 46');
  glyphs["'"] = ar("'", "ء/ع (')", 'M 58 34 C 38 30, 38 52, 62 54 C 34 62, 34 86, 68 80');
  glyphs['-'] = ar('-', '-', 'M 15 58 L 85 58', undefined, undefined, 'M 25 58 L 85 58');

  return {
    name: 'Arabic RTL Cursive Abjad (العربية)',
    flowDirection: 'RTL',
    cursiveBonding: true,
    contextualVariants: true,
    glyphScale: 0.72,
    letterSpacing: 2,
    lineSpacing: 90,
    wrapLimit: 680,
    glyphs,
    featuricRules: [
      {
        id: 'arabic-final-tail',
        name: 'Final Calligraphic Sweep',
        targetGlyphId: '*',
        contextPosition: 'final',
        appendSvgPath: 'M 24 58 Q 10 74 30 82',
        offsetX: 0,
        offsetY: 0,
        enabled: true,
      },
    ],
  };
}

function createJapaneseWritingSystem(): WritingSystemConfig {
  const glyphs: Record<string, GlyphDefinition> = {};
  const kana = (id: string, label: string, svgPath: string) =>
    g(id, label, svgPath, {
      enteringAnchor: { x: 50, y: 10 },
      exitingAnchor: { x: 50, y: 90 },
      advanceWidth: 74,
      advanceHeight: 80,
    });

  const dakuten = 'M 74 18 L 82 28 M 84 14 L 92 24';
  const handakuten = 'M 78 20 A 5 5 0 1 0 88 20 A 5 5 0 1 0 78 20';

  glyphs.a = kana('a', 'あ (a)', 'M 24 30 L 74 28 M 50 16 L 46 78 M 68 42 C 68 78, 22 78, 26 56 C 30 40, 74 44, 72 74');
  glyphs.i = kana('i', 'い (i)', 'M 32 24 C 28 54, 34 72, 46 60 M 64 28 C 72 44, 74 62, 68 72');
  glyphs.u = kana('u', 'う (u)', 'M 42 18 L 58 26 M 28 42 C 74 30, 74 68, 40 82');
  glyphs.e = kana('e', 'え (e)', 'M 38 18 L 58 24 M 26 40 L 66 36 L 28 76 L 52 54 L 74 74');
  glyphs.o = kana('o', 'お (o)', 'M 24 32 L 62 30 M 44 18 L 44 76 C 32 76, 22 66, 36 56 C 54 44, 76 58, 64 78 M 70 22 L 80 30');
  glyphs.k = kana('k', 'か (k)', 'M 24 38 C 56 32, 60 66, 44 78 M 46 20 L 34 76 M 68 32 L 80 54');
  glyphs.g = kana('g', 'が (g)', `${glyphs.k.svgPath} ${dakuten}`);
  glyphs.s = kana('s', 'さ (s)', 'M 24 34 L 74 28 M 44 18 L 58 56 M 30 62 C 38 78, 60 78, 70 72');
  glyphs.z = kana('z', 'ざ (z)', `${glyphs.s.svgPath} ${dakuten}`);
  glyphs.sh = kana('sh', 'し (sh)', 'M 38 20 L 38 66 C 38 82, 68 80, 78 60');
  glyphs.j = kana('j', 'じ (j)', `${glyphs.sh.svgPath} ${dakuten}`);
  glyphs.t = kana('t', 'た (t)', 'M 22 32 L 58 28 M 42 18 L 30 78 M 52 46 L 74 44 M 50 68 C 58 76, 72 76, 78 72');
  glyphs.d = kana('d', 'だ (d)', `${glyphs.t.svgPath} ${dakuten}`);
  glyphs.ch = kana('ch', 'ち (ch)', 'M 24 32 L 72 28 M 46 18 L 38 52 C 74 42, 74 76, 40 80');
  glyphs.ts = kana('ts', 'つ (ts)', 'M 22 38 C 78 24, 80 68, 36 78');
  glyphs.n = kana('n', 'な (n)', 'M 22 30 L 54 28 M 38 18 L 28 62 M 66 28 L 78 36 M 60 44 L 60 74 C 48 80, 34 74, 42 64 C 52 56, 72 68, 76 74');
  glyphs.h = kana('h', 'は (h)', 'M 28 22 C 24 48, 26 68, 32 76 M 46 34 L 76 32 M 62 20 L 62 72 C 50 78, 40 72, 46 62 C 56 54, 72 64, 78 72');
  glyphs.b = kana('b', 'ば (b)', `${glyphs.h.svgPath} ${dakuten}`);
  glyphs.p = kana('p', 'ぱ (p)', `${glyphs.h.svgPath} ${handakuten}`);
  glyphs.f = kana('f', 'ふ (f)', 'M 44 18 L 56 26 M 38 42 C 54 46, 58 68, 44 76 M 22 58 L 32 72 M 68 54 L 80 70');
  glyphs.m = kana('m', 'ま (m)', 'M 26 30 L 72 28 M 28 48 L 70 46 M 50 18 L 50 74 C 36 80, 26 70, 36 62 C 48 56, 72 68, 76 74');
  glyphs.y = kana('y', 'や (y)', 'M 24 46 C 72 24, 78 58, 52 62 M 56 20 L 48 34 M 34 26 L 48 80');
  glyphs.r = kana('r', 'ら (r)', 'M 40 18 L 62 26 M 34 36 L 32 58 C 72 44, 74 76, 38 80');
  glyphs.w = kana('w', 'わ (w)', 'M 36 18 L 36 80 M 22 36 L 48 32 L 22 58 C 74 30, 78 68, 48 78');
  glyphs['ɴ'] = kana('ɴ', 'ん (ɴ)', 'M 48 20 L 24 76 M 38 56 C 52 44, 56 76, 76 68');
  glyphs.aa = kana('aa', 'あー', `${glyphs.a.svgPath} M 50 82 L 50 92`);
  glyphs.ii = kana('ii', 'いー', `${glyphs.i.svgPath} M 50 82 L 50 92`);
  glyphs.uu = kana('uu', 'うー', `${glyphs.u.svgPath} M 50 84 L 50 94`);
  glyphs.ee = kana('ee', 'えー', `${glyphs.e.svgPath} M 50 82 L 50 92`);
  glyphs.oo = kana('oo', 'おー', `${glyphs.o.svgPath} M 50 82 L 50 92`);
  glyphs['-'] = kana('-', 'ー', 'M 50 20 L 50 80');

  return {
    name: 'Vertical Kana-Style Tategaki (日本語)',
    flowDirection: 'TTB_RTL',
    cursiveBonding: false,
    contextualVariants: true,
    glyphScale: 0.68,
    letterSpacing: 8,
    lineSpacing: 86,
    wrapLimit: 380,
    glyphs,
    featuricRules: [
      {
        id: 'japanese-brush-sweep',
        name: 'Vertical Brush Sweep',
        targetGlyphId: '*',
        contextPosition: 'final',
        appendSvgPath: 'M 44 82 Q 62 92 78 76',
        offsetX: 0,
        offsetY: 0,
        enabled: true,
      },
    ],
  };
}

function createTurkishWritingSystem(): WritingSystemConfig {
  const glyphs = createLatinPrintGlyphs();
  // Distinct dotless ı vs dotted i, cedillas on ç/ş, breve on ğ, umlauts ö/ü
  glyphs.ı = g('ı', 'ı (dotless)', 'M 50 34 L 50 72', { advanceWidth: 50 });
  glyphs.ç = g('ç', 'ç', 'M 68 38 C 30 26, 28 74, 68 64 M 50 70 L 46 82 C 56 86, 56 92, 44 90', { advanceHeight: 86 });
  glyphs.ş = g('ş', 'ş', 'M 64 38 C 36 26, 26 48, 50 52 C 74 56, 62 76, 30 66 M 48 72 L 44 84 C 54 88, 54 94, 42 92', { advanceWidth: 68, advanceHeight: 86 });
  glyphs.ğ = g('ğ', 'ğ', 'M 66 36 C 28 28, 28 70, 66 62 M 66 34 L 66 80 C 66 94, 32 94, 28 80 M 34 18 Q 50 28 66 18', { advanceHeight: 86 });
  glyphs.ö = g('ö', 'ö', `${glyphs.o.svgPath} M 36 18 A 3 3 0 1 0 42 18 A 3 3 0 1 0 36 18 M 58 18 A 3 3 0 1 0 64 18 A 3 3 0 1 0 58 18`);
  glyphs.ü = g('ü', 'ü', `${glyphs.u.svgPath} M 36 18 A 3 3 0 1 0 42 18 A 3 3 0 1 0 36 18 M 58 18 A 3 3 0 1 0 64 18 A 3 3 0 1 0 58 18`);
  return {
    name: 'Harmonic Extended Latin (Türkçe)',
    flowDirection: 'LTR',
    cursiveBonding: false,
    contextualVariants: true,
    glyphScale: 0.68,
    letterSpacing: 4,
    lineSpacing: 86,
    wrapLimit: 680,
    glyphs,
    featuricRules: [],
  };
}

function createSwahiliWritingSystem(): WritingSystemConfig {
  const glyphs = createLatinPrintGlyphs();
  glyphs.ny = g('ny', 'ny', 'M 18 36 L 18 72 M 18 46 C 30 32, 46 34, 46 72 M 58 36 L 72 64 M 86 36 L 62 88', { advanceWidth: 84, advanceHeight: 84 });
  glyphs["ng'"] = g("ng'", "ng'", 'M 16 72 L 16 36 C 28 30, 42 36, 42 72 M 72 38 C 48 32, 48 68, 72 62 M 72 34 L 72 82 C 72 90, 48 90, 46 80 M 84 18 C 88 26, 84 34, 78 38', { advanceWidth: 86 });
  glyphs.dh = g('dh', 'dh', 'M 46 16 L 46 72 M 46 44 C 16 34, 16 74, 46 70 M 58 16 L 58 72 M 58 48 C 70 34, 86 36, 86 72', { advanceWidth: 84 });
  glyphs.mb = g('mb', 'mb', 'M 14 72 L 14 36 C 24 30, 34 36, 34 72 M 34 44 C 42 32, 52 36, 52 72 M 62 18 L 62 72 M 62 46 C 88 38, 88 72, 62 70', { advanceWidth: 86 });
  glyphs.nd = g('nd', 'nd', 'M 18 36 L 18 72 M 18 46 C 32 32, 46 34, 46 72 M 82 18 L 82 72 M 82 46 C 54 36, 54 72, 82 70', { advanceWidth: 84 });
  return {
    name: 'Coastal Bantu Script (Kiswahili)',
    flowDirection: 'LTR',
    cursiveBonding: false,
    contextualVariants: true,
    glyphScale: 0.68,
    letterSpacing: 4,
    lineSpacing: 86,
    wrapLimit: 680,
    glyphs,
    featuricRules: [],
  };
}

function createRussianWritingSystem(): WritingSystemConfig {
  const glyphs = createLatinPrintGlyphs();
  // Cyrillic-inspired geometric glyphs mapped to Russian orthography tokens!
  glyphs.a = g('a', 'А (a)', 'M 24 72 L 50 22 L 76 72 M 34 54 L 66 54');
  glyphs.b = g('b', 'Б (b)', 'M 28 24 L 72 24 M 28 24 L 28 72 L 66 72 C 74 52, 66 44, 28 46');
  glyphs.v = g('v', 'В (v)', 'M 28 22 L 28 72 M 28 22 C 70 22, 70 46, 28 46 C 74 46, 74 72, 28 72');
  glyphs.g = g('g', 'Г (g)', 'M 32 72 L 32 24 L 72 24');
  glyphs.d = g('d', 'Д (d)', 'M 26 64 L 38 24 L 64 24 L 72 64 M 18 64 L 82 64 M 22 64 L 22 78 M 78 64 L 78 78');
  glyphs.e = g('e', 'Е (e)', 'M 68 24 L 30 24 L 30 72 L 68 72 M 30 48 L 60 48');
  glyphs.zh = g('zh', 'Ж (zh)', 'M 50 22 L 50 74 M 22 24 L 78 72 M 78 24 L 22 72', { advanceWidth: 78 });
  glyphs.z = g('z', 'З (z)', 'M 28 28 C 72 18, 72 46, 40 48 C 74 50, 74 78, 28 68');
  glyphs.i = g('i', 'И (i)', 'M 28 24 L 28 72 L 72 24 L 72 72');
  glyphs.y = g('y', 'Ы/Й (y)', 'M 22 24 L 22 72 L 50 72 C 60 54, 54 46, 22 48 M 74 24 L 74 72', { advanceWidth: 78 });
  glyphs.k = g('k', 'К (k)', 'M 30 22 L 30 72 M 70 24 L 30 48 L 70 72');
  glyphs.l = g('l', 'Л (l)', 'M 22 72 C 30 68, 36 42, 42 24 L 72 24 L 72 72');
  glyphs.m = g('m', 'М (m)', 'M 24 72 L 24 24 L 50 56 L 76 24 L 76 72', { advanceWidth: 78 });
  glyphs.n = g('n', 'Н (n)', 'M 28 24 L 28 72 M 72 24 L 72 72 M 28 48 L 72 48');
  glyphs.o = g('o', 'О (o)', 'M 50 24 C 76 24, 76 72, 50 72 C 24 72, 24 24, 50 24');
  glyphs.p = g('p', 'П (p)', 'M 28 72 L 28 24 L 72 24 L 72 72');
  glyphs.r = g('r', 'Р (r)', 'M 30 24 L 30 74 M 30 24 C 72 24, 72 52, 30 52');
  glyphs.s = g('s', 'С (s)', 'M 70 30 C 26 18, 26 78, 70 66');
  glyphs.t = g('t', 'Т (t)', 'M 22 24 L 78 24 M 50 24 L 50 72');
  glyphs.u = g('u', 'У (u)', 'M 26 24 L 50 54 M 72 24 L 40 78');
  glyphs.f = g('f', 'Ф (f)', 'M 50 16 L 50 80 M 50 28 C 82 28, 82 64, 50 64 C 18 64, 18 28, 50 28', { advanceWidth: 78 });
  glyphs.kh = g('kh', 'Х (kh)', 'M 26 24 L 74 72 M 74 24 L 26 72');
  glyphs.ts = g('ts', 'Ц (ts)', 'M 26 24 L 26 68 L 70 68 L 70 24 M 70 68 L 76 82', { advanceWidth: 76 });
  glyphs.ch = g('ch', 'Ч (ch)', 'M 28 24 L 28 48 C 28 58, 68 58, 68 44 M 68 24 L 68 72');
  glyphs.sh = g('sh', 'Ш (sh)', 'M 22 24 L 22 72 L 78 72 L 78 24 M 50 24 L 50 72', { advanceWidth: 78 });
  glyphs["'"] = g("'", 'Ь (pal)', 'M 34 22 L 34 72 L 64 72 C 74 52, 64 46, 34 48', { advanceWidth: 64 });

  return {
    name: 'Cyrillic-Geometric Script (Русский)',
    flowDirection: 'LTR',
    cursiveBonding: false,
    contextualVariants: true,
    glyphScale: 0.68,
    letterSpacing: 4,
    lineSpacing: 86,
    wrapLimit: 680,
    glyphs,
    featuricRules: [],
  };
}

function createIndonesianWritingSystem(): WritingSystemConfig {
  const glyphs = createLatinPrintGlyphs();
  glyphs.ny = g('ny', 'ny', 'M 18 36 L 18 72 M 18 46 C 30 32, 46 34, 46 72 M 58 36 L 72 64 M 86 36 L 62 88', { advanceWidth: 84, advanceHeight: 84 });
  glyphs.sy = g('sy', 'sy', 'M 42 38 C 20 30, 18 48, 34 52 C 50 56, 42 74, 20 66 M 56 36 L 70 64 M 86 36 L 60 88', { advanceWidth: 84, advanceHeight: 84 });
  glyphs.kh = g('kh', 'kh', 'M 22 18 L 22 72 M 48 34 L 22 52 L 48 72 M 60 18 L 60 72 M 60 48 C 72 34, 86 36, 86 72', { advanceWidth: 84 });
  glyphs.ə = g('ə', 'ə', 'M 68 48 L 30 48 C 28 70, 68 72, 68 48 C 68 28, 36 28, 32 36');
  return {
    name: 'Nusantara Latin Script (Bahasa Indonesia)',
    flowDirection: 'LTR',
    cursiveBonding: false,
    contextualVariants: true,
    glyphScale: 0.68,
    letterSpacing: 4,
    lineSpacing: 86,
    wrapLimit: 680,
    glyphs,
    featuricRules: [],
  };
}

export function createLanguageWritingSystem(langId: string): WritingSystemConfig {
  switch (langId.toLowerCase()) {
    case 'english':
      return {
        name: 'Latin Print & Digraphs (English)',
        flowDirection: 'LTR',
        cursiveBonding: false,
        contextualVariants: true,
        glyphScale: 0.66,
        letterSpacing: 4,
        lineSpacing: 84,
        wrapLimit: 680,
        glyphs: createLatinPrintGlyphs(),
        featuricRules: [
          {
            id: 'english-serif-tick',
            name: 'Word-Final Serif Tick',
            targetGlyphId: '*',
            contextPosition: 'final',
            appendSvgPath: 'M 70 72 L 80 68',
            offsetX: 0,
            offsetY: 0,
            enabled: false,
          },
        ],
      };
    case 'spanish':
      return createSpanishWritingSystem();
    case 'german':
      return createGermanWritingSystem();
    case 'mandarin':
      return createMandarinWritingSystem();
    case 'arabic':
      return createArabicWritingSystem();
    case 'japanese':
      return createJapaneseWritingSystem();
    case 'turkish':
      return createTurkishWritingSystem();
    case 'swahili':
      return createSwahiliWritingSystem();
    case 'russian':
      return createRussianWritingSystem();
    case 'indonesian':
      return createIndonesianWritingSystem();
    default:
      return createDefaultWritingSystem();
  }
}

export function getWritingSystemSampleInput(langId: string): string {
  switch (langId.toLowerCase()) {
    case 'english':
      return 'the quick fox saw a mouse';
    case 'spanish':
      return 'el perro pequeño vio la casa';
    case 'german':
      return 'der große hund sah das buch';
    case 'mandarin':
      return 'rén kàn xiǎo māo hé shū';
    case 'arabic':
      return 'kataba al-rajul kitaab fii bayt';
    case 'japanese':
      return 'watashi wa inu to neko o miru';
    case 'turkish':
      return 'küçük köpek güzel ev gördü';
    case 'swahili':
      return 'mtu mdogo aliona nyumba kubwa';
    case 'russian':
      return 'bolshoy chelovek videt dom';
    case 'indonesian':
      return 'anak kecil melihat rumah besar';
    default:
      return 'shika taka person';
  }
}

export function createDefaultWritingSystem(): WritingSystemConfig {
  return {
    name: 'Agnostic Cursive Matrix',
    flowDirection: 'RTL',
    cursiveBonding: true,
    contextualVariants: true,
    glyphScale: 0.72,
    letterSpacing: 6,
    lineSpacing: 88,
    wrapLimit: 680,
    glyphs: structuredClone(DEFAULT_GLYPHS),
    featuricRules: [
      {
        id: 'voicing-tick',
        name: 'Word-Final Flourish Tick',
        targetGlyphId: '*',
        contextPosition: 'final',
        appendSvgPath: 'M 80 48 Q 94 35 90 66',
        offsetX: 0,
        offsetY: 0,
        enabled: true,
      },
      {
        id: 'initial-crest',
        name: 'Word-Initial Crest Loop',
        targetGlyphId: '*',
        contextPosition: 'initial',
        appendSvgPath: 'M 10 40 C 2 22, 24 22, 16 48',
        offsetX: 0,
        offsetY: 0,
        enabled: false,
      },
    ],
  };
}

export function createPresetWritingSystem(
  preset: 'rtl-western' | 'vertical-mongolian' | 'boustrophedon'
): WritingSystemConfig {
  const base = createDefaultWritingSystem();

  if (preset === 'vertical-mongolian') {
    const verticalGlyphs: Record<string, GlyphDefinition> = {};
    for (const [k, gDef] of Object.entries(base.glyphs)) {
      verticalGlyphs[k] = {
        ...gDef,
        svgPath:
          k === 'a' || k === 'e' || k === 'i' || k === 'o' || k === 'u'
            ? 'M 50 10 L 50 38 C 72 45, 72 65, 50 70 L 50 90'
            : k === 's' || k === 'sh' || k === 'z'
              ? 'M 50 10 L 50 30 L 28 45 L 72 60 L 50 75 L 50 90'
              : 'M 50 10 L 50 32 L 74 48 L 50 55 L 50 90',
        initialPath: 'M 50 12 C 30 12, 30 32, 50 35 L 50 90',
        finalPath: 'M 50 10 L 50 65 C 50 85, 80 85, 88 70',
        enteringAnchor: { x: 50, y: 10 },
        exitingAnchor: { x: 50, y: 90 },
        advanceWidth: 72,
        advanceHeight: 80,
      };
    }
    return {
      ...base,
      name: 'Traditional Vertical Spine (Mongolian-style)',
      flowDirection: 'TTB_LTR',
      cursiveBonding: true,
      contextualVariants: true,
      glyphScale: 0.72,
      letterSpacing: 0,
      lineSpacing: 84,
      wrapLimit: 360,
      glyphs: verticalGlyphs,
      featuricRules: [
        {
          id: 'vertical-tail',
          name: 'Final Spine Sweep',
          targetGlyphId: '*',
          contextPosition: 'final',
          appendSvgPath: 'M 50 86 Q 72 95 85 80',
          offsetX: 0,
          offsetY: 0,
          enabled: true,
        },
      ],
    };
  }

  if (preset === 'boustrophedon') {
    return {
      ...base,
      name: 'Boustrophedon Alternating Matrix',
      flowDirection: 'Boustrophedon',
      cursiveBonding: false,
      contextualVariants: true,
      wrapLimit: 420,
    };
  }

  return base;
}

function synthesizeFallbackGlyph(id: string, flow: FlowDirection): GlyphDefinition {
  let hash = 0;
  for (let i = 0; i < id.length; i++) {
    hash = (hash * 31 + id.charCodeAt(i)) | 0;
  }
  const h = Math.abs(hash);
  const loopY = 25 + (h % 30);
  const midX = 35 + ((h >> 3) % 30);

  if (flow === 'TTB_LTR' || flow === 'TTB_RTL') {
    return {
      id,
      label: id,
      svgPath: `M 50 10 L 50 32 Q ${midX} ${loopY} 50 65 L 50 90`,
      enteringAnchor: { x: 50, y: 10 },
      exitingAnchor: { x: 50, y: 90 },
      advanceWidth: 72,
      advanceHeight: 80,
    };
  }

  return {
    id,
    label: id,
    svgPath: `M 15 55 Q ${midX} ${loopY} 65 45 T 85 55`,
    enteringAnchor: { x: 15, y: 55 },
    exitingAnchor: { x: 85, y: 55 },
    advanceWidth: 74,
    advanceHeight: 75,
  };
}

export function tokenizeRawInput(input: string, knownGlyphIds: string[]): GraphemeToken[] {
  const words = input.trim().split(/\s+/).filter(Boolean);
  const sortedIds = [...knownGlyphIds].sort((a, b) => b.length - a.length);
  const tokens: GraphemeToken[] = [];

  words.forEach((word, wordIndex) => {
    const lower = word.toLowerCase();
    const wordTokens: string[] = [];
    for (let i = 0; i < lower.length; ) {
      const match = sortedIds.find((gid) => gid.length > 1 && lower.startsWith(gid.toLowerCase(), i));
      if (match) {
        wordTokens.push(match);
        i += match.length;
      } else {
        wordTokens.push(lower[i]);
        i++;
      }
    }

    wordTokens.forEach((gTok, idx) => {
      let positionInWord: GraphemeToken['positionInWord'] = 'medial';
      if (wordTokens.length === 1) positionInWord = 'isolated';
      else if (idx === 0) positionInWord = 'initial';
      else if (idx === wordTokens.length - 1) positionInWord = 'final';

      tokens.push({
        id: gTok,
        grapheme: gTok,
        wordIndex,
        positionInWord,
      });
    });
  });

  return tokens;
}

function matchesFeaturicRule(
  rule: FeaturicAssemblyRule,
  tokens: readonly GraphemeToken[],
  idx: number
): boolean {
  if (!rule.enabled) return false;
  const tok = tokens[idx];
  if (rule.targetGlyphId !== '*' && rule.targetGlyphId.toLowerCase() !== tok.id.toLowerCase()) {
    return false;
  }

  switch (rule.contextPosition) {
    case 'any':
      return true;
    case 'initial':
      return tok.positionInWord === 'initial' || tok.positionInWord === 'isolated';
    case 'medial':
      return tok.positionInWord === 'medial';
    case 'final':
      return tok.positionInWord === 'final' || tok.positionInWord === 'isolated';
    case 'after':
      return (
        idx > 0 &&
        tokens[idx - 1].wordIndex === tok.wordIndex &&
        (!rule.contextGlyphId ||
          tokens[idx - 1].id.toLowerCase() === rule.contextGlyphId.toLowerCase())
      );
    case 'before':
      return (
        idx < tokens.length - 1 &&
        tokens[idx + 1].wordIndex === tok.wordIndex &&
        (!rule.contextGlyphId ||
          tokens[idx + 1].id.toLowerCase() === rule.contextGlyphId.toLowerCase())
      );
    default:
      return false;
  }
}

export function runShapingGraph(
  tokens: readonly GraphemeToken[],
  config: WritingSystemConfig
): ShapingResult {
  const scale = config.glyphScale || 0.72;
  const padding = 48;
  const isVertical = config.flowDirection === 'TTB_LTR' || config.flowDirection === 'TTB_RTL';
  const wordGap = 28 * scale;

  const glyphs: ShapedGlyph[] = [];
  const trace: ShapingTraceEntry[] = [];

  let lineIndex = 0;
  let primaryCursor = 0;
  let prevWordIndex = tokens[0]?.wordIndex ?? 0;
  let prevExit: AnchorPoint | null = null;

  const wrap = Math.max(200, config.wrapLimit);

  interface PlacedDraft {
    idx: number;
    token: GraphemeToken;
    def: GlyphDefinition;
    variant: GraphemeToken['positionInWord'];
    svgPath: string;
    extraPaths: string[];
    appliedFeatures: string[];
    line: number;
    isNewWord: boolean;
  }

  const drafts: PlacedDraft[] = [];
  let logicalSpan = 0;

  for (let i = 0; i < tokens.length; i++) {
    const token = tokens[i];
    const lookupKey = token.id.toLowerCase();
    const def =
      config.glyphs[token.id] ??
      config.glyphs[lookupKey] ??
      synthesizeFallbackGlyph(token.id, config.flowDirection);

    const isNewWord = i > 0 && token.wordIndex !== prevWordIndex;
    const stepSpan =
      (isVertical ? def.advanceHeight : def.advanceWidth) * scale +
      config.letterSpacing +
      (isNewWord ? wordGap : 0);

    if (i > 0 && logicalSpan + stepSpan > wrap && isNewWord) {
      lineIndex++;
      logicalSpan = 0;
    }

    let variant: GraphemeToken['positionInWord'] = token.positionInWord;
    let chosenPath = def.svgPath;
    if (config.contextualVariants) {
      if (variant === 'initial' && def.initialPath) chosenPath = def.initialPath;
      else if (variant === 'medial' && def.medialPath) chosenPath = def.medialPath;
      else if (variant === 'final' && def.finalPath) chosenPath = def.finalPath;
    } else {
      variant = 'isolated';
    }

    const extraPaths: string[] = [];
    const appliedFeatures: string[] = [];
    for (const rule of config.featuricRules) {
      if (matchesFeaturicRule(rule, tokens, i) && rule.appendSvgPath.trim()) {
        extraPaths.push(rule.appendSvgPath.trim());
        appliedFeatures.push(rule.name);
      }
    }

    drafts.push({
      idx: i,
      token,
      def,
      variant,
      svgPath: chosenPath,
      extraPaths,
      appliedFeatures,
      line: lineIndex,
      isNewWord,
    });

    logicalSpan += stepSpan;
    prevWordIndex = token.wordIndex;
  }

  const totalLines = lineIndex + 1;
  let maxX = padding * 2 + 240;
  let maxY = padding * 2 + 180;

  for (let l = 0; l < totalLines; l++) {
    const lineDrafts = drafts.filter((d) => d.line === l);
    const lineFlow: 'LTR' | 'RTL' | 'TTB' = isVertical
      ? 'TTB'
      : config.flowDirection === 'RTL' ||
          (config.flowDirection === 'Boustrophedon' && l % 2 === 1)
        ? 'RTL'
        : 'LTR';

    prevExit = null;
    if (lineFlow === 'LTR') {
      primaryCursor = padding;
    } else if (lineFlow === 'RTL') {
      primaryCursor = wrap + padding;
    } else {
      primaryCursor = padding;
    }

    const secondaryCoord =
      config.flowDirection === 'TTB_RTL'
        ? padding + (totalLines - 1 - l) * config.lineSpacing
        : padding + l * config.lineSpacing;

    for (let k = 0; k < lineDrafts.length; k++) {
      const d = lineDrafts[k];
      const { def } = d;
      const newWordOnLine = k > 0 && d.isNewWord;

      let gx = 0;
      let gy = 0;
      let anchorMode = 'GridAdvance';

      if (lineFlow === 'LTR') {
        if (config.cursiveBonding && prevExit && !newWordOnLine) {
          gx = prevExit.x - def.enteringAnchor.x * scale + config.letterSpacing * 0.25;
          gy = prevExit.y - def.enteringAnchor.y * scale;
          anchorMode = 'Bond(#exiting -> #entering)';
        } else {
          if (newWordOnLine) primaryCursor += wordGap;
          gx = primaryCursor;
          gy = secondaryCoord;
        }
        primaryCursor = gx + def.advanceWidth * scale + config.letterSpacing;
      } else if (lineFlow === 'RTL') {
        if (config.cursiveBonding && prevExit && !newWordOnLine) {
          gx = prevExit.x - def.exitingAnchor.x * scale - config.letterSpacing * 0.25;
          gy = prevExit.y - def.exitingAnchor.y * scale;
          anchorMode = 'RTL-Bond(#entering <- #exiting)';
        } else {
          if (newWordOnLine) primaryCursor -= wordGap;
          gx = primaryCursor - def.advanceWidth * scale;
          gy = secondaryCoord;
        }
        primaryCursor = gx - config.letterSpacing;
      } else {
        if (config.cursiveBonding && prevExit && !newWordOnLine) {
          gx = prevExit.x - def.enteringAnchor.x * scale;
          gy = prevExit.y - def.enteringAnchor.y * scale + config.letterSpacing * 0.25;
          anchorMode = 'VerticalBond(#exiting -> #entering)';
        } else {
          if (newWordOnLine) primaryCursor += wordGap;
          gx = secondaryCoord;
          gy = primaryCursor;
        }
        primaryCursor = gy + def.advanceHeight * scale + config.letterSpacing;
      }

      const enteringWorld: AnchorPoint = {
        x: gx + def.enteringAnchor.x * scale,
        y: gy + def.enteringAnchor.y * scale,
      };
      const exitingWorld: AnchorPoint = {
        x: gx + def.exitingAnchor.x * scale,
        y: gy + def.exitingAnchor.y * scale,
      };

      prevExit = lineFlow === 'RTL' ? enteringWorld : exitingWorld;

      maxX = Math.max(maxX, gx + 100 * scale + padding);
      maxY = Math.max(maxY, gy + 100 * scale + padding);

      glyphs.push({
        index: d.idx,
        token: d.token,
        x: gx,
        y: gy,
        svgPath: d.svgPath,
        extraPaths: d.extraPaths,
        enteringWorld,
        exitingWorld,
        variantUsed: d.variant,
        appliedFeatures: d.appliedFeatures,
      });

      trace.push({
        step: d.idx + 1,
        tokenId: d.token.id,
        variant: d.variant,
        anchorMode,
        position: `(${Math.round(gx)}, ${Math.round(gy)})`,
      });
    }
  }

  return {
    glyphs,
    trace,
    bounds: { width: Math.ceil(maxX), height: Math.ceil(maxY) },
  };
}

export function exportShapedSvg(
  result: ShapingResult,
  config: WritingSystemConfig
): string {
  const scale = config.glyphScale || 0.72;
  const paths = result.glyphs
    .map((gItem) => {
      const extras = gItem.extraPaths
        .map(
          (ep) =>
            `<path d="${ep}" fill="none" stroke="#1e293b" stroke-width="4" stroke-linecap="round" stroke-linejoin="round" />`
        )
        .join('');
      return `<g transform="translate(${gItem.x.toFixed(1)}, ${gItem.y.toFixed(1)}) scale(${scale})">
        <path d="${gItem.svgPath}" fill="none" stroke="#0f172a" stroke-width="4.5" stroke-linecap="round" stroke-linejoin="round" />
        ${extras}
      </g>`;
    })
    .join('\n  ');

  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${result.bounds.width} ${result.bounds.height}" width="${result.bounds.width}" height="${result.bounds.height}">
  <rect width="100%" height="100%" fill="#ffffff" />
  ${paths}
</svg>`;
}

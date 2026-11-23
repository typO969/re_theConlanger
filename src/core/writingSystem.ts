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

// Clean geometric 100x100 glyph paths with natural #entering (left/top) & #exiting (right/bottom) anchors
const DEFAULT_GLYPHS: Record<string, GlyphDefinition> = {
  a: {
    id: 'a',
    label: 'a',
    svgPath: 'M 15 55 C 15 30, 55 25, 65 45 L 65 70 C 65 75, 80 75, 85 55',
    enteringAnchor: { x: 15, y: 55 },
    exitingAnchor: { x: 85, y: 55 },
    advanceWidth: 75,
    advanceHeight: 75,
  },
  e: {
    id: 'e',
    label: 'e',
    svgPath: 'M 15 55 L 70 50 C 75 30, 35 25, 30 50 C 25 75, 70 75, 85 55',
    enteringAnchor: { x: 15, y: 55 },
    exitingAnchor: { x: 85, y: 55 },
    advanceWidth: 75,
    advanceHeight: 75,
  },
  i: {
    id: 'i',
    label: 'i',
    svgPath: 'M 15 55 C 35 55, 45 30, 50 25 C 50 45, 55 70, 85 55 M 48 12 A 4 4 0 1 0 56 12 A 4 4 0 1 0 48 12',
    enteringAnchor: { x: 15, y: 55 },
    exitingAnchor: { x: 85, y: 55 },
    advanceWidth: 65,
    advanceHeight: 75,
  },
  o: {
    id: 'o',
    label: 'o',
    svgPath: 'M 15 55 C 25 35, 65 30, 65 52 C 65 72, 30 72, 30 52 C 30 42, 60 55, 85 55',
    enteringAnchor: { x: 15, y: 55 },
    exitingAnchor: { x: 85, y: 55 },
    advanceWidth: 75,
    advanceHeight: 75,
  },
  u: {
    id: 'u',
    label: 'u',
    svgPath: 'M 15 55 L 32 30 L 35 65 C 40 72, 62 72, 65 30 L 68 65 L 85 55',
    enteringAnchor: { x: 15, y: 55 },
    exitingAnchor: { x: 85, y: 55 },
    advanceWidth: 75,
    advanceHeight: 75,
  },
  p: {
    id: 'p',
    label: 'p',
    svgPath: 'M 15 55 L 35 28 L 35 88 M 35 38 C 68 25, 72 60, 35 58 L 85 55',
    enteringAnchor: { x: 15, y: 55 },
    exitingAnchor: { x: 85, y: 55 },
    advanceWidth: 75,
    advanceHeight: 80,
  },
  t: {
    id: 't',
    label: 't',
    svgPath: 'M 15 55 L 45 15 L 48 68 C 52 74, 72 70, 85 55 M 28 32 L 66 32',
    enteringAnchor: { x: 15, y: 55 },
    exitingAnchor: { x: 85, y: 55 },
    advanceWidth: 70,
    advanceHeight: 75,
  },
  k: {
    id: 'k',
    label: 'k',
    svgPath: 'M 15 55 L 35 15 L 35 72 M 66 30 L 35 52 L 85 55',
    enteringAnchor: { x: 15, y: 55 },
    exitingAnchor: { x: 85, y: 55 },
    advanceWidth: 75,
    advanceHeight: 75,
  },
  c: {
    id: 'c',
    label: 'c',
    svgPath: 'M 15 55 C 35 30, 70 28, 72 38 M 36 45 C 25 68, 65 72, 85 55',
    enteringAnchor: { x: 15, y: 55 },
    exitingAnchor: { x: 85, y: 55 },
    advanceWidth: 70,
    advanceHeight: 75,
  },
  s: {
    id: 's',
    label: 's',
    svgPath: 'M 15 55 L 42 30 C 65 38, 25 62, 55 70 L 85 55',
    enteringAnchor: { x: 15, y: 55 },
    exitingAnchor: { x: 85, y: 55 },
    advanceWidth: 70,
    advanceHeight: 75,
  },
  sh: {
    id: 'sh',
    label: 'sh (ʃ)',
    svgPath: 'M 15 55 C 30 22, 48 22, 52 55 C 56 80, 72 80, 85 55 M 32 18 L 68 18',
    enteringAnchor: { x: 15, y: 55 },
    exitingAnchor: { x: 85, y: 55 },
    advanceWidth: 78,
    advanceHeight: 75,
  },
  z: {
    id: 'z',
    label: 'z',
    svgPath: 'M 15 55 L 32 32 L 66 32 L 32 68 L 85 55',
    enteringAnchor: { x: 15, y: 55 },
    exitingAnchor: { x: 85, y: 55 },
    advanceWidth: 75,
    advanceHeight: 75,
  },
  n: {
    id: 'n',
    label: 'n',
    svgPath: 'M 15 55 L 32 30 L 34 68 M 34 45 C 48 25, 65 28, 66 65 L 85 55',
    enteringAnchor: { x: 15, y: 55 },
    exitingAnchor: { x: 85, y: 55 },
    advanceWidth: 75,
    advanceHeight: 75,
  },
  r: {
    id: 'r',
    label: 'r',
    svgPath: 'M 15 55 L 35 30 L 35 68 M 35 44 C 48 28, 65 28, 70 40 L 85 55',
    enteringAnchor: { x: 15, y: 55 },
    exitingAnchor: { x: 85, y: 55 },
    advanceWidth: 70,
    advanceHeight: 75,
  },
  f: {
    id: 'f',
    label: 'f',
    svgPath: 'M 15 55 C 35 35, 50 15, 65 18 L 45 85 M 28 45 L 85 55',
    enteringAnchor: { x: 15, y: 55 },
    exitingAnchor: { x: 85, y: 55 },
    advanceWidth: 72,
    advanceHeight: 80,
  },
  m: {
    id: 'm',
    label: 'm',
    svgPath: 'M 15 55 L 25 32 L 35 65 L 50 32 L 62 65 L 85 55',
    enteringAnchor: { x: 15, y: 55 },
    exitingAnchor: { x: 85, y: 55 },
    advanceWidth: 78,
    advanceHeight: 75,
  },
  l: {
    id: 'l',
    label: 'l',
    svgPath: 'M 15 55 C 35 45, 55 12, 48 12 C 40 12, 42 70, 85 55',
    enteringAnchor: { x: 15, y: 55 },
    exitingAnchor: { x: 85, y: 55 },
    advanceWidth: 68,
    advanceHeight: 75,
  },
};

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

export function createPresetWritingSystem(preset: 'rtl-western' | 'vertical-mongolian' | 'boustrophedon'): WritingSystemConfig {
  const base = createDefaultWritingSystem();

  if (preset === 'vertical-mongolian') {
    const verticalGlyphs: Record<string, GlyphDefinition> = {};
    for (const [k, g] of Object.entries(base.glyphs)) {
      verticalGlyphs[k] = {
        ...g,
        // Vertical top-to-bottom spine with top entering & bottom exiting anchor
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

/**
 * Fallback procedural glyph for any character/digraph not explicitly drawn yet,
 * so arbitrary conlang orthographies always render cleanly on the Vector Canvas.
 */
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

    wordTokens.forEach((g, idx) => {
      let positionInWord: GraphemeToken['positionInWord'] = 'medial';
      if (wordTokens.length === 1) positionInWord = 'isolated';
      else if (idx === 0) positionInWord = 'initial';
      else if (idx === wordTokens.length - 1) positionInWord = 'final';

      tokens.push({
        id: g,
        grapheme: g,
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

  // Track cursor in logical coordinates, wrap lines/columns cleanly
  let lineIndex = 0;
  let primaryCursor = 0;
  let prevWordIndex = tokens[0]?.wordIndex ?? 0;
  let prevExit: AnchorPoint | null = null;

  const wrap = Math.max(200, config.wrapLimit);

  // Pre-group into lines/columns so RTL or Boustrophedon lines position deterministically
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

  // Resolve coordinates per line based on FlowVector
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
        // In RTL flow, glyph enters from the right or bonds #entering -> #exiting leftward
        if (config.cursiveBonding && prevExit && !newWordOnLine) {
          // Bond current glyph's exiting anchor on the right to previous glyph's entering anchor on the left
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
        // TTB (Vertical)
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

      // In RTL, next leftward glyph attaches to the left-side anchor (enteringWorld)
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
    .map((g) => {
      const extras = g.extraPaths
        .map(
          (ep) =>
            `<path d="${ep}" fill="none" stroke="#1e293b" stroke-width="4" stroke-linecap="round" stroke-linejoin="round" />`
        )
        .join('');
      return `<g transform="translate(${g.x.toFixed(1)}, ${g.y.toFixed(1)}) scale(${scale})">
        <path d="${g.svgPath}" fill="none" stroke="#0f172a" stroke-width="4.5" stroke-linecap="round" stroke-linejoin="round" />
        ${extras}
      </g>`;
    })
    .join('\n  ');

  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${result.bounds.width} ${result.bounds.height}" width="${result.bounds.width}" height="${result.bounds.height}">
  <rect width="100%" height="100%" fill="#ffffff" />
  ${paths}
</svg>`;
}

import {
  AffixRule,
  CapitalizationMode,
  ClauseTemplate,
  DerivationStep,
  DiagnosticIssue,
  Feat,
  FeatBundle,
  GraphemeToken,
  LanguageProjectState,
  Lexeme,
  MorphemeToken,
  OrthographyException,
  OrthographyResult,
  OrthographyRule,
  Phoneme,
  PhonologicalFeatures,
  PhonotacticsConfig,
  PipelineSelection,
  RealizedWordContext,
  RuleSpec,
  SentenceSpec,
  StressPattern,
  Syllable,
  WordOrder,
} from './types';
import { RNG } from './rng';
import { createDefaultWritingSystem } from './writingSystem';

export function emptyFeatures(): PhonologicalFeatures {
  return {
    voicing: '',
    manner: '',
    place: '',
    height: '',
    backness: '',
    roundness: '',
    custom: '',
  };
}

export function stripStressMark(symbol: string): string {
  return symbol.replace(/^ˈ/, '');
}

export function hasStressMark(symbol: string): boolean {
  return symbol.startsWith('ˈ');
}

const DEFAULT_IPA_FEATURES: Record<string, Partial<PhonologicalFeatures>> = {
  p: { voicing: 'voiceless', manner: 'stop', place: 'bilabial' },
  'pʰ': { voicing: 'voiceless', manner: 'stop', place: 'bilabial', custom: 'aspirated' },
  t: { voicing: 'voiceless', manner: 'stop', place: 'alveolar' },
  'tʰ': { voicing: 'voiceless', manner: 'stop', place: 'alveolar', custom: 'aspirated' },
  k: { voicing: 'voiceless', manner: 'stop', place: 'velar' },
  'kʰ': { voicing: 'voiceless', manner: 'stop', place: 'velar', custom: 'aspirated' },
  b: { voicing: 'voiced', manner: 'stop', place: 'bilabial' },
  d: { voicing: 'voiced', manner: 'stop', place: 'alveolar' },
  g: { voicing: 'voiced', manner: 'stop', place: 'velar' },
  ʔ: { voicing: 'voiceless', manner: 'stop', place: 'glottal' },
  'tʃ': { voicing: 'voiceless', manner: 'affricate', place: 'postalveolar', custom: 'sibilant' },
  'dʒ': { voicing: 'voiced', manner: 'affricate', place: 'postalveolar', custom: 'sibilant' },
  s: { voicing: 'voiceless', manner: 'fricative', place: 'alveolar', custom: 'sibilant' },
  z: { voicing: 'voiced', manner: 'fricative', place: 'alveolar', custom: 'sibilant' },
  ʃ: { voicing: 'voiceless', manner: 'fricative', place: 'postalveolar', custom: 'sibilant' },
  ʒ: { voicing: 'voiced', manner: 'fricative', place: 'postalveolar', custom: 'sibilant' },
  f: { voicing: 'voiceless', manner: 'fricative', place: 'labiodental' },
  v: { voicing: 'voiced', manner: 'fricative', place: 'labiodental' },
  θ: { voicing: 'voiceless', manner: 'fricative', place: 'dental' },
  ð: { voicing: 'voiced', manner: 'fricative', place: 'dental' },
  h: { voicing: 'voiceless', manner: 'fricative', place: 'glottal' },
  m: { voicing: 'voiced', manner: 'nasal', place: 'bilabial' },
  ɱ: { voicing: 'voiced', manner: 'nasal', place: 'labiodental' },
  n: { voicing: 'voiced', manner: 'nasal', place: 'alveolar' },
  ɲ: { voicing: 'voiced', manner: 'nasal', place: 'palatal' },
  ŋ: { voicing: 'voiced', manner: 'nasal', place: 'velar' },
  ɾ: { voicing: 'voiced', manner: 'tap', place: 'alveolar' },
  r: { voicing: 'voiced', manner: 'trill', place: 'alveolar', custom: 'liquid' },
  ɹ: { voicing: 'voiced', manner: 'approximant', place: 'postalveolar', custom: 'liquid rhotic' },
  l: { voicing: 'voiced', manner: 'approximant', place: 'alveolar', custom: 'lateral liquid' },
  ɫ: { voicing: 'voiced', manner: 'approximant', place: 'velar', custom: 'lateral dark liquid' },
  w: { voicing: 'voiced', manner: 'approximant', place: 'bilabial', custom: 'glide' },
  j: { voicing: 'voiced', manner: 'approximant', place: 'palatal', custom: 'glide' },
  a: { voicing: 'voiced', manner: 'vowel', height: 'low', backness: 'central', roundness: 'unrounded' },
  'ɑː': { voicing: 'voiced', manner: 'vowel', height: 'low', backness: 'back', roundness: 'unrounded', custom: 'tense long' },
  æ: { voicing: 'voiced', manner: 'vowel', height: 'low', backness: 'front', roundness: 'unrounded', custom: 'lax' },
  e: { voicing: 'voiced', manner: 'vowel', height: 'mid', backness: 'front', roundness: 'unrounded' },
  'eɪ': { voicing: 'voiced', manner: 'vowel', height: 'mid', backness: 'front', roundness: 'unrounded', custom: 'diphthong tense' },
  ɛ: { voicing: 'voiced', manner: 'vowel', height: 'mid', backness: 'front', roundness: 'unrounded', custom: 'lax' },
  i: { voicing: 'voiced', manner: 'vowel', height: 'high', backness: 'front', roundness: 'unrounded' },
  'iː': { voicing: 'voiced', manner: 'vowel', height: 'high', backness: 'front', roundness: 'unrounded', custom: 'tense long' },
  ɪ: { voicing: 'voiced', manner: 'vowel', height: 'high', backness: 'front', roundness: 'unrounded', custom: 'lax' },
  o: { voicing: 'voiced', manner: 'vowel', height: 'mid', backness: 'back', roundness: 'rounded' },
  'oʊ': { voicing: 'voiced', manner: 'vowel', height: 'mid', backness: 'back', roundness: 'rounded', custom: 'diphthong tense' },
  'ɔː': { voicing: 'voiced', manner: 'vowel', height: 'mid', backness: 'back', roundness: 'rounded', custom: 'tense long' },
  u: { voicing: 'voiced', manner: 'vowel', height: 'high', backness: 'back', roundness: 'rounded' },
  'uː': { voicing: 'voiced', manner: 'vowel', height: 'high', backness: 'back', roundness: 'rounded', custom: 'tense long' },
  ʊ: { voicing: 'voiced', manner: 'vowel', height: 'high', backness: 'back', roundness: 'rounded', custom: 'lax' },
  ʌ: { voicing: 'voiced', manner: 'vowel', height: 'mid', backness: 'central', roundness: 'unrounded', custom: 'lax' },
  ə: { voicing: 'voiced', manner: 'vowel', height: 'mid', backness: 'central', roundness: 'unrounded', custom: 'reduced' },
  ɚ: { voicing: 'voiced', manner: 'vowel', height: 'mid', backness: 'central', roundness: 'unrounded', custom: 'rhotic reduced' },
  'ɝː': { voicing: 'voiced', manner: 'vowel', height: 'mid', backness: 'central', roundness: 'unrounded', custom: 'rhotic stressed' },
  'aɪ': { voicing: 'voiced', manner: 'vowel', height: 'low', backness: 'front', roundness: 'unrounded', custom: 'diphthong' },
  'aʊ': { voicing: 'voiced', manner: 'vowel', height: 'low', backness: 'back', roundness: 'rounded', custom: 'diphthong' },
  'ɔɪ': { voicing: 'voiced', manner: 'vowel', height: 'mid', backness: 'back', roundness: 'rounded', custom: 'diphthong' },
};

export function createPhoneme(symbol: string, type: 'C' | 'V', overrides?: Partial<Phoneme>): Phoneme {
  const guessed = DEFAULT_IPA_FEATURES[symbol] ?? (type === 'V' ? { manner: 'vowel', voicing: 'voiced' } : {});
  return {
    symbol,
    type,
    weight: overrides?.weight ?? 1,
    allowOnset: overrides?.allowOnset ?? true,
    allowCoda: overrides?.allowCoda ?? true,
    features: {
      ...emptyFeatures(),
      ...guessed,
      ...(overrides?.features ?? {}),
    },
  };
}

export class Phonotactics {
  public readonly config: PhonotacticsConfig;
  public readonly allowedOnsets: Set<string>;
  public readonly allowedCodas: Set<string>;

  constructor(config: PhonotacticsConfig) {
    this.config = config;
    this.allowedOnsets = new Set(config.allowedOnsets);
    this.allowedCodas = new Set(config.allowedCodas);
  }

  public isValidOnset(cluster: string, segmentCount?: number): boolean {
    const clean = cluster.replace(/[ˈʰ]/g, '');
    if (clean.length === 0) return true;
    const count = segmentCount ?? clean.length;
    if (this.allowedOnsets.has(clean)) return true;
    if (count > this.config.maxOnsetCluster) return false;
    if (count === 1 || clean.length === 1) return true;
    return this.allowedOnsets.size === 0 || this.allowedOnsets.has(clean);
  }

  public isValidCoda(cluster: string, segmentCount?: number): boolean {
    const clean = cluster.replace(/[ˈʰ]/g, '');
    if (clean.length === 0) return true;
    const count = segmentCount ?? clean.length;
    if (this.allowedCodas.has(clean)) return true;
    if (count > this.config.maxCodaCluster) return false;
    if (count === 1 || clean.length === 1) return true;
    return this.allowedCodas.size === 0 || this.allowedCodas.has(clean);
  }
}

export class Syllabifier {
  private readonly vowelSet: Set<string>;
  private readonly tactics?: Phonotactics;

  constructor(vowels: readonly Phoneme[] = [], tactics?: Phonotactics) {
    this.vowelSet = new Set([
      ...'aeiouɑɔɛɪʊyøœæəɨʌɒɐɯɤɚ',
      ...vowels.map((v) => stripStressMark(v.symbol)),
    ]);
    this.tactics = tactics;
  }

  public isVowel(p: string): boolean {
    if (!p) return false;
    const clean = stripStressMark(p);
    return this.vowelSet.has(clean) || (clean.length > 0 && this.vowelSet.has(clean[0]));
  }

  public split(phones: readonly string[]): Syllable[] {
    if (phones.length === 0) return [];

    const vowelIndices: number[] = [];
    for (let i = 0; i < phones.length; i++) {
      if (this.isVowel(phones[i])) vowelIndices.push(i);
    }

    if (vowelIndices.length === 0) {
      return [{ onset: [...phones], nucleus: [], coda: [] }];
    }

    // Multi-consonant Maximal Onset Principle guided by Phonotactics
    const boundaries = new Array<number>(vowelIndices.length + 1);
    boundaries[0] = 0;
    boundaries[vowelIndices.length] = phones.length;

    const maxOnset = this.tactics ? Math.max(0, this.tactics.config.maxOnsetCluster) : 2;

    for (let k = 0; k < vowelIndices.length - 1; k++) {
      const v1 = vowelIndices[k];
      const v2 = vowelIndices[k + 1];
      const clusterLen = v2 - v1 - 1;
      if (clusterLen <= 0) {
        boundaries[k + 1] = v2;
        continue;
      }

      let bestSplit = v2;
      for (let split = Math.max(v1 + 1, v2 - maxOnset); split <= v2; split++) {
        const onsetPhones = phones.slice(split, v2);
        const codaPhones = phones.slice(v1 + 1, split);
        const onsetSlice = onsetPhones.map((p) => p.replace(/[ˈʰ]/g, '')).join('');
        const codaSlice = codaPhones.map((p) => p.replace(/[ˈʰ]/g, '')).join('');
        const onsetOk = !this.tactics || this.tactics.isValidOnset(onsetSlice, onsetPhones.length);
        const codaOk = !this.tactics || this.tactics.isValidCoda(codaSlice, codaPhones.length);
        if (onsetOk && codaOk) {
          bestSplit = split;
          break;
        }
      }

      if (bestSplit === v2 && clusterLen >= 1 && maxOnset >= 1) {
        bestSplit = v2 - 1;
      }
      boundaries[k + 1] = bestSplit;
    }

    const syllables: Syllable[] = [];
    for (let s = 0; s < vowelIndices.length; s++) {
      syllables.push({
        onset: phones.slice(boundaries[s], vowelIndices[s]),
        nucleus: [phones[vowelIndices[s]]],
        coda: phones.slice(vowelIndices[s] + 1, boundaries[s + 1]),
      });
    }
    return syllables;
  }
}

export function resolveStressSyllableIndex(
  syllableCount: number,
  pattern: StressPattern,
  syllables?: readonly Syllable[]
): number {
  if (syllableCount <= 0 || pattern === 'None') return -1;
  if (pattern === 'Lexical' && syllables) {
    const marked = syllables.findIndex(
      (s) =>
        s.onset.some(hasStressMark) ||
        s.nucleus.some(hasStressMark) ||
        s.coda.some(hasStressMark)
    );
    if (marked >= 0) return marked;
    return -1;
  }
  if (pattern === 'Initial') return 0;
  if (pattern === 'Final') return syllableCount - 1;
  if (pattern === 'Penultimate') return syllableCount >= 2 ? syllableCount - 2 : 0;
  return -1;
}

function getEffectiveFeatures(symbol: string, inventory: readonly Phoneme[]): { phoneme?: Phoneme; features: PhonologicalFeatures } | null {
  const clean = stripStressMark(symbol);
  const phoneme = inventory.find((p) => stripStressMark(p.symbol) === clean);
  if (phoneme) return { phoneme, features: phoneme.features };
  const def = DEFAULT_IPA_FEATURES[clean];
  if (def) {
    return {
      features: {
        ...emptyFeatures(),
        ...def,
      },
    };
  }
  return null;
}

function matchesSingleFeatureClause(
  symbol: string,
  clause: string,
  inventory: readonly Phoneme[],
  stressedSet?: ReadonlySet<number>,
  phoneIndex?: number
): boolean {
  const cond = clause.trim();
  if (!cond) return true;
  const clean = stripStressMark(symbol);

  if (cond.startsWith('+') || cond.startsWith('-')) {
    const positive = cond.startsWith('+');
    const name = cond.slice(1).toLowerCase();
    if (name === 'stress' || name === 'stressed') {
      if (!stressedSet || phoneIndex === undefined) return !positive;
      const isStressed = stressedSet.has(phoneIndex);
      return positive ? isStressed : !isStressed;
    }

    const resolved = getEffectiveFeatures(clean, inventory);
    if (!resolved) return false;
    const feat = resolved.features;
    const isVowel = resolved.phoneme ? resolved.phoneme.type === 'V' : feat.manner === 'vowel';

    if (name === 'voice' || name === 'voicing' || name === 'voiced') {
      const v = feat.voicing.toLowerCase();
      return positive ? v === 'voiced' : v === 'voiceless';
    }
    if (name === 'round' || name === 'rounded') {
      const r = feat.roundness.toLowerCase();
      return positive ? r === 'rounded' : r === 'unrounded';
    }
    if (name === 'sibilant') {
      const isSib =
        ['s', 'z', 'ʃ', 'ʒ', 'tʃ', 'dʒ'].includes(clean) ||
        feat.custom.toLowerCase().includes('sibilant');
      return positive ? isSib : !isSib;
    }
    if (name === 'sonorant') {
      const isSon =
        ['nasal', 'approximant', 'trill', 'tap', 'lateral', 'vowel'].includes(feat.manner.toLowerCase()) ||
        isVowel;
      return positive ? isSon : !isSon;
    }
    const allMeta = `${feat.manner} ${feat.place} ${feat.height} ${feat.backness} ${feat.custom}`.toLowerCase();
    return positive ? allMeta.includes(name) : !allMeta.includes(name);
  }

  if (cond.includes('=')) {
    const resolved = getEffectiveFeatures(clean, inventory);
    if (!resolved) return false;
    const feat = resolved.features;
    const [rawKey, rawVal] = cond.split('=', 2);
    const key = rawKey.trim().toLowerCase();
    const val = rawVal.trim().toLowerCase();
    const featVal =
      key === 'voicing'
        ? feat.voicing
        : key === 'manner'
          ? feat.manner
          : key === 'place'
            ? feat.place
            : key === 'height'
              ? feat.height
              : key === 'backness'
                ? feat.backness
                : key === 'roundness'
                  ? feat.roundness
                  : feat.custom;
    return featVal.toLowerCase().includes(val);
  }

  return true;
}

function matchesCondition(
  symbol: string,
  condition: string | undefined,
  inventory: readonly Phoneme[],
  stressedSet?: ReadonlySet<number>,
  phoneIndex?: number
): boolean {
  if (!condition || !condition.trim()) return true;
  let cond = condition.trim();
  if (cond.startsWith('[') && cond.endsWith(']')) {
    cond = cond.slice(1, -1).trim();
  }
  if (!cond) return true;

  const clauses = cond.split(',').map((c) => c.trim()).filter(Boolean);
  return clauses.every((cl) => matchesSingleFeatureClause(symbol, cl, inventory, stressedSet, phoneIndex));
}

function matchesPhoneOrClass(
  actual: string | null,
  expected: string,
  inventory: readonly Phoneme[],
  stressedSet?: ReadonlySet<number>,
  actualIndex?: number
): boolean {
  if (!expected) return true;
  if (expected === '#') return actual === null;
  if (actual === null) return false;
  const cleanActual = stripStressMark(actual);

  if (expected === 'V') {
    const ph = inventory.find((p) => stripStressMark(p.symbol) === cleanActual);
    return ph ? ph.type === 'V' : 'aeiouɑɔɛɪʊyøœæəɨʌɒɐɯɤɚ'.includes(cleanActual[0] ?? '');
  }
  if (expected === 'C') {
    const ph = inventory.find((p) => stripStressMark(p.symbol) === cleanActual);
    return ph ? ph.type === 'C' : !'aeiouɑɔɛɪʊyøœæəɨʌɒɐɯɤɚ'.includes(cleanActual[0] ?? '');
  }
  if (expected.startsWith('[') && expected.endsWith(']')) {
    return matchesCondition(cleanActual, expected, inventory, stressedSet, actualIndex);
  }
  return expected.startsWith('ˈ') ? actual === expected : cleanActual === expected;
}

function computeSyllableBoundarySets(
  phones: readonly string[],
  inventory: readonly Phoneme[],
  syllabifier?: Syllabifier
): { starts: Set<number>; ends: Set<number> } {
  const starts = new Set<number>();
  const ends = new Set<number>();
  const syls = (syllabifier ?? new Syllabifier(inventory.filter((p) => p.type === 'V'))).split(phones);
  let offset = 0;
  for (const s of syls) {
    const len = s.onset.length + s.nucleus.length + s.coda.length;
    if (len > 0) {
      starts.add(offset);
      ends.add(offset + len - 1);
    }
    offset += len;
  }
  return { starts, ends };
}

function contextMatch(
  phones: readonly string[],
  pos: number,
  prevTarget: string,
  nextTarget: string,
  spanLen: number,
  inventory: readonly Phoneme[],
  stressedSet?: ReadonlySet<number>,
  sylBounds?: { starts: Set<number>; ends: Set<number> }
): boolean {
  let prevOk: boolean;
  if (prevTarget === '$' || prevTarget === '.') {
    prevOk = Boolean(sylBounds?.starts.has(pos));
  } else {
    const prevPhone = pos > 0 ? phones[pos - 1] : null;
    prevOk = matchesPhoneOrClass(prevPhone, prevTarget, inventory, stressedSet, pos - 1);
  }

  const afterIdx = pos + spanLen;
  let nextOk: boolean;
  if (nextTarget === '$' || nextTarget === '.') {
    nextOk = Boolean(sylBounds?.ends.has(pos + spanLen - 1));
  } else {
    const nextPhone = afterIdx < phones.length ? phones[afterIdx] : null;
    nextOk = matchesPhoneOrClass(nextPhone, nextTarget, inventory, stressedSet, afterIdx);
  }
  return prevOk && nextOk;
}

const NASAL_PLACE_FALLBACK: Record<string, string> = {
  bilabial: 'm',
  labiodental: 'ɱ',
  dental: 'n',
  alveolar: 'n',
  postalveolar: 'ɲ',
  palatal: 'ɲ',
  velar: 'ŋ',
};

function resolveAlphaPlaceReplacement(
  targetPhone: string,
  phones: readonly string[],
  pos: number,
  prevTarget: string,
  nextTarget: string,
  inventory: readonly Phoneme[]
): string {
  let condPhone: string | null = null;
  if (nextTarget && nextTarget !== '#' && nextTarget !== '$' && nextTarget !== '.' && pos + 1 < phones.length) {
    condPhone = phones[pos + 1];
  } else if (prevTarget && prevTarget !== '#' && prevTarget !== '$' && prevTarget !== '.' && pos > 0) {
    condPhone = phones[pos - 1];
  }
  if (!condPhone) return targetPhone;

  const condFeat = getEffectiveFeatures(condPhone, inventory)?.features;
  const targetPlace = condFeat?.place.trim().toLowerCase() ?? '';
  if (!targetPlace) return targetPhone;

  const cleanTarget = stripStressMark(targetPhone);
  const srcFeat = getEffectiveFeatures(cleanTarget, inventory)?.features;
  const srcManner = srcFeat?.manner.toLowerCase() ?? '';
  const srcVoicing = srcFeat?.voicing.toLowerCase() ?? '';

  const exactInvMatch = inventory.find(
    (p) =>
      p.type === 'C' &&
      p.features.place.toLowerCase() === targetPlace &&
      (!srcManner || p.features.manner.toLowerCase() === srcManner) &&
      (!srcVoicing || p.features.voicing.toLowerCase() === srcVoicing)
  );
  if (exactInvMatch) {
    return hasStressMark(targetPhone) && !hasStressMark(exactInvMatch.symbol)
      ? `ˈ${exactInvMatch.symbol}`
      : exactInvMatch.symbol;
  }

  if (srcManner === 'nasal' || ['m', 'ɱ', 'n', 'ɲ', 'ŋ'].includes(cleanTarget)) {
    const mapped = NASAL_PLACE_FALLBACK[targetPlace] ?? cleanTarget;
    return hasStressMark(targetPhone) ? `ˈ${mapped}` : mapped;
  }

  for (const [sym, def] of Object.entries(DEFAULT_IPA_FEATURES)) {
    if (
      (def.place ?? '').toLowerCase() === targetPlace &&
      (!srcManner || (def.manner ?? '').toLowerCase() === srcManner) &&
      (!srcVoicing || (def.voicing ?? '').toLowerCase() === srcVoicing)
    ) {
      return hasStressMark(targetPhone) ? `ˈ${sym}` : sym;
    }
  }
  return targetPhone;
}

export function applyRuleSpec(
  spec: RuleSpec,
  phones: readonly string[],
  inventory: readonly Phoneme[],
  stressedSet?: ReadonlySet<number>,
  syllabifier?: Syllabifier
): string[] {
  if (!spec.enabled) return [...phones];

  const target = spec.target.trim();
  const replacement = spec.replacement.trim();
  const prevTarget = spec.prevPhone?.trim() || '';
  const nextTarget = spec.nextPhone?.trim() || '';
  const needsBounds =
    prevTarget === '$' || prevTarget === '.' || nextTarget === '$' || nextTarget === '.';
  const sylBounds = needsBounds ? computeSyllableBoundarySets(phones, inventory, syllabifier) : undefined;

  // Epenthesis: target is ∅ or empty -> insert replacement between prev and next
  if (target === '∅' || target === '') {
    if (!replacement || replacement === '∅') return [...phones];
    const repPhones = replacement.split(/\s+/).filter(Boolean);
    const out: string[] = [];
    for (let i = 0; i <= phones.length; i++) {
      const prevPhone = i > 0 ? phones[i - 1] : null;
      const nextPhone = i < phones.length ? phones[i] : null;
      const prevOk =
        prevTarget === '$' || prevTarget === '.'
          ? Boolean(sylBounds?.starts.has(i))
          : matchesPhoneOrClass(prevPhone, prevTarget, inventory, stressedSet, i - 1);
      const nextOk =
        nextTarget === '$' || nextTarget === '.'
          ? Boolean(sylBounds?.ends.has(i - 1))
          : matchesPhoneOrClass(nextPhone, nextTarget, inventory, stressedSet, i);
      if ((prevTarget || nextTarget) && prevOk && nextOk) {
        out.push(...repPhones);
      }
      if (i < phones.length) out.push(phones[i]);
    }
    return out;
  }

  const isAlphaPlace =
    replacement === '[αplace]' || replacement.toLowerCase() === '[alpha-place]';
  const output: string[] = [];
  const repPhones =
    replacement === '∅' || replacement === '' ? [] : replacement.split(/\s+/).filter(Boolean);

  for (let i = 0; i < phones.length; i++) {
    const phone = phones[i];
    const targetHit = matchesPhoneOrClass(phone, target, inventory, stressedSet, i);
    if (
      targetHit &&
      contextMatch(phones, i, prevTarget, nextTarget, 1, inventory, stressedSet, sylBounds) &&
      matchesCondition(phone, spec.condition, inventory, stressedSet, i)
    ) {
      if (isAlphaPlace) {
        output.push(resolveAlphaPlaceReplacement(phone, phones, i, prevTarget, nextTarget, inventory));
      } else if (repPhones.length > 0 && hasStressMark(phone) && !target.startsWith('ˈ') && !hasStressMark(repPhones[0])) {
        output.push(`ˈ${repPhones[0]}`, ...repPhones.slice(1));
      } else {
        output.push(...repPhones);
      }
    } else {
      output.push(phone);
    }
  }
  return output;
}

export function formatRuleSpec(spec: RuleSpec): string {
  const target = spec.target.trim() || '∅';
  const repl = spec.replacement.trim() || '∅';
  const prev = spec.prevPhone?.trim() ? spec.prevPhone.trim() : '_';
  const next = spec.nextPhone?.trim() ? spec.nextPhone.trim() : '_';
  const cond = spec.condition?.trim() ? ` ${spec.condition.trim()}` : '';
  return `${spec.name}: ${target}→${repl} / ${prev} __ ${next}${cond}`;
}

export interface PluginPhonoRule {
  id: string;
  name: string;
  description: string;
  apply: (phones: readonly string[], inventory: readonly Phoneme[], stressedSet?: ReadonlySet<number>) => string[];
}

export class Phonology {
  public readonly phonemes: Phoneme[];
  public readonly tactics: Phonotactics;
  public readonly syllabifier: Syllabifier;
  public readonly rules: RuleSpec[];
  public readonly stressPattern: StressPattern;
  public readonly pluginRules: PluginPhonoRule[];

  constructor(
    phonemes: Phoneme[],
    tactics: Phonotactics,
    rules: RuleSpec[] = [],
    stressPattern: StressPattern = 'None',
    pluginRules: PluginPhonoRule[] = []
  ) {
    this.phonemes = phonemes;
    this.tactics = tactics;
    this.syllabifier = new Syllabifier(phonemes.filter((p) => p.type === 'V'), tactics);
    this.rules = rules;
    this.stressPattern = stressPattern;
    this.pluginRules = pluginRules;
  }

  public surface(morphemes: readonly MorphemeToken[]): string {
    return this.derive(morphemes).finalSurface;
  }

  private computeStressedPhoneIndices(phones: readonly string[]): Set<number> {
    const stressed = new Set<number>();
    const hasExplicitMark = phones.some(hasStressMark);
    if ((this.stressPattern === 'None' && !hasExplicitMark) || phones.length === 0) {
      return stressed;
    }
    const syls = this.syllabifier.split(phones);
    const effectivePattern: StressPattern =
      hasExplicitMark && this.stressPattern === 'None' ? 'Lexical' : this.stressPattern;
    const targetIdx = resolveStressSyllableIndex(syls.length, effectivePattern, syls);
    if (targetIdx < 0) return stressed;

    let offset = 0;
    for (let i = 0; i < syls.length; i++) {
      const s = syls[i];
      const nucStart = offset + s.onset.length;
      if (i === targetIdx) {
        for (let k = offset; k < offset + s.onset.length + s.nucleus.length + s.coda.length; k++) {
          stressed.add(k);
        }
        if (s.nucleus.length > 0) stressed.add(nucStart);
        break;
      }
      offset += s.onset.length + s.nucleus.length + s.coda.length;
    }
    return stressed;
  }

  public derive(morphemes: readonly MorphemeToken[]): {
    finalSurface: string;
    surfacePhones: string[];
    syllables: Syllable[];
    stressIndex: number;
    steps: DerivationStep[];
    phonotacticIssues: string[];
  } {
    const underlying = morphemes.flatMap((m) => m.underlyingPhones);
    const steps: DerivationStep[] = [{ label: 'UR', value: underlying.join(' ') }];

    let current = [...underlying];
    let stressedIndices = this.computeStressedPhoneIndices(current);

    for (const rule of this.rules) {
      current = applyRuleSpec(rule, current, this.phonemes, stressedIndices, this.syllabifier);
      stressedIndices = this.computeStressedPhoneIndices(current);
      steps.push({ label: rule.name + (rule.enabled ? '' : ' (disabled)'), value: current.join(' ') });
    }

    for (const pRule of this.pluginRules) {
      current = pRule.apply(current, this.phonemes, stressedIndices);
      stressedIndices = this.computeStressedPhoneIndices(current);
      steps.push({ label: `[Plugin] ${pRule.name}`, value: current.join(' ') });
    }

    const syllables = this.syllabifier.split(current);
    const hasExplicitMark = current.some(hasStressMark);
    const effectivePattern: StressPattern =
      hasExplicitMark && this.stressPattern === 'None' ? 'Lexical' : this.stressPattern;
    const stressIndex = resolveStressSyllableIndex(syllables.length, effectivePattern, syllables);
    const issues = this.checkPhonotactics(current, syllables);
    if (issues.length > 0) {
      steps.push({ label: 'Phonotactics', value: issues.join('; ') });
    }

    return {
      finalSurface: current.join(''),
      surfacePhones: current,
      syllables,
      stressIndex,
      steps,
      phonotacticIssues: issues,
    };
  }

  public checkPhonotactics(phones: readonly string[], syllables?: Syllable[]): string[] {
    const issues: string[] = [];
    const wordStr = phones.join('');

    for (const pat of this.tactics.config.forbiddenPatterns) {
      try {
        if (pat.trim() && new RegExp(pat).test(wordStr)) {
          issues.push(`Forbidden pattern /${pat}/ matched in "${wordStr}"`);
        }
      } catch {
        // invalid regex flagged by central validator
      }
    }

    const syls = syllables ?? this.syllabifier.split(phones);
    syls.forEach((syl, idx) => {
      const onset = syl.onset.map((p) => p.replace(/[ˈʰ]/g, '')).join('');
      const coda = syl.coda.map((p) => p.replace(/[ˈʰ]/g, '')).join('');
      if (onset && !this.tactics.isValidOnset(onset, syl.onset.length)) {
        issues.push(`Illegal onset "${onset}" in syllable ${idx + 1}`);
      }
      if (coda && !this.tactics.isValidCoda(coda, syl.coda.length)) {
        issues.push(`Illegal coda "${coda}" in syllable ${idx + 1}`);
      }
    });

    return issues;
  }
}

export class InMemoryLexicon {
  private dict = new Map<string, Lexeme>();

  constructor(initial: Lexeme[]) {
    this.replaceAll(initial);
  }

  public resolveExact(lemmaId: string): Lexeme | undefined {
    return this.dict.get(lemmaId);
  }

  public resolve(lemmaId: string): Lexeme {
    const found = this.dict.get(lemmaId);
    if (found) return found;
    return { lemma: lemmaId, pos: 'V', rootPhones: { UR: lemmaId.split('').join(' ') } };
  }

  public getAll(): Lexeme[] {
    return Array.from(this.dict.values());
  }

  public replaceAll(lexemes: readonly Lexeme[]): void {
    this.dict.clear();
    for (const lexeme of lexemes) {
      this.dict.set(lexeme.lemma, lexeme);
    }
  }
}

export class SyntaxNode {
  public pos: string;
  public lemmaId: string;
  public features: FeatBundle;
  public children: SyntaxNode[];

  constructor(
    pos = 'N',
    lemmaId = '',
    features: FeatBundle = new FeatBundle([]),
    children: SyntaxNode[] = []
  ) {
    this.pos = pos;
    this.lemmaId = lemmaId;
    this.features = features;
    this.children = children;
  }

  public inOrder(): SyntaxNode[] {
    if (
      this.children.length > 0 ||
      this.pos === 'CLAUSE' ||
      this.pos === 'NP' ||
      this.pos === 'VP' ||
      this.pos === 'PP' ||
      this.pos === 'CP'
    ) {
      return this.children.flatMap((c) => c.inOrder());
    }
    return [this];
  }
}

export class SyntaxTree {
  public readonly root: SyntaxNode;

  constructor(root: SyntaxNode) {
    this.root = root;
  }

  public linearize(): SyntaxNode[] {
    return this.root.inOrder();
  }
}

export class ConfigurableSyntaxGenerator {
  private readonly lexicon: InMemoryLexicon;
  public wordOrder: WordOrder;
  public clauseTemplate: ClauseTemplate;

  constructor(
    lexicon: InMemoryLexicon,
    wordOrder: WordOrder = 'SVO',
    clauseTemplate: ClauseTemplate = 'Transitive'
  ) {
    this.lexicon = lexicon;
    this.wordOrder = wordOrder;
    this.clauseTemplate = clauseTemplate;
  }

  public realize(spec: SentenceSpec, rng: RNG): SyntaxTree {
    const nodes = this.buildClauseNodes(spec, rng);
    const order = this.expandWordOrder(this.wordOrder, Object.keys(nodes));
    const root = new SyntaxNode('CLAUSE', this.clauseTemplate);

    for (const role of order) {
      const node = nodes[role];
      if (node) root.children.push(node);
    }

    return new SyntaxTree(root);
  }

  private pickFrom(items: Lexeme[], rng: RNG, fallback: string): string {
    if (items.length === 0) return fallback;
    return items[rng.next(items.length)].lemma;
  }

  private buildClauseNodes(spec: SentenceSpec, rng: RNG): Record<string, SyntaxNode> {
    const lexemes = this.lexicon.getAll();
    const nouns = lexemes.filter((l) => l.pos === 'N');
    const verbs = lexemes.filter((l) => l.pos === 'V');
    const adjs = lexemes.filter((l) => l.pos === 'ADJ');

    const subjLemma = this.pickFrom(nouns, rng, 'person');
    const otherNouns = nouns.filter((n) => n.lemma !== subjLemma);
    const objLemma = this.pickFrom(otherNouns.length > 0 ? otherNouns : nouns, rng, subjLemma);
    const verbLemma = this.pickFrom(verbs, rng, 'see');
    const predLemma = this.pickFrom(adjs, rng, nouns[0]?.lemma ?? 'small');

    const tense = spec.constraints?.Tense ?? 'Pres';
    const numSubj = spec.constraints?.Number ?? (rng.nextDouble() < 0.35 ? 'Pl' : 'Sing');
    const numObj = spec.constraints?.Number ?? (rng.nextDouble() < 0.35 ? 'Pl' : 'Sing');

    let subjNode: SyntaxNode = this.newRoleNode('N', subjLemma, 'Subj', [
      { name: 'Number', value: numSubj },
    ]);
    let objNode: SyntaxNode = this.newRoleNode('N', objLemma, 'Obj', [
      { name: 'Number', value: numObj },
    ]);

    // If approxWords >= 4 and adjectives exist, expand NP with an adjective modifier
    if (spec.approxWords >= 4 && adjs.length > 0) {
      const modAdj = this.pickFrom(adjs, rng, 'small');
      subjNode = new SyntaxNode('NP', 'SubjNP', new FeatBundle([{ name: 'Role', value: 'SubjNP' }]), [
        this.newRoleNode('ADJ', modAdj, 'Mod', []),
        subjNode,
      ]);
    }
    if (spec.approxWords >= 5 && adjs.length > 0 && this.clauseTemplate === 'Transitive') {
      const modAdj2 = this.pickFrom(adjs, rng, 'small');
      objNode = new SyntaxNode('NP', 'ObjNP', new FeatBundle([{ name: 'Role', value: 'ObjNP' }]), [
        this.newRoleNode('ADJ', modAdj2, 'Mod', []),
        objNode,
      ]);
    }

    const verbNode = this.newRoleNode('V', verbLemma, 'Verb', [{ name: 'Tense', value: tense }]);
    const predNode = this.newRoleNode('ADJ', predLemma, 'Pred', []);

    if (this.clauseTemplate === 'Intransitive') {
      return { S: subjNode, V: verbNode };
    }
    if (this.clauseTemplate === 'Copular') {
      const copulaLex = this.lexicon.resolveExact('be') ? 'be' : 'e';
      return {
        S: subjNode,
        V: this.newRoleNode('V', copulaLex, 'Copula', [{ name: 'Tense', value: tense }]),
        O: predNode,
      };
    }
    return { S: subjNode, V: verbNode, O: objNode };
  }

  private expandWordOrder(order: WordOrder, available: string[]): string[] {
    const canonicalMap: Record<WordOrder, string[]> = {
      SVO: ['S', 'V', 'O'],
      SOV: ['S', 'O', 'V'],
      VSO: ['V', 'S', 'O'],
      VOS: ['V', 'O', 'S'],
      OVS: ['O', 'V', 'S'],
      OSV: ['O', 'S', 'V'],
    };
    const present = new Set(available);
    return canonicalMap[order].filter((role) => present.has(role));
  }

  private newRoleNode(pos: string, lemma: string, role: string, extra: Feat[]): SyntaxNode {
    return new SyntaxNode(pos, lemma, new FeatBundle([{ name: 'Role', value: role }, ...extra]));
  }
}

export class Morphology {
  public readonly lexicon: InMemoryLexicon;
  public readonly affixRules: AffixRule[];
  public readonly phonemes: Phoneme[];

  constructor(lexicon: InMemoryLexicon, affixRules: AffixRule[], phonemes: Phoneme[] = []) {
    this.lexicon = lexicon;
    this.affixRules = affixRules;
    this.phonemes = phonemes;
  }

  private resolveIrregularStem(
    lex: Lexeme,
    features: FeatBundle
  ): { ur: string; matchedFeatures: Set<string>; isIrregular: boolean } {
    const irregulars = lex.irregulars;
    if (irregulars) {
      let bestScore = 0;
      let bestUr = '';
      let bestKeys = new Set<string>();

      for (const [keySpec, form] of Object.entries(irregulars)) {
        const clauses = keySpec
          .split(',')
          .map((c) => c.trim())
          .filter(Boolean)
          .map((c) => c.split('=', 2).map((s) => s.trim()))
          .filter((pair) => pair.length === 2 && pair[0] && pair[1]);
        if (clauses.length === 0) continue;
        if (clauses.every(([k, v]) => features.has(k, v)) && clauses.length > bestScore) {
          bestScore = clauses.length;
          bestUr = form;
          bestKeys = new Set(clauses.map(([k]) => k));
        }
      }

      if (bestScore > 0) {
        if (bestKeys.has('Person') || bestKeys.has('Tense')) {
          bestKeys.add('Agreement');
        }
        return { ur: bestUr, matchedFeatures: bestKeys, isIrregular: true };
      }
    }

    return {
      ur: lex.rootPhones.UR ?? lex.lemma,
      matchedFeatures: new Set<string>(),
      isIrregular: false,
    };
  }

  public inflectNodeDetailed(node: SyntaxNode): { tokens: MorphemeToken[]; isIrregular: boolean; spelling?: string } {
    const lex = this.lexicon.resolve(node.lemmaId);
    const { ur, matchedFeatures, isIrregular } = this.resolveIrregularStem(lex, node.features);
    const basePhones = Morphology.splitPhones(ur);
    const morphemes = [
      {
        id: isIrregular ? 'STEM_IRREG' : 'STEM',
        gloss: lex.lemma,
        underlyingPhones: basePhones,
      },
    ];

    const handledFeatures = new Set<string>(matchedFeatures);

    for (const rule of this.affixRules) {
      if (!rule.enabled || rule.pos !== node.pos) continue;
      if (handledFeatures.has(rule.featureName)) continue;
      if (!node.features.has(rule.featureName, rule.featureValue)) continue;

      if (rule.condition?.trim()) {
        const currentFlat = morphemes.flatMap((m) => m.underlyingPhones);
        if (currentFlat.length === 0) continue;
        const anchor = rule.isPrefix ? currentFlat[0] : currentFlat[currentFlat.length - 1];
        if (!matchesCondition(anchor, rule.condition, this.phonemes)) continue;
      }

      const affixPhones = Morphology.splitPhones(rule.affix.replace(/-/g, ''));
      const morpheme = {
        id: `AFX_${rule.name}`,
        gloss: rule.name,
        underlyingPhones: affixPhones,
      };
      if (rule.isPrefix) {
        morphemes.unshift(morpheme);
      } else {
        morphemes.push(morpheme);
      }
      handledFeatures.add(rule.featureName);
    }

    return {
      tokens: morphemes.map((m) => ({ morpheme: m, underlyingPhones: m.underlyingPhones })),
      isIrregular,
      spelling: lex.spelling,
    };
  }

  public inflectNode(node: SyntaxNode): MorphemeToken[] {
    return this.inflectNodeDetailed(node).tokens;
  }

  public inflect(tree: SyntaxTree): MorphemeToken[] {
    return tree.linearize().flatMap((n) => this.inflectNode(n));
  }

  public realizeForPreview(lemma: string, pos: string, features: FeatBundle): string {
    const node = new SyntaxNode(pos, lemma, features);
    const tokens = this.inflectNode(node);
    return tokens.flatMap((t) => t.underlyingPhones).join(' ');
  }

  private static readonly MULTI_CHAR_SEGMENTS = new Set([
    'iː', 'eɪ', 'oʊ', 'ɔː', 'uː', 'ɑː', 'ɝː', 'aɪ', 'aʊ', 'ɔɪ',
    'tʃ', 'dʒ', 'pʰ', 'tʰ', 'kʰ',
  ]);

  public static splitPhones(source: string): string[] {
    const trimmed = source.trim();
    if (!trimmed) return [];
    if (trimmed.includes(' ')) {
      return trimmed.split(/\s+/).filter(Boolean);
    }
    const noStress = trimmed.replace(/^ˈ/, '');
    if (Morphology.MULTI_CHAR_SEGMENTS.has(noStress)) {
      return [trimmed];
    }
    const chars = [...trimmed];
    const result: string[] = [];
    for (let i = 0; i < chars.length; i++) {
      const ch = chars[i];
      if (ch === 'ˈ' && i + 1 < chars.length) {
        result.push(ch + chars[++i]);
      } else if ((ch === 'ː' || ch === 'ʰ') && result.length > 0) {
        result[result.length - 1] += ch;
      } else {
        result.push(ch);
      }
    }
    return result;
  }
}

export class Orthography {
  public readonly rules: OrthographyRule[];
  public readonly exceptions: OrthographyException[];
  public readonly capitalization: CapitalizationMode;

  constructor(
    rules: OrthographyRule[],
    exceptions: OrthographyException[] = [],
    capitalization: CapitalizationMode = 'SentenceInitial'
  ) {
    this.rules = rules;
    this.exceptions = exceptions;
    this.capitalization = capitalization;
  }

  public render(surfacePhones: string): string {
    return this.renderWithTokens(surfacePhones).text;
  }

  public renderWordsWithTokens(words: readonly RealizedWordContext[]): OrthographyResult {
    const surfaceWithSpaces = words.map((w) => w.surface).join(' ');
    return this.renderWithTokens(surfaceWithSpaces);
  }

  /**
   * Produces BOTH rendered display text AND Layer 1 abstract GraphemeToken[]
   * for the Agnostic Writing Systems Shaping Engine.
   */
  public renderWithTokens(surfacePhones: string): OrthographyResult {
    if (!surfacePhones || !surfacePhones.trim()) {
      return { text: '', tokens: [] };
    }

    const words = surfacePhones.trim().split(/\s+/);
    const mappedWords: string[] = [];
    const tokens: GraphemeToken[] = [];

    words.forEach((w, wordIndex) => {
      const { wordText, graphemes } = this.mapWordDetailed(w);
      mappedWords.push(wordText);

      graphemes.forEach((g, idx) => {
        let positionInWord: GraphemeToken['positionInWord'] = 'medial';
        if (graphemes.length === 1) positionInWord = 'isolated';
        else if (idx === 0) positionInWord = 'initial';
        else if (idx === graphemes.length - 1) positionInWord = 'final';

        tokens.push({
          id: g.toLowerCase(),
          grapheme: g,
          wordIndex,
          positionInWord,
        });
      });
    });

    const text = this.applyCapitalization(mappedWords.join(' '));
    return { text, tokens };
  }

  protected mapWordDetailed(word: string): { wordText: string; graphemes: string[] } {
    const cleanWord = word.replace(/ˈ/g, '');
    const excepted =
      this.exceptions.find((e) => e.input === word) ??
      this.exceptions.find((e) => e.input.replace(/ˈ/g, '') === cleanWord);
    if (excepted) {
      return {
        wordText: excepted.output,
        graphemes: [...excepted.output],
      };
    }

    const orderedRules = this.rules
      .filter((r) => r.enabled && r.phone.trim().length > 0)
      .sort((a, b) => b.phone.length - a.phone.length);

    const graphemes: string[] = [];
    let out = '';
    for (let i = 0; i < cleanWord.length; ) {
      let matched = false;
      for (const rule of orderedRules) {
        if (i + rule.phone.length > cleanWord.length) continue;
        if (cleanWord.slice(i, i + rule.phone.length) !== rule.phone) continue;
        if (!this.matchesContext(cleanWord, i, rule)) continue;

        out += rule.grapheme;
        if (rule.grapheme) graphemes.push(rule.grapheme);
        i += rule.phone.length;
        matched = true;
        break;
      }
      if (!matched) {
        out += cleanWord[i];
        graphemes.push(cleanWord[i]);
        i++;
      }
    }
    return { wordText: out, graphemes };
  }

  private matchesContext(word: string, index: number, rule: OrthographyRule): boolean {
    const prev = rule.prevContext?.trim() ?? '';
    if (prev.length > 0) {
      const prevIndex = index - prev.length;
      if (prevIndex < 0 || word.slice(prevIndex, prevIndex + prev.length) !== prev) {
        return false;
      }
    }

    const next = rule.nextContext?.trim() ?? '';
    if (next.length > 0) {
      const nextIndex = index + rule.phone.length;
      if (nextIndex + next.length > word.length || word.slice(nextIndex, nextIndex + next.length) !== next) {
        return false;
      }
    }

    return true;
  }

  protected applyCapitalization(text: string): string {
    if (!text.trim()) return text;
    if (this.capitalization === 'AllWords') {
      return text
        .split(/\s+/)
        .map((w) => (w ? w[0].toUpperCase() + w.slice(1) : w))
        .join(' ');
    }
    if (this.capitalization === 'SentenceInitial') {
      return text[0].toUpperCase() + text.slice(1);
    }
    return text;
  }
}

export class WordGenerator {
  private readonly state: LanguageProjectState;
  private readonly engine: LangEngine;
  private readonly rng: RNG;

  constructor(state: LanguageProjectState, seed?: number) {
    this.state = state;
    this.engine = buildEngineFromState(state);
    this.rng = new RNG(seed ?? Date.now());
  }

  public generateWords(count: number, syllables: number): string[] {
    const results: string[] = [];
    const seen = new Set<string>();
    let safety = 0;
    const allowDuplicates = this.state.GeneratorOptions.allowDuplicates;

    while (results.length < count && safety < count * 300) {
      const w = this.generateWord(syllables, 0);
      safety++;
      if (allowDuplicates || !seen.has(w)) {
        seen.add(w);
        results.push(w);
      }
    }
    return results;
  }

  public generateWord(syllables = 2, depth = 0): string {
    const pieces: string[][] = [];
    const templates =
      this.state.SyllableTemplates.length > 0
        ? this.state.SyllableTemplates
        : [{ pattern: 'CV', weight: 1 }];

    for (let i = 0; i < syllables; i++) {
      const tmpl = this.weightedPickTemplate(templates);
      pieces.push(this.renderSyllablePhones(tmpl.pattern));
    }

    let phones = pieces.flat();

    if (this.state.Affixes.length > 0 && this.rng.nextDouble() < 0.2) {
      const enabledAffixes = this.state.Affixes.filter((a) => a.Enabled && a.Affix.trim());
      if (enabledAffixes.length > 0) {
        const afx = enabledAffixes[this.rng.next(enabledAffixes.length)];
        const afxPhones = Morphology.splitPhones(afx.Affix.replace(/-/g, ''));
        phones = afx.IsPrefix ? [...afxPhones, ...phones] : [...phones, ...afxPhones];
      }
    }

    const derived = this.engine.phonology.derive([
      {
        morpheme: { id: 'GEN', gloss: 'gen', underlyingPhones: phones },
        underlyingPhones: phones,
      },
    ]);

    if (derived.phonotacticIssues.length > 0 && depth < 25) {
      return this.generateWord(syllables, depth + 1);
    }

    const joined = this.state.GeneratorOptions.hyphenateSyllables
      ? pieces.map((s) => this.engine.orthography.render(s.join(''))).join('-').toLowerCase()
      : this.engine.orthography.render(derived.finalSurface).toLowerCase();

    return joined;
  }

  private renderSyllablePhones(pattern: string): string[] {
    const consonants = this.state.Phonemes.filter((p) => p.type === 'C');
    const vowels = this.state.Phonemes.filter((p) => p.type === 'V');
    const out: string[] = [];
    let optional = false;
    let seenVowel = false;

    for (const ch of pattern) {
      if (ch === '(') {
        optional = true;
        continue;
      }
      if (ch === ')') {
        optional = false;
        continue;
      }
      if (optional && this.rng.nextDouble() < 0.5) continue;

      if (ch === 'C') {
        const pool = consonants.filter((c) => (!seenVowel ? c.allowOnset : c.allowCoda));
        const picked = this.pickWeightedPhoneme(pool.length > 0 ? pool : consonants);
        if (picked) out.push(picked.symbol);
      } else if (ch === 'V') {
        seenVowel = true;
        const picked = this.pickWeightedPhoneme(vowels);
        if (picked) out.push(picked.symbol);
      } else {
        out.push(ch);
      }
    }
    return out;
  }

  private weightedPickTemplate<T extends { weight: number }>(items: T[]): T {
    let total = items.reduce((acc, it) => acc + Math.max(0, it.weight), 0);
    if (total <= 0) total = items.length;
    const r = this.rng.next(1, total + 1);
    let acc = 0;
    for (const it of items) {
      acc += Math.max(0, it.weight);
      if (r <= acc) return it;
    }
    return items[items.length - 1];
  }

  private pickWeightedPhoneme(items: Phoneme[]): Phoneme | null {
    if (items.length === 0) return null;
    return this.weightedPickTemplate(items);
  }
}

export interface ISyntaxGeneratorModule {
  id: string;
  name: string;
  description: string;
  create: (lexicon: InMemoryLexicon, wordOrder: WordOrder, clauseTemplate: ClauseTemplate) => ConfigurableSyntaxGenerator;
}

export interface IMorphologyRealizerModule {
  id: string;
  name: string;
  description: string;
  create: (lexicon: InMemoryLexicon, affixRules: AffixRule[], phonemes?: Phoneme[]) => Morphology;
}

export interface IOrthographyRendererModule {
  id: string;
  name: string;
  description: string;
  create: (
    rules: OrthographyRule[],
    exceptions: OrthographyException[],
    capitalization: CapitalizationMode
  ) => Orthography;
}

class HeadFinalRichSyntaxGenerator extends ConfigurableSyntaxGenerator {
  public override realize(spec: SentenceSpec, rng: RNG): SyntaxTree {
    const boostedSpec: SentenceSpec = {
      ...spec,
      approxWords: Math.max(spec.approxWords, 5),
    };
    return super.realize(boostedSpec, rng);
  }
}

class PhraseStructureSyntaxGenerator extends ConfigurableSyntaxGenerator {
  private readonly lexRef: InMemoryLexicon;

  constructor(lexicon: InMemoryLexicon, wordOrder: WordOrder = 'SVO', clauseTemplate: ClauseTemplate = 'Transitive') {
    super(lexicon, wordOrder, clauseTemplate);
    this.lexRef = lexicon;
  }

  public override realize(spec: SentenceSpec, rng: RNG): SyntaxTree {
    const lexemes = this.lexRef.getAll();
    const nouns = lexemes.filter((l) => l.pos === 'N');
    const verbs = lexemes.filter((l) => l.pos === 'V');
    const adjs = lexemes.filter((l) => l.pos === 'ADJ');
    const dets = lexemes.filter((l) => l.pos === 'DET');
    const prons = lexemes.filter((l) => l.pos === 'PRON');
    const auxes = lexemes.filter((l) => l.pos === 'AUX');
    const negs = lexemes.filter((l) => l.pos === 'NEG');
    const preps = lexemes.filter((l) => l.pos === 'PREP');
    const advs = lexemes.filter((l) => l.pos === 'ADV');

    const pick = <T,>(list: T[]): T | undefined => (list.length > 0 ? list[rng.next(list.length)] : undefined);

    const tense = spec.constraints?.Tense ?? 'Pres';
    const constrainedNumber = spec.constraints?.Number;
    const isNeg = spec.constraints?.Polarity === 'Neg' || (negs.length > 0 && rng.nextDouble() < 0.15);

    const buildNp = (
      rolePrefix: 'Subj' | 'Obj',
      gramCase: 'Nom' | 'Acc',
      avoidLemma?: string
    ): { node: SyntaxNode; person: string; number: string; headLemma: string } => {
      const eligibleProns = avoidLemma ? prons.filter((p) => p.lemma !== avoidLemma) : prons;
      if (eligibleProns.length > 0 && rng.nextDouble() < 0.35) {
        const pron = pick(eligibleProns)!;
        const lower = pron.lemma.toLowerCase();
        const person = lower === 'i' || lower === 'we' ? '1' : lower === 'you' ? '2' : '3';
        const number = constrainedNumber ?? (lower === 'they' || lower === 'we' || lower === 'you' ? 'Pl' : 'Sing');
        const pronNode = new SyntaxNode(
          'PRON',
          pron.lemma,
          new FeatBundle([
            { name: 'Role', value: rolePrefix },
            { name: 'Person', value: person },
            { name: 'Number', value: number },
            { name: 'Case', value: gramCase },
          ])
        );
        return { node: pronNode, person, number, headLemma: pron.lemma };
      }

      const pool = nouns.filter((n) => n.lemma !== avoidLemma);
      const noun = pick(pool.length > 0 ? pool : nouns);
      const nounLemma = noun?.lemma ?? 'person';
      const number = constrainedNumber ?? (rng.nextDouble() < 0.35 ? 'Pl' : 'Sing');
      const children: SyntaxNode[] = [];

      if (dets.length > 0) {
        const isMass = nounLemma.toLowerCase() === 'water';
        const compatibleDets =
          number === 'Pl' || isMass
            ? dets.filter((d) => d.lemma.toLowerCase() !== 'a' && d.lemma.toLowerCase() !== 'an')
            : dets;
        const det = pick(compatibleDets.length > 0 ? compatibleDets : dets);
        if (det) {
          children.push(
            new SyntaxNode(
              'DET',
              det.lemma,
              new FeatBundle([
                { name: 'Role', value: 'Det' },
                { name: 'Number', value: number },
              ])
            )
          );
        }
      }

      if (spec.approxWords >= 4 && adjs.length > 0 && rng.nextDouble() < 0.65) {
        const adj = pick(adjs)!;
        children.push(new SyntaxNode('ADJ', adj.lemma, new FeatBundle([{ name: 'Role', value: 'Mod' }])));
      }

      children.push(
        new SyntaxNode(
          'N',
          nounLemma,
          new FeatBundle([
            { name: 'Role', value: rolePrefix },
            { name: 'Person', value: '3' },
            { name: 'Number', value: number },
            { name: 'Case', value: gramCase },
          ])
        )
      );

      const np = new SyntaxNode(
        'NP',
        `${rolePrefix}NP`,
        new FeatBundle([{ name: 'Role', value: `${rolePrefix}NP` }]),
        children
      );
      return { node: np, person: '3', number, headLemma: nounLemma };
    };

    const subj = buildNp('Subj', 'Nom');
    const obj = buildNp('Obj', 'Acc', subj.headLemma);

    const verbPool = this.clauseTemplate === 'Copular'
      ? verbs.filter((v) => v.lemma === 'be')
      : verbs.filter((v) => v.lemma !== 'be');
    const chosenVerb =
      this.clauseTemplate === 'Copular'
        ? (this.lexRef.resolveExact('be')?.lemma ?? 'be')
        : (pick(verbPool.length > 0 ? verbPool : verbs)?.lemma ?? 'see');

    const vpChildren: SyntaxNode[] = [];
    const wantFutureAux = tense === 'Fut' && auxes.some((a) => a.lemma === 'will');
    const doAux = auxes.find((a) => a.lemma === 'do');
    const advNode =
      advs.length > 0 && spec.approxWords >= 5 && rng.nextDouble() < 0.45
        ? new SyntaxNode('ADV', pick(advs)!.lemma, new FeatBundle([{ name: 'Role', value: 'AdvMod' }]))
        : null;

    if (wantFutureAux) {
      vpChildren.push(
        new SyntaxNode('AUX', 'will', new FeatBundle([{ name: 'Role', value: 'Aux' }, { name: 'Tense', value: 'Pres' }]))
      );
      if (isNeg && negs.length > 0) {
        vpChildren.push(new SyntaxNode('NEG', negs[0].lemma, new FeatBundle([{ name: 'Role', value: 'Neg' }])));
      }
      if (advNode) vpChildren.push(advNode);
      vpChildren.push(
        new SyntaxNode(
          'V',
          chosenVerb,
          new FeatBundle([{ name: 'Role', value: 'Verb' }, { name: 'Finiteness', value: 'Bare' }])
        )
      );
    } else if (isNeg && negs.length > 0 && chosenVerb !== 'be' && doAux) {
      const auxFeats: Feat[] = [
        { name: 'Role', value: 'Aux' },
        { name: 'Tense', value: tense },
        { name: 'Person', value: subj.person },
        { name: 'Number', value: subj.number },
      ];
      if (tense === 'Pres' && subj.person === '3' && subj.number === 'Sing') {
        auxFeats.push({ name: 'Agreement', value: '3sg' });
      }
      vpChildren.push(new SyntaxNode('AUX', doAux.lemma, new FeatBundle(auxFeats)));
      vpChildren.push(new SyntaxNode('NEG', negs[0].lemma, new FeatBundle([{ name: 'Role', value: 'Neg' }])));
      if (advNode) vpChildren.push(advNode);
      vpChildren.push(
        new SyntaxNode(
          'V',
          chosenVerb,
          new FeatBundle([{ name: 'Role', value: 'Verb' }, { name: 'Finiteness', value: 'Bare' }])
        )
      );
    } else {
      if (advNode && chosenVerb !== 'be') vpChildren.push(advNode);
      const verbFeats: Feat[] = [
        { name: 'Role', value: this.clauseTemplate === 'Copular' ? 'Copula' : 'Verb' },
        { name: 'Tense', value: tense },
        { name: 'Person', value: subj.person },
        { name: 'Number', value: subj.number },
      ];
      if (tense === 'Pres' && subj.person === '3' && subj.number === 'Sing') {
        verbFeats.push({ name: 'Agreement', value: '3sg' });
      }
      vpChildren.push(new SyntaxNode('V', chosenVerb, new FeatBundle(verbFeats)));
      if (isNeg && negs.length > 0 && chosenVerb === 'be') {
        vpChildren.push(new SyntaxNode('NEG', negs[0].lemma, new FeatBundle([{ name: 'Role', value: 'Neg' }])));
      }
      if (advNode && chosenVerb === 'be') vpChildren.push(advNode);
    }

    const vNode =
      vpChildren.length === 1
        ? vpChildren[0]
        : new SyntaxNode('VP', 'VP', new FeatBundle([{ name: 'Role', value: 'VP' }]), vpChildren);

    const roles: Record<string, SyntaxNode> =
      this.clauseTemplate === 'Intransitive'
        ? { S: subj.node, V: vNode }
        : this.clauseTemplate === 'Copular'
          ? {
              S: subj.node,
              V: vNode,
              O: new SyntaxNode(
                'ADJ',
                pick(adjs)?.lemma ?? 'small',
                new FeatBundle([{ name: 'Role', value: 'Pred' }])
              ),
            }
          : { S: subj.node, V: vNode, O: obj.node };

    const canonical: Record<WordOrder, string[]> = {
      SVO: ['S', 'V', 'O'],
      SOV: ['S', 'O', 'V'],
      VSO: ['V', 'S', 'O'],
      VOS: ['V', 'O', 'S'],
      OVS: ['O', 'V', 'S'],
      OSV: ['O', 'S', 'V'],
    };

    const root = new SyntaxNode('CLAUSE', this.clauseTemplate);
    for (const r of canonical[this.wordOrder]) {
      if (roles[r]) root.children.push(roles[r]);
    }

    if (spec.approxWords >= 6 && preps.length > 0 && nouns.length > 0) {
      const prep = pick(preps)!;
      const pobj = buildNp('Obj', 'Acc');
      root.children.push(
        new SyntaxNode('PP', 'PP', new FeatBundle([{ name: 'Role', value: 'PP' }]), [
          new SyntaxNode('PREP', prep.lemma, new FeatBundle([{ name: 'Role', value: 'Prep' }])),
          pobj.node,
        ])
      );
    }

    return new SyntaxTree(root);
  }
}

class ReduplicativePluralMorphology extends Morphology {
  public override inflectNode(node: SyntaxNode): MorphemeToken[] {
    const tokens = super.inflectNode(node);
    if (node.pos === 'N' && node.features.has('Number', 'Pl')) {
      const stem = tokens.find((t) => t.morpheme.id === 'STEM');
      if (stem && stem.underlyingPhones.length >= 2) {
        const redup = stem.underlyingPhones.slice(0, 2);
        return [
          {
            morpheme: { id: 'REDUP_PL', gloss: 'PL.REDUP', underlyingPhones: redup },
            underlyingPhones: redup,
          },
          ...tokens,
        ];
      }
    }
    return tokens;
  }

  public override realizeForPreview(lemma: string, pos: string, features: FeatBundle): string {
    const base = super.realizeForPreview(lemma, pos, features);
    if (pos === 'N' && features.has('Number', 'Pl')) {
      const parts = Morphology.splitPhones(this.lexicon.resolve(lemma).rootPhones.UR ?? lemma);
      if (parts.length >= 2) return `${parts.slice(0, 2).join(' ')} ${base}`;
    }
    return base;
  }
}

class IpaBroadOrthography extends Orthography {
  public override renderWithTokens(surfacePhones: string): OrthographyResult {
    const base = super.renderWithTokens(surfacePhones);
    const words = surfacePhones.trim().split(/\s+/).filter(Boolean);
    const ipaText = words.length > 0 ? `/${words.join(' ')}/` : '';
    return {
      text: ipaText,
      tokens: base.tokens,
    };
  }
}

class MorphophonemicOrthography extends Orthography {
  private inflectRegularSpelling(baseSpelling: string, ctx: RealizedWordContext): string {
    const feats = ctx.features;
    if (!feats) return baseSpelling;

    const isPluralN = ctx.pos === 'N' && feats.has('Number', 'Pl');
    const is3sgPres =
      (ctx.pos === 'V' || ctx.pos === 'AUX') &&
      feats.has('Tense', 'Pres') &&
      feats.has('Person', '3') &&
      feats.has('Number', 'Sing');
    const isPastV = (ctx.pos === 'V' || ctx.pos === 'AUX') && feats.has('Tense', 'Past');

    if (isPluralN || is3sgPres) {
      if (/(s|x|z|ch|sh)$/i.test(baseSpelling)) return `${baseSpelling}es`;
      if (/[^aeiou]y$/i.test(baseSpelling)) return `${baseSpelling.slice(0, -1)}ies`;
      return `${baseSpelling}s`;
    }

    if (isPastV) {
      if (baseSpelling.endsWith('e')) return `${baseSpelling}d`;
      if (/[^aeiou]y$/i.test(baseSpelling)) return `${baseSpelling.slice(0, -1)}ied`;
      return `${baseSpelling}ed`;
    }

    return baseSpelling;
  }

  public override renderWordsWithTokens(words: readonly RealizedWordContext[]): OrthographyResult {
    if (words.length === 0) return { text: '', tokens: [] };

    const mappedWords: string[] = [];
    const tokens: GraphemeToken[] = [];

    words.forEach((ctx, wordIndex) => {
      const cleanSurface = ctx.surface.replace(/ˈ/g, '');
      const excepted =
        this.exceptions.find((e) => e.input === ctx.surface) ??
        this.exceptions.find((e) => e.input.replace(/ˈ/g, '') === cleanSurface);

      let wordText: string;
      let graphemes: string[];

      if (excepted) {
        wordText = excepted.output;
        graphemes = [...excepted.output];
      } else if (ctx.spelling && ctx.spelling.trim() && !ctx.isIrregular) {
        wordText = this.inflectRegularSpelling(ctx.spelling.trim(), ctx);
        graphemes = [...wordText];
      } else {
        const detailed = this.mapWordDetailed(ctx.surface);
        wordText = detailed.wordText;
        graphemes = detailed.graphemes;
      }

      mappedWords.push(wordText);
      graphemes.forEach((g, idx) => {
        let positionInWord: GraphemeToken['positionInWord'] = 'medial';
        if (graphemes.length === 1) positionInWord = 'isolated';
        else if (idx === 0) positionInWord = 'initial';
        else if (idx === graphemes.length - 1) positionInWord = 'final';

        tokens.push({
          id: g.toLowerCase(),
          grapheme: g,
          wordIndex,
          positionInWord,
        });
      });
    });

    const text = this.applyCapitalization(mappedWords.join(' '));
    return { text, tokens };
  }
}

export const BUILTIN_SYNTAX_MODULES: readonly ISyntaxGeneratorModule[] = [
  {
    id: 'configurable-syntax',
    name: 'Configurable Syntax (Default)',
    description: 'Standard SVO/SOV/VSO clause planner with NP adjective expansion.',
    create: (lex, wo, ct) => new ConfigurableSyntaxGenerator(lex, wo, ct),
  },
  {
    id: 'rich-np-syntax',
    name: 'Expanded Modifier Syntax (Phase 4)',
    description: 'Always expands subject/object NPs with adjective modifiers regardless of word target.',
    create: (lex, wo, ct) => new HeadFinalRichSyntaxGenerator(lex, wo, ct),
  },
  {
    id: 'phrase-structure-syntax',
    name: 'Living-Language Phrase Structure (DET, PRON, AUX, PP)',
    description: 'Hierarchical clauses supporting determiners, pronouns with Case/Person/Number, auxiliaries, negation, and PPs.',
    create: (lex, wo, ct) => new PhraseStructureSyntaxGenerator(lex, wo, ct),
  },
];

export const BUILTIN_MORPHOLOGY_MODULES: readonly IMorphologyRealizerModule[] = [
  {
    id: 'standard-morphology',
    name: 'Affixation Realizer (Default)',
    description: 'Lexicon-backed stem, irregular suppletion, and conditioned prefix/suffix allomorphy.',
    create: (lex, affixes, phonemes = []) => new Morphology(lex, affixes, phonemes),
  },
  {
    id: 'reduplicative-plural',
    name: 'CV Reduplication + Affixes (Phase 4)',
    description: 'Prepends initial CV stem reduplication for Number=Pl alongside active affixes.',
    create: (lex, affixes, phonemes = []) => new ReduplicativePluralMorphology(lex, affixes, phonemes),
  },
];

export const BUILTIN_ORTHOGRAPHY_MODULES: readonly IOrthographyRendererModule[] = [
  {
    id: 'standard-orthography',
    name: 'Rule-Driven Orthography (Default)',
    description: 'Context-sensitive grapheme rules, exceptions, and capitalization.',
    create: (rules, exc, cap) => new Orthography(rules, exc, cap),
  },
  {
    id: 'morphophonemic-orthography',
    name: 'Morphophonemic / Deep Orthography (English-Style)',
    description: 'Uses lexical historical spellings + morphological spelling rules alongside surface phonological exceptions.',
    create: (rules, exc, cap) => new MorphophonemicOrthography(rules, exc, cap),
  },
  {
    id: 'ipa-broad',
    name: 'IPA Broad Transcription (Phase 4)',
    description: 'Renders surface phonemic output wrapped in /.../ slashes for linguistic inspection.',
    create: (rules, exc, cap) => new IpaBroadOrthography(rules, exc, cap),
  },
];

export const BUILTIN_PLUGIN_PHONO_RULES: readonly PluginPhonoRule[] = [
  {
    id: 'intervocalic-stop-lenition',
    name: 'Intervocalic Stop Voicing',
    description: 'Voiceless stops become voiced between vowels (p→b, t→d, k→g / V__V).',
    apply: (phones, inventory, stressed) => {
      const pairs: Array<[string, string]> = [
        ['p', 'b'],
        ['t', 'd'],
        ['k', 'g'],
      ];
      let cur = [...phones];
      for (const [src, dst] of pairs) {
        cur = applyRuleSpec(
          { name: 'Lenition', target: src, replacement: dst, prevPhone: 'V', nextPhone: 'V', enabled: true },
          cur,
          inventory,
          stressed
        );
      }
      return cur;
    },
  },
  {
    id: 'final-obstruent-devoicing',
    name: 'Word-Final Obstruent Devoicing',
    description: 'Voiced stops/fricatives devoice word-finally (b→p, d→t, g→k, z→s / __#).',
    apply: (phones, inventory, stressed) => {
      const pairs: Array<[string, string]> = [
        ['b', 'p'],
        ['d', 't'],
        ['g', 'k'],
        ['z', 's'],
        ['v', 'f'],
      ];
      let cur = [...phones];
      for (const [src, dst] of pairs) {
        cur = applyRuleSpec(
          { name: 'FinalDevoicing', target: src, replacement: dst, prevPhone: null, nextPhone: '#', enabled: true },
          cur,
          inventory,
          stressed
        );
      }
      return cur;
    },
  },
  {
    id: 'unstressed-vowel-reduction',
    name: 'Unstressed Vowel Reduction (-> ə)',
    description: 'Reduces unstressed vowels to ə when StressPattern is active ([-stressed]).',
    apply: (phones, inventory, stressed) => {
      if (!stressed || stressed.size === 0) return [...phones];
      return applyRuleSpec(
        {
          name: 'SchwaReduction',
          target: 'V',
          replacement: 'ə',
          prevPhone: null,
          nextPhone: null,
          condition: '[-stressed]',
          enabled: true,
        },
        phones,
        inventory,
        stressed
      );
    },
  },
];

export const DEFAULT_PIPELINE_SELECTION: PipelineSelection = {
  syntaxGeneratorId: 'configurable-syntax',
  morphologyRealizerId: 'standard-morphology',
  orthographyRendererId: 'standard-orthography',
  enabledPhonoPluginRules: [],
};

export class LangEngine {
  public readonly phonology: Phonology;
  public readonly morphology: Morphology;
  public readonly syntax: ConfigurableSyntaxGenerator;
  public readonly orthography: Orthography;

  constructor(
    phonology: Phonology,
    morphology: Morphology,
    syntax: ConfigurableSyntaxGenerator,
    orthography: Orthography
  ) {
    this.phonology = phonology;
    this.morphology = morphology;
    this.syntax = syntax;
    this.orthography = orthography;
  }

  public generateSentence(rng: RNG, spec: SentenceSpec): {
    written: string;
    tokens: GraphemeToken[];
    surface: string;
    issues: string[];
  } {
    const sTree = this.syntax.realize(spec, rng);
    const linearized = sTree.linearize();
    const wordInfos = linearized.map((node) => {
      const { tokens, isIrregular, spelling } = this.morphology.inflectNodeDetailed(node);
      const derived = this.phonology.derive(tokens);
      return {
        derived,
        context: {
          surface: derived.finalSurface,
          lemma: node.lemmaId,
          pos: node.pos,
          features: node.features,
          spelling,
          isIrregular,
        } satisfies RealizedWordContext,
      };
    });

    const surfaceWithSpaces = wordInfos.map((w) => w.derived.finalSurface).join(' ');
    const orthResult = this.orthography.renderWordsWithTokens(wordInfos.map((w) => w.context));
    const issues = wordInfos.flatMap((w) => w.derived.phonotacticIssues);

    return {
      written: orthResult.text,
      tokens: orthResult.tokens,
      surface: surfaceWithSpaces,
      issues,
    };
  }
}

export function validateProject(state: LanguageProjectState): DiagnosticIssue[] {
  const issues: DiagnosticIssue[] = [];
  const consonants = state.Phonemes.filter((p) => p.type === 'C');
  const vowels = state.Phonemes.filter((p) => p.type === 'V');

  if (consonants.length === 0) {
    issues.push({ severity: 'error', subsystem: 'Phonology', message: 'No consonants in phoneme inventory.' });
  }
  if (vowels.length === 0) {
    issues.push({ severity: 'error', subsystem: 'Phonology', message: 'No vowels in phoneme inventory.' });
  }

  // Duplicate phonemes
  const seenPhon = new Set<string>();
  for (const p of state.Phonemes) {
    if (seenPhon.has(p.symbol)) {
      issues.push({ severity: 'warning', subsystem: 'Phonology', message: `Duplicate phoneme symbol "${p.symbol}".` });
    }
    seenPhon.add(p.symbol);
  }

  // Lexicon checks
  const seenLemmas = new Set<string>();
  let nounCount = 0;
  let verbCount = 0;
  const engine = buildEngineFromState(state);

  for (const lex of state.Lexemes) {
    const key = lex.Lemma.toLowerCase();
    if (seenLemmas.has(key)) {
      issues.push({ severity: 'error', subsystem: 'Morphology', message: `Duplicate lexeme lemma "${lex.Lemma}".` });
    }
    seenLemmas.add(key);

    if (lex.Pos === 'N') nounCount++;
    if (lex.Pos === 'V') verbCount++;

    const phones = Morphology.splitPhones(lex.UR);
    if (phones.length === 0) {
      issues.push({ severity: 'warning', subsystem: 'Morphology', message: `Lexeme "${lex.Lemma}" has empty UR.` });
    } else {
      for (const ph of phones) {
        const cleanPh = stripStressMark(ph);
        if (!seenPhon.has(cleanPh)) {
          issues.push({
            severity: 'warning',
            subsystem: 'Phonology',
            message: `Lexeme "${lex.Lemma}" uses phone "${cleanPh}" which is not in the inventory.`,
          });
        }
      }
      const phonIssues = engine.phonology.checkPhonotactics(phones);
      for (const pi of phonIssues) {
        issues.push({
          severity: 'warning',
          subsystem: 'Phonotactics',
          message: `"${lex.Lemma}" (${lex.UR}): ${pi}`,
        });
      }
    }
  }

  if (nounCount === 0) {
    issues.push({ severity: 'warning', subsystem: 'Syntax', message: 'No Noun (N) lexemes in lexicon; syntax falls back to defaults.' });
  }
  if (verbCount === 0 && state.ClauseTemplate !== 'Copular') {
    issues.push({ severity: 'warning', subsystem: 'Syntax', message: 'No Verb (V) lexemes in lexicon; syntax falls back to defaults.' });
  }

  // Forbidden pattern regex validation
  for (const pat of state.Phonotactics.forbiddenPatterns) {
    try {
      new RegExp(pat);
    } catch {
      issues.push({
        severity: 'error',
        subsystem: 'Phonotactics',
        message: `Invalid forbidden regex pattern "${pat}".`,
      });
    }
  }

  // Non-ASCII IPA symbols missing explicit orthography mappings
  const mappedPhones = new Set(state.OrthographyRules.filter((r) => r.Enabled).map((r) => r.Phone));
  for (const p of state.Phonemes) {
    if (/[^\x20-\x7E]/.test(p.symbol) && !mappedPhones.has(p.symbol)) {
      issues.push({
        severity: 'warning',
        subsystem: 'Orthography',
        message: `IPA symbol "${p.symbol}" has no active orthography mapping rule.`,
      });
    }
  }

  return issues;
}

export function createDefaultProjectState(): LanguageProjectState {
  const consonants = ['p', 't', 'k', 'ʃ', 's', 'z'];
  const vowels = ['a', 'e', 'i', 'o', 'u'];
  const phonemes: Phoneme[] = [
    ...consonants.map((c) => createPhoneme(c, 'C')),
    ...vowels.map((v) => createPhoneme(v, 'V')),
  ];

  return {
    SchemaVersion: 2,
    Name: 'My Conlang',
    Phonemes: phonemes,
    Consonants: consonants,
    Vowels: vowels,
    SyllableTemplates: [
      { pattern: 'CV', weight: 3 },
      { pattern: 'CVC', weight: 2 },
    ],
    Phonotactics: {
      allowedOnsets: ['pr', 'tr', 'kr', 'sp', 'st', 'sk'],
      allowedCodas: ['m', 'n', 's', 't', 'k', 'r', 'l'],
      maxOnsetCluster: 2,
      maxCodaCluster: 2,
      forbiddenPatterns: [],
    },
    GeneratorOptions: {
      hyphenateSyllables: false,
      allowDuplicates: true,
    },
    StressPattern: 'None',
    PhonologyRules: [
      {
        Name: 'Intervocalic voicing',
        Target: 's',
        Replacement: 'z',
        PrevPhone: 'a',
        NextPhone: 'a',
        Condition: '',
        Enabled: true,
      },
    ],
    Lexemes: [
      { Lemma: 'person', Pos: 'N', UR: 'p e r s o n' },
      { Lemma: 'fish', Pos: 'N', UR: 'f i ʃ' },
      { Lemma: 'water', Pos: 'N', UR: 'k a s a' },
      { Lemma: 'see', Pos: 'V', UR: 's i' },
      { Lemma: 'seek', Pos: 'V', UR: 't i k a' },
      { Lemma: 'small', Pos: 'ADJ', UR: 's m a l' },
    ],
    Affixes: [
      {
        Name: 'Plural',
        Pos: 'N',
        FeatureName: 'Number',
        FeatureValue: 'Pl',
        Affix: '-i',
        IsPrefix: false,
        Condition: '',
        Enabled: true,
      },
      {
        Name: 'Past',
        Pos: 'V',
        FeatureName: 'Tense',
        FeatureValue: 'Past',
        Affix: '-ta',
        IsPrefix: false,
        Condition: '',
        Enabled: true,
      },
    ],
    WordOrder: 'SVO',
    ClauseTemplate: 'Transitive',
    OrthographyRules: [
      { Phone: 'ʃ', Grapheme: 'sh', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 't', Grapheme: 'c', PrevContext: null, NextContext: 'i', Enabled: true },
      { Phone: 'k', Grapheme: 'c', PrevContext: null, NextContext: 'e', Enabled: true },
      { Phone: 'k', Grapheme: 'k', PrevContext: null, NextContext: null, Enabled: true },
    ],
    OrthographyExceptions: [],
    Capitalization: 'SentenceInitial',
    WritingSystem: createDefaultWritingSystem(),
    Pipeline: { ...DEFAULT_PIPELINE_SELECTION },
  };
}

export function createEnglishPresetProjectState(): LanguageProjectState {
  const consonants = [
    'p', 't', 'k', 'b', 'd', 'g',
    'tʃ', 'dʒ',
    'f', 'v', 'θ', 'ð', 's', 'z', 'ʃ', 'ʒ', 'h',
    'm', 'n', 'ŋ',
    'l', 'ɹ', 'w', 'j',
    'pʰ', 'tʰ', 'kʰ', 'ɾ', 'ɫ', 'ɱ', 'ɲ',
  ];
  const vowels = [
    'iː', 'ɪ', 'eɪ', 'ɛ', 'æ',
    'ɑː', 'ɔː', 'oʊ', 'ʊ', 'uː',
    'ʌ', 'ə', 'ɚ', 'ɝː', 'aɪ', 'aʊ', 'ɔɪ',
  ];
  const allophones = new Set(['pʰ', 'tʰ', 'kʰ', 'ɾ', 'ɫ', 'ɱ', 'ɲ']);
  const phonemes: Phoneme[] = [
    ...consonants.map((c) =>
      createPhoneme(c, 'C', { weight: allophones.has(c) ? 0 : 2 })
    ),
    ...vowels.map((v) => createPhoneme(v, 'V', { weight: v === 'ə' ? 3 : 2 })),
  ];

  return {
    SchemaVersion: 2,
    Name: 'General American English',
    Phonemes: phonemes,
    Consonants: consonants,
    Vowels: vowels,
    SyllableTemplates: [
      { pattern: 'CV', weight: 2 },
      { pattern: 'CVC', weight: 3 },
      { pattern: '(C)CVC(C)', weight: 2 },
    ],
    Phonotactics: {
      allowedOnsets: [
        'p', 't', 'k', 'b', 'd', 'g', 'f', 'v', 'θ', 'ð', 's', 'z', 'ʃ', 'h', 'm', 'n', 'l', 'ɹ', 'w', 'j',
        'tʃ', 'dʒ', 'pʰ', 'tʰ', 'kʰ',
        'pl', 'pr', 'pɹ', 'bl', 'br', 'bɹ', 'kl', 'kr', 'kɹ', 'kw', 'gl', 'gr', 'gɹ',
        'tr', 'tɹ', 'dr', 'dɹ', 'fl', 'fr', 'fɹ', 'θr', 'θɹ', 'sl', 'sm', 'sn', 'sp', 'st', 'sk', 'sw',
        'spl', 'spr', 'spɹ', 'str', 'stɹ', 'skr', 'skɹ',
      ],
      allowedCodas: [
        'p', 't', 'k', 'b', 'd', 'g', 'f', 'v', 'θ', 'ð', 's', 'z', 'ʃ', 'ʒ', 'm', 'n', 'ŋ', 'l', 'ɫ', 'ɹ',
        'tʃ', 'dʒ', 'ɾ',
        'st', 'sk', 'sp', 'nt', 'nd', 'ns', 'nz', 'mp', 'ŋk', 'lt', 'ld', 'ɫt', 'ɫd', 'lk', 'lp',
        'rt', 'rd', 'rk', 'rp', 'rs', 'rz', 'ɹt', 'ɹd', 'ɹk', 'ɹp', 'ɹs', 'ɹz', 'ks', 'ts', 'dz', 'ps',
      ],
      maxOnsetCluster: 3,
      maxCodaCluster: 4,
      forbiddenPatterns: [],
    },
    GeneratorOptions: {
      hyphenateSyllables: false,
      allowDuplicates: true,
    },
    StressPattern: 'Lexical',
    PhonologyRules: [
      {
        Name: 'Aspiration (p/t/k in stressed onset)',
        Target: '[manner=stop, -voice]',
        Replacement: '',
        PrevPhone: '$',
        NextPhone: '[+stressed]',
        Condition: '',
        Enabled: false,
      },
      {
        Name: 'Aspirate /p/ in stressed onset',
        Target: 'p',
        Replacement: 'pʰ',
        PrevPhone: '$',
        NextPhone: '[+stressed]',
        Condition: '',
        Enabled: true,
      },
      {
        Name: 'Aspirate /t/ in stressed onset',
        Target: 't',
        Replacement: 'tʰ',
        PrevPhone: '$',
        NextPhone: '[+stressed]',
        Condition: '',
        Enabled: true,
      },
      {
        Name: 'Aspirate /k/ in stressed onset',
        Target: 'k',
        Replacement: 'kʰ',
        PrevPhone: '$',
        NextPhone: '[+stressed]',
        Condition: '',
        Enabled: true,
      },
      {
        Name: 'Intervocalic /t/ Flapping',
        Target: 't',
        Replacement: 'ɾ',
        PrevPhone: 'V',
        NextPhone: '[-stressed]',
        Condition: '',
        Enabled: true,
      },
      {
        Name: 'Nasal Place Assimilation',
        Target: 'n',
        Replacement: '[αplace]',
        PrevPhone: null,
        NextPhone: '[manner=stop]',
        Condition: '',
        Enabled: true,
      },
      {
        Name: 'Syllable-Final Dark L',
        Target: 'l',
        Replacement: 'ɫ',
        PrevPhone: null,
        NextPhone: '$',
        Condition: '',
        Enabled: true,
      },
    ],
    Lexemes: [
      { Lemma: 'the', Pos: 'DET', UR: 'ð ə', Spelling: 'the' },
      { Lemma: 'a', Pos: 'DET', UR: 'ə', Spelling: 'a' },
      { Lemma: 'I', Pos: 'PRON', UR: 'ˈaɪ', Spelling: 'I', Irregulars: { 'Case=Acc': 'ˈm iː' } },
      { Lemma: 'she', Pos: 'PRON', UR: 'ˈʃ iː', Spelling: 'she', Irregulars: { 'Case=Acc': 'ˈh ɝː' } },
      { Lemma: 'they', Pos: 'PRON', UR: 'ˈð eɪ', Spelling: 'they', Irregulars: { 'Case=Acc': 'ˈð ɛ m' } },
      {
        Lemma: 'be',
        Pos: 'V',
        UR: 'ˈb iː',
        Spelling: 'be',
        Irregulars: {
          'Person=1,Number=Sing,Tense=Pres': 'ˈæ m',
          'Person=3,Number=Sing,Tense=Pres': 'ˈɪ z',
          'Number=Pl,Tense=Pres': 'ˈɑː ɹ',
          'Number=Sing,Tense=Past': 'ˈw ʌ z',
          'Number=Pl,Tense=Past': 'ˈw ɝː',
        },
      },
      { Lemma: 'do', Pos: 'AUX', UR: 'ˈd uː', Spelling: 'do', Irregulars: { 'Person=3,Number=Sing,Tense=Pres': 'ˈd ʌ z', 'Tense=Past': 'ˈd ɪ d' } },
      { Lemma: 'will', Pos: 'AUX', UR: 'ˈw ɪ l', Spelling: 'will' },
      { Lemma: 'not', Pos: 'NEG', UR: 'ˈn ɑː t', Spelling: 'not' },
      { Lemma: 'in', Pos: 'PREP', UR: 'ɪ n', Spelling: 'in' },
      { Lemma: 'near', Pos: 'PREP', UR: 'ˈn ɪ ɹ', Spelling: 'near' },
      { Lemma: 'quickly', Pos: 'ADV', UR: 'ˈk w ɪ k l iː', Spelling: 'quickly' },
      { Lemma: 'person', Pos: 'N', UR: 'ˈp ɝː s ə n', Spelling: 'person', Irregulars: { 'Number=Pl': 'ˈp iː p ə l' } },
      { Lemma: 'mouse', Pos: 'N', UR: 'ˈm aʊ s', Spelling: 'mouse', Irregulars: { 'Number=Pl': 'ˈm aɪ s' } },
      { Lemma: 'fish', Pos: 'N', UR: 'ˈf ɪ ʃ', Spelling: 'fish', Irregulars: { 'Number=Pl': 'ˈf ɪ ʃ' } },
      { Lemma: 'cat', Pos: 'N', UR: 'ˈk æ t', Spelling: 'cat' },
      { Lemma: 'dog', Pos: 'N', UR: 'ˈd ɔː g', Spelling: 'dog' },
      { Lemma: 'fox', Pos: 'N', UR: 'ˈf ɑː k s', Spelling: 'fox' },
      { Lemma: 'water', Pos: 'N', UR: 'ˈw ɔː t ɚ', Spelling: 'water', Irregulars: { 'Number=Pl': 'ˈw ɔː t ɚ z' } },
      { Lemma: 'park', Pos: 'N', UR: 'ˈp ɑː ɹ k', Spelling: 'park' },
      { Lemma: 'knight', Pos: 'N', UR: 'ˈn aɪ t', Spelling: 'knight' },
      { Lemma: 'walk', Pos: 'V', UR: 'ˈw ɔː k', Spelling: 'walk' },
      { Lemma: 'chase', Pos: 'V', UR: 'ˈtʃ eɪ s', Spelling: 'chase' },
      { Lemma: 'see', Pos: 'V', UR: 'ˈs iː', Spelling: 'see', Irregulars: { 'Tense=Past': 'ˈs ɔː' } },
      { Lemma: 'go', Pos: 'V', UR: 'ˈg oʊ', Spelling: 'go', Irregulars: { 'Tense=Past': 'ˈw ɛ n t', 'Person=3,Number=Sing,Tense=Pres': 'ˈg oʊ z' } },
      { Lemma: 'small', Pos: 'ADJ', UR: 'ˈs m ɔː l', Spelling: 'small' },
      { Lemma: 'tall', Pos: 'ADJ', UR: 'ˈt ɔː l', Spelling: 'tall' },
    ],
    Affixes: [
      { Name: 'Plural /ɪz/', Pos: 'N', FeatureName: 'Number', FeatureValue: 'Pl', Affix: '-ɪ z', IsPrefix: false, Condition: '[+sibilant]', Enabled: true },
      { Name: 'Plural /s/', Pos: 'N', FeatureName: 'Number', FeatureValue: 'Pl', Affix: '-s', IsPrefix: false, Condition: '[-voice]', Enabled: true },
      { Name: 'Plural /z/', Pos: 'N', FeatureName: 'Number', FeatureValue: 'Pl', Affix: '-z', IsPrefix: false, Condition: '', Enabled: true },
      { Name: 'Past /ɪd/', Pos: 'V', FeatureName: 'Tense', FeatureValue: 'Past', Affix: '-ɪ d', IsPrefix: false, Condition: '[place=alveolar, manner=stop]', Enabled: true },
      { Name: 'Past /t/', Pos: 'V', FeatureName: 'Tense', FeatureValue: 'Past', Affix: '-t', IsPrefix: false, Condition: '[-voice]', Enabled: true },
      { Name: 'Past /d/', Pos: 'V', FeatureName: 'Tense', FeatureValue: 'Past', Affix: '-d', IsPrefix: false, Condition: '', Enabled: true },
      { Name: '3sg /ɪz/', Pos: 'V', FeatureName: 'Agreement', FeatureValue: '3sg', Affix: '-ɪ z', IsPrefix: false, Condition: '[+sibilant]', Enabled: true },
      { Name: '3sg /s/', Pos: 'V', FeatureName: 'Agreement', FeatureValue: '3sg', Affix: '-s', IsPrefix: false, Condition: '[-voice]', Enabled: true },
      { Name: '3sg /z/', Pos: 'V', FeatureName: 'Agreement', FeatureValue: '3sg', Affix: '-z', IsPrefix: false, Condition: '', Enabled: true },
    ],
    WordOrder: 'SVO',
    ClauseTemplate: 'Transitive',
    OrthographyRules: [
      { Phone: 'tʃ', Grapheme: 'ch', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'dʒ', Grapheme: 'j', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'ʃ', Grapheme: 'sh', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'ʒ', Grapheme: 'zh', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'θ', Grapheme: 'th', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'ð', Grapheme: 'th', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'ŋ', Grapheme: 'ng', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'ɱ', Grapheme: 'm', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'ɲ', Grapheme: 'n', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'pʰ', Grapheme: 'p', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'tʰ', Grapheme: 't', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'kʰ', Grapheme: 'k', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'ɾ', Grapheme: 't', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'ɫ', Grapheme: 'l', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'ɹ', Grapheme: 'r', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'j', Grapheme: 'y', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'iː', Grapheme: 'ee', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'ɪ', Grapheme: 'i', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'eɪ', Grapheme: 'ay', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'ɛ', Grapheme: 'e', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'æ', Grapheme: 'a', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'ɑː', Grapheme: 'o', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'ɔː', Grapheme: 'aw', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'oʊ', Grapheme: 'o', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'ʊ', Grapheme: 'oo', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'uː', Grapheme: 'oo', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'ʌ', Grapheme: 'u', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'ə', Grapheme: 'a', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'ɚ', Grapheme: 'er', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'ɝː', Grapheme: 'er', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'aɪ', Grapheme: 'i', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'aʊ', Grapheme: 'ou', PrevContext: null, NextContext: null, Enabled: true },
      { Phone: 'ɔɪ', Grapheme: 'oy', PrevContext: null, NextContext: null, Enabled: true },
    ],
    OrthographyExceptions: [
      { Input: 'miː', Output: 'me' },
      { Input: 'hɝː', Output: 'her' },
      { Input: 'hɚ', Output: 'her' },
      { Input: 'ðɛm', Output: 'them' },
      { Input: 'aɪ', Output: 'I' },
      { Input: 'ʃiː', Output: 'she' },
      { Input: 'ðeɪ', Output: 'they' },
      { Input: 'maɪs', Output: 'mice' },
      { Input: 'piːpəl', Output: 'people' },
      { Input: 'pʰiːpəl', Output: 'people' },
      { Input: 'wɛnt', Output: 'went' },
      { Input: 'sɔː', Output: 'saw' },
      { Input: 'wʌz', Output: 'was' },
      { Input: 'wɝː', Output: 'were' },
      { Input: 'ɪz', Output: 'is' },
      { Input: 'ɑːɹ', Output: 'are' },
      { Input: 'æm', Output: 'am' },
      { Input: 'dʌz', Output: 'does' },
      { Input: 'dɪd', Output: 'did' },
      { Input: 'goʊz', Output: 'goes' },
      { Input: 'ðə', Output: 'the' },
      { Input: 'ə', Output: 'a' },
      { Input: 'wɔːɾɚz', Output: 'waters' },
    ],
    Capitalization: 'SentenceInitial',
    WritingSystem: createDefaultWritingSystem(),
    Pipeline: {
      syntaxGeneratorId: 'phrase-structure-syntax',
      morphologyRealizerId: 'standard-morphology',
      orthographyRendererId: 'morphophonemic-orthography',
      enabledPhonoPluginRules: [],
    },
  };
}

export function normalizeLoadedProjectState(raw: Partial<LanguageProjectState>): LanguageProjectState {
  const defaults = createDefaultProjectState();
  const consonants = Array.isArray(raw.Consonants) ? raw.Consonants : defaults.Consonants;
  const vowels = Array.isArray(raw.Vowels) ? raw.Vowels : defaults.Vowels;

  const phonemes: Phoneme[] =
    Array.isArray(raw.Phonemes) && raw.Phonemes.length > 0
      ? raw.Phonemes.map((p) =>
          createPhoneme(p.symbol, p.type, {
            weight: p.weight,
            allowOnset: p.allowOnset,
            allowCoda: p.allowCoda,
            features: p.features,
          })
        )
      : [
          ...consonants.map((c) => createPhoneme(c, 'C')),
          ...vowels.map((v) => createPhoneme(v, 'V')),
        ];

  return {
    ...defaults,
    ...raw,
    SchemaVersion: 2,
    Phonemes: phonemes,
    Consonants: phonemes.filter((p) => p.type === 'C').map((p) => p.symbol),
    Vowels: phonemes.filter((p) => p.type === 'V').map((p) => p.symbol),
    SyllableTemplates: Array.isArray(raw.SyllableTemplates)
      ? raw.SyllableTemplates
      : defaults.SyllableTemplates,
    Phonotactics: raw.Phonotactics ? { ...defaults.Phonotactics, ...raw.Phonotactics } : defaults.Phonotactics,
    GeneratorOptions: raw.GeneratorOptions
      ? { ...defaults.GeneratorOptions, ...raw.GeneratorOptions }
      : defaults.GeneratorOptions,
    StressPattern: raw.StressPattern ?? defaults.StressPattern,
    PhonologyRules: Array.isArray(raw.PhonologyRules) ? raw.PhonologyRules : defaults.PhonologyRules,
    Lexemes: Array.isArray(raw.Lexemes) ? raw.Lexemes : defaults.Lexemes,
    Affixes: Array.isArray(raw.Affixes) ? raw.Affixes : defaults.Affixes,
    OrthographyRules: Array.isArray(raw.OrthographyRules) ? raw.OrthographyRules : defaults.OrthographyRules,
    OrthographyExceptions: Array.isArray(raw.OrthographyExceptions)
      ? raw.OrthographyExceptions
      : defaults.OrthographyExceptions,
    WritingSystem: raw.WritingSystem
      ? { ...defaults.WritingSystem, ...raw.WritingSystem }
      : defaults.WritingSystem,
    Pipeline: raw.Pipeline
      ? { ...defaults.Pipeline, ...raw.Pipeline }
      : defaults.Pipeline,
  };
}

export function buildEngineFromState(state: LanguageProjectState): LangEngine {
  const tactics = new Phonotactics(state.Phonotactics);
  const pipeline = state.Pipeline ?? DEFAULT_PIPELINE_SELECTION;
  const activePluginRules = BUILTIN_PLUGIN_PHONO_RULES.filter((r) =>
    pipeline.enabledPhonoPluginRules.includes(r.id)
  );

  const phonology = new Phonology(
    state.Phonemes,
    tactics,
    state.PhonologyRules.map((r) => ({
      name: r.Name,
      target: r.Target,
      replacement: r.Replacement,
      prevPhone: r.PrevPhone ?? null,
      nextPhone: r.NextPhone ?? null,
      condition: r.Condition ?? '',
      enabled: r.Enabled,
    })),
    state.StressPattern ?? 'None',
    activePluginRules
  );

  const lexicon = new InMemoryLexicon(
    state.Lexemes.map((l) => ({
      lemma: l.Lemma,
      pos: l.Pos,
      rootPhones: { UR: l.UR },
      spelling: l.Spelling,
      irregulars: l.Irregulars,
    }))
  );

  const morphMod =
    BUILTIN_MORPHOLOGY_MODULES.find((m) => m.id === pipeline.morphologyRealizerId) ??
    BUILTIN_MORPHOLOGY_MODULES[0];
  const morphology = morphMod.create(
    lexicon,
    state.Affixes.map((a) => ({
      name: a.Name,
      pos: a.Pos,
      featureName: a.FeatureName,
      featureValue: a.FeatureValue,
      affix: a.Affix,
      isPrefix: a.IsPrefix,
      condition: a.Condition ?? '',
      enabled: a.Enabled,
    })),
    state.Phonemes
  );

  const syntaxMod =
    BUILTIN_SYNTAX_MODULES.find((m) => m.id === pipeline.syntaxGeneratorId) ??
    BUILTIN_SYNTAX_MODULES[0];
  const syntax = syntaxMod.create(lexicon, state.WordOrder, state.ClauseTemplate);

  const orthMod =
    BUILTIN_ORTHOGRAPHY_MODULES.find((m) => m.id === pipeline.orthographyRendererId) ??
    BUILTIN_ORTHOGRAPHY_MODULES[0];
  const orthography = orthMod.create(
    state.OrthographyRules.map((r) => ({
      phone: r.Phone,
      grapheme: r.Grapheme,
      prevContext: r.PrevContext ?? null,
      nextContext: r.NextContext ?? null,
      enabled: r.Enabled,
    })),
    state.OrthographyExceptions.map((e) => ({
      input: e.Input,
      output: e.Output,
    })),
    state.Capitalization
  );

  return new LangEngine(phonology, morphology, syntax, orthography);
}

export function formatSyntaxTree(node: SyntaxNode, depth = 0, parenthesesRole = false): string {
  const indent = '  '.repeat(depth);
  const role = node.features.items.find((f) => f.name === 'Role')?.value;
  const extras = node.features.items
    .filter((f) => f.name !== 'Role')
    .map((f) => `${f.name}=${f.value}`)
    .join(', ');
  let header: string;
  if (!role) {
    header = `${node.pos}:${node.lemmaId}`;
  } else if (parenthesesRole) {
    header = `${node.pos}:${node.lemmaId} (${role})`;
  } else {
    header = `${node.pos}:${node.lemmaId} [${role}]`;
  }
  if (extras) {
    header += ` {${extras}}`;
  }

  const lines = [indent + header, ...node.children.map((c) => formatSyntaxTree(c, depth + 1, parenthesesRole))];
  return lines.join('\n');
}

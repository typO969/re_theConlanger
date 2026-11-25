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

const DEFAULT_IPA_FEATURES: Record<string, Partial<PhonologicalFeatures>> = {
  p: { voicing: 'voiceless', manner: 'stop', place: 'bilabial' },
  t: { voicing: 'voiceless', manner: 'stop', place: 'alveolar' },
  k: { voicing: 'voiceless', manner: 'stop', place: 'velar' },
  b: { voicing: 'voiced', manner: 'stop', place: 'bilabial' },
  d: { voicing: 'voiced', manner: 'stop', place: 'alveolar' },
  g: { voicing: 'voiced', manner: 'stop', place: 'velar' },
  s: { voicing: 'voiceless', manner: 'fricative', place: 'alveolar' },
  z: { voicing: 'voiced', manner: 'fricative', place: 'alveolar' },
  ʃ: { voicing: 'voiceless', manner: 'fricative', place: 'postalveolar' },
  f: { voicing: 'voiceless', manner: 'fricative', place: 'labiodental' },
  m: { voicing: 'voiced', manner: 'nasal', place: 'bilabial' },
  n: { voicing: 'voiced', manner: 'nasal', place: 'alveolar' },
  r: { voicing: 'voiced', manner: 'trill', place: 'alveolar' },
  l: { voicing: 'voiced', manner: 'approximant', place: 'alveolar' },
  a: { voicing: 'voiced', manner: 'vowel', height: 'low', backness: 'central', roundness: 'unrounded' },
  e: { voicing: 'voiced', manner: 'vowel', height: 'mid', backness: 'front', roundness: 'unrounded' },
  i: { voicing: 'voiced', manner: 'vowel', height: 'high', backness: 'front', roundness: 'unrounded' },
  o: { voicing: 'voiced', manner: 'vowel', height: 'mid', backness: 'back', roundness: 'rounded' },
  u: { voicing: 'voiced', manner: 'vowel', height: 'high', backness: 'back', roundness: 'rounded' },
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

  public isValidOnset(cluster: string): boolean {
    if (cluster.length === 0) return true;
    if (cluster.length > this.config.maxOnsetCluster) return false;
    if (cluster.length === 1) return true;
    return this.allowedOnsets.size === 0 || this.allowedOnsets.has(cluster);
  }

  public isValidCoda(cluster: string): boolean {
    if (cluster.length === 0) return true;
    if (cluster.length > this.config.maxCodaCluster) return false;
    if (cluster.length === 1) return true;
    return this.allowedCodas.size === 0 || this.allowedCodas.has(cluster);
  }
}

export class Syllabifier {
  private readonly vowelSet: Set<string>;
  private readonly tactics?: Phonotactics;

  constructor(vowels: readonly Phoneme[] = [], tactics?: Phonotactics) {
    this.vowelSet = new Set([
      ...'aeiouɑɔɛɪʊyøœæəɨʌɒɐɯɤ',
      ...vowels.map((v) => v.symbol),
    ]);
    this.tactics = tactics;
  }

  public isVowel(p: string): boolean {
    if (!p) return false;
    const clean = p.replace(/^ˈ/, '');
    return this.vowelSet.has(clean) || this.vowelSet.has(clean[0]);
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
        const onsetSlice = phones.slice(split, v2).join('');
        const codaSlice = phones.slice(v1 + 1, split).join('');
        const onsetOk = !this.tactics || this.tactics.isValidOnset(onsetSlice);
        const codaOk = !this.tactics || this.tactics.isValidCoda(codaSlice);
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

export function resolveStressSyllableIndex(syllableCount: number, pattern: StressPattern): number {
  if (syllableCount <= 0 || pattern === 'None') return -1;
  if (pattern === 'Initial') return 0;
  if (pattern === 'Final') return syllableCount - 1;
  // Penultimate
  return syllableCount >= 2 ? syllableCount - 2 : 0;
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

  if (cond.startsWith('+') || cond.startsWith('-')) {
    const positive = cond.startsWith('+');
    const name = cond.slice(1).toLowerCase();
    if (name === 'stress' || name === 'stressed') {
      if (!stressedSet || phoneIndex === undefined) return !positive;
      const isStressed = stressedSet.has(phoneIndex);
      return positive ? isStressed : !isStressed;
    }

    const phoneme = inventory.find((p) => p.symbol === symbol);
    if (!phoneme) return false;
    const feat = phoneme.features;

    if (name === 'voice' || name === 'voicing' || name === 'voiced') {
      const v = feat.voicing.toLowerCase();
      return positive ? v === 'voiced' : v === 'voiceless';
    }
    if (name === 'round' || name === 'rounded') {
      const r = feat.roundness.toLowerCase();
      return positive ? r === 'rounded' : r === 'unrounded';
    }
    if (name === 'sibilant') {
      const isSib = ['s', 'z', 'ʃ', 'ʒ', 'tʃ', 'dʒ'].includes(symbol) || feat.custom.toLowerCase().includes('sibilant');
      return positive ? isSib : !isSib;
    }
    if (name === 'sonorant') {
      const isSon = ['nasal', 'approximant', 'trill', 'tap', 'lateral', 'vowel'].includes(feat.manner.toLowerCase()) || phoneme.type === 'V';
      return positive ? isSon : !isSon;
    }
    const allMeta = `${feat.manner} ${feat.place} ${feat.height} ${feat.backness} ${feat.custom}`.toLowerCase();
    return positive ? allMeta.includes(name) : !allMeta.includes(name);
  }

  if (cond.includes('=')) {
    const phoneme = inventory.find((p) => p.symbol === symbol);
    if (!phoneme) return false;
    const feat = phoneme.features;
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

  if (expected === 'V') {
    const ph = inventory.find((p) => p.symbol === actual);
    return ph ? ph.type === 'V' : 'aeiouɑɔɛɪʊyøœæəɨʌɒɐɯɤ'.includes(actual[0]);
  }
  if (expected === 'C') {
    const ph = inventory.find((p) => p.symbol === actual);
    return ph ? ph.type === 'C' : !'aeiouɑɔɛɪʊyøœæəɨʌɒɐɯɤ'.includes(actual[0]);
  }
  if (expected.startsWith('[') && expected.endsWith(']')) {
    return matchesCondition(actual, expected, inventory, stressedSet, actualIndex);
  }
  return actual === expected;
}

function contextMatch(
  phones: readonly string[],
  pos: number,
  prevTarget: string,
  nextTarget: string,
  spanLen: number,
  inventory: readonly Phoneme[],
  stressedSet?: ReadonlySet<number>
): boolean {
  const prevPhone = pos > 0 ? phones[pos - 1] : null;
  const prevOk = matchesPhoneOrClass(prevPhone, prevTarget, inventory, stressedSet, pos - 1);
  const afterIdx = pos + spanLen;
  const nextPhone = afterIdx < phones.length ? phones[afterIdx] : null;
  const nextOk = matchesPhoneOrClass(nextPhone, nextTarget, inventory, stressedSet, afterIdx);
  return prevOk && nextOk;
}

export function applyRuleSpec(
  spec: RuleSpec,
  phones: readonly string[],
  inventory: readonly Phoneme[],
  stressedSet?: ReadonlySet<number>
): string[] {
  if (!spec.enabled) return [...phones];

  const target = spec.target.trim();
  const replacement = spec.replacement.trim();
  const prevTarget = spec.prevPhone?.trim() || '';
  const nextTarget = spec.nextPhone?.trim() || '';

  // Epenthesis: target is ∅ or empty -> insert replacement between prev and next
  if (target === '∅' || target === '') {
    if (!replacement || replacement === '∅') return [...phones];
    const repPhones = replacement.split(/\s+/).filter(Boolean);
    const out: string[] = [];
    for (let i = 0; i <= phones.length; i++) {
      const prevPhone = i > 0 ? phones[i - 1] : null;
      const nextPhone = i < phones.length ? phones[i] : null;
      const prevOk = matchesPhoneOrClass(prevPhone, prevTarget, inventory, stressedSet, i - 1);
      const nextOk = matchesPhoneOrClass(nextPhone, nextTarget, inventory, stressedSet, i);
      if ((prevTarget || nextTarget) && prevOk && nextOk) {
        out.push(...repPhones);
      }
      if (i < phones.length) out.push(phones[i]);
    }
    return out;
  }

  // Substitution or Deletion (supports exact symbol, C/V class, or [feature] target)
  const output: string[] = [];
  const repPhones = replacement === '∅' || replacement === '' ? [] : replacement.split(/\s+/).filter(Boolean);
  for (let i = 0; i < phones.length; i++) {
    const phone = phones[i];
    const targetHit = matchesPhoneOrClass(phone, target, inventory, stressedSet, i);
    if (
      targetHit &&
      contextMatch(phones, i, prevTarget, nextTarget, 1, inventory, stressedSet) &&
      matchesCondition(phone, spec.condition, inventory, stressedSet, i)
    ) {
      output.push(...repPhones);
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
    if (this.stressPattern === 'None' || phones.length === 0) return stressed;
    const syls = this.syllabifier.split(phones);
    const targetIdx = resolveStressSyllableIndex(syls.length, this.stressPattern);
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
      current = applyRuleSpec(rule, current, this.phonemes, stressedIndices);
      stressedIndices = this.computeStressedPhoneIndices(current);
      steps.push({ label: rule.name + (rule.enabled ? '' : ' (disabled)'), value: current.join(' ') });
    }

    for (const pRule of this.pluginRules) {
      current = pRule.apply(current, this.phonemes, stressedIndices);
      stressedIndices = this.computeStressedPhoneIndices(current);
      steps.push({ label: `[Plugin] ${pRule.name}`, value: current.join(' ') });
    }

    const syllables = this.syllabifier.split(current);
    const stressIndex = resolveStressSyllableIndex(syllables.length, this.stressPattern);
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
      const onset = syl.onset.join('');
      const coda = syl.coda.join('');
      if (onset && !this.tactics.isValidOnset(onset)) {
        issues.push(`Illegal onset "${onset}" in syllable ${idx + 1}`);
      }
      if (coda && !this.tactics.isValidCoda(coda)) {
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
    if (this.pos === 'CLAUSE' || this.pos === 'NP') {
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

  constructor(lexicon: InMemoryLexicon, affixRules: AffixRule[]) {
    this.lexicon = lexicon;
    this.affixRules = affixRules;
  }

  public inflectNode(node: SyntaxNode): MorphemeToken[] {
    const lex = this.lexicon.resolve(node.lemmaId);
    const ur = lex.rootPhones.UR ?? lex.lemma;
    const basePhones = Morphology.splitPhones(ur);
    const morphemes = [{ id: 'STEM', gloss: lex.lemma, underlyingPhones: basePhones }];

    for (const rule of this.affixRules) {
      if (!rule.enabled || rule.pos !== node.pos || !node.features.has(rule.featureName, rule.featureValue)) {
        continue;
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
    }

    return morphemes.map((m) => ({ morpheme: m, underlyingPhones: m.underlyingPhones }));
  }

  public inflect(tree: SyntaxTree): MorphemeToken[] {
    return tree.linearize().flatMap((n) => this.inflectNode(n));
  }

  public realizeForPreview(lemma: string, pos: string, features: FeatBundle): string {
    const lex = this.lexicon.resolve(lemma);
    let surface = lex.rootPhones.UR ?? lemma;

    for (const rule of this.affixRules) {
      if (!rule.enabled || rule.pos !== pos || !features.has(rule.featureName, rule.featureValue)) {
        continue;
      }
      const cleanAffix = rule.affix.replace(/-/g, '');
      surface = rule.isPrefix ? `${cleanAffix} ${surface}` : `${surface} ${cleanAffix}`;
    }

    return surface;
  }

  public static splitPhones(source: string): string[] {
    const trimmed = source.trim();
    if (!trimmed) return [];
    if (trimmed.includes(' ')) {
      return trimmed.split(/\s+/).filter(Boolean);
    }
    return [...trimmed];
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

  private mapWordDetailed(word: string): { wordText: string; graphemes: string[] } {
    const excepted = this.exceptions.find((e) => e.input === word);
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
    for (let i = 0; i < word.length; ) {
      let matched = false;
      for (const rule of orderedRules) {
        if (i + rule.phone.length > word.length) continue;
        if (word.slice(i, i + rule.phone.length) !== rule.phone) continue;
        if (!this.matchesContext(word, i, rule)) continue;

        out += rule.grapheme;
        if (rule.grapheme) graphemes.push(rule.grapheme);
        i += rule.phone.length;
        matched = true;
        break;
      }
      if (!matched) {
        out += word[i];
        graphemes.push(word[i]);
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

  private applyCapitalization(text: string): string {
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
  create: (lexicon: InMemoryLexicon, affixRules: AffixRule[]) => Morphology;
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
];

export const BUILTIN_MORPHOLOGY_MODULES: readonly IMorphologyRealizerModule[] = [
  {
    id: 'standard-morphology',
    name: 'Affixation Realizer (Default)',
    description: 'Lexicon-backed stem plus ordered prefix/suffix rules.',
    create: (lex, affixes) => new Morphology(lex, affixes),
  },
  {
    id: 'reduplicative-plural',
    name: 'CV Reduplication + Affixes (Phase 4)',
    description: 'Prepends initial CV stem reduplication for Number=Pl alongside active affixes.',
    create: (lex, affixes) => new ReduplicativePluralMorphology(lex, affixes),
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
    const wordOutputs = sTree.linearize().map((node) => {
      const morphemes = this.morphology.inflectNode(node);
      return this.phonology.derive(morphemes);
    });

    const surfaceWithSpaces = wordOutputs.map((w) => w.finalSurface).join(' ');
    const orthResult = this.orthography.renderWithTokens(surfaceWithSpaces);
    const issues = wordOutputs.flatMap((w) => w.phonotacticIssues);

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
        if (!seenPhon.has(ph)) {
          issues.push({
            severity: 'warning',
            subsystem: 'Phonology',
            message: `Lexeme "${lex.Lemma}" uses phone "${ph}" which is not in the inventory.`,
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
        Enabled: true,
      },
      {
        Name: 'Past',
        Pos: 'V',
        FeatureName: 'Tense',
        FeatureValue: 'Past',
        Affix: '-ta',
        IsPrefix: false,
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
      enabled: a.Enabled,
    }))
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
  let header: string;
  if (!role) {
    header = `${node.pos}:${node.lemmaId}`;
  } else if (parenthesesRole) {
    header = `${node.pos}:${node.lemmaId} (${role})`;
  } else {
    header = `${node.pos}:${node.lemmaId} [${role}]`;
  }

  const lines = [indent + header, ...node.children.map((c) => formatSyntaxTree(c, depth + 1, parenthesesRole))];
  return lines.join('\n');
}

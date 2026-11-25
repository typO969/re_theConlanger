export interface Feat {
  name: string;
  value: string;
}

export class FeatBundle {
  public readonly items: readonly Feat[];

  constructor(items: readonly Feat[] = []) {
    this.items = items;
  }

  public has(name: string, val: string): boolean {
    return this.items.some((f) => f.name === name && f.value === val);
  }
}

export interface SentenceSpec {
  clauseType: string;
  approxWords: number;
  constraints?: Record<string, string>;
}

export interface PhonologicalFeatures {
  voicing: string;
  manner: string;
  place: string;
  height: string;
  backness: string;
  roundness: string;
  custom: string;
}

export interface Phoneme {
  symbol: string;
  type: 'C' | 'V';
  weight: number;
  allowOnset: boolean;
  allowCoda: boolean;
  features: PhonologicalFeatures;
}

export interface SyllableTemplate {
  pattern: string;
  weight: number;
}

export interface PhonotacticsConfig {
  allowedOnsets: string[];
  allowedCodas: string[];
  maxOnsetCluster: number;
  maxCodaCluster: number;
  forbiddenPatterns: string[];
}

export interface GeneratorOptions {
  hyphenateSyllables: boolean;
  allowDuplicates: boolean;
}

export interface RuleSpec {
  name: string;
  target: string;
  replacement: string;
  prevPhone?: string | null;
  nextPhone?: string | null;
  condition?: string;
  enabled: boolean;
}

export interface DerivationStep {
  label: string;
  value: string;
}

export interface Syllable {
  onset: string[];
  nucleus: string[];
  coda: string[];
}

export type AffixType = 'Prefix' | 'Suffix' | 'Infix' | 'Circumfix' | 'Template';

export interface Lexeme {
  lemma: string;
  pos: string;
  rootPhones: Record<string, string>;
  lexClass?: string;
  spelling?: string;
  irregulars?: Record<string, string>;
  semanticTags?: string[];
  protoUr?: string;
}

export interface Morpheme {
  id: string;
  gloss: string;
  underlyingPhones: string[];
}

export interface MorphemeToken {
  morpheme: Morpheme;
  underlyingPhones: string[];
}

export interface AffixRule {
  name: string;
  pos: string;
  featureName: string;
  featureValue: string;
  affix: string;
  isPrefix: boolean;
  affixType?: AffixType;
  condition?: string;
  enabled: boolean;
}

export type WordOrder = 'SVO' | 'SOV' | 'VSO' | 'VOS' | 'OVS' | 'OSV';
export type ClauseTemplate = 'Intransitive' | 'Transitive' | 'Copular';
export type CapitalizationMode = 'None' | 'SentenceInitial' | 'AllWords';
export type StressPattern = 'None' | 'Initial' | 'Penultimate' | 'Final' | 'Lexical';
export type AlignmentSystem = 'NomAcc' | 'ErgAbs' | 'Tripartite';
export type ModifierOrder = 'BeforeNoun' | 'AfterNoun';
export type AdpositionOrder = 'Preposition' | 'Postposition';
export type QuestionStrategy = 'Inversion' | 'Particle';
export type SentenceMood = 'Decl' | 'YNQuestion' | 'Imp';

export interface SyntaxConfig {
  alignment: AlignmentSystem;
  adjectiveOrder: ModifierOrder;
  determinerOrder: ModifierOrder;
  adpositionType: AdpositionOrder;
  questionStrategy: QuestionStrategy;
  proDrop: boolean;
  polypersonalAgreement: boolean;
  allowEmbeddedClauses: boolean;
  allowRelativeClauses: boolean;
  allowPassive: boolean;
  maxClauseDepth: number;
  customRules: string[];
}

export interface PipelineSelection {
  syntaxGeneratorId: string;
  morphologyRealizerId: string;
  orthographyRendererId: string;
  enabledPhonoPluginRules: string[];
}

export interface OrthographyRule {
  phone: string;
  grapheme: string;
  prevContext?: string | null;
  nextContext?: string | null;
  enabled: boolean;
}

export interface OrthographyException {
  input: string;
  output: string;
}

export interface GraphemeToken {
  id: string;
  grapheme: string;
  wordIndex: number;
  positionInWord: 'isolated' | 'initial' | 'medial' | 'final';
}

export interface OrthographyResult {
  text: string;
  tokens: GraphemeToken[];
}

export type FlowDirection = 'LTR' | 'RTL' | 'TTB_LTR' | 'TTB_RTL' | 'Boustrophedon';

export interface AnchorPoint {
  x: number;
  y: number;
}

export interface GlyphDefinition {
  id: string;
  label: string;
  svgPath: string;
  initialPath?: string;
  medialPath?: string;
  finalPath?: string;
  enteringAnchor: AnchorPoint;
  exitingAnchor: AnchorPoint;
  advanceWidth: number;
  advanceHeight: number;
}

export interface FeaturicAssemblyRule {
  id: string;
  name: string;
  targetGlyphId: string;
  contextPosition: 'any' | 'initial' | 'medial' | 'final' | 'after' | 'before';
  contextGlyphId?: string;
  appendSvgPath: string;
  offsetX: number;
  offsetY: number;
  enabled: boolean;
}

export interface WritingSystemConfig {
  name: string;
  flowDirection: FlowDirection;
  cursiveBonding: boolean;
  contextualVariants: boolean;
  glyphScale: number;
  letterSpacing: number;
  lineSpacing: number;
  wrapLimit: number;
  glyphs: Record<string, GlyphDefinition>;
  featuricRules: FeaturicAssemblyRule[];
}

export interface LexemeState {
  Lemma: string;
  Pos: string;
  UR: string;
  LexClass?: string;
  Spelling?: string;
  Irregulars?: Record<string, string>;
  SemanticTags?: string[];
  ProtoUR?: string;
}

export interface AffixState {
  Name: string;
  Pos: string;
  FeatureName: string;
  FeatureValue: string;
  Affix: string;
  IsPrefix: boolean;
  AffixType?: AffixType;
  Condition?: string;
  Enabled: boolean;
}

export interface RealizedWordContext {
  surface: string;
  lemma?: string;
  pos?: string;
  features?: FeatBundle;
  spelling?: string;
  isIrregular?: boolean;
}

export interface OrthRuleState {
  Phone: string;
  Grapheme: string;
  PrevContext?: string | null;
  NextContext?: string | null;
  Enabled: boolean;
}

export interface OrthExceptionState {
  Input: string;
  Output: string;
}

export interface PhonologyRuleState {
  Name: string;
  Target: string;
  Replacement: string;
  PrevPhone?: string | null;
  NextPhone?: string | null;
  Condition?: string;
  Enabled: boolean;
}

export interface DiachronicRule {
  Name: string;
  Era?: string;
  Target: string;
  Replacement: string;
  PrevPhone?: string | null;
  NextPhone?: string | null;
  Condition?: string;
  Enabled: boolean;
}

export interface DiachronicStepTrace {
  ruleName: string;
  era: string;
  phones: string[];
  changed: boolean;
}

export interface DiachronicCognateDiff {
  lemma: string;
  pos: string;
  protoUr: string;
  daughterPhones: string[];
  daughterUr: string;
  daughterWritten: string;
  changed: boolean;
  steps: DiachronicStepTrace[];
}

export interface DiachronicAffixDiff {
  name: string;
  pos: string;
  protoAffix: string;
  daughterAffix: string;
  changed: boolean;
}

export interface DiachronicSimulationResult {
  cognates: DiachronicCognateDiff[];
  affixDiffs: DiachronicAffixDiff[];
  emergentPhones: string[];
  lostPhones: string[];
  changedCount: number;
}

export interface DiscourseOptions {
  sentenceCount?: number;
  approxWords?: number;
  constraints?: Record<string, string>;
  trackDefiniteness?: boolean;
  useAnaphoricPronouns?: boolean;
  useConnectives?: boolean;
  enforceSelectionalRestrictions?: boolean;
}

export interface DiscourseSentenceItem {
  index: number;
  written: string;
  surface: string;
  entitiesMentioned: string[];
  issues: string[];
}

export interface DiscourseResult {
  paragraph: string;
  sentences: DiscourseSentenceItem[];
  trackedEntities: string[];
}

export interface DiagnosticIssue {
  severity: 'error' | 'warning';
  subsystem: 'Phonology' | 'Morphology' | 'Syntax' | 'Orthography' | 'Phonotactics';
  message: string;
}

export interface LanguageProjectState {
  SchemaVersion: number;
  Name: string;
  Phonemes: Phoneme[];
  // Kept for backward-compatible .clproj.json import/export
  Consonants: string[];
  Vowels: string[];
  SyllableTemplates: SyllableTemplate[];
  Phonotactics: PhonotacticsConfig;
  GeneratorOptions: GeneratorOptions;
  StressPattern: StressPattern;
  PhonologyRules: PhonologyRuleState[];
  DiachronicRules?: DiachronicRule[];
  Lexemes: LexemeState[];
  Affixes: AffixState[];
  WordOrder: WordOrder;
  ClauseTemplate: ClauseTemplate;
  SyntaxConfig?: SyntaxConfig;
  OrthographyRules: OrthRuleState[];
  OrthographyExceptions: OrthExceptionState[];
  Capitalization: CapitalizationMode;
  WritingSystem: WritingSystemConfig;
  Pipeline: PipelineSelection;
}

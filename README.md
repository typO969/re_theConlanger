# re_the.Conlanger (.NET 10 LTS)

## it has come a long way, but it has so very far to go.

Clean, unified C# / WPF solution designed for offline local compilation and full Visual Studio 2026 XAML Designer support.

## Solution Architecture

- **`src/Conlanger.SDK`** (`net10.0`) — Zero-dependency plugin contracts (`IPlugin`, `IPluginAction`, `ILabContext`), extensible linguistics pipeline interfaces (`ISyntaxGenerator`, `IPhonoRule`, `IMorphologyRealizer`, `IOrthographyRenderer`), and the unified Schema v2 `LanguageProject` DTOs (shared JSON schema with the live web workspace).
- **`src/Conlanger.Core`** (`net10.0`) — Unified linguistics engine combining the clean `Core.Language` pipeline with the harvested WinForms features (`PhonologicalFeatures`, `Phonotactics`, `Syllabifier`, feature-conditioned/epenthesis/deletion `PhonologyEngine`, `ConfigurableSyntaxGenerator`, `WordGenerator`, `ProjectValidator`, and the 3-layer Writing Systems `ShapingEngine`).
- **`src/Conlanger.PluginHost`** (`net10.0`) — Isolated collectible `AssemblyLoadContext` runner executing plugins out-of-process with timeout protection directly against `LanguageProject` JSON.
- **`src/Conlanger.Desktop`** (`net10.0-windows`, WPF) — Declarative XAML views with `d:DataContext` design-time view-model wiring so every window and tab opens in the Visual Studio 2026 XAML Designer.
- **`plugins/EvolutionSimPlugin`** & **`plugins/GraphvizPlugin`** (`net10.0`) — First-party plugins targeting `Conlanger.SDK` directly (no lossy `PluginModelBuilder` adapter).
- **`tests/Conlanger.Tests`** (`net10.0`, xUnit) — Unit tests covering word-boundary preservation, syllabification/phonotactics, feature-conditioned sound changes, weighted `WordGenerator`, central validation, and RTL/Vertical vector shaping.

## Build & Run (Local Windows + Visual Studio 2026)

```bash
dotnet restore Conlanger.sln
dotnet test Conlanger.sln
dotnet build Conlanger.sln -c Release
dotnet run --project src/Conlanger.Desktop
```

## OVERALL & FEATURAL ROADMAP

### PHASE 0 — STABILIZE THE FOUNDATION: **~95%** Done

1) Persistence: Unified Schema v2 .clproj.json save/load + automatic autosave (localStorage on web; %LocalAppData%\Conlanger\autosave.clproj.json in .NET).
2) Unify Engine State: Done. LanguageProject / LanguageProjectState is now the single source of truth; PluginModelBuilder and duplicate v1 models are gone.
3) Validation & Diagnostics: Done. Central validateProject / ProjectValidator feeds the collapsible Diagnostics Bar in both Web and WPF.

### PHASE 1 — CORE LANGUAGE LOGIC: **~85%** Done

4) Syntax: SVO/SOV/VSO/VOS/OVS/OSV, Intransitive/Transitive/Copular, approxWords NP expansion, and seeded RNG sampling are implemented. (Still mono-clausal—no determiners, auxiliaries, or recursion yet.)
5) Morphology: Feature-tagged AffixRules (prefix/suffix, enable toggle) and single-form Preview Realizer work. (Missing automatic full-paradigm matrices per lexeme and irregular/suppletive stems.)
6) Phonology & Phonotactics: Distinctive features matrix, phoneme weights & allowOnset/allowCoda, weighted SyllableTemplates, Phonotactics, Syllabifier, ordered rewrite rules (∅ epenthesis/deletion, # boundaries, [+voice]/[manner=...] target conditions), and derivation step traces are live. (Still missing natural-class/feature conditions on prevPhone/nextPhone in TS and lexical stress.)

### PHASE 2 — ORTHOGRAPHY & OUTPUT QUALITY: **~95%** Done (Web) / **~75%** (WPF)

7) Orthography: Context-aware grapheme rules, exceptions, capitalization modes, and Layer 1 GraphemeToken[] emission are Done in both engines.
8) Generation Controls:
Web (OutputTab.tsx): N samples, ApproxWords, Seed, Feature constraint filters, and TXT/CSV export are Done.
WPF (OutputTab.xaml): Word generator + single sentence preview are wired, but batch N-sample generation, feature constraint fields, and TXT/CSV export aren't wired into the WPF view yet.

### PHASE 3 — PROJECT-LEVEL UX: **~80%** Done (Web) / **~45%** (WPF)

9) Project Management:
Web: Undo/Redo, Recent Projects, and Import/Export Lexicon (CSV/JSON) are Done. Missing: versioned project backup snapshots.
WPF: Undo/Redo and Open/Save are wired; Recent Files UI, Lexicon Import/Export commands, and .clproj-backups rotation still need to be added.
10) Inspectors / Visualizers:
Web (InspectorsTab.tsx): Lexeme UR → SR → Orthography, Syntax Tree viewer, and syllabified bracket traces are Done (short of an auto-generated all-inflections paradigm grid).
WPF: Has inline derivation preview on PhonologyTab.xaml, but no dedicated InspectorsTab.xaml yet.

### PHASE 4 — PLUGINS: **~70%** Done

11) Plugin Architecture: Conlanger.SDK defines clean plugin contracts (IPlugin, IPluginAction, ILabContext) and pipeline-swap interfaces (ISyntaxGenerator, IPhonoRule, IMorphologyRealizer, IOrthographyRenderer). Conlanger.PluginHost runs collectible out-of-process AssemblyLoadContext plugins directly on LanguageProject JSON, and EvolutionSimPlugin + GraphvizPlugin are updated to v2.
What's left for 100% Phase 4:
Let LangEngine dynamically load/swap ISyntaxGenerator / IPhonoRule / IOrthographyRenderer implementations from plugins at runtime (right now plugins only execute batch export actions).
Wire an interactive "Run Action" argument form + PluginHost launcher into WPF ExtensionsTab.xaml (mirroring Web ExtensionsTab.tsx).

### Bonus Track — Writing Systems Architecture (conlanger-writingsystems.pdf): Milestones 1 & 2 Reached

Ahead of the original V3 roadmap, the 3-layer shaping architecture (Layer 1 GraphemeToken[], Layer 2 Flow Vectors LTR/RTL/TTB_LTR/TTB_RTL/Boustrophedon + #entering/#exiting anchors + featuric rules, Layer 3 SVG & WPF Canvas/Path renderer + RTL cursive & Vertical Mongolian presets) is already operational as a foundation. (Milestone 3 polish would be a visual SVG glyph/anchor drawer and Arabic contextual-variant preset.)

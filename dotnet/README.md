# Conlanger (.NET 10 LTS)

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

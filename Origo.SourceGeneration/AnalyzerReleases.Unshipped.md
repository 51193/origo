; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ORIGOSG009 | Origo.SourceGeneration | Error | TypedDataGenerator: void, ref-like, static, unbound-generic, nullable-value, or special runtime types cannot appear in generated accessor signatures

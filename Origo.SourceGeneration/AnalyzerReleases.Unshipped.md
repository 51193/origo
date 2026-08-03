; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ORIGOSG001 | Origo.SourceGeneration | Error | TypedDataGenerator: system primitive registered outside the TypedData home assembly
ORIGOSG002 | Origo.SourceGeneration | Error | TypedDataGenerator: unsupported value type in the TypedData home assembly
ORIGOSG003 | Origo.SourceGeneration | Error | TypedDataGenerator: kind out of byte range [1, 254] — startKind + count exceeds 254
ORIGOSG004 | Origo.SourceGeneration | Error | TypedDataGenerator: kind collision — overlapping SndInlineTypes ranges map one kind to multiple types

; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 0.0.9

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ORIGOSG001 | Origo.SourceGeneration | Error | TypedDataGenerator: system primitive registered outside the TypedData home assembly
ORIGOSG002 | Origo.SourceGeneration | Error | TypedDataGenerator: unsupported value type in the TypedData home assembly
ORIGOSG003 | Origo.SourceGeneration | Error | TypedDataGenerator: kind out of byte range [1, 254] — startKind + count exceeds 254
ORIGOSG004 | Origo.SourceGeneration | Error | TypedDataGenerator: kind collision — overlapping SndInlineTypes ranges map one kind to multiple types
ORIGOSG005 | Origo.SourceGeneration | Error | TypedDataGenerator: kind name collision — same-named types from different namespaces/generic instantiations map to one identifier
ORIGOSG006 | Origo.SourceGeneration | Error | TypedDataGenerator: kind name is not a valid C# identifier (e.g. pointer types)
ORIGOSG007 | Origo.SourceGeneration | Error | TypedDataGenerator: adapter assembly lacks friend access to TypedData internals

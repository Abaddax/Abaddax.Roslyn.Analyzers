## Release 0.0.1

### New Rules

Rule ID | Category    | Severity | Notes
--------|-------------|----------|--------------------
ABX0001 | AsyncUsage  | Warning  | [CancellationTokenAnalyzer](./Analyzers/CancellationTokenAnalyzer.cs)
ABX0002 | AsyncUsage  | Info     | [AsyncSuffixAnalyzer](./Analyzers/AsyncSuffixAnalyzer.cs)
ABX0003 | AsyncUsage  | Info     | [SuggestAsyncOverloadAnalyzer](./Analyzers/SuggestAsyncOverloadAnalyzer.cs)
ABX0004 | AsyncUsage  | Warning  | [EfCorePreferAsyncCallAnalyzer](./Analyzers/EfCorePreferAsyncCallAnalyzer.cs)
ABX0005 | Style       | Info     | [EfCoreExplicitTrackingAnalyzer](./Analyzers/EfCoreExplicitTrackingAnalyzer.cs)
ABX0006 | Reliability | Error    | [UnconditionalSelfRecursionAnalyzer](./Analyzers/UnconditionalSelfRecursionAnalyzer.cs)
ABX0007 | Style       | Info     | [EfCoreThenIncludeFormattingAnalyzer](./Analyzers/EfCoreThenIncludeFormattingAnalyzer.cs)
ABX0008 | Usage       | Warning  | [SuppressionJustificationAnalyzer](./Analyzers/SuppressionJustificationAnalyzer.cs)


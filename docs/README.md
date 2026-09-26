# Abaddax.Roslyn.Analyzers 
## Analyzers

| Id | Category | Description | Default Severity | Enabled by default | Code fix | Configurable |
| -- | -------- | ----------- | :--------------: | :----------------: | :------: | :----------: |
| [ABX0001](./Analyzers/ABX0001.md) | AsyncUsage | Async method should accept CancellationToken | <span title='Warning'>⚠️</span> | ✔️ | <span title='Planned'>📋</span> | ✔️ |
| [ABX0002](./Analyzers/ABX0002.md) | AsyncUsage | Async method should end with 'Async' | <span title='Info'>ℹ️</span> | ✔️ | <span title='Planned'>📋</span> | ✔️ |
| [ABX0003](./Analyzers/ABX0003.md) | AsyncUsage | Use async overload in async function | <span title='Info'>ℹ️</span> | ✔️ | <span title='Planned'>📋</span> | ❌ |
| [ABX0004](./Analyzers/ABX0004.md) | AsyncUsage | Use async EF Core methods inside async function | <span title='Warning'>⚠️</span> | ✔️ | <span title='Planned'>📋</span> | ❌ |
| [ABX0005](./Analyzers/ABX0005.md) | Style | Explicitly qualify the tracking behavior when using EF Core | <span title='Info'>ℹ️</span> | ✔️ | <span title='Planned'>📋</span> | ❌ |
| [ABX0006](./Analyzers/ABX0006.md) | Reliability | Unconditional self recursion | <span title='Error'>❌</span> | ✔️ | ❌ | ❌ |
| [ABX0007](./Analyzers/ABX0007.md) | Style | 'ThenInclude' formatting | <span title='Info'>ℹ️</span> | ✔️ | <span title='Planned'>📋</span> | ✔️ |
| [ABX0008](./Analyzers/ABX0008.md) | Usage | Suppression requires a justification | <span title='Warning'>⚠️</span> | ✔️ | <span title='Planned'>📋</span> | ✔️ |

## Suppressors
| Id | Description | Suppressed Id's | Enabled by default |  Configurable |
| -- | ----------- | :---------------: | :----------------: | :-----------: |
| [ABX1001](./Suppressors/ABX1001.md) | Suppresses nullability warnings for [MaybeNull] navigation property when using EF Core. | [CS8600, CS8601, CS8602, CS8603, CS8604, CS8605, CS8607, CS8629, CS8714](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/nullable-warnings) | ❌ | ✔️ |
| [ABX1002](./Suppressors/ABX1002.md) | Suppresses nullability warnings inside EF Core LINQ queries | [CS8602, CS8604, CS8622, CS8634](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/nullable-warnings) | ❌ | ✔️ |
| [ABX1003](./Suppressors/ABX1003.md) | Suppresses unused variable warnings for exceptions | [CS0168](https://learn.microsoft.com/en-us/dotnet/csharp/misc/cs0168), [IDE0059](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/ide0059) | ✔️ | ❌ |
| [ABX1004](./Suppressors/ABX1004.md) | Suppresses unused CancellationToken parameter warnings | [IDE0060](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/ide0060) | ✔️ | ❌ |
| [ABX1005](./Suppressors/ABX1005.md) | Suppresses warning for visible protected readonly fields | [CA1051](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1051) | ❌ | ❌ |

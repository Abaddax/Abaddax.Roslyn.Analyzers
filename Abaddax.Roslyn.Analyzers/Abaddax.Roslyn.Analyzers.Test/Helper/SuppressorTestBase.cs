using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Abaddax.Roslyn.Analyzers.Test.Helper
{
    public abstract partial class SuppressorTestBase<TSuppressor>
        where TSuppressor : DiagnosticSuppressor, new()
    {
        protected virtual void SetupTestState(SolutionState state)
        {
            return;
        }
        protected Task VerifySuppressorAsync(string source, params DiagnosticResult[] expected)
        {
            return VerifySuppressorAsync(source, (_) => { }, expected);
        }
        protected Task VerifySuppressorAsync(string source, OutputKind? outputKind, params DiagnosticResult[] expected)
        {
            return VerifySuppressorAsync(source, state =>
            {
                state.OutputKind = outputKind;
            }, expected);
        }
        protected async Task VerifySuppressorAsync(string source, Action<SolutionState> configureTestState, params DiagnosticResult[] expected)
        {
            if (expected.Any(x => x.IsSuppressed == null))
                throw new Exception("'DiagnosticResult.IsSuppressed' must be set. Use '.WithIsSuppressed(suppressed)'");
            var test = new CSharpAnalyzerTest<TSuppressor, DefaultVerifier>()
            {
                TestCode = source,
                CompilerDiagnostics = CompilerDiagnostics.All,
            };
            SetupTestState(test.TestState);
            configureTestState.Invoke(test.TestState);
            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync(CancellationToken.None);
        }
    }
}

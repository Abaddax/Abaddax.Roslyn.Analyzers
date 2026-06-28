using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Abaddax.Roslyn.Analyzers.Test.Helper
{
    public abstract partial class SuppressorTestBase<TSuppressor>
        where TSuppressor : DiagnosticSuppressor, new()
    {
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
            var test = new CSharpAnalyzerTest<TSuppressor, DefaultVerifier>()
            {
                TestCode = source,
                CompilerDiagnostics = CompilerDiagnostics.All,
            };
            configureTestState.Invoke(test.TestState);
            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync(CancellationToken.None);
        }
    }
}

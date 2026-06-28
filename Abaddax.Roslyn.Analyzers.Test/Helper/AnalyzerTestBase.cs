using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Abaddax.Roslyn.Analyzers.Test.Helper
{
    public abstract class AnalyzerTestBase<TAnalyzer>
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        protected virtual void SetupTestState(SolutionState state)
        {
            return;
        }

        protected Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            return VerifyAnalyzerAsync(source, (_) => { }, expected);
        }
        protected Task VerifyAnalyzerAsync(string source, OutputKind? outputKind, params DiagnosticResult[] expected)
        {
            return VerifyAnalyzerAsync(source, state =>
            {
                state.OutputKind = outputKind;
            }, expected);
        }
        protected Task VerifyAnalyzerAsync(string source, Action<SolutionState> configureTestState, params DiagnosticResult[] expected)
        {
            var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>()
            {
                TestCode = source,
            };
            SetupTestState(test.TestState);
            configureTestState.Invoke(test.TestState);
            test.ExpectedDiagnostics.AddRange(expected);
            return test.RunAsync(CancellationToken.None);
        }
    }
}

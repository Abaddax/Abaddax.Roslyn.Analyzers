using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using System.Diagnostics.CodeAnalysis;

namespace Abaddax.Roslyn.Analyzers.Tests.Common
{
    public abstract class AnalyzerTestBase<TAnalyzer>
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        protected virtual void SetupTestState(SolutionState state)
        {
            return;
        }

#pragma warning disable ABX0001 // Async method should accept CancellationToken
        protected Task VerifyAnalyzerAsync([StringSyntax(StringSyntaxHelper.CSharpTest)] string source,
            params DiagnosticResult[] expected)
        {
            return VerifyAnalyzerAsync(source, (_) => { }, expected);
        }
        protected Task VerifyAnalyzerAsync([StringSyntax(StringSyntaxHelper.CSharpTest)] string source,
            OutputKind? outputKind,
            params DiagnosticResult[] expected)
        {
            return VerifyAnalyzerAsync(source, state =>
            {
                state.OutputKind = outputKind;
            }, expected);
        }
        protected Task VerifyAnalyzerAsync([StringSyntax(StringSyntaxHelper.CSharpTest)] string source,
            Action<SolutionState> configureTestState,
            params DiagnosticResult[] expected)
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
#pragma warning restore ABX0001 // Async method should accept CancellationToken
    }
}

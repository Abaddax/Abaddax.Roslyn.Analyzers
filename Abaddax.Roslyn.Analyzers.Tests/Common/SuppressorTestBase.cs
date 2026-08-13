using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using System.Diagnostics.CodeAnalysis;

namespace Abaddax.Roslyn.Analyzers.Tests.Common
{
    public abstract class SuppressorTestBase<TSuppressor>
        where TSuppressor : DiagnosticSuppressor, new()
    {
        protected virtual IEnumerable<DiagnosticAnalyzer> AdditionalAnalyzers
        {
            get
            {
                yield break;
            }
        }

        protected virtual void SetupTestState(SolutionState state)
        {
            return;
        }

#pragma warning disable ABX0001 // Async method should accept CancellationToken
        protected Task VerifySuppressorAsync([StringSyntax(StringSyntaxHelper.CSharpTest)] string source,
            params DiagnosticResult[] expected)
        {
            return VerifySuppressorAsync(source, (_) => { }, expected);
        }
        protected Task VerifySuppressorAsync([StringSyntax(StringSyntaxHelper.CSharpTest)] string source,
            OutputKind? outputKind,
            params DiagnosticResult[] expected)
        {
            return VerifySuppressorAsync(source, state =>
            {
                state.OutputKind = outputKind;
            }, expected);
        }
        protected async Task VerifySuppressorAsync([StringSyntax(StringSyntaxHelper.CSharpTest)] string source,
            Action<SolutionState> configureTestState,
            params DiagnosticResult[] expected)
        {
            if (expected.Any(x => x.IsSuppressed == null))
                throw new Exception("'DiagnosticResult.IsSuppressed' must be set. Use '.WithIsSuppressed(suppressed)'");
            var test = new CSharpSuppressorTest<DefaultVerifier>()
            {
                TestCode = source,
                CompilerDiagnostics = CompilerDiagnostics.All,
                AdditionalAnalyzers = AdditionalAnalyzers.ToList()
            };
            SetupTestState(test.TestState);
            configureTestState.Invoke(test.TestState);
            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync(CancellationToken.None);
        }
#pragma warning restore ABX0001 // Async method should accept CancellationToken

        #region Helper
        private sealed class CSharpSuppressorTest<TVerifier> : CSharpAnalyzerTest<TSuppressor, TVerifier>
            where TVerifier : IVerifier, new()
        {
            public IList<DiagnosticAnalyzer> AdditionalAnalyzers { get; set; } = [];
            protected override IEnumerable<DiagnosticAnalyzer> GetDiagnosticAnalyzers()
            {
                foreach (var analyzer in base.GetDiagnosticAnalyzers())
                    yield return analyzer;
                foreach (var analyzer in AdditionalAnalyzers)
                    yield return analyzer;
            }
        }
        #endregion
    }
}

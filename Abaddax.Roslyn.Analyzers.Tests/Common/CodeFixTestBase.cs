using Abaddax.Roslyn.Analyzers.Framework.Tests.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using System.Diagnostics.CodeAnalysis;

namespace Abaddax.Roslyn.Analyzers.Tests.Common
{
    public abstract class CodeFixTestBase<TCodeFix, TAnalyzer>
        where TCodeFix : CodeFixProvider, new()
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        protected virtual void SetupTestState(SolutionState state)
        {
            return;
        }

#pragma warning disable ABX0001 // Async method should accept CancellationToken
        protected Task VerifyCodeFixAsync(
            [StringSyntax(StringSyntaxHelper.CSharpTest)] string source,
            [StringSyntax(StringSyntaxHelper.CSharpTest)] string fixedSource,
            params DiagnosticResult[] expected)
        {
            return VerifyCodeFixAsync(
                source,
                fixedSource,
                (_) => { },
                expected);
        }
        protected Task VerifyCodeFixAsync([StringSyntax(StringSyntaxHelper.CSharpTest)] string source,
            [StringSyntax(StringSyntaxHelper.CSharpTest)] string fixedSource,
            OutputKind? outputKind,
            params DiagnosticResult[] expected)
        {
            return VerifyCodeFixAsync(
                source,
                fixedSource,
                state =>
                {
                    state.OutputKind = outputKind;
                },
                expected);
        }
        protected Task VerifyCodeFixAsync(
            [StringSyntax(StringSyntaxHelper.CSharpTest)] string source,
            [StringSyntax(StringSyntaxHelper.CSharpTest)] string fixedSource,
            Action<SolutionState> configureTestState,
            params DiagnosticResult[] expected)
        {
            var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>()
            {
                TestCode = source,
                FixedCode = fixedSource,
            };
            SetupTestState(test.TestState);
            configureTestState.Invoke(test.TestState);
            test.ExpectedDiagnostics.AddRange(expected);
            return test.RunAsync(CancellationToken.None);
        }
#pragma warning restore ABX0001 // Async method should accept CancellationToken

    }
}

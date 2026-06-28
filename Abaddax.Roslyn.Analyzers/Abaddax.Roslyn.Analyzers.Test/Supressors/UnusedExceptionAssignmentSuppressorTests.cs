using Abaddax.Roslyn.Analyzers.Supressors;
using Abaddax.Roslyn.Analyzers.Test.Helper;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Abaddax.Roslyn.Analyzers.Test.Supressors
{
    public sealed class UnusedExceptionAssignmentSuppressorTests
        : SuppressorTestBase<UnusedExceptionAssignmentSuppressor>
    {
        [Test]
        public async Task ShouldSuppressIfInsideCatchBlock()
        {
            var source =
                """
                using System;

                #pragma warning disable CS1591

                namespace TestNamespace
                {
                    public class Test
                    {
                        public void Func()
                        {
                            try
                            {
                                throw new Exception();
                            }
                            catch(Exception {|#0:ex|})
                            {
                                return;
                            }
                        }
                    }
                }
                """;
            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("CS0168")
                    .WithLocation(0)
                    .WithIsSuppressed(true)
                );
        }
    }
}

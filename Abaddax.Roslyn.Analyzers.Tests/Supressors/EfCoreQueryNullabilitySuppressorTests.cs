using Abaddax.Roslyn.Analyzers.Supressors;
using Abaddax.Roslyn.Analyzers.Tests.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Abaddax.Roslyn.Analyzers.Tests.Supressors
{
    public sealed class EfCoreQueryNullabilitySuppressorTests
         : SuppressorTestBase<EfCoreQueryNullabilitySuppressor>
    {
        protected override void SetupTestState(SolutionState state)
        {
            state.Sources.Add("""
                global using System;
                global using System.Collections;
                global using System.Collections.Generic;
                global using System.Linq;
                global using System.Linq.Expressions;
                global using Microsoft.EntityFrameworkCore;
                global using Microsoft.EntityFrameworkCore.Query;
                """);
            state.Sources.Add(
                """
                #nullable enable

                namespace TestNamespace
                {
                    public class TestContext : DbContext
                    {
                        public class TestEntity
                        {
                            public TestEntity? Mother { get; set; }
                            public TestEntity? Father { get; set; }
                            public List<TestEntity> Childs { get; set; } = new();
                        }
                        public DbSet<TestEntity> Persons { get; set; } = null!;
                    }
                }
                """);
            state.AnalyzerConfigFiles.Add(("/.editorconfig",
                    $"""
                    root = true

                    [*.cs]
                    dotnet_code_quality.{AnalyzerIdentifiers.EfCoreQueryNullReferenceSuppression}.enabled = true

                    [*.cs]
                    dotnet_diagnostic.CS1591.severity = none
                    dotnet_diagnostic.CS8019.severity = none
                    """));
            state.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(DbContext).Assembly.Location));
            base.SetupTestState(state);
        }

        [Test]
        public async Task ShouldSuppressIfInsideQuery()
        {
            var source =
                """
                #nullable enable

                namespace TestNamespace
                {
                    public class Test
                    {
                        public void Func()
                        {
                            var db = new TestContext();

                            var q = db.Persons
                                .Include(x => x.Mother).ThenInclude(x => {|#0:x|}.Father)
                                .Include(x => x.Mother).ThenInclude(x => {|#1:x|}.Mother);
                        }
                    }
                }
                """;
            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(0)
                    .WithIsSuppressed(true),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(1)
                    .WithIsSuppressed(true)
                );
        }
        [Test]
        public async Task ShouldNotSuppressIfInsideLinq()
        {
            var source =
                """
                #nullable enable

                namespace TestNamespace
                {
                    public class Test
                    {
                        public void Func()
                        {
                            var db = new TestContext();

                            var q = db.Persons
                                .Include(x => x.Mother)
                                .Select(x => {|#0:x.Mother|}.Father)
                                .Select(x => x!)
                                .AsEnumerable()
                                .Select(x => {|#1:x.Mother|}.Father);
                        }
                    }
                }
                """;
            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(0)
                    .WithIsSuppressed(true),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(1)
                    .WithIsSuppressed(false)
                );
        }
        [Test]
        public async Task ShouldNotSuppressIfQueryExternalVariable()
        {
            var source =
                """
                #nullable enable

                namespace TestNamespace
                {
                    public class Test
                    {
                        public void Func()
                        {
                            var db = new TestContext();

                            TestContext.TestEntity? entity = null;

                            var q = db.Persons
                                .Include(x => x.Mother)
                                .Where(x => {|#0:x.Mother|}.Mother == {|#1:entity|}.Mother);
                        }
                    }
                }
                """;
            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(0)
                    .WithIsSuppressed(true),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(1)
                    .WithIsSuppressed(false)
                );
        }
        [Test]
        public async Task ShouldSuppressIfNestedDbSet()
        {
            var source =
                """
                #nullable enable

                namespace TestNamespace
                { 
                    public class Container
                    {
                        public TestContext DB { get; set; } = new();
                    }
                    public class Test
                    {
                        public void Func()
                        {
                            var container = new Container();

                            var q = container.DB.Persons
                                .Include(x => x.Mother).ThenInclude(x => {|#0:x|}.Father)
                                .Include(x => x.Mother).ThenInclude(x => {|#1:x|}.Mother);
                        }
                    }
                }
                """;
            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(0)
                    .WithIsSuppressed(true),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(1)
                    .WithIsSuppressed(true)
                );
        }
        [Test]
        public async Task ShouldSuppressIfDbContextFactory()
        {
            var source =
                """
                #nullable enable

                namespace TestNamespace
                { 
                    public static class Container
                    {
                        public static TestContext CreateDb() => new();
                    }
                    public class Test
                    {
                        public void Func()
                        {
                            var q = Container.CreateDb().Persons
                                .Include(x => x.Mother).ThenInclude(x => {|#0:x|}.Father)
                                .Include(x => x.Mother).ThenInclude(x => {|#1:x|}.Mother);
                        }
                    }
                }
                """;
            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(0)
                    .WithIsSuppressed(true),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(1)
                    .WithIsSuppressed(true)
                );
        }
        [Test]
        public async Task ShouldSuppressIfLinqInsideQuery()
        {
            var source =
                """
                #nullable enable

                namespace TestNamespace
                {
                    public class Test
                    {
                        public void Func()
                        {
                            var db = new TestContext();

                            var q = db.Persons
                                .Where(x => x.Childs.Any(x => {|#0:x.Mother|}.Father == null));
                        }
                    }
                }
                """;
            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(0)
                    .WithIsSuppressed(true)
                );
        }
        [Test]
        public async Task ShouldNotSuppressIfExternalLinqInsideQuery()
        {
            var source =
                """
                #nullable enable

                namespace TestNamespace
                {
                    public class Test
                    {
                        public void Func()
                        {
                            var db = new TestContext();

                            var local = new TestContext.TestEntity();

                            var q = db.Persons
                                .Where(x => x.Childs.Any(x => {|#0:local.Mother|}.Father == {|#1:x.Mother|}.Father));
                        }
                    }
                }
                """;
            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(0)
                    .WithIsSuppressed(false),
                 DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(1)
                    .WithIsSuppressed(true)
                );
        }
    }
}

using Abaddax.Roslyn.Analyzers.Supressors;
using Abaddax.Roslyn.Analyzers.Tests.Common;
using Microsoft.CodeAnalysis.Testing;
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
                namespace Microsoft.EntityFrameworkCore
                {
                    public class DbContext;
                    public class DbSet<TEntity> : IQueryable<TEntity>
                        where TEntity : class
                    {
                        Type IQueryable.ElementType => throw new NotImplementedException();
                        Expression IQueryable.Expression => throw new NotImplementedException();
                        IQueryProvider IQueryable.Provider => throw new NotImplementedException();
                        IEnumerator<TEntity> IEnumerable<TEntity>.GetEnumerator() => throw new NotImplementedException();
                        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
                    }
                    public static class EntityFrameworkQueryableExtensions
                    {
                        #pragma warning disable CS0626
                        public static extern IIncludableQueryable<TEntity, TProperty> Include<TEntity, TProperty>(
                            this IQueryable<TEntity> source,
                            Expression<Func<TEntity, TProperty>> navigationPropertyPath)
                            where TEntity : class;
                        public static extern IIncludableQueryable<TEntity, TProperty> ThenInclude<TEntity, TPreviousProperty, TProperty>(
                            this IIncludableQueryable<TEntity, TPreviousProperty> source,
                            Expression<Func<TPreviousProperty, TProperty>> navigationPropertyPath) 
                            where TEntity : class;
                    }
                    namespace Query
                    {
                        public interface IIncludableQueryable<out TEntity, out TProperty> : IQueryable<TEntity>;
                    }
                }
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
                        public DbSet<TestEntity> Persons { get; set; } = new();
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
                    """));
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

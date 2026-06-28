using Abaddax.Roslyn.Analyzers.Supressors;
using Abaddax.Roslyn.Analyzers.Test.Helper;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;

namespace Abaddax.Roslyn.Analyzers.Test.Supressors
{
    public sealed class EfCoreMaybeNullNavigationSuppressorTests
        : SuppressorTestBase<EfCoreMaybeNullNavigationSuppressor>
    {
        protected override void SetupTestState(SolutionState state)
        {
            state.Sources.Add("""
                global using System;
                global using System.Collections;
                global using System.Collections.Generic;
                global using System.Diagnostics.CodeAnalysis;
                global using System.Linq;
                global using System.Linq.Expressions;
                global using System.Threading;
                global using System.Threading.Tasks;
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
                        public static IIncludableQueryable<TEntity, TProperty> Include<TEntity, TProperty>(
                            this IQueryable<TEntity> source,
                            Expression<Func<TEntity, TProperty>> navigationPropertyPath)
                            where TEntity : class => throw new NotImplementedException();
                        public static IIncludableQueryable<TEntity, TProperty> ThenInclude<TEntity, TPreviousProperty, TProperty>(
                            this IIncludableQueryable<TEntity, TPreviousProperty> source,
                            Expression<Func<TPreviousProperty, TProperty>> navigationPropertyPath) 
                            where TEntity : class => throw new NotImplementedException();
                        public static Task<TSource> FirstAsync<TSource>(
                            this IQueryable<TSource> source,
                            CancellationToken cancellationToken = default) => throw new NotImplementedException();
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
                            [MaybeNull]
                            public TestEntity Mother { get; set; }
                            [MaybeNull]
                            public TestEntity Father { get; set; }
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
                    dotnet_code_quality.{AnalyzerIdentifiers.EfCoreDereferencePossibleNullReferenceSuppression}.enabled = true

                    [*.cs]
                    dotnet_diagnostic.CS1591.severity = none
                    """));
            base.SetupTestState(state);
        }

        [Test]
        public async Task ShouldSuppressIfIncluded()
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

                            var p = db.Persons
                                .Include(x => x.Mother)
                                .First();

                            var m = {|#0:p.Mother|}.ToString();
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
        public async Task ShouldSuppressIfIncludedChain()
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

                            var p = db.Persons
                                .Include(x => x.Mother).ThenInclude(x => {|#0:x|}.Father)
                                .First();

                            var f = {|#2:{|#1:p.Mother|}.Father|}.ToString();
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
                    .WithIsSuppressed(true),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(2)
                    .WithIsSuppressed(true)
                );
        }
        [Test]
        public async Task ShouldSuppressIfIncludedAsync()
        {
            var source =
                """
                #nullable enable

                namespace TestNamespace
                {
                    public class Test
                    {
                        public async Task FuncAsync(CancellationToken cancellationToken)
                        {
                            var db = new TestContext();

                            var p = await db.Persons
                                .Include(x => x.Mother)
                                .FirstAsync(cancellationToken);

                            var m = {|#0:p.Mother|}.ToString();
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
        public async Task ShouldSuppressIfIncludedChainAsync()
        {
            var source =
                """
                #nullable enable

                namespace TestNamespace
                {
                    public class Test
                    {
                        public async Task FuncAsync()
                        {
                            var db = new TestContext();

                            var p = await db.Persons
                                .Include(x => x.Mother).ThenInclude(x => {|#0:x|}.Father)
                                .FirstAsync();

                            var f = {|#2:{|#1:p.Mother|}.Father|}.ToString();
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
                    .WithIsSuppressed(true),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(2)
                    .WithIsSuppressed(true)
                );
        }

        [Test]
        public async Task ShouldNotSuppressIfNotIncluded()
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

                            var p = db.Persons
                                .Include(x => x.Mother)
                                .First();

                            var m = {|#0:p.Father|}.ToString();
                        }
                    }
                }
                """;
            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(0)
                    .WithIsSuppressed(false)
                );
        }
        [Test]
        public async Task ShouldNotSuppressIfNotIncludedChain()
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

                            var p = db.Persons
                                .Include(x => x.Mother).ThenInclude(x => {|#0:x|}.Father)
                                .First();

                            var f = {|#2:{|#1:p.Mother|}.Mother|}.ToString();
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
                    .WithIsSuppressed(true),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(2)
                    .WithIsSuppressed(false)
                );
        }
        [Test]
        public async Task ShouldNotSuppressIfNotIncludedAsync()
        {
            var source =
                """
                #nullable enable

                namespace TestNamespace
                {
                    public class Test
                    {
                        public async Task FuncAsync(CancellationToken cancellationToken)
                        {
                            var db = new TestContext();

                            var p = await db.Persons
                                .Include(x => x.Mother)
                                .FirstAsync(cancellationToken);

                            var m = {|#0:p.Father|}.ToString();
                        }
                    }
                }
                """;
            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(0)
                    .WithIsSuppressed(false)
                );
        }
        [Test]
        public async Task ShouldNotSuppressIfNotIncludedChainAsync()
        {
            var source =
                """
                #nullable enable

                namespace TestNamespace
                {
                    public class Test
                    {
                        public async Task FuncAsync()
                        {
                            var db = new TestContext();

                            var p = await db.Persons
                                .Include(x => x.Mother).ThenInclude(x => {|#0:x|}.Father)
                                .FirstAsync();

                            var f = {|#2:{|#1:p.Mother|}.Mother|}.ToString();
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
                    .WithIsSuppressed(true),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(2)
                    .WithIsSuppressed(false)
                );
        }

        [Test]
        public async Task ShouldNotSuppressIfSelectProjection()
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
                            var p = db.Persons
                                .Include(x => x.Mother)
                                .Select(x => new { x.Mother })
                                .First();

                            var ms = {|#0:p.Mother|}.ToString();
                        }
                    }
                }
                """;

            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(0)
                    .WithIsSuppressed(false)
                );
        }
        [Test]
        public async Task ShouldSuppressIfIncludedSelectProperty()
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
                            var m = db.Persons
                                .Include(x => x.Mother).ThenInclude(x => {|#0:x|}.Father)
                                .Select(x => x.Mother)
                                .First();

                            var ms = {|#2:{|#1:m|}.Father|}.ToString();
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
                    .WithIsSuppressed(true),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(2)
                    .WithIsSuppressed(true)
                );
        }
        [Test]
        public async Task ShouldNotSuppressIfNotIncludedSelectProperty()
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
                            var m = db.Persons
                                .Include(x => x.Mother)
                                .Select(x => x.Mother)
                                .First();

                            var ms = {|#1:{|#0:m|}.Father|}.ToString();
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
        public async Task ShouldSuppressIfIncludedInEnumerable()
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
                            var p = db.Persons
                                .Include(x => x.Mother)
                                .ToArray();

                            var ms = {|#0:p[0].Mother|}.ToString();
                            foreach(var x in p)
                            {
                                var ms2 = {|#1:x.Mother|}.ToString();
                            }
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
        public async Task ShouldSuppressIfIncludedInEnumerableFiltered()
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
                            var p = db.Persons
                                .Include(x => x.Mother)
                                .ToArray();

                            foreach(var x in p.Where(x => x.Childs.Count == 0))
                            {
                                var ms2 = {|#0:x.Mother|}.ToString();
                            }
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
        public async Task ShouldNotSuppressIfIncludedInEnumerableSelectProjection()
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
                            var p = db.Persons
                                .Include(x => x.Mother)
                                .ToArray();

                            foreach(var x in p.Select(x => new { x.Mother }))
                            {
                                var ms2 = {|#0:x.Mother|}.ToString();
                            }
                        }
                    }
                }
                """;

            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(0)
                    .WithIsSuppressed(false)
                );
        }
        [Test]
        public async Task ShouldSuppressIfIncludedInEnumerableSelectProperty()
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
                            var p = db.Persons
                                .Include(x => x.Mother).ThenInclude(x => {|#0:x|}.Father)
                                .ToArray();

                            foreach(var x in p.Select(x => x.Mother))
                            {
                                var ms2 = {|#2:{|#1:x|}.Father|}.ToString();
                            }
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
                    .WithIsSuppressed(true),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(2)
                    .WithIsSuppressed(true)
                );
        }
        [Test]
        public async Task ShouldNotSuppressIfNotIncludedInEnumerableSelectProperty()
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
                            var p = db.Persons
                                .Include(x => x.Mother)
                                .ToArray();

                            foreach(var x in p.Select(x => x.Mother))
                            {
                                var ms2 = {|#1:{|#0:x|}.Father|}.ToString();
                            }
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
        public async Task ShouldSuppressIfVariableAssignedFromIncluded()
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

                            var p = db.Persons
                                .Include(x => x.Mother).ThenInclude(x => {|#0:x|}.Father)
                                .First();

                            var f = {|#1:p.Mother|}.Father;
                            var f2 = f;
                            var s = {|#2:f2|}.ToString();
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
                    .WithIsSuppressed(true),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(2)
                    .WithIsSuppressed(true)
                );
        }
        [Test]
        public async Task ShouldSuppressIfVariableAssignedFromIncludedAsync()
        {
            var source =
                """
                #nullable enable

                namespace TestNamespace
                {
                    public class Test
                    {
                        public async Task FuncAsync()
                        {
                            var db = new TestContext();

                            var pT = db.Persons
                                .Include(x => x.Mother).ThenInclude(x => {|#0:x|}.Father)
                                .FirstAsync();

                            var p = await pT;

                            var f = {|#1:p.Mother|}.Father;
                            var f2 = f;
                            var s = {|#2:f2|}.ToString();
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
                    .WithIsSuppressed(true),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(2)
                    .WithIsSuppressed(true)
                );
        }
        [Test]
        public async Task ShouldSuppressIfMultipleIncludes()
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

                            var p = db.Persons
                                .Include(x => x.Mother).ThenInclude(x => {|#0:x|}.Father)
                                .Include(x => x.Father).ThenInclude(x => {|#1:x|}.Mother).ThenInclude(x => {|#2:x|}.Mother)
                                .First();

                            var f = {|#4:{|#3:p.Mother|}.Father|}.ToString();
                            var m = {|#6:p.Father|}.Mother;
                            var ms = {|#5:m|}.ToString();
                            var f2 = {|#7:m.Father|}.ToString();
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
                    .WithIsSuppressed(false),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(2)
                    .WithIsSuppressed(false),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(3)
                    .WithIsSuppressed(true),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(4)
                    .WithIsSuppressed(true),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(5)
                    .WithIsSuppressed(true),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(6)
                    .WithIsSuppressed(true),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(7)
                    .WithIsSuppressed(false)
                );
        }
        [Test]
        public async Task ShouldSuppressIfCasted()
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

                            var p = db.Persons
                                .Include(x => {|#0:(TestContext.TestEntity)x.Mother|})
                                .Include(x => x.Father as TestContext.TestEntity)
                                .First();

                            var m = {|#1:p.Mother|}.ToString();
                            var f = {|#2:p.Father|}.ToString();
                        }
                    }
                }
                """;
            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("CS8600")
                    .WithLocation(0)
                    .WithIsSuppressed(false),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(1)
                    .WithIsSuppressed(true),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(2)
                    .WithIsSuppressed(true)
                );
        }

        [Test]
        public async Task ShouldNotSuppressIfReassignedAfterQuery()
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

                            var p = db.Persons
                                .Include(x => x.Mother)
                                .First();
                            
                            TestContext.TestEntity? m2 = null;                            
                            var m = p.Mother;
                            p.Mother = {|#0:m2|};
                            var ms1 = {|#1:m|}.ToString();
                            var ms2 = {|#2:p.Mother|}.ToString();
                        }
                    }
                }
                """;

            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("CS8601")
                    .WithLocation(0)
                    .WithIsSuppressed(false),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(1)
                    .WithIsSuppressed(true),
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(2)
                    .WithIsSuppressed(false)
                );
        }
        [Test]
        public async Task ShouldNotSuppressIfAssignedInBranch()
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

                            TestContext.TestEntity p;
                            if (DateTime.UtcNow.Second > 100)
                            {                            
                                p = db.Persons
                                    .Include(x => x.Mother)
                                    .First();
                            }
                            else
                            {
                                p = new();
                            }

                            var ms = {|#0:p.Mother|}.ToString();
                        }
                    }
                }
                """;

            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(0)
                    .WithIsSuppressed(false)
                );
        }
        [Test]
        public async Task ShouldSuppressIfAssignedInAlwaysReachableBranch()
        {
            var source =
                """
                #nullable enable

                namespace TestNamespace
                {
                    #pragma warning disable CS0162

                    public class Test
                    {
                        public void Func()
                        {
                            var db = new TestContext();

                            TestContext.TestEntity p;
                            if (true)
                            {                            
                                p = db.Persons
                                    .Include(x => x.Mother)
                                    .First();
                            }
                            else
                            {
                                p = new();
                            }

                            var ms = {|#0:p.Mother|}.ToString();
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
        public async Task ShouldNotSuppressIfReassignedInLoop()
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

                            TestContext.TestEntity p = new();
                            for (int i = 0; i < 10; i++)
                            {                      
                                p = db.Persons
                                    .Include(x => x.Mother)
                                    .First();
                            }

                            var ms = {|#0:p.Mother|}.ToString();
                        }
                    }
                }
                """;

            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(0)
                    .WithIsSuppressed(false)
                );
        }
        [Test]
        public async Task ShouldNotSuppressIfAccessedAfterCatch()
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

                            TestContext.TestEntity p;
                            try
                            {
                                 p = db.Persons
                                    .Include(x => x.Mother)
                                    .First();
                            }
                            catch
                            {
                                p = new();
                            }

                            var ms = {|#0:p.Mother|}.ToString();
                        }
                    }
                }
                """;

            await VerifySuppressorAsync(source,
                DiagnosticResult.CompilerWarning("CS8602")
                    .WithLocation(0)
                    .WithIsSuppressed(false)
                );
        }

    }
}

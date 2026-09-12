using Abaddax.Roslyn.Analyzers.Tests.Common;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
using ExpressionSyntaxHelper = Abaddax.Roslyn.Analyzers.Helper.ExpressionSyntaxHelper;

namespace Abaddax.Roslyn.Analyzers.Tests.Helpers
{
    public sealed class ExpressionSyntaxHelperTests : HelperTestBase
    {
        private ExpressionSyntax Process(string source)
        {
            Parse(source,
                out var semanticModel,
                out var syntaxNode);

            var expression = syntaxNode as ExpressionSyntax;
            Assert.That(expression, Is.Not.Null);

            return ExpressionSyntaxHelper.TraverseAssignments(
                 expression,
                 semanticModel,
                 default);
        }
        private ExpressionSyntaxHelper.ExpressionOrigin? ProcessOrigin(string source)
        {
            Parse(source,
                out var semanticModel,
                out var syntaxNode);

            var expression = syntaxNode as ExpressionSyntax;
            Assert.That(expression, Is.Not.Null);

            return ExpressionSyntaxHelper.TryExpand(
               expression,
               semanticModel,
               default);
        }

        [Test]
        public void ShouldTraverseAssignment()
        {
            var result = Process(
                """
                using System;
                
                public class Test
                {
                    public void Func()
                    {
                        var x = 1;
                        var y = x;
                        [|y|].ToString();
                    }
                }
                """);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }

        //TODO:  ShouldTraverseAssignmentIfLoop -> How should it behave correclty?

        [Test]
        public void ShouldTraverseAssignmentIfForeach()
        {
            var result = Process(
                """
                using System;
                
                public class Test
                {
                    public void Func()
                    {
                        var x = new int[10];
                        int y;
                        foreach (var i in x)
                        {
                            y = i;
                            [|y|].ToString();
                        }
                       
                    }
                }
                """);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToFullString(), Is.EqualTo("new int[10]").IgnoreWhiteSpace);
        }

        [Test]
        public void ShouldTraverseAssignmentField()
        {
            var result = Process(
                """
                using System;
                
                public class Test
                {
                    int X;
                    public void Func()
                    {
                        var x = 1;
                        var y = new Test();
                        y.X = x;
                        [|y.X|].ToString();
                    }
                }
                """);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        public void ShouldTraverseAssignmentProperty()
        {
            var result = Process(
                """
                using System;
                
                public class Test
                {
                    int X { get; set; }
                    public void Func()
                    {
                        var x = 1;
                        var y = new Test();
                        y.X = x;
                        [|y.X|].ToString();
                    }
                }
                """);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        public void ShouldTraverseAssignmentPropertyCtor()
        {
            var result = Process(
                """
                using System;
                
                public class Test
                {
                    int X { get; set; }
                    public void Func()
                    {
                        var x = 1;
                        var y = new Test()
                        {
                            X = x
                        };
                        [|y.X|].ToString();
                    }
                }
                """);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }


        [Test]
        public void ShouldExpand()
        {
            var result = ProcessOrigin(
                """
                using System;
                
                public class Test
                {
                    Test Parent { get; set; }
                    int X { get; set;}
                    public void Func()
                    {
                        var x = 1;
                        var y = new Test();
                        this.Parent = y;
                        y.Parent.X = x;
                        [|Parent.Parent.X|].ToString();
                    }
                }
                """);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        public void ShouldExpandTask()
        {
            var result = ProcessOrigin(
                """
                using System;
                using System.Threading.Tasks;

                public class Test
                {
                    Test Parent { get; set; }
                    int X { get; set;}
                    public async Task Func()
                    {
                        var t = Task.FromResult(1);
                        var x = await t;
                        var y = new Test();
                        this.Parent = y;
                        y.Parent.X = x;
                        [|Parent.Parent.X|].ToString();
                    }
                }
                """);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToFullString(), Is.EqualTo("await Task.FromResult(1)").IgnoreWhiteSpace);
        }
        [Test]
        public void ShouldExpandLocalFunctionAssignments()
        {
            var result = ProcessOrigin(
                """
                using System;

                public class Test
                {
                    public int X()
                    {
                        var x = 1;
                        var y = x;
                        x = 2;
                        {
                            return y;
                        }
                        x = 3;
                    }

                    public void Func()
                    {
                        var x = X();
                        [|x|].ToString();
                    }
                }
                """);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        public void ShouldExpandLamdaLocalFunctionAssignments()
        {
            var result = ProcessOrigin(
                """
                using System;
                
                public class Test
                {
                    public int X() => 1;

                    public void Func()
                    {
                        var x = X();
                        [|x|].ToString();
                    }
                }
                """);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        public void ShouldExpandLocalFunctionOutAssignments()
        {
            var result = ProcessOrigin(
                """
                using System;
                
                public class Test
                {
                    public void X(out int y)
                    {
                        var x = 1;
                        y = x;
                        x = 3;
                    }
                    public void X2(out int y)
                    {
                        var x = 2;
                        y = x;
                        x = 3;
                    }

                    public void Func()
                    {
                        X(out var x);
                        X2(out x);
                        [|x|].ToString();
                    }
                }
                """);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToFullString(), Is.EqualTo("2").IgnoreWhiteSpace);
        }
        [Test]
        public void ShouldExpandLocalFunctionOutDeclarations()
        {
            var result = ProcessOrigin(
                """
                using System;
                
                public class Test
                {
                    public void X(out int y)
                    {
                        var x = 1;
                        y = x;
                        x = 3;
                    }

                    public void Func()
                    {
                        X(out var x);
                        [|x|].ToString();
                    }
                }
                """);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        public void ShouldExpandForwardingLocalFunctionAssignments()
        {
            var result = ProcessOrigin(
                """
                using System;
                
                public class Test
                {
                    public int X(int y)
                    {
                        var x = y;
                        return x;
                    }

                    public void Func()
                    {
                        var x = 1;
                        var y = X(x);
                        [|y|].ToString();
                    }
                }
                """);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        public void ShouldExpandForwardingLamdaFunctionAssignments()
        {
            var result = ProcessOrigin(
                """
                using System;

                public class Test
                {
                    public int X(int y, Func<int, int> selector)
                    {
                        var x = y;
                        var z = selector.Invoke(x);
                        return z;
                    }

                    public void Func()
                    {
                        var x = 1;
                        var y = X(x, (a) => a);
                        [|y|].ToString();
                    }
                }
                """);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        public void ShouldNotExpandOutsideCurrentFunction()
        {
            var result = ProcessOrigin(
                """
                using System;

                public class Test
                {
                    public void Main()
                    {
                        Func(1);
                    }

                    public void Func(int x)
                    {
                        [|x|].ToString();
                    }
                }
                """);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToFullString(), Is.EqualTo("x").IgnoreWhiteSpace);
        }

    }
}

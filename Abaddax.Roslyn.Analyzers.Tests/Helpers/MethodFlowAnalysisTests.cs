using Abaddax.Roslyn.Analyzers.Tests.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using NUnit.Framework;
using MethodFlowAnalysis = Abaddax.Roslyn.Analyzers.Helper.MethodFlowAnalysis;

namespace Abaddax.Roslyn.Analyzers.Tests.Helpers
{
    public sealed class MethodFlowAnalysisTests : HelperTestBase
    {
        private TOperation Process<TOperation>(string source,
            out SemanticModel semanticModel)
            where TOperation : class, IOperation
        {
            Parse(source,
                out semanticModel,
                out var syntaxNode);

            var expression = syntaxNode as ExpressionSyntax;
            Assert.That(expression, Is.Not.Null);

            var operation = semanticModel.GetOperation(
              syntaxNode) as TOperation;

            Assert.That(operation, Is.Not.Null);

            return operation;
        }

        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleReturnValues))]
        public void ShouldFindSimpleReturnValue()
        {
            var operation = Process<IInvocationOperation>("""
                using System;

                public class Test
                {
                    void Main()
                    {
                	    var a = [|Func()|];
                    }
                    int Func()
                    {
                	    var x = 1;
                	    return x;
                    }
                }
                """,
                out var semanticModel);

            var possibleReturns = MethodFlowAnalysis.GetPossibleReturnValues(
                operation,
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleReturns, Has.Length.EqualTo(1));
            Assert.That(possibleReturns[0].Syntax.ToFullString(), Is.EqualTo("x").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleReturnValues))]
        public void ShouldFindSimpleReturnValues()
        {
            var operation = Process<IInvocationOperation>("""
                using System;

                public class Test
                {
                    void Main(bool flag)
                    {
                	    var a = [|Func(flag)|];
                    }
                    int Func(bool flag)
                    {
                        if (flag)
                		    return 1;
                	    else
                		    return 2;
                        return 3;   
                    }
                }
                """,
                out var semanticModel);

            var possibleReturns = MethodFlowAnalysis.GetPossibleReturnValues(
                operation,
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleReturns, Has.Length.EqualTo(2));
            Assert.That(possibleReturns[0].Syntax.ToFullString(), Is.EqualTo("2").IgnoreWhiteSpace);
            Assert.That(possibleReturns[1].Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleReturnValues))]
        public void ShouldFindReturnValuesIfBoolBranch()
        {
            var operation = Process<IInvocationOperation>("""
                using System;

                public class Test
                {
                    void Main()
                    {
                	    var a = [|Func(true)|];
                    }
                    int Func(bool flag)
                    {
                        if (flag)
                		    return 1;
                	    else
                		    return 2;
                        return 3;   
                    }
                }
                """,
                out var semanticModel);

            var possibleReturns = MethodFlowAnalysis.GetPossibleReturnValues(
                operation,
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleReturns, Has.Length.EqualTo(1));
            Assert.That(possibleReturns[0].Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleReturnValues))]
        public void ShouldFindReturnValuesIfCompareBranch()
        {
            var operation = Process<IInvocationOperation>("""
                using System;

                public class Test
                {
                    void Main()
                    {
                	    var a = [|Func(10)|];
                    }
                    int Func(int num)
                    {
                        if (num == 10)
                		    return 1;
                	    else
                		    return 2;
                        return 3;   
                    }
                }
                """,
                out var semanticModel);

            var possibleReturns = MethodFlowAnalysis.GetPossibleReturnValues(
                operation,
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleReturns, Has.Length.EqualTo(1));
            Assert.That(possibleReturns[0].Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleReturnValues))]
        public void ShouldFindReturnValuesIfGoto()
        {
            var operation = Process<IInvocationOperation>("""
                using System;

                public class Test
                {
                    void Main()
                    {
                	    var a = [|Func("cba")|];
                    }
                    int Func(string flag)
                    {
                        goto START;
                        RETURN1:
                        return 1;
                        RETURN2:
                        return 2;
                        BRANCH:
                        if (flag=="abc")
                		    goto RETURN1;
                	    else
                		    goto RETURN2;
                        START:
                        if(flag == "cba")
                            goto BRANCH;
                        return 0;
                    }
                }
                """,
                out var semanticModel);

            var possibleReturns = MethodFlowAnalysis.GetPossibleReturnValues(
                operation,
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleReturns, Has.Length.EqualTo(1));
            Assert.That(possibleReturns[0].Syntax.ToFullString(), Is.EqualTo("2").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleReturnValues))]
        public void ShouldFindReturnValuesIfLocalFunctionCall()
        {
            var operation = Process<IInvocationOperation>("""
                using System;

                public class Test
                {
                    public enum Flags
                    {
                        A,
                        B
                    }
                
                    void Main()
                    {
                	    var a = [|Func(Flags.A)|];
                    }
                    int Func(Flags flag)
                    {
                        return X();
                        int X()
                        {
                            if (flag == Flags.A)
                		        return A();
                	        else
                		        return B();
                            
                            int A()
                            {
                                return 1;
                            }
                            int B()
                            {
                                return 2;
                            }
                        }
                    }
                }
                """,
                out var semanticModel);

            var possibleReturns = MethodFlowAnalysis.GetPossibleReturnValues(
                operation,
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleReturns, Has.Length.EqualTo(1));
            Assert.That(possibleReturns[0].Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleReturnValues))]
        public void ShouldFindReturnValuesIfForwardParameter()
        {
            var operation = Process<IInvocationOperation>("""
                using System;

                public class Test
                {               
                    void Main()
                    {
                	    var a = [|Func(1)|];
                    }
                    int Func(int x)
                    {
                        return x;
                    }
                }
                """,
                out var semanticModel);

            var possibleReturns = MethodFlowAnalysis.GetPossibleReturnValues(
                operation,
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleReturns, Has.Length.EqualTo(1));
            Assert.That(possibleReturns[0].Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleReturnValues))]
        public void ShouldFindReturnValuesIfLoop()
        {
            var operation = Process<IInvocationOperation>("""
                using System;

                public class Test
                {               
                    void Main()
                    {
                	    var a = [|Func()|];
                    }
                    int Func()
                    {
                        int x = 1;
                        for(int i = 0; i < 10; i++)
                        {
                            x = i;
                            if(i > 20)
                                return i;
                        }
                        return x;
                    }
                }
                """,
                out var semanticModel);

            var possibleReturns = MethodFlowAnalysis.GetPossibleReturnValues(
                operation,
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleReturns, Has.Length.EqualTo(2));
            Assert.That(possibleReturns[0].Syntax.ToFullString(), Is.EqualTo("x").IgnoreWhiteSpace);
            Assert.That(possibleReturns[1].Syntax.ToFullString(), Is.EqualTo("i").IgnoreWhiteSpace);
        }


        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleOutParameterValues))]
        public void ShouldFindSimpleOutParameterValue()
        {
            var operation = Process<IInvocationOperation>("""
                using System;

                public class Test
                {
                    void Main()
                    {
                	    [|Func(out int a)|];
                    }
                    void Func(out int y)
                    {
                        var x = 1;
                	    y = x;
                    }
                }
                """,
               out var semanticModel);

            var possibleOutParameters = MethodFlowAnalysis.GetPossibleOutParameterValues(
                operation,
                operation.Arguments[0],
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleOutParameters, Has.Length.EqualTo(1));
            Assert.That(possibleOutParameters[0].Syntax.ToFullString(), Is.EqualTo("x").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleOutParameterValues))]
        public void ShouldFindSimpleOutParameterValues()
        {
            var operation = Process<IInvocationOperation>("""
                using System;

                public class Test
                {
                    void Main(bool flag)
                    {
                	    [|Func(flag, out int a)|];
                    }
                    void Func(bool flag, out int x)
                    {
                        if (flag)
                		    x = 1;
                	    else
                		    x = 2;
                    }
                }
                """,
               out var semanticModel);

            var possibleOutParameters = MethodFlowAnalysis.GetPossibleOutParameterValues(
                operation,
                operation.Arguments[1],
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleOutParameters, Has.Length.EqualTo(2));
            Assert.That(possibleOutParameters[0].Syntax.ToFullString(), Is.EqualTo("2").IgnoreWhiteSpace);
            Assert.That(possibleOutParameters[1].Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleOutParameterValues))]
        public void ShouldFindOutParameterValuesIfBoolBranch()
        {
            var operation = Process<IInvocationOperation>("""
                using System;

                public class Test
                {
                    void Main()
                    {
                	    [|Func(true, out int a)|];
                    }
                    void Func(bool flag, out int x)
                    {
                        if (flag)
                		    x = 1;
                	    else
                		    x = 2;
                    }
                }
                """,
               out var semanticModel);

            var possibleOutParameters = MethodFlowAnalysis.GetPossibleOutParameterValues(
                operation,
                operation.Arguments[1],
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleOutParameters, Has.Length.EqualTo(1));
            Assert.That(possibleOutParameters[0].Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleOutParameterValues))]
        public void ShouldFindOutParameterValuesIfCompareBranch()
        {
            var operation = Process<IInvocationOperation>("""
                using System;

                public class Test
                {
                    void Main()
                    {
                	    [|Func(10, out int a)|];
                    }
                    void Func(int num, out int x)
                    {
                        if (num == 10)
                		    x = 1;
                	    else
                		    x = 2;
                    }
                }
                """,
               out var semanticModel);

            var possibleOutParameters = MethodFlowAnalysis.GetPossibleOutParameterValues(
                operation,
                operation.Arguments[1],
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleOutParameters, Has.Length.EqualTo(1));
            Assert.That(possibleOutParameters[0].Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleOutParameterValues))]
        public void ShouldFindOutParameterValuesIfGoto()
        {
            var operation = Process<IInvocationOperation>("""
                using System;

                public class Test
                {
                    void Main()
                    {
                	    [|Func("cba", out int a)|];
                    }
                    void Func(string flag, out int x)
                    {
                        goto START;
                        RETURN1:
                        x = 1;
                        return;
                        RETURN2:
                        x = 2;
                        return;
                        BRANCH:
                        if (flag=="abc")
                		    goto RETURN1;
                	    else
                		    goto RETURN2;
                        START:
                        if(flag == "cba")
                            goto BRANCH;
                        x = 0;
                        return;                        
                    }
                }
                """,
               out var semanticModel);

            var possibleOutParameters = MethodFlowAnalysis.GetPossibleOutParameterValues(
                operation,
                operation.Arguments[1],
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleOutParameters, Has.Length.EqualTo(1));
            Assert.That(possibleOutParameters[0].Syntax.ToFullString(), Is.EqualTo("2").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleOutParameterValues))]
        public void ShouldFindOutParameterValuesIfLocalFunctionCall()
        {
            var operation = Process<IInvocationOperation>("""
                using System;

                public class Test
                {
                    public enum Flags
                    {
                        A,
                        B
                    }

                    void Main()
                    {
                	    [|Func(Flags.A, out int a)|];
                    }
                    void Func(Flags flag, out int x)
                    {
                        X(out x);
                        void X(out int y)
                        {
                            if (flag == Flags.A)
                                A(out y);
                            else
                                B(out y);
                            void A(out int z)
                            {
                                z = 1;
                            }
                            void B(out int z)
                            {
                                z = 2;
                            }
                        }
                    }
                }
                """,
               out var semanticModel);

            var possibleOutParameters = MethodFlowAnalysis.GetPossibleOutParameterValues(
                operation,
                operation.Arguments[1],
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleOutParameters, Has.Length.EqualTo(1));
            Assert.That(possibleOutParameters[0].Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleOutParameterValues))]
        public void ShouldFindOutParameterValuesIfForwardParameter()
        {
            var operation = Process<IInvocationOperation>("""
                using System;

                public class Test
                {
                    void Main()
                    {
                	    [|Func(1, out int a)|];
                    }
                    void Func(int x, out int y)
                    {
                        y = x;
                    }
                }
                """,
               out var semanticModel);

            var possibleOutParameters = MethodFlowAnalysis.GetPossibleOutParameterValues(
                operation,
                operation.Arguments[1],
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleOutParameters, Has.Length.EqualTo(1));
            Assert.That(possibleOutParameters[0].Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleOutParameterValues))]
        public void ShouldFindOutParameterValuesIfLoop()
        {
            var operation = Process<IInvocationOperation>("""
                using System;

                public class Test
                {
                    void Main()
                    {
                	    [|Func(out int a)|];
                    }
                    void Func(out int x)
                    {
                        var y = 1;
                        for(int i = 0; i < 10; i++)
                        {
                            y = i;
                            if(i > 20)
                            {
                                x = i;
                                return;
                            }
                        }
                        x = y;
                        return;
                    }
                }
                """,
               out var semanticModel);

            var possibleOutParameters = MethodFlowAnalysis.GetPossibleOutParameterValues(
                operation,
                operation.Arguments[0],
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleOutParameters, Has.Length.EqualTo(2));
            Assert.That(possibleOutParameters[0].Syntax.ToFullString(), Is.EqualTo("y").IgnoreWhiteSpace);
            Assert.That(possibleOutParameters[1].Syntax.ToFullString(), Is.EqualTo("i").IgnoreWhiteSpace);
        }


        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleLastAssignment))]
        public void ShouldFindSimpleLastAssignment()
        {
            var operation = Process<IOperation>(
                """
                public class Test
                {
                    public void Func()
                    {
                        var x = 1;
                        var y = x;
                        [|y|].ToString();
                    }
                }
                """,
                out var semanticModel);

            var possibleLastAssignments = MethodFlowAnalysis.GetPossibleLastAssignment(
                operation,
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleLastAssignments, Has.Length.EqualTo(1));
            Assert.That(possibleLastAssignments[0].Syntax.ToFullString(), Is.EqualTo("x").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleLastAssignment))]
        public void ShouldFindLastAssignmentIfBranch()
        {
            var operation = Process<IOperation>(
                """
                public class Test
                {
                    public void Func()
                    {
                        int y;
                        if (false)
                            y = 1;
                        else
                            y = 2;

                        [|y|].ToString();
                    }
                }
                """,
                out var semanticModel);

            var possibleLastAssignments = MethodFlowAnalysis.GetPossibleLastAssignment(
                operation,
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleLastAssignments, Has.Length.EqualTo(1));
            Assert.That(possibleLastAssignments[0].Syntax.ToFullString(), Is.EqualTo("2").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleLastAssignment))]
        public void ShouldFindLastAssignmentIfAssignedAfterBranch()
        {
            var operation = Process<IOperation>(
                """
                public class Test
                {
                    public void Func()
                    {
                        int y;
                        if (true)
                            y = 1;
                        else
                            y = 2;
                        y = 3;
                        [|y|].ToString();
                    }
                }
                """,
                out var semanticModel);

            var possibleLastAssignments = MethodFlowAnalysis.GetPossibleLastAssignment(
                operation,
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleLastAssignments, Has.Length.EqualTo(1));
            Assert.That(possibleLastAssignments[0].Syntax.ToFullString(), Is.EqualTo("3").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleLastAssignment))]
        public void ShouldFindLastAssignmentIfLoop()
        {
            var operation = Process<IOperation>(
                """
                public class Test
                {
                    public void Func()
                    {
                        int y = 0;
                        for(int i = 0; i < 10; i++)
                        {
                            y = i;
                        }
                        [|y|].ToString();
                    }
                }
                """,
                out var semanticModel);

            var possibleLastAssignments = MethodFlowAnalysis.GetPossibleLastAssignment(
                operation,
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleLastAssignments, Has.Length.EqualTo(2));
            Assert.That(possibleLastAssignments[0].Syntax.ToFullString(), Is.EqualTo("i").IgnoreWhiteSpace);
            Assert.That(possibleLastAssignments[1].Syntax.ToFullString(), Is.EqualTo("0").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleLastAssignment))]
        public void ShouldFindLastAssignmentIfForeach()
        {
            var operation = Process<IOperation>(
                """
                public class Test
                {
                    public void Func()
                    {
                        int y = 0;
                        foreach (var i in new int[10])
                        {
                            y = i;
                        }
                        [|y|].ToString();
                    }
                }
                """,
                out var semanticModel);

            var possibleLastAssignments = MethodFlowAnalysis.GetPossibleLastAssignment(
                operation,
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleLastAssignments, Has.Length.EqualTo(2));
            Assert.That(possibleLastAssignments[0].Syntax.ToFullString(), Is.EqualTo("i").IgnoreWhiteSpace);
            Assert.That(possibleLastAssignments[1].Syntax.ToFullString(), Is.EqualTo("0").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleLastAssignment))]
        public void ShouldFindSimpleLastAssignmentField()
        {
            var operation = Process<IOperation>(
                """
                public class Test
                {
                    int X;
                    public void Func()
                    {
                        var x = new Test();
                        x.X = 1;
                        [|x.X|].ToString();
                    }
                }
                """,
                out var semanticModel);

            var possibleLastAssignments = MethodFlowAnalysis.GetPossibleLastAssignment(
                operation,
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleLastAssignments, Has.Length.EqualTo(1));
            Assert.That(possibleLastAssignments[0].Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleLastAssignment))]
        public void ShouldFindSimpleLastAssignmentProperty()
        {
            var operation = Process<IOperation>(
                """
                public class Test
                {
                    int X { get; set; }
                    public void Func()
                    {
                        var x = new Test();
                        x.X = 1;
                        [|x.X|].ToString();
                    }
                }
                """,
                out var semanticModel);

            var possibleLastAssignments = MethodFlowAnalysis.GetPossibleLastAssignment(
                operation,
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleLastAssignments, Has.Length.EqualTo(1));
            Assert.That(possibleLastAssignments[0].Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleLastAssignment))]
        public void ShouldFindSimpleLastAssignmentPropertyCtor()
        {
            var operation = Process<IOperation>(
                """
                public class Test
                {
                    int X { get; set; }
                    public void Func()
                    {
                        var x = new Test()
                        {
                            X = 1
                        };
                        [|x.X|].ToString();
                    }
                }
                """,
                out var semanticModel);

            var possibleLastAssignments = MethodFlowAnalysis.GetPossibleLastAssignment(
                operation,
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleLastAssignments, Has.Length.EqualTo(1));
            Assert.That(possibleLastAssignments[0].Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleLastAssignment))]
        public void ShouldFindLastAssignmentIfLocalFunctionCall()
        {
            var operation = Process<IOperation>(
                """
                public class Test
                {
                    public void Func()
                    {
                        var x = 1;
                        X();
                        [|x|].ToString();
                        void X()
                        {
                            x = 2;
                        }
                    }
                }
                """,
                out var semanticModel);

            var possibleLastAssignments = MethodFlowAnalysis.GetPossibleLastAssignment(
                operation,
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleLastAssignments, Has.Length.EqualTo(1));
            Assert.That(possibleLastAssignments[0].Syntax.ToFullString(), Is.EqualTo("2").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleLastAssignment))]
        public void ShouldNotFindLastIfInstanceMethodCall()
        {
            var operation = Process<IOperation>(
                """
                public class Test
                {
                    int X { get; set; }
                    public void Func()
                    {
                        X = 1;
                        ResetX();
                        [|X|].ToString();
                        
                    }
                    void ResetX()
                    {
                        X = 0;
                    }
                }
                """,
                out var semanticModel);

            var possibleLastAssignments = MethodFlowAnalysis.GetPossibleLastAssignment(
                operation,
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleLastAssignments, Has.Length.EqualTo(0));
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleLastAssignment))]
        public void ShouldNotFindLastIfDerivedInstanceMethodCall()
        {
            var operation = Process<IOperation>(
                """
                public class TestBase
                {
                    public int X { get; set; }
                }

                public class Test : TestBase
                {
                    public void Func()
                    {
                        X = 1;
                        ResetX();
                        [|X|].ToString();
                        
                    }
                    void ResetX()
                    {
                        X = 0;
                    }
                }
                """,
                out var semanticModel);

            var possibleLastAssignments = MethodFlowAnalysis.GetPossibleLastAssignment(
                operation,
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleLastAssignments, Has.Length.EqualTo(0));
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.GetPossibleLastAssignment))]
        public void ShouldNotFindLastIfThisPassedMethodCall()
        {
            var operation = Process<IOperation>(
                """
                public class Test
                {
                    public int X;
                    public void Func()
                    {
                        X = 1;
                        ResetX(this);
                        [|X|].ToString();
                        
                    }
                    static void ResetX(Test test)
                    {
                        test.X = 0;
                    }
                }
                """,
                out var semanticModel);

            var possibleLastAssignments = MethodFlowAnalysis.GetPossibleLastAssignment(
                operation,
                semanticModel,
                default)
                .ToArray();

            Assert.That(possibleLastAssignments, Has.Length.EqualTo(0));
        }

        [Test]
        [Category(nameof(MethodFlowAnalysis.TraverseAssignments))]
        public void ShouldTraverseLamdaAssignmentResult()
        {
            var operation = Process<IOperation>(
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
                """,
              out var semanticModel);

            var traversed = MethodFlowAnalysis.TraverseAssignments(
                operation,
                semanticModel,
                default);

            Assert.That(traversed, Is.Not.Null);
            Assert.That(traversed.Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.TraverseAssignments))]
        public void ShouldTraverseAssignmentInsideLamda()
        {
            var operation = Process<IOperation>(
              """
                using System;

                public class Test
                {
                    public void X(int y, Func<int, string> selector)
                    {
                        var x = y;
                        var z = selector.Invoke(x);
                    }

                    public void Func()
                    {
                        var x = 1;
                        X(x, (a) => 
                        {
                            return [|a|].ToString();
                        });
                    }
                }
                """,
              out var semanticModel);

            var traversed = MethodFlowAnalysis.TraverseAssignments(
                operation,
                semanticModel,
                default);

            Assert.That(traversed, Is.Not.Null);
            Assert.That(traversed.Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.TraverseAssignments))]
        public void ShouldTraverseAssignmentInsideLocalFunction()
        {
            var operation = Process<IOperation>(
              """
                using System;

                public class Test
                {
                    public void Func()
                    {
                        var x = 1;
                        X(x);
                        string X(int a)
                        {
                            return [|a|].ToString();
                        }
                    }
                }
                """,
              out var semanticModel);

            var traversed = MethodFlowAnalysis.TraverseAssignments(
                operation,
                semanticModel,
                default);

            Assert.That(traversed, Is.Not.Null);
            Assert.That(traversed.Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(MethodFlowAnalysis.TraverseAssignments))]
        [Ignore("Linq currently not supported")]
        public void ShouldTraverseAssignmentInsideLinqLamda()
        {
            var operation = Process<IOperation>(
              """
                using System;
                using System.Linq;

                public class Test
                {
                    public void Func()
                    {
                        var x = new int[10];
                        var y = x.Where(x => [|x|] > 5);
                    }
                }
                """,
              out var semanticModel);

            var traversed = MethodFlowAnalysis.TraverseAssignments(
                operation,
                semanticModel,
                default);

            Assert.That(traversed, Is.Not.Null);
            Assert.That(traversed.Syntax.ToFullString(), Is.EqualTo("new int[10]").IgnoreWhiteSpace);
        }

        [Test]
        [Category(nameof(MethodFlowAnalysis.TraverseAssignments))]
        public void ShouldNotTraverseAssignmentOutsideCurrentFunction()
        {
            var operation = Process<IOperation>(
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
                """,
              out var semanticModel);

            var traversed = MethodFlowAnalysis.TraverseAssignments(
                operation,
                semanticModel,
                default);

            Assert.That(traversed, Is.Not.Null);
            Assert.That(traversed.Syntax.ToFullString(), Is.EqualTo("x").IgnoreWhiteSpace);
        }

    }
}

using Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow;
using Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Models;
using Abaddax.Roslyn.Analyzers.Framework.Extensions;
using Abaddax.Roslyn.Analyzers.Framework.Tests.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using NUnit.Framework;
using System.Diagnostics.CodeAnalysis;

namespace Abaddax.Roslyn.Analyzers.Framework.Tests.Analysis
{
    public sealed class VariableFlowAnalysisTest : HelperTestBase
    {
        private IOperation Process([StringSyntax(StringSyntaxHelper.CSharpTest)] string source,
            out SemanticModel semanticModel,
            out MethodDeclarationSyntax methodDecl)
        {
            return ProcessMultiple(source,
                out semanticModel,
                out methodDecl)[0];
        }
        private IOperation[] ProcessMultiple([StringSyntax(StringSyntaxHelper.CSharpTest)] string source,
            out SemanticModel semanticModel,
            out MethodDeclarationSyntax methodDecl)
        {
            var syntaxNodes = ParseMultiple(source, out semanticModel);

            var operations = new List<IOperation>();
            foreach (var syntaxNode in syntaxNodes)
            {
                var operation = semanticModel.GetOperation(syntaxNode);
                Assert.That(operation, Is.Not.Null);
                operations.Add(operation);
            }

            var methodDeclaration = syntaxNodes[0].GetContainingMethodDeclarationBlock();
            Assert.That(methodDeclaration, Is.Not.Null);
            methodDecl = methodDeclaration;

            return operations.ToArray();
        }

        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Simple")]
        public void ShouldProcessSimpleValueTypeAssignment()
        {
            var operation = Process("""
                using System;

                public class Test
                {
                    void Func()
                    {
                        var x = 1;
                        [|x|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ValueTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Fields, Is.Empty);
            Assert.That(value.PossibleValues, Has.Count.EqualTo(1));

            var possibleValue = value.PossibleValues.First();
            Assert.That(possibleValue.Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Simple")]
        public void ShouldProcessSimpleReferenceTypeAssignment()
        {
            var operation = Process("""
                using System;

                public class Test
                {
                    void Func()
                    {
                        var x = new Test();
                        [|x|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ReferenceTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Targets.IsEmpty, Is.False);
            Assert.That(value.Targets.Nodes, Has.Count.EqualTo(1));

            var node = value.Targets.Nodes.First();
            Assert.That(node.AllocationOperation, Is.Not.Null);
            Assert.That(node.AllocationOperation.Syntax.ToFullString(), Is.EqualTo("new Test()").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Simple")]
        public void ShouldProcessSimpleValueTypeReassignment()
        {
            var operation = Process("""
                using System;

                public class Test
                {
                    void Func()
                    {
                        var x = 1;
                        x = 2;
                        [|x|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ValueTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Fields, Is.Empty);
            Assert.That(value.PossibleValues, Has.Count.EqualTo(1));

            var possibleValue = value.PossibleValues.First();
            Assert.That(possibleValue.Syntax.ToFullString(), Is.EqualTo("2").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Simple")]
        public void ShouldProcessSimpleReferenceTypeReassignment()
        {
            var operation = Process("""
                using System;
                public class Derived : Test;
                public class Test
                {
                    void Func()
                    {
                        var x = new Test();
                        x = new Derived();
                        [|x|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ReferenceTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Targets.IsEmpty, Is.False);
            Assert.That(value.Targets.Nodes, Has.Count.EqualTo(1));

            var node = value.Targets.Nodes.First();
            Assert.That(node.AllocationOperation, Is.Not.Null);
            Assert.That(node.AllocationOperation.Syntax.ToFullString(), Is.EqualTo("new Derived()").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Simple")]
        public void ShouldProcessSimpleValueTypeCopy()
        {
            var operation = Process("""
                using System;

                public class Test
                {
                    void Func()
                    {
                        var x = 1;
                        var y = x;
                        [|y|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("y").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ValueTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Fields, Is.Empty);
            Assert.That(value.PossibleValues, Has.Count.EqualTo(1));

            var possibleValue = value.PossibleValues.First();
            Assert.That(possibleValue.Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Simple")]
        public void ShouldProcessSimpleReferenceTypeCopy()
        {
            var operation = Process("""
                using System;
                public class Test
                {
                    void Func()
                    {
                        var x = new Test();
                        var y = x;
                        [|y|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("y").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ReferenceTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Targets.IsEmpty, Is.False);
            Assert.That(value.Targets.Nodes, Has.Count.EqualTo(1));

            var node = value.Targets.Nodes.First();
            Assert.That(node.AllocationOperation, Is.Not.Null);
            Assert.That(node.AllocationOperation.Syntax.ToFullString(), Is.EqualTo("new Test()").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Simple")]
        public void ShouldProcessSimpleNullableValueTypeNullAssignment()
        {
            var operation = Process("""
                using System;

                public class Test
                {
                    void Func()
                    {
                        int? x = null;
                        [|x|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ValueTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Fields, Is.Empty);
            Assert.That(value.PossibleValues, Has.Count.EqualTo(1));

            var possibleValue = value.PossibleValues.First();
            Assert.That(possibleValue.Syntax.ToFullString(), Is.EqualTo("null").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Simple")]
        public void ShouldProcessSimpleReferenceTypeNullAssignment()
        {
            var operation = Process("""
                using System;

                public class Test
                {
                    void Func()
                    {
                        Test x = null;
                        [|x|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ReferenceTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Targets.IsEmpty, Is.False);
            Assert.That(value.Targets.Nodes, Has.Count.EqualTo(1));

            var node = value.Targets.Nodes.First();
            Assert.That(node.AllocationOperation, Is.Not.Null);
            Assert.That(node.AllocationOperation.Syntax.ToFullString(), Is.EqualTo("null").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Simple")]
        public void ShouldProcessSimpleValueTypeStaticAssignment()
        {
            var operation = Process("""
                using System;

                public static class Static
                {
                    public static int Value { get; }
                }
                public class Test
                {
                    void Func()
                    {
                        var x = Static.Value;
                        [|x|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ValueTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Fields, Is.Empty);
            Assert.That(value.PossibleValues, Has.Count.EqualTo(1));

            var possibleValue = value.PossibleValues.First();
            Assert.That(possibleValue.Syntax.ToFullString(), Is.EqualTo("Static.Value").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Simple")]
        public void ShouldProcessSimpleReferenceTypeStaticAssignment()
        {
            var operation = Process("""
                using System;

                public static class Static
                {
                    public static Test Value { get; }
                }
                public class Test
                {
                    void Func()
                    {
                        Test x = Static.Value;
                        [|x|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ReferenceTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Targets.IsEmpty, Is.False);
            Assert.That(value.Targets.Nodes, Has.Count.EqualTo(1));

            var node = value.Targets.Nodes.First();
            Assert.That(node.AllocationOperation, Is.Not.Null);
            Assert.That(node.AllocationOperation.Syntax.ToFullString(), Is.EqualTo("Static.Value").IgnoreWhiteSpace);
        }


        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Nested")]
        public void ShouldProcessNestedValueTypeAssignment()
        {
            var operation = Process("""
                using System;

                public struct Test
                {
                    public int X { get; set; }
                    void Func()
                    {
                        var x = new Test()
                        {
                            X = 1
                        };
                        [|x.X|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x.X").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ValueTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Fields, Has.Count.EqualTo(1));
            Assert.That(value.PossibleValues, Is.Empty);

            var fieldValue = value.Fields[path.Members[0]] as ValueTypeValue;
            Assert.That(fieldValue, Is.Not.Null);
            Assert.That(fieldValue.Fields, Is.Empty);
            Assert.That(fieldValue.PossibleValues, Has.Count.EqualTo(1));

            var possibleFieldValue = fieldValue.PossibleValues.First();
            Assert.That(possibleFieldValue.Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Nested")]
        public void ShouldProcessNestedReferenceTypeAssignment()
        {
            var operation = Process("""
                using System;
                public class Derived : Test;
                public class Test
                {
                    public Test X { get; set; }
                    void Func()
                    {
                        var x = new Test()
                        {
                            X = new Derived()
                        };
                        [|x.X|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x.X").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ReferenceTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Targets.IsEmpty, Is.False);
            Assert.That(value.Targets.Nodes, Has.Count.EqualTo(1));

            var node = value.Targets.Nodes.First();
            var heapNode = state.HeapStore[node];
            Assert.That(heapNode.Allocation, Is.EqualTo(node));
            Assert.That(heapNode.Fields, Has.Count.EqualTo(1));

            var fieldValue = heapNode.Fields[path.Members[0]] as ReferenceTypeValue;
            Assert.That(fieldValue, Is.Not.Null);
            Assert.That(fieldValue.Targets.IsEmpty, Is.False);
            Assert.That(fieldValue.Targets.Nodes, Has.Count.EqualTo(1));

            var fieldNode = fieldValue.Targets.Nodes.First();
            Assert.That(fieldNode.AllocationOperation, Is.Not.Null);
            Assert.That(fieldNode.AllocationOperation.Syntax.ToFullString(), Is.EqualTo("new Derived()").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Nested")]
        public void ShouldProcessNestedValueTypeReassignment()
        {
            var operation = Process("""
                using System;

                public struct Test
                {
                    public int X { get; set; }
                    void Func()
                    {
                        var x = new Test()
                        {
                            X = 1
                        };
                        x.X = 2;
                        [|x.X|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x.X").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ValueTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Fields, Has.Count.EqualTo(1));
            Assert.That(value.PossibleValues, Is.Empty);

            var fieldValue = value.Fields[path.Members[0]] as ValueTypeValue;
            Assert.That(fieldValue, Is.Not.Null);
            Assert.That(fieldValue.Fields, Is.Empty);
            Assert.That(fieldValue.PossibleValues, Has.Count.EqualTo(1));

            var possibleFieldValue = fieldValue.PossibleValues.First();
            Assert.That(possibleFieldValue.Syntax.ToFullString(), Is.EqualTo("2").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Nested")]
        public void ShouldProcessNestedReferenceTypeReassignment()
        {
            var operation = Process("""
                using System;
                public class Derived : Test;
                public class Derived2 : Test;
                public class Test
                {
                    public Test X { get; set; }
                    void Func()
                    {
                        var x = new Test()
                        {
                            X = new Derived()
                        };
                        x.X = new Derived2();
                        [|x.X|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x.X").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ReferenceTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Targets.IsEmpty, Is.False);
            Assert.That(value.Targets.Nodes, Has.Count.EqualTo(1));

            var node = value.Targets.Nodes.First();
            var heapNode = state.HeapStore[node];
            Assert.That(heapNode.Allocation, Is.EqualTo(node));
            Assert.That(heapNode.Fields, Has.Count.EqualTo(1));

            var fieldValue = heapNode.Fields[path.Members[0]] as ReferenceTypeValue;
            Assert.That(fieldValue, Is.Not.Null);
            Assert.That(fieldValue.Targets.IsEmpty, Is.False);
            Assert.That(fieldValue.Targets.Nodes, Has.Count.EqualTo(1));

            var fieldNode = fieldValue.Targets.Nodes.First();
            Assert.That(fieldNode.AllocationOperation, Is.Not.Null);
            Assert.That(fieldNode.AllocationOperation.Syntax.ToFullString(), Is.EqualTo("new Derived2()").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Nested")]
        public void ShouldProcessNestedValueTypeCopy()
        {
            var operation = Process("""
                using System;

                public struct Test
                {
                    public int X { get; set; }
                    void Func()
                    {
                        var x = new Test()
                        {
                            X = 1
                        };
                        x.X = 2;
                        var y = new Test();
                        y.X = x.X;
                        x.X = 3;
                        [|y.X|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("y.X").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ValueTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Fields, Has.Count.EqualTo(1));
            Assert.That(value.PossibleValues, Is.Empty);

            var fieldValue = value.Fields[path.Members[0]] as ValueTypeValue;
            Assert.That(fieldValue, Is.Not.Null);
            Assert.That(fieldValue.Fields, Is.Empty);
            Assert.That(fieldValue.PossibleValues, Has.Count.EqualTo(1));

            var possibleFieldValue = fieldValue.PossibleValues.First();
            Assert.That(possibleFieldValue.Syntax.ToFullString(), Is.EqualTo("2").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Nested")]
        public void ShouldProcessNestedReferenceTypeCopy()
        {
            var operation = Process("""
                using System;
                public class Derived : Test;
                public class Derived2 : Test;
                public class Test
                {
                    public Test X { get; set; }
                    void Func()
                    {
                        var x = new Test()
                        {
                            X = new Derived()
                        };
                        x.X = new Derived2();
                        var y = new Test();
                        y.X = x.X;
                        x.X = new Derived();
                        [|y.X|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("y.X").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ReferenceTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Targets.IsEmpty, Is.False);
            Assert.That(value.Targets.Nodes, Has.Count.EqualTo(1));

            var node = value.Targets.Nodes.First();
            var heapNode = state.HeapStore[node];
            Assert.That(heapNode.Allocation, Is.EqualTo(node));
            Assert.That(heapNode.Fields, Has.Count.EqualTo(1));

            var fieldValue = heapNode.Fields[path.Members[0]] as ReferenceTypeValue;
            Assert.That(fieldValue, Is.Not.Null);
            Assert.That(fieldValue.Targets.IsEmpty, Is.False);
            Assert.That(fieldValue.Targets.Nodes, Has.Count.EqualTo(1));

            var fieldNode = fieldValue.Targets.Nodes.First();
            Assert.That(fieldNode.AllocationOperation, Is.Not.Null);
            Assert.That(fieldNode.AllocationOperation.Syntax.ToFullString(), Is.EqualTo("new Derived2()").IgnoreWhiteSpace);
        }

        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Condition")]
        public void ShouldProcessBranchedValueTypeAssignment()
        {
            var operation = Process("""
                using System;

                public class Test
                {
                    void Func(bool condition)
                    {
                        int x = 0;
                        if (condition)
                            x = 1;
                        else
                            x = 2;
                        [|x|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ValueTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Fields, Is.Empty);
            Assert.That(value.PossibleValues, Has.Count.EqualTo(2));

            Assert.That(
                value.PossibleValues.Select(x => x.Syntax.ToFullString()),
                Is.EquivalentTo(["1", "2"]).IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Condition")]
        public void ShouldProcessBranchedReferenceTypeAssignment()
        {
            var operation = Process("""
                using System;
                public class Derived : Test;
                public class Derived2 : Test;
                public class Test
                {
                    void Func(bool condition)
                    {
                        Test x = new Test();
                        if (condition)
                            x = new Derived();
                        else
                            x = new Derived2();
                        [|x|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ReferenceTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Targets.IsEmpty, Is.False);
            Assert.That(value.Targets.Nodes, Has.Count.EqualTo(2));

            Assert.That(
                value.Targets.Nodes.Select(x => x.AllocationOperation?.Syntax.ToFullString()),
                Is.EquivalentTo(["new Derived()", "new Derived2()"]).IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Condition")]
        public void ShouldProcessTernaryValueTypeAssignment()
        {
            var operation = Process("""
                using System;

                public class Test
                {
                    void Func(bool condition)
                    {
                        int x = condition
                            ? 1
                            : 2;
                        [|x|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ValueTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Fields, Is.Empty);
            Assert.That(value.PossibleValues, Has.Count.EqualTo(2));

            Assert.That(
                value.PossibleValues.Select(x => x.Syntax.ToFullString()),
                Is.EquivalentTo(["1", "2"]).IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Condition")]
        public void ShouldProcessTernaryReferenceTypeAssignment()
        {
            var operation = Process("""
                using System;
                public class Derived : Test;
                public class Derived2 : Test;
                public class Test
                {
                    void Func(bool condition)
                    {
                        Test x = condition
                            ? new Derived()
                            : new Derived2();
                        [|x|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ReferenceTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Targets.IsEmpty, Is.False);
            Assert.That(value.Targets.Nodes, Has.Count.EqualTo(2));

            Assert.That(
                value.Targets.Nodes.Select(x => x.AllocationOperation?.Syntax.ToFullString()),
                Is.EquivalentTo(["new Derived()", "new Derived2()"]).IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Condition")]
        public void ShouldProcessFunctionForeachLoop()
        {
            var operation = Process(""""
                using System;

                public class Test
                {
                    void Func()
                    {
                        var items = new int[10];
                        int x = 0;
                        foreach(var i in items)
                        {
                            x = i;
                        }
                        [|x|].ToString();
                    }
                }
                """",
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ValueTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Fields, Is.Empty);
            Assert.That(value.PossibleValues, Has.Count.EqualTo(2));

            Assert.That(
                value.PossibleValues.Select(x => x.Syntax.ToFullString()),
                Is.EquivalentTo(["0", "var"]).IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Condition")]
        public void ShouldProcessTryCatchFinally()
        {
            var operations = ProcessMultiple(""""
                using System;

                public class Test
                {
                    void Func()
                    {
                        var x = 1;
                        var y = 1;
                        try
                        {
                            x = 2;
                            y = 2;
                        }
                        catch (Exception ex)
                        {
                            x = 3;
                            y = 3;
                        }
                        finally
                        {
                            y = 4;
                        }
                        [|x|].ToString();
                        [|y|].ToString();
                    }
                }
                """",
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var operation1 = operations[0];
            var operation2 = operations[1];

            var state1 = flowAnalysis.TryGetState(operation1);
            Assert.That(state1, Is.Not.Null);
            var path1 = VariableFlowAnalysis.BuildAccessPath(operation1);
            Assert.That(path1.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);

            var state2 = flowAnalysis.TryGetState(operation2);
            Assert.That(state2, Is.Not.Null);
            var path2 = VariableFlowAnalysis.BuildAccessPath(operation2);
            Assert.That(path2.ToString(), Is.EqualTo("y").IgnoreWhiteSpace);

            var value1 = state1.Environment[path1.Root] as ValueTypeValue;
            Assert.That(value1, Is.Not.Null);
            Assert.That(value1.Fields, Is.Empty);
            Assert.That(value1.PossibleValues, Has.Count.EqualTo(2));

            var value2 = state2.Environment[path2.Root] as ValueTypeValue;
            Assert.That(value2, Is.Not.Null);
            Assert.That(value2.Fields, Is.Empty);
            Assert.That(value2.PossibleValues, Has.Count.EqualTo(1));

            Assert.That(
                value1.PossibleValues.Select(x => x.Syntax.ToFullString()),
                Is.EquivalentTo(["2", "3"]).IgnoreWhiteSpace);

            var possibleValue2 = value2.PossibleValues.First();
            Assert.That(possibleValue2.Syntax.ToFullString(), Is.EqualTo("4").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Condition")]
        public void ShouldProcessTryCatchFinallyWithExceptionReturn()
        {
            var operations = ProcessMultiple(""""
                using System;

                public class Test
                {
                    void Func(bool b)
                    {
                        var x = 1;
                        var y = 1;
                        try
                        {
                            if (b)
                                x = 2;
                            else
                                x = 0;
                            y = 2;
                        }
                        catch (Exception ex) when (ex is ArgumentException)
                        {
                            x = 3;
                            y = 3;
                            goto END;
                        }
                        finally
                        {
                            y = 4;
                        }
                        [|x|].ToString();
                        [|y|].ToString();
                    END:
                        Console.WriteLine("End");
                        return;
                    }
                }
                """",
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var operation1 = operations[0];
            var operation2 = operations[1];

            var state1 = flowAnalysis.TryGetState(operation1);
            Assert.That(state1, Is.Not.Null);
            var path1 = VariableFlowAnalysis.BuildAccessPath(operation1);
            Assert.That(path1.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);

            var state2 = flowAnalysis.TryGetState(operation2);
            Assert.That(state2, Is.Not.Null);
            var path2 = VariableFlowAnalysis.BuildAccessPath(operation2);
            Assert.That(path2.ToString(), Is.EqualTo("y").IgnoreWhiteSpace);

            var value1 = state1.Environment[path1.Root] as ValueTypeValue;
            Assert.That(value1, Is.Not.Null);
            Assert.That(value1.Fields, Is.Empty);
            Assert.That(value1.PossibleValues, Has.Count.EqualTo(4));

            var value2 = state2.Environment[path2.Root] as ValueTypeValue;
            Assert.That(value2, Is.Not.Null);
            Assert.That(value2.Fields, Is.Empty);
            Assert.That(value2.PossibleValues, Has.Count.EqualTo(1));

            Assert.That(
                  value1.PossibleValues.Select(x => x.Syntax.ToFullString()),
                  Is.EquivalentTo(["0", "1", "2", "3"]).IgnoreWhiteSpace);

            var possibleValue2 = value2.PossibleValues.First();
            Assert.That(possibleValue2.Syntax.ToFullString(), Is.EqualTo("4").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Condition")]
        public void ShouldProcessTryCatchFinallyWithMultipleCatch()
        {
            var operations = ProcessMultiple(""""
                using System;

                public class Test
                {
                    void Func(bool b)
                    {
                        var x = 1;
                        var y = 1;
                        try
                        {
                            x = 2;
                            y = 2;
                        }
                        catch (ArgumentException ex) when (ex is Exception)
                        {
                            x = 3;
                            y = 3;
                        }
                        catch (Exception ex) when (ex is not null)
                        {
                            x = 4;
                            y = 4;
                        }
                        catch (Exception ex)
                        {
                            x = 5;
                            y = 5;
                        }
                        finally
                        {
                            y = 4;
                        }
                        [|x|].ToString();
                        [|y|].ToString();
                    }
                }
                """",
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var operation1 = operations[0];
            var operation2 = operations[1];

            var state1 = flowAnalysis.TryGetState(operation1);
            Assert.That(state1, Is.Not.Null);
            var path1 = VariableFlowAnalysis.BuildAccessPath(operation1);
            Assert.That(path1.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);

            var state2 = flowAnalysis.TryGetState(operation2);
            Assert.That(state2, Is.Not.Null);
            var path2 = VariableFlowAnalysis.BuildAccessPath(operation2);
            Assert.That(path2.ToString(), Is.EqualTo("y").IgnoreWhiteSpace);

            var value1 = state1.Environment[path1.Root] as ValueTypeValue;
            Assert.That(value1, Is.Not.Null);
            Assert.That(value1.Fields, Is.Empty);
            Assert.That(value1.PossibleValues, Has.Count.EqualTo(4));

            var value2 = state2.Environment[path2.Root] as ValueTypeValue;
            Assert.That(value2, Is.Not.Null);
            Assert.That(value2.Fields, Is.Empty);
            Assert.That(value2.PossibleValues, Has.Count.EqualTo(1));

            Assert.That(
                  value1.PossibleValues.Select(x => x.Syntax.ToFullString()),
                  Is.EquivalentTo(["2", "3", "4", "5"]).IgnoreWhiteSpace);

            var possibleValue2 = value2.PossibleValues.First();
            Assert.That(possibleValue2.Syntax.ToFullString(), Is.EqualTo("4").IgnoreWhiteSpace);
        }



        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        public void ShouldProcessFunctionResultAssignment()
        {
            var operation = Process("""
                using System;

                public class Test
                {
                    int Create() => 1;
                    void Func()
                    {
                        var x = Create();
                        [|x|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ValueTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Fields, Is.Empty);
            Assert.That(value.PossibleValues, Has.Count.EqualTo(1));

            var possibleValue = value.PossibleValues.First();
            Assert.That(possibleValue.Syntax.ToFullString(), Is.EqualTo("Create()").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        public void ShouldProcessDelegateResultAssignment()
        {
            var operation = Process("""
                using System;

                public class Test
                {
                    int Create() => 1;
                    void Func()
                    {
                        var func = Create;
                        var x = func.Invoke();
                        [|x|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ValueTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Fields, Is.Empty);
            Assert.That(value.PossibleValues, Has.Count.EqualTo(1));

            var possibleValue = value.PossibleValues.First();
            Assert.That(possibleValue.Syntax.ToFullString(), Is.EqualTo("func.Invoke()").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        public void ShouldProcessAnonymousDelegateResultAssignment()
        {
            var operation = Process("""
                using System;

                public class Test
                {
                    void Func()
                    {
                        var func = () => 1;
                        var x = func.Invoke();
                        [|x|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ValueTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Fields, Is.Empty);
            Assert.That(value.PossibleValues, Has.Count.EqualTo(1));

            var possibleValue = value.PossibleValues.First();
            Assert.That(possibleValue.Syntax.ToFullString(), Is.EqualTo("func.Invoke()").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        public void ShouldProcessAsyncFunctionResultAssignment()
        {
            var operation = Process("""
                using System;
                using System.Threading.Tasks;

                public class Test
                {
                    Task<int> CreateAsync() => Task.FromResult(1);
                    async Task Func()
                    {
                        var x = await CreateAsync();
                        [|x|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ValueTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Fields, Is.Empty);
            Assert.That(value.PossibleValues, Has.Count.EqualTo(1));

            var possibleValue = value.PossibleValues.First();
            Assert.That(possibleValue.Syntax.ToFullString(), Is.EqualTo("await CreateAsync()").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        public void ShouldNotTreatMultipleFunctionResultsAsSameReference()
        {
            var operations = ProcessMultiple("""
                using System;

                public class Test
                {
                    Test Create() => new Test();
                    void Func()
                    {
                        var x = Create();
                        var y = Create();
                        [|x|].ToString();
                        [|y|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var operation1 = operations[0];
            var operation2 = operations[1];

            var state1 = flowAnalysis.TryGetState(operation1);
            Assert.That(state1, Is.Not.Null);
            var path1 = VariableFlowAnalysis.BuildAccessPath(operation1);
            Assert.That(path1.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);

            var state2 = flowAnalysis.TryGetState(operation2);
            Assert.That(state2, Is.Not.Null);
            var path2 = VariableFlowAnalysis.BuildAccessPath(operation2);
            Assert.That(path2.ToString(), Is.EqualTo("y").IgnoreWhiteSpace);

            var value1 = state1.Environment[path1.Root] as ReferenceTypeValue;
            Assert.That(value1, Is.Not.Null);

            var value2 = state1.Environment[path2.Root] as ReferenceTypeValue;
            Assert.That(value2, Is.Not.Null);

            Assert.That(value1.IsEquivalentTo(value2), Is.False);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        public void ShouldTreatSameTaskAwaitAsSameReference()
        {
            var operations = ProcessMultiple("""
                using System;
                using System.Threading.Tasks;

                public class Test
                {
                    Task<Test> CreateAsync() => Task.FromResult(new Test());
                    async Task Func()
                    {
                        var task = CreateAsync();
                        var x = await task;
                        var y = await task;
                        [|x|].ToString();
                        [|y|].ToString();
                    }
                }
                """,
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var operation1 = operations[0];
            var operation2 = operations[1];

            var state1 = flowAnalysis.TryGetState(operation1);
            Assert.That(state1, Is.Not.Null);
            var path1 = VariableFlowAnalysis.BuildAccessPath(operation1);
            Assert.That(path1.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);

            var state2 = flowAnalysis.TryGetState(operation2);
            Assert.That(state2, Is.Not.Null);
            var path2 = VariableFlowAnalysis.BuildAccessPath(operation2);
            Assert.That(path2.ToString(), Is.EqualTo("y").IgnoreWhiteSpace);

            var value1 = state1.Environment[path1.Root] as ReferenceTypeValue;
            Assert.That(value1, Is.Not.Null);

            var value2 = state1.Environment[path2.Root] as ReferenceTypeValue;
            Assert.That(value2, Is.Not.Null);

            Assert.That(value1.IsEquivalentTo(value2), Is.True);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        public void ShouldProcessInlineFunctionArgumentValueTypeAssignment()
        {
            var operation = Process(""""
                using System;

                public class Test
                {
                    int Func(int x) => x;
                    void Func()
                    {
                        var x = 1;
                        var y = Func(x = 2);
                        [|x|].ToString();
                    }
                }
                """",
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ValueTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Fields, Is.Empty);
            Assert.That(value.PossibleValues, Has.Count.EqualTo(1));

            var possibleValue = value.PossibleValues.First();
            Assert.That(possibleValue.Syntax.ToFullString(), Is.EqualTo("2").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        public void ShouldProcessInlineFunctionArgumentReferenceTypeAssignment()
        {
            var operation = Process(""""
                using System;
                public class Derived : Test;
                public class Test
                {
                    Test Func(Test x) => x;
                    void Func()
                    {
                        var x = new Test();
                        var y = Func(x = new Derived());
                        [|x|].ToString();
                    }
                }
                """",
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ReferenceTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Targets.IsEmpty, Is.False);
            Assert.That(value.Targets.Nodes, Has.Count.EqualTo(1));

            var node = value.Targets.Nodes.First();
            Assert.That(node.AllocationOperation, Is.Not.Null);
            Assert.That(node.AllocationOperation.Syntax.ToFullString(), Is.EqualTo("new Derived()").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        public void ShouldProcessMultipleInlineAssignment()
        {
            var operations = ProcessMultiple(""""
                using System;

                public class Test
                {
                    int Func(int x) => x;
                    void Func()
                    {
                        var x = 1;
                        var y = 2;
                        x = y = 3;
                        [|x|].ToString();
                        [|y|].ToString();
                    }
                }
                """",
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var operation1 = operations[0];
            var operation2 = operations[1];

            var state1 = flowAnalysis.TryGetState(operation1);
            Assert.That(state1, Is.Not.Null);
            var path1 = VariableFlowAnalysis.BuildAccessPath(operation1);
            Assert.That(path1.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);

            var state2 = flowAnalysis.TryGetState(operation2);
            Assert.That(state2, Is.Not.Null);
            var path2 = VariableFlowAnalysis.BuildAccessPath(operation2);
            Assert.That(path2.ToString(), Is.EqualTo("y").IgnoreWhiteSpace);

            var value1 = state1.Environment[path1.Root] as ValueTypeValue;
            Assert.That(value1, Is.Not.Null);
            Assert.That(value1.Fields, Is.Empty);
            Assert.That(value1.PossibleValues, Has.Count.EqualTo(1));

            var value2 = state2.Environment[path2.Root] as ValueTypeValue;
            Assert.That(value2, Is.Not.Null);
            Assert.That(value2.Fields, Is.Empty);
            Assert.That(value2.PossibleValues, Has.Count.EqualTo(1));

            var possibleValue1 = value1.PossibleValues.First();
            Assert.That(possibleValue1.Syntax.ToFullString(), Is.EqualTo("3").IgnoreWhiteSpace);

            var possibleValue2 = value2.PossibleValues.First();
            Assert.That(possibleValue2.Syntax.ToFullString(), Is.EqualTo("3").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        public void ShouldProcessFunctionOutAssignment()
        {
            var operation = Process(""""
                using System;

                public class Test
                {
                    void Func(out int x) => x = 1;
                    void Func()
                    {
                        Func(out var x);
                        [|x|].ToString();
                    }
                }
                """",
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ValueTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Fields, Is.Empty);
            Assert.That(value.PossibleValues, Has.Count.EqualTo(1));

            var possibleValue = value.PossibleValues.First();
            Assert.That(possibleValue.Syntax.ToFullString(), Is.EqualTo("var x").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        public void ShouldProcessValueTupleAssignment()
        {
            var operations = ProcessMultiple(""""
                using System;

                public class Test
                {
                    void Func()
                    {
                        (var x, var y) = (1, 2);
                        [|x|].ToString();
                        [|y|].ToString();
                    }
                }
                """",
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var operation1 = operations[0];
            var operation2 = operations[1];

            var state1 = flowAnalysis.TryGetState(operation1);
            Assert.That(state1, Is.Not.Null);
            var path1 = VariableFlowAnalysis.BuildAccessPath(operation1);
            Assert.That(path1.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);

            var state2 = flowAnalysis.TryGetState(operation2);
            Assert.That(state2, Is.Not.Null);
            var path2 = VariableFlowAnalysis.BuildAccessPath(operation2);
            Assert.That(path2.ToString(), Is.EqualTo("y").IgnoreWhiteSpace);

            var value1 = state1.Environment[path1.Root] as ValueTypeValue;
            Assert.That(value1, Is.Not.Null);
            Assert.That(value1.Fields, Is.Empty);
            Assert.That(value1.PossibleValues, Has.Count.EqualTo(1));

            var value2 = state2.Environment[path2.Root] as ValueTypeValue;
            Assert.That(value2, Is.Not.Null);
            Assert.That(value2.Fields, Is.Empty);
            Assert.That(value2.PossibleValues, Has.Count.EqualTo(1));

            var possibleValue1 = value1.PossibleValues.First();
            Assert.That(possibleValue1.Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);

            var possibleValue2 = value2.PossibleValues.First();
            Assert.That(possibleValue2.Syntax.ToFullString(), Is.EqualTo("2").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        public void ShouldProcessValueTupleSwap()
        {
            var operations = ProcessMultiple(""""
                using System;

                public class Test
                {
                    void Func()
                    {
                        var x = 1;
                        var y = 2;
                        (x, y) = (y, x);
                        [|x|].ToString();
                        [|y|].ToString();
                    }
                }
                """",
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var operation1 = operations[0];
            var operation2 = operations[1];

            var state1 = flowAnalysis.TryGetState(operation1);
            Assert.That(state1, Is.Not.Null);
            var path1 = VariableFlowAnalysis.BuildAccessPath(operation1);
            Assert.That(path1.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);

            var state2 = flowAnalysis.TryGetState(operation2);
            Assert.That(state2, Is.Not.Null);
            var path2 = VariableFlowAnalysis.BuildAccessPath(operation2);
            Assert.That(path2.ToString(), Is.EqualTo("y").IgnoreWhiteSpace);

            var value1 = state1.Environment[path1.Root] as ValueTypeValue;
            Assert.That(value1, Is.Not.Null);
            Assert.That(value1.Fields, Is.Empty);
            Assert.That(value1.PossibleValues, Has.Count.EqualTo(1));

            var value2 = state2.Environment[path2.Root] as ValueTypeValue;
            Assert.That(value2, Is.Not.Null);
            Assert.That(value2.Fields, Is.Empty);
            Assert.That(value2.PossibleValues, Has.Count.EqualTo(1));

            var possibleValue1 = value1.PossibleValues.First();
            Assert.That(possibleValue1.Syntax.ToFullString(), Is.EqualTo("2").IgnoreWhiteSpace);

            var possibleValue2 = value2.PossibleValues.First();
            Assert.That(possibleValue2.Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }

        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Operator")]
        public void ShouldProcessOperatorResultAssignments()
        {
            var operation = Process(""""
                using System;

                public class Test
                {
                    void Func()
                    {
                        var x = 1;
                        var y = 2;
                        var z = x + y;
                        [|z|].ToString();
                    }
                }
                """",
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("z").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ValueTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Fields, Is.Empty);
            Assert.That(value.PossibleValues, Has.Count.EqualTo(1));

            var possibleValue = value.PossibleValues.First();
            Assert.That(possibleValue.Syntax.ToFullString(), Is.EqualTo("x + y").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Operator")]
        public void ShouldProcessPostfixIncrementAndInvalidatePossibleValues()
        {
            var operation = Process(""""
                using System;

                public class Test
                {
                    void Func()
                    {
                        var x = 1;
                        x++;
                        [|x|].ToString();
                    }
                }
                """",
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ValueTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Fields, Is.Empty);
            Assert.That(value.PossibleValues, Has.Count.EqualTo(1));

            var possibleValue = value.PossibleValues.First();
            Assert.That(possibleValue.Syntax.ToFullString(), Is.EqualTo("x++").IgnoreWhiteSpace);
        }

        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("LocalFunction")]
        public void ShouldProcessLocalFunctionInvocation()
        {
            var operations = ProcessMultiple(""""
                using System;

                public class Test
                {
                    void Func()
                    {
                        var y = 1;
                        var x = 1;
                        X();
                        [|x|].ToString();
                        [|y|].ToString();
                        void X()
                        {
                            var y = 2;
                            x = 2;
                        }
                    }

                }
                """",
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var operation1 = operations[0];
            var operation2 = operations[1];

            var state1 = flowAnalysis.TryGetState(operation1);
            Assert.That(state1, Is.Not.Null);
            var path1 = VariableFlowAnalysis.BuildAccessPath(operation1);
            Assert.That(path1.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);

            var state2 = flowAnalysis.TryGetState(operation2);
            Assert.That(state2, Is.Not.Null);
            var path2 = VariableFlowAnalysis.BuildAccessPath(operation2);
            Assert.That(path2.ToString(), Is.EqualTo("y").IgnoreWhiteSpace);

            var value1 = state1.Environment[path1.Root] as ValueTypeValue;
            Assert.That(value1, Is.Not.Null);
            Assert.That(value1.Fields, Is.Empty);
            Assert.That(value1.PossibleValues, Has.Count.EqualTo(1));

            var value2 = state2.Environment[path2.Root] as ValueTypeValue;
            Assert.That(value2, Is.Not.Null);
            Assert.That(value2.Fields, Is.Empty);
            Assert.That(value2.PossibleValues, Has.Count.EqualTo(1));

            var possibleValue1 = value1.PossibleValues.First();
            Assert.That(possibleValue1.Syntax.ToFullString(), Is.EqualTo("2").IgnoreWhiteSpace);

            var possibleValue2 = value2.PossibleValues.First();
            Assert.That(possibleValue2.Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("LocalFunction")]
        [Ignore("TODO: How should this behave?")]
        public void ShouldProcessLocalLambdaInvocationPassedAsParameter()
        {
            var operations = ProcessMultiple(""""
                using System;

                public class Test
                {
                    void Func()
                    {
                        var y = 1;
                        var x = 1;
                        X(() =>
                        {
                            var y = 2;
                            x = 2;
                        });
                        [|x|].ToString();
                        [|y|].ToString();
                        
                    }
                    void X(Action x)
                    {
                        x.Invoke();
                    }

                }
                """",
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var operation1 = operations[0];
            var operation2 = operations[1];

            var state1 = flowAnalysis.TryGetState(operation1);
            Assert.That(state1, Is.Not.Null);
            var path1 = VariableFlowAnalysis.BuildAccessPath(operation1);
            Assert.That(path1.ToString(), Is.EqualTo("x").IgnoreWhiteSpace);

            var state2 = flowAnalysis.TryGetState(operation2);
            Assert.That(state2, Is.Not.Null);
            var path2 = VariableFlowAnalysis.BuildAccessPath(operation2);
            Assert.That(path2.ToString(), Is.EqualTo("y").IgnoreWhiteSpace);

            var value1 = state1.Environment[path1.Root] as ValueTypeValue;
            Assert.That(value1, Is.Not.Null);
            Assert.That(value1.Fields, Is.Empty);
            Assert.That(value1.PossibleValues, Has.Count.EqualTo(1));

            var value2 = state2.Environment[path2.Root] as ValueTypeValue;
            Assert.That(value2, Is.Not.Null);
            Assert.That(value2.Fields, Is.Empty);
            Assert.That(value2.PossibleValues, Has.Count.EqualTo(1));

            var possibleValue1 = value1.PossibleValues.First();
            Assert.That(possibleValue1.Syntax.ToFullString(), Is.EqualTo("2").IgnoreWhiteSpace);

            var possibleValue2 = value2.PossibleValues.First();
            Assert.That(possibleValue2.Syntax.ToFullString(), Is.EqualTo("1").IgnoreWhiteSpace);
        }
        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("LocalFunction")]
        //[Ignore("TODO: Filter for known 'const' functions and also [Pure] filtering")]
        public void ShouldNotKeepPossibleValueAfterInstanceMethodInvocation()
        {
            var operation = Process(""""
                using System;

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
                """",
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var state = flowAnalysis.TryGetState(operation);
            Assert.That(state, Is.Not.Null);

            var path = VariableFlowAnalysis.BuildAccessPath(operation);
            Assert.That(path.ToString(), Is.EqualTo("this.X").IgnoreWhiteSpace);
            var value = state.Environment[path.Root] as ReferenceTypeValue;
            Assert.That(value, Is.Not.Null);
            Assert.That(value.Targets.IsEmpty, Is.False);
            Assert.That(value.Targets.Nodes, Has.Count.EqualTo(1));

            var node = value.Targets.Nodes.First();
            var heapNode = state.HeapStore[node];
            Assert.That(heapNode.Allocation, Is.EqualTo(node));
            Assert.That(heapNode.Fields, Has.Count.EqualTo(1));

            var fieldValue = heapNode.Fields[path.Members[0]] as ValueTypeValue;
            Assert.That(fieldValue, Is.Not.Null);
            Assert.That(fieldValue.PossibleValues, Has.Count.EqualTo(0));
        }









        [Test]
        [Category(nameof(VariableFlowAnalysis.RunAnalysis))]
        [Category("Complex")]
        public void ShouldProcessAllCombined()
        {
            var operations = ProcessMultiple(""""
                using System;
                using System.Collections.Generic;
                using System.Threading.Tasks;

                public class Test
                {
                    public int AllCombined(
                        int regularParam,
                        ref int refParam,
                        out string outParam,
                        in double inParam,
                        params object[] paramsArray)
                    {
                        // 1. Initialization, Locals, Constants, and Tuples
                        outParam = "Initialized";
                        const int constLocal = 42;
                        var inferredLocal = 100;
                        int explicitLocal = 10;
                        (int x, string y) tupleLocal = (1, "test");
                        Span<int> stackAllocSpan = stackalloc int[3] { 1, 2, 3 };
                        dynamic dynamicLocal = 10.5;

                        // 2. Checked / Unchecked & Unsafe / Fixed Statements
                        checked
                        { explicitLocal++; }
                        unchecked
                        { explicitLocal--; }

                        int rawValue = 10;
                        //int* ptr = &rawValue;
                        //fixed (int* fixedPtr = &stackAllocSpan[0])
                        //{
                        //    *fixedPtr = 5;
                        //}

                        // 3. Collection Expressions & Spreads (C# 12+)
                        int[] firstList = [1, 2];
                        int[] combinedList = [.. firstList, 3, 4, .. stackAllocSpan];

                        // 4. Arithmetic, Bitwise, Logical, and Null-Coalescing Operators
                        int mathResult = +explicitLocal - constLocal * 2 / 1 % 3;
                        int bitwiseResult = (1 << 2) & 3 | 5 ^ ~0;
                        bool logicResult = true && false || !true;
                        string nullCoalesce = null ?? "default";
                        string nullAssign = null;
                        nullAssign ??= "assigned";
                        int ternary = (mathResult > 0) ? 1 : 0;

                        // 5. Ranges and Indices
                        int[] rangeTest = [10, 20, 30, 40, 50];
                        int lastElement = rangeTest[^1];
                        var subRange = rangeTest[1..3];
                        rangeTest[0] = 1;

                        // 6. Using Declarations and Lock Statements
                        using var disposableResource = new System.IO.MemoryStream();
                        lock (disposableResource)
                        {
                            disposableResource.Write([1, 2, 3]);
                        }

                        // 7. Control Flow: If, Switch Statement, and Switch Expression
                        if (inferredLocal > 50)
                        {
                            inferredLocal += 1;
                        }
                        else if (inferredLocal == 50)
                        {
                            inferredLocal = 0;
                        }
                        else
                        {
                            inferredLocal -= 1;
                        }

                        switch (explicitLocal)
                        {
                            case 0:
                                goto TargetLabel;
                            case 1 when dynamicLocal > 0:
                                break;
                            default:
                                break;
                        }

                        string switchExprResult = explicitLocal switch
                        {
                            > 5 => "Large",
                            <= 5 and not 0 => "Small",
                            _ => "Zero"
                        };

                        // 8. Pattern Matching (Property, Positional, Relational, List, Type)
                        object patternObj = "Hello Roslyn";
                        if (patternObj is string { Length: > 5 } strVal)
                        {
                            // Matched property pattern
                        }

                        if (tupleLocal is (1, var nameVal))
                        {
                            // Matched positional/tuple pattern
                        }

                        if (combinedList is [1, .., > 2])
                        {
                            // Matched list pattern
                        }

                        // 9. Loops: For, Foreach, While, Do-While
                        for (int i = 0; i < 2; i++)
                        {
                            if (i == 1)
                                continue;
                            break;
                        }

                        foreach (var item in combinedList)
                        {
                            if (item == 3)
                                goto breakLabel;
                        }
                    breakLabel:

                        int whileCounter = 0;
                        while (whileCounter < 1)
                        {
                            whileCounter++;
                        }

                        do
                        {
                            whileCounter--;
                        } while (whileCounter > 0);

                        // 10. Exception Handling: Try, Catch with Filters, Finally
                        try
                        {
                            throw new InvalidOperationException("Demo exception");
                        }
                        catch (ArgumentNullException ex) when (ex.ParamName != null)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                        finally
                        {
                            refParam++;
                        }

                        // 11. Anonymous Functions & Local Functions
                        Func<int, int> anonymousMethod = delegate (int val)
                        {
                            return val * 2;
                        };
                        Func<int, int> lambdaExpr = val => val * 3;

                        static int InnerLocalFunction(int a) => a + 10;
                        int localFuncResult = InnerLocalFunction(5);

                        mathResult = mathResult + [|localFuncResult|];
                    TargetLabel:
                        return [|mathResult|];
                    }
                }
                """",
                out var semanticModel,
                out var methodBody);

            var flowAnalysis = VariableFlowAnalysis.RunAnalysis(
                methodBody,
                semanticModel,
                null,
                default);
            Assert.That(flowAnalysis, Is.Not.Null);

            var operation1 = operations[0];
            var operation2 = operations[1];

            var state1 = flowAnalysis.TryGetState(operation1);
            Assert.That(state1, Is.Not.Null);
            var path1 = VariableFlowAnalysis.BuildAccessPath(operation1);
            Assert.That(path1.ToString(), Is.EqualTo("localFuncResult").IgnoreWhiteSpace);

            var state2 = flowAnalysis.TryGetState(operation2);
            Assert.That(state2, Is.Not.Null);
            var path2 = VariableFlowAnalysis.BuildAccessPath(operation2);
            Assert.That(path2.ToString(), Is.EqualTo("mathResult").IgnoreWhiteSpace);

            var value1 = state1.Environment[path1.Root] as ValueTypeValue;
            Assert.That(value1, Is.Not.Null);
            Assert.That(value1.Fields, Is.Empty);
            Assert.That(value1.PossibleValues, Has.Count.EqualTo(1));

            var value2 = state2.Environment[path2.Root] as ValueTypeValue;
            Assert.That(value2, Is.Not.Null);
            Assert.That(value2.Fields, Is.Empty);
            Assert.That(value2.PossibleValues, Has.Count.EqualTo(2));

            var possibleValue1 = value1.PossibleValues.First();
            Assert.That(possibleValue1.Syntax.ToFullString(), Is.EqualTo("InnerLocalFunction(5)").IgnoreWhiteSpace);

            Assert.That(
                value2.PossibleValues.Select(x => x.Syntax.ToFullString()),
                Is.EquivalentTo([
                    "+explicitLocal - constLocal * 2 / 1 % 3",
                    "mathResult + localFuncResult"
                    ]).IgnoreWhiteSpace);

        }
    }

}

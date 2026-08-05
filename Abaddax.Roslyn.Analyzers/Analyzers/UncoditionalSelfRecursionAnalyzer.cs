using Abaddax.Roslyn.Analyzers.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;

namespace Abaddax.Roslyn.Analyzers.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UncoditionalSelfRecursionAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor _Rule = new DiagnosticDescriptor(
            id: AnalyzerIdentifiers.UncoditionalSelfRecursionAnalyzer,
            title: "Uncoditional self recursion",
            messageFormat: "The method '{0}' unconditioanlly calls ifself and will cause a 'StackOverflowException'",
            category: "Reliability",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
            = ImmutableArray.Create(_Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterOperationBlockAction(AnalyzeOperationBlock);
        }


        private static void AnalyzeOperationBlock(OperationBlockAnalysisContext context)
        {
            // Both methods and properties will have a method as the owning symbol
            if (context.OwningSymbol is not IMethodSymbol method)
                return;

            foreach (var operation in context.OperationBlocks)
            {
                // 'operation' will always be a 'BlockOperation'
                // We only care about method bodies, is this case 'operation.Parent' will be an 'IMethodBodyOperation'
                if (operation.Parent is not IMethodBodyOperation methodBodyOperation)
                    continue;
                var cfg = methodBodyOperation.GetControlFlowGraph(context.CancellationToken);
                if (cfg == null)
                    continue;

                if (HasUncoditionalRecursion(cfg, method))
                {
                    var diagnostic = Diagnostic.Create(_Rule, method.Locations[0], method.Name);
                    context.ReportDiagnostic(diagnostic);
                    //Only report once per method
                    break;
                }
            }
        }

        private static bool HasUncoditionalRecursion(
            ControlFlowGraph cfg,
            IMethodSymbol method)
        {
            var entry = cfg.Blocks.Where(x => x.Kind == BasicBlockKind.Entry)
               .ExactlyOneOrDefault();
            if (entry == null)
                return false;

            var visited = new HashSet<BasicBlock>();
            var queue = new Queue<BasicBlock>();

            queue.Enqueue(entry);
            bool foundRecursion = false;
            while (queue.Count > 0)
            {
                var currentBlock = queue.Dequeue();

                // Prevent infinite loops in `while`/`for` loops
                if (!visited.Add(currentBlock))
                    continue;

                // 1. Check if the block contains a recursiv call
                if (BlockContainsRecursivCall(currentBlock, method))
                {
                    foundRecursion = true;
                    continue; // Stop exploring this specific execution path
                }

                // 2. If we reached the exit cleanly without hiting a recursion
                // This means there is a safe path to exit
                if (currentBlock.Kind == BasicBlockKind.Exit)
                {
                    return false;
                }

                // 3. Enqueue the next blocks in the flow
                if (currentBlock.ConditionalSuccessor?.Destination != null)
                    queue.Enqueue(currentBlock.ConditionalSuccessor.Destination);
                if (currentBlock.FallThroughSuccessor?.Destination != null)
                    queue.Enqueue(currentBlock.FallThroughSuccessor.Destination);
            }
            // We exhaused all paths, if we found any uncoditional recursion this will be true
            return foundRecursion;
        }
        private static bool BlockContainsRecursivCall(
            BasicBlock currentBlock,
            IMethodSymbol method)
        {
            // Search all statements in the block
            foreach (var operation in currentBlock.Operations)
            {
                if (HasRecursivCall(operation, method))
                    return true;
            }

            // Search the branch conditions
            if (HasRecursivCall(currentBlock.BranchValue, method))
                return true;

            return false;
        }
        private static bool HasRecursivCall(
            IOperation? operation,
            IMethodSymbol method)
        {
            if (operation == null)
                return false;

            // Check method calls
            if (operation is IInvocationOperation invocation)
            {
                // Only check static calls and calls via this (explicit or implicit)
                // But not 'other.Method()'!
                if (invocation.Instance is null || invocation.Instance is IInstanceReferenceOperation)
                {
                    if (SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.OriginalDefinition, method.OriginalDefinition))
                    {
                        return true;
                    }
                }
            }
            // Check property getters and setters
            else if (operation is IPropertyReferenceOperation propertyReference)
            {
                // Only check static props and props via this (explicit or implicit)
                // But not 'other.Prop'!
                if (propertyReference.Instance is null || propertyReference.Instance is IInstanceReferenceOperation)
                {
                    if (SymbolEqualityComparer.Default.Equals(propertyReference.Property.GetMethod?.OriginalDefinition, method.OriginalDefinition) ||
                        SymbolEqualityComparer.Default.Equals(propertyReference.Property.SetMethod?.OriginalDefinition, method.OriginalDefinition))
                    {
                        return true;
                    }
                }
            }

            // Recursivly check child operations
            foreach (var child in operation.ChildOperations)
            {
                if (HasRecursivCall(child, method))
                    return true;
            }
            return false;
        }
    }
}

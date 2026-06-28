using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Abaddax.Roslyn.Analyzers.Extensions
{
    internal static class AssignmentHelper
    {
        public sealed record LastAssignment
        {
            public enum MatchType
            {
                UnableToResolve,
                NotFound,
                Found,
                FoundAmbiguous
            }
            public IAssignmentOperation? Assignment { get; }
            public MatchType Type { get; }
            public LastAssignment(IAssignmentOperation? assignment, MatchType type)
            {
                Assignment = assignment;
                Type = type;
                if (type == MatchType.Found && Assignment == null)
                    throw new ArgumentNullException(nameof(assignment));
            }
        }
        /// <summary>
        /// Find the last assignment made to <paramref name="variableUsageNode"/>
        /// </summary>
        /// <returns></returns>
        public static LastAssignment GetDefinitiveLastAssignment(
            MethodDeclarationSyntax methodSyntax,
            SyntaxNode variableUsageNode,
            ISymbol targetSymbol,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            // 1. Setup the Control Flow Graph
            var cfg = GetControlFlowGraph(methodSyntax, semanticModel, cancellationToken);
            if (cfg == null)
                return new LastAssignment(null, LastAssignment.MatchType.UnableToResolve);

            var usageOperation = semanticModel.GetOperation(variableUsageNode, cancellationToken);
            if (usageOperation == null)
                return new LastAssignment(null, LastAssignment.MatchType.UnableToResolve);

            // 2. Locate the starting BasicBlock and our index within it
            FindStartingBlock(cfg, variableUsageNode,
                out var startBlock,
                out var usageIndex);
            if (startBlock == null || usageIndex == -1)
                return new LastAssignment(null, LastAssignment.MatchType.UnableToResolve);

            // 3. Prepare for backwards traversal
            var foundAssignments = new HashSet<IAssignmentOperation>();
            var visitedBlocks = new HashSet<BasicBlock>();
            var queue = new Queue<(BasicBlock Block, int StartIndex)>();

            // Start looking just before the variable usage
            queue.Enqueue((startBlock, usageIndex - 1));

            // 4. Traverse backwards
            while (queue.Count > 0)
            {
                var (currentBlock, startIndex) = queue.Dequeue();

                // Prevent infinite loops in `while`/`for` loops
                if (!visitedBlocks.Add(currentBlock))
                    continue;

                bool assignmentFoundInCurrentBlock = false;

                // Walk backwards through the current block's operations
                if (currentBlock.IsReachable)
                {
                    for (int i = startIndex; i >= 0; i--)
                    {
                        var topLevelOp = currentBlock.Operations[i];
                        var nestedAssignments = topLevelOp.DescendantsAndSelf()
                                                    .OfType<IAssignmentOperation>()
                                                    .Reverse();
                        foreach (var assignment in nestedAssignments)
                        {
                            // 2. Check if the assignment target matches our symbol
                            if (IsTargetMatch(assignment, targetSymbol))
                            {
                                foundAssignments.Add(assignment);
                                assignmentFoundInCurrentBlock = true;
                                break; // Stop looking at earlier assignments in this statement
                            }
                        }
                        // We found the latest assignment in this specific branch. 
                        // Stop looking further up in THIS block.
                        if (assignmentFoundInCurrentBlock)
                            break;
                    }
                }

                // If we didn't find an assignment in this block, queue up its predecessors
                if (!assignmentFoundInCurrentBlock)
                {
                    foreach (var branch in currentBlock.Predecessors)
                    {
                        if (branch.Source != null)
                        {
                            // Start at the very end of the predecessor block
                            queue.Enqueue((branch.Source, branch.Source.Operations.Length - 1));
                        }
                    }
                }
            }

            // 5. Evaluate results for inconclusiveness
            // If exactly one assignment was found across all paths, return it.
            // If 0 (uninitialized/parameter) or >1 (branching/inconclusive), return null.
            return foundAssignments.Count switch
            {
                0 => new LastAssignment(null, LastAssignment.MatchType.NotFound),
                1 => new LastAssignment(foundAssignments.First(), LastAssignment.MatchType.Found),
                _ => new LastAssignment(null, LastAssignment.MatchType.FoundAmbiguous)
            };
        }

        private static ControlFlowGraph? GetControlFlowGraph(
            MethodDeclarationSyntax body,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            var operation = semanticModel.GetOperation(
                (SyntaxNode)body,
                cancellationToken);

            if (operation is IMethodBodyOperation methodBody)
            {
                return ControlFlowGraph.Create(methodBody, cancellationToken);
            }
            if (operation is IConstructorBodyOperation constructorBody)
            {
                return ControlFlowGraph.Create(constructorBody, cancellationToken);
            }
            return null;
        }
        private static void FindStartingBlock(
            ControlFlowGraph cfg,
            SyntaxNode variableUsageNode,
            out BasicBlock? startBlock,
            out int usageIndex)
        {
            startBlock = null;
            usageIndex = -1;
            foreach (var block in cfg.Blocks)
            {
                // 1. Scan the main Operations array
                for (int i = 0; i < block.Operations.Length; i++)
                {
                    var topLevelOp = block.Operations[i];

                    // If the top-level statement's syntax span encompasses our variable's syntax span,
                    // then our variable usage is nested somewhere inside this statement.
                    if (topLevelOp.Syntax != null && topLevelOp.Syntax.Span.Contains(variableUsageNode.Span))
                    {
                        startBlock = block;
                        usageIndex = i;
                        break;
                    }
                }

                if (startBlock != null)
                    break; // Found it!

                // 2. Scan the BranchValue (e.g., the condition of an 'if', 'while', or 'return' statement)
                if (block.BranchValue != null &&
                    block.BranchValue.Syntax != null &&
                    block.BranchValue.Syntax.Span.Contains(variableUsageNode.Span))
                {
                    startBlock = block;
                    // Branch values are evaluated last in the block.
                    // So to look backwards from here, we start at the very end of the Operations array.
                    usageIndex = block.Operations.Length - 1;
                    break;
                }
            }
        }
        // Helper method to verify the assignment target matches our symbol
        private static bool IsTargetMatch(IAssignmentOperation assignment, ISymbol targetSymbol)
        {
            ISymbol? symbol = assignment.Target switch
            {
                ILocalReferenceOperation localRef => localRef.Local,
                IFieldReferenceOperation fieldRef => fieldRef.Field,
                IPropertyReferenceOperation propRef => propRef.Property,
                _ => null
            };
            if (symbol == null)
                return false;

            return SymbolEqualityComparer.Default.Equals(symbol, targetSymbol);

        }

    }
}

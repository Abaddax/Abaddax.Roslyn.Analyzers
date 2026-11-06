using Abaddax.Roslyn.Analyzers.Framework.Helper;
using Abaddax.Roslyn.Analyzers.Framework.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Abaddax.Roslyn.Analyzers.Framework.Extensions
{
    public static class ControlFlowGraphExtensions
    {
        public static IEnumerable<IOperation> ListAllOperations(this ControlFlowGraph cfg)
        {
            return cfg.Blocks
                    .SelectMany(x =>
                    {
                        if (x.BranchValue != null)
                            return x.Operations.Add(x.BranchValue);
                        return x.Operations;
                    })
                    .SelectMany(x => x.DescendantsAndSelf());
        }

        /// <summary>
        /// Tries to get the sub CFG of <see cref="ControlFlowGraph"> for the given <paramref name="localMethod"/>
        /// </summary>
        public static ControlFlowGraph? GetLocalFunctionCfg(this ControlFlowGraph cfg,
            IMethodSymbol localMethod,
            CancellationToken cancellationToken)
        {
            ControlFlowGraph? currentCfg = cfg;
            while (currentCfg != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Check if the current CFG scope is the one that declares this local function
                if (currentCfg.LocalFunctions.Any(f => SymbolEqualityComparer.Default.Equals(f, localMethod)))
                {
                    return currentCfg.GetLocalFunctionControlFlowGraph(localMethod, cancellationToken);
                }

                // Check if the current CFG scope is the one that contains the lambda function
                var flowAnonymous = currentCfg
                    .ListAllOperations()
                    .OfType<IFlowAnonymousFunctionOperation>()
                    .Where(x => SymbolEqualityComparer.Default.Equals(x.Symbol, localMethod))
                    .ExactlyOneOrDefault();
                if (flowAnonymous != null)
                {
                    return currentCfg.GetAnonymousFunctionControlFlowGraph(flowAnonymous, cancellationToken);
                }

                // Walk up to the parent scope
                currentCfg = currentCfg.Parent;
            }
            return null;
        }

        /// <summary>
        /// Finds the matching operation for <paramref name="operation"/> in the <paramref name="cfg"/>
        /// </summary>
        /// <remarks>
        /// Sometimes the operations in the <paramref name="cfg"/> and the operation retrieved via <see cref="SemanticModel.GetOperation(SyntaxNode, CancellationToken)"/> are not the same.
        /// This function resolved the exact operation-reference that is also present in the cfg
        /// </remarks>
        public static IOperation? FindMatchingOperation(this ControlFlowGraph cfg,
            IOperation operation)
        {
            var syntaxMatchCandidates = new List<IOperation>();
            foreach (var currentOperation in cfg.ListAllOperations())
            {
                if (OperationEqualityComparer.Default.Equals(currentOperation, operation))
                    return operation;
                if (OperationEqualityComparer.SyntaxEquality.Equals(currentOperation, operation))
                    syntaxMatchCandidates.Add(currentOperation);
            }
            return syntaxMatchCandidates.ExactlyOneOrDefault();
        }


        public static IEnumerable<BasicBlock> TraverseCfg(this ControlFlowGraph cfg,
            IReadOnlyDictionary<IParameterSymbol, ParameterValue>? knownArguments = null,
            CancellationToken cancellationToken = default)
        {
            // Traverse the CFG contextually
            var visited = new HashSet<BasicBlock>();
            var queue = new Queue<BasicBlock>();
            queue.Enqueue(cfg.Blocks[0]); // Start at the Entry block

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentBlock = queue.Dequeue();

                // If we reached the method exit, always yield
                if (currentBlock.Kind == BasicBlockKind.Exit)
                {
                    yield return currentBlock;
                    continue;
                }

                // Prevent infinite loops in `while`/`for` loops
                if (!visited.Add(currentBlock))
                    continue;

                if (!currentBlock.IsReachable)
                    continue;

                // Yield current block
                yield return currentBlock;

                //Enqueue catch and finally block
                if (currentBlock.EnclosingRegion.Kind == ControlFlowRegionKind.Try)
                {
                    foreach (var catchFinallyHandler in GetHandlerForRegion(currentBlock, cfg, cancellationToken))
                    {
                        if (!visited.Contains(catchFinallyHandler))
                            queue.Enqueue(catchFinallyHandler);
                    }
                }

                // Default non-exception branch processing
                if (currentBlock.ConditionalSuccessor != null)
                {
                    bool? conditionResult = EvaluateCondition(currentBlock.BranchValue, knownArguments);

                    // If ConditionKind is JumpIfTrue, and result is true, we take the ConditionalSuccessor.
                    // If result is unknown (null), we must traverse BOTH paths.
                    bool jumpWhen = currentBlock.ConditionKind == ControlFlowConditionKind.WhenTrue;

                    if (conditionResult == null)
                    {
                        // Unknown condition, traverse both
                        if (currentBlock.ConditionalSuccessor.Destination != null)
                            queue.Enqueue(currentBlock.ConditionalSuccessor.Destination);
                        if (currentBlock.FallThroughSuccessor?.Destination != null)
                            queue.Enqueue(currentBlock.FallThroughSuccessor.Destination);
                    }
                    else if (conditionResult == jumpWhen)
                    {
                        // Condition matches the jump requirement
                        if (currentBlock.ConditionalSuccessor.Destination != null)
                            queue.Enqueue(currentBlock.ConditionalSuccessor.Destination);
                    }
                    else
                    {
                        // Condition fails the jump requirement, fall through
                        if (currentBlock.FallThroughSuccessor?.Destination != null)
                            queue.Enqueue(currentBlock.FallThroughSuccessor.Destination);
                    }
                }
                else if (currentBlock.FallThroughSuccessor?.Destination != null)
                {
                    // Unconditional branch
                    queue.Enqueue(currentBlock.FallThroughSuccessor.Destination);
                    foreach (var finallyBlock in currentBlock.FallThroughSuccessor.FinallyRegions.SelectMany(region => region.GetBasicBlocks(cfg)).Distinct())
                    {
                        queue.Enqueue(finallyBlock);
                    }
                }
            }

            /// <summary>
            /// Tries to evaluate the <paramref name="condition"/> using the <paramref name="knownArguments"/> values
            /// </summary>
            /// <remarks>Only works for compiler constants</remarks>
            static bool? EvaluateCondition(
                IOperation? condition,
                IReadOnlyDictionary<IParameterSymbol, ParameterValue>? knownArguments)
            {
                if (condition == null)
                    return null;
                if (knownArguments == null)
                    return null;
                condition = condition.IgnoreCasts();

                //if (true)
                if (condition.ConstantValue.HasValue &&
                    condition.ConstantValue.Value is bool literalBool)
                {
                    return literalBool;
                }

                //if (x)
                if (condition is IParameterReferenceOperation paramRef &&
                    paramRef.Parameter != null)
                {
                    if (knownArguments.TryGetValue(paramRef.Parameter, out var parameterValue) &&
                        parameterValue.ConstValue.HasValue &&
                        parameterValue.ConstValue.Value is bool boolVal)
                    {
                        return boolVal;
                    }
                }
                //if (!x)
                if (condition is IUnaryOperation unaryOp &&
                    unaryOp.OperatorKind == UnaryOperatorKind.Not)
                {
                    bool? operandResult = EvaluateCondition(unaryOp.Operand, knownArguments);
                    if (operandResult.HasValue)
                        return !operandResult.Value;
                }

                //if (x == true)
                if (condition is IBinaryOperation binaryOp &&
                    binaryOp.OperatorKind is BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals)
                {
                    if (TryEvaluateValue(binaryOp.LeftOperand, knownArguments, out object? left) &&
                        TryEvaluateValue(binaryOp.RightOperand, knownArguments, out object? right))
                    {
                        bool areEqual = object.Equals(left, right);
                        return binaryOp.OperatorKind == BinaryOperatorKind.Equals
                            ? areEqual :
                            !areEqual;
                    }
                }

                return null;

                static bool TryEvaluateValue(IOperation? op, IReadOnlyDictionary<IParameterSymbol, ParameterValue> knownArguments, out object? value)
                {
                    value = null;
                    if (op == null)
                        return false;
                    op = op.IgnoreCasts();

                    // Is it a known compile-time constant? (Covers literals, const fields, and const locals)
                    if (op.ConstantValue.HasValue)
                    {
                        value = op.ConstantValue.Value;
                        return true;
                    }

                    // Is it a parameter we mapped from the caller?
                    if (op is IParameterReferenceOperation paramRef && paramRef.Parameter != null)
                    {
                        if (!knownArguments.TryGetValue(paramRef.Parameter, out var parameterValue))
                            return false;
                        if (!parameterValue.ConstValue.HasValue)
                            return false;
                        value = parameterValue.ConstValue.Value;
                        return true;
                    }

                    return false; // Unknown value
                }
            }

            /// <summary>
            /// Get possible catch/finally blocks for the current block (if inside a try/catch/finally)
            /// </summary>
            static IEnumerable<BasicBlock> GetHandlerForRegion(
                BasicBlock basicBlock,
                ControlFlowGraph cfg,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var catchFinallyBlock in basicBlock.GetTryCatchFinallyBlocks(cfg, includeCatch: true, includeFinally: true))
                {
                    yield return catchFinallyBlock;
                }
            }
        }


        public static IEnumerable<IOperation> GetOperations(this BasicBlock basicBlock)
        {
            foreach (var operation in basicBlock.Operations)
                yield return operation;
            if (basicBlock.BranchValue != null)
                yield return basicBlock.BranchValue;
        }
        public static IEnumerable<BasicBlock> GetPredecessors(this BasicBlock basicBlock, ControlFlowGraph cfg)
        {
            foreach (var predecessor in basicBlock.Predecessors)
            {
                bool anyFinally = false;
                foreach (var finallyBlock in predecessor.FinallyRegions.SelectMany(region => region.GetBasicBlocks(cfg)))
                {
                    anyFinally = true;
                    yield return finallyBlock;
                }
                if (!anyFinally)
                    yield return predecessor.Source;
            }
            if (basicBlock.Predecessors.Length == 0)
            {
                switch (basicBlock.EnclosingRegion.Kind)
                {
                    case ControlFlowRegionKind.Catch:
                    {
                        foreach (var tryBlock in basicBlock.GetTryCatchFinallyBlocks(cfg, includeTry: true))
                            yield return tryBlock;
                        yield break;
                    }
                    case ControlFlowRegionKind.Finally:
                    {
                        foreach (var tryCatchBlock in basicBlock.GetTryCatchFinallyBlocks(cfg, includeTry: true, includeCatch: true))
                            yield return tryCatchBlock;
                        yield break;
                    }
                    default:
                        yield break;
                }
            }
        }

        private static IEnumerable<BasicBlock> GetBasicBlocks(this ControlFlowRegion? region, ControlFlowGraph cfg)
        {
            if (region == null)
                yield break;
            for (int blockOrdinal = region.FirstBlockOrdinal; blockOrdinal <= region.LastBlockOrdinal; blockOrdinal++)
                yield return cfg.Blocks[blockOrdinal];
        }

        private static void GetTryCatchFinallyRegion(this ControlFlowRegion region,
            out ControlFlowRegion? tryRegion,
            out ControlFlowRegion[] catchRegions,
            out ControlFlowRegion? finallyRegion)
        {
            switch (region.Kind)
            {
                case ControlFlowRegionKind.Try:
                case ControlFlowRegionKind.Catch:
                case ControlFlowRegionKind.Finally:
                {
                    //We are either in a try/catch or a try/finally
                    if (region.EnclosingRegion == null)
                        goto default;
                    region.EnclosingRegion.GetTryCatchFinallyRegion(
                        out tryRegion,
                        out catchRegions,
                        out finallyRegion);
                    return;
                }
                case ControlFlowRegionKind.TryAndCatch:
                {
                    if (region.EnclosingRegion?.Kind == ControlFlowRegionKind.Try &&
                        region.EnclosingRegion.EnclosingRegion?.Kind == ControlFlowRegionKind.TryAndFinally)
                    {
                        region.EnclosingRegion.EnclosingRegion.GetTryCatchFinallyRegion(
                            out tryRegion,
                            out catchRegions,
                            out finallyRegion);
                    }
                    else
                    {
                        tryRegion = GetTryRegion(region.NestedRegions);
                        catchRegions = GetCatchRegions(region.NestedRegions).ToArray();
                        finallyRegion = null;
                    }
                    return;
                }
                case ControlFlowRegionKind.TryAndFinally:
                {
                    finallyRegion = GetFinallyRegion(region.NestedRegions);
                    tryRegion = GetTryRegion(region.NestedRegions);
                    if (tryRegion.NestedRegions.Length == 1 && tryRegion.NestedRegions[0].Kind == ControlFlowRegionKind.TryAndCatch)
                    {
                        var tryCatchRegion = tryRegion.NestedRegions[0];
                        tryRegion = GetTryRegion(tryCatchRegion.NestedRegions);
                        catchRegions = GetCatchRegions(tryCatchRegion.NestedRegions).ToArray();
                        return;
                    }
                    catchRegions = [];
                    return;
                }
                default:
                {
                    tryRegion = null;
                    catchRegions = [];
                    finallyRegion = null;
                    return;
                }
            }
            static ControlFlowRegion GetTryRegion(IEnumerable<ControlFlowRegion> nestedRegions)
            {
                return nestedRegions.FirstOrDefault(x => x.Kind == ControlFlowRegionKind.Try) ?? throw new Exception("Failed to find 'try'-region");
            }
            static IEnumerable<ControlFlowRegion> GetCatchRegions(IEnumerable<ControlFlowRegion> nestedRegions)
            {
                bool any = false;
                foreach (var nestedRegion in nestedRegions)
                {
                    if (nestedRegion.Kind == ControlFlowRegionKind.Catch)
                    {
                        any = true;
                        yield return nestedRegion;
                    }
                    else if (nestedRegion.Kind == ControlFlowRegionKind.FilterAndHandler)
                    {
                        var catchRegions = nestedRegion.NestedRegions.Where(x => x.Kind == ControlFlowRegionKind.Catch);
                        foreach (var catchRegion in catchRegions)
                        {
                            any = true;
                            yield return catchRegion;
                        }
                    }
                }
                if (!any)
                    throw new Exception("Failed to find 'catch'-region");
            }
            static ControlFlowRegion? GetFinallyRegion(IEnumerable<ControlFlowRegion> nestedRegions)
            {
                return nestedRegions.FirstOrDefault(x => x.Kind == ControlFlowRegionKind.Finally) ?? throw new Exception("Failed to find 'finally'-region");
            }
        }
        private static IEnumerable<BasicBlock> GetTryCatchFinallyBlocks(this BasicBlock basicBlock, ControlFlowGraph cfg,
            bool includeTry = false,
            bool includeCatch = false,
            bool includeFinally = false)
        {
            basicBlock.EnclosingRegion.GetTryCatchFinallyRegion(
                out var tryRegion,
                out var catchRegions,
                out var finallyRegion);
            if (tryRegion == null)
                yield break;
            if (includeTry && tryRegion != null)
            {
                foreach (var block in tryRegion.GetBasicBlocks(cfg))
                {
                    yield return block;
                }
            }
            if (includeCatch && catchRegions.Length > 0)
            {
                foreach (var block in catchRegions.SelectMany(region => region.GetBasicBlocks(cfg)).Distinct())
                {
                    yield return block;
                }
            }
            if (includeFinally && finallyRegion != null)
            {
                foreach (var block in finallyRegion.GetBasicBlocks(cfg))
                {
                    yield return block;
                }
            }
        }
    }
}

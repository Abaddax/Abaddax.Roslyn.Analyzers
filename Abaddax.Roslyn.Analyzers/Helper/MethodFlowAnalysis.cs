using Abaddax.Roslyn.Analyzers.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Abaddax.Roslyn.Analyzers.Helper
{
    internal static class MethodFlowAnalysis
    {
        private const int _MaxCallStackDepth = 10;

        public static IEnumerable<IOperation> GetPossibleReturnValues(
            IInvocationOperation invocation,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            var callStack = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            return GetPossibleReturnValuesInternal(
                invocation,
                semanticModel,
                cfg: null,
                outerArguments: null,
                callStack: callStack,
                maxCallStackDepth: _MaxCallStackDepth,
                cancellationToken);
        }

        public static IEnumerable<IOperation> GetPossibleOutParameterValues(
            IInvocationOperation invocation,
            IArgumentOperation outArgument,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            var callStack = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            return GetPossibleOutParameterValuesInternal(
                invocation,
                outArgument,
                semanticModel,
                cfg: null,
                outerArguments: null,
                callStack: callStack,
                maxCallStackDepth: _MaxCallStackDepth,
                cancellationToken);
        }

        public static IEnumerable<IOperation> GetPossibleLastAssignment(
            IOperation variable,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            var callStack = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            return GetPossibleLastAssignmentInternal(
                variable,
                semanticModel,
                cfg: null,
                outerArguments: null,
                callStack: callStack,
                maxCallStackDepth: _MaxCallStackDepth,
                cancellationToken);
        }

        public static IOperation TraverseAssignments(
            IOperation operation,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            var callStack = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            var callStackInfo = new Stack<TraversalStackInfo>();
            return TraverseAssignmentsInternal(
                operation,
                semanticModel,
                cfg: null,
                outerArguments: null,
                callStack: callStack,
                callStackInfo: callStackInfo,
                maxCallStackDepth: _MaxCallStackDepth,
                cancellationToken);
        }

        #region Internal
        private readonly struct ParameterValue
        {
            public IOperation Parameter { get; }
            public Optional<object?> ConstValue => Parameter.ConstantValue;

            public ParameterValue(IOperation parameterValue)
            {
                Parameter = parameterValue ?? throw new ArgumentNullException(nameof(parameterValue));
            }
        }
        private readonly struct TraversalStackInfo
        {
            public SyntaxNode MethodSyntax { get; }
            public IMethodSymbol Method { get; }
            public SemanticModel PreviousSemanticModel { get; }
            public IReadOnlyDictionary<IParameterSymbol, ParameterValue>? PreviousParameters { get; }
            public ControlFlowGraph? PreviousControlFlowGraph { get; }

            public TraversalStackInfo(
                SyntaxNode methodSyntax,
                IMethodSymbol method,
                SemanticModel previousSemanticModel,
                IReadOnlyDictionary<IParameterSymbol, ParameterValue>? previousParameters,
                ControlFlowGraph? previousControlFlowGraph)
            {
                MethodSyntax = methodSyntax ?? throw new ArgumentNullException(nameof(methodSyntax));
                Method = method ?? throw new ArgumentNullException(nameof(method));
                PreviousSemanticModel = previousSemanticModel ?? throw new ArgumentNullException(nameof(previousSemanticModel));
                PreviousParameters = previousParameters;
                PreviousControlFlowGraph = previousControlFlowGraph;
            }
        }

        /// <summary>
        /// Internal resolver for <see cref="GetPossibleReturnValues(IInvocationOperation, SemanticModel, CancellationToken)"/>
        /// </summary>
        /// <remarks>Also traversed local function</remarks>
        private static IEnumerable<IOperation> GetPossibleReturnValuesInternal(
            IInvocationOperation invocation,
            SemanticModel semanticModel,
            ControlFlowGraph? cfg,
            IReadOnlyDictionary<IParameterSymbol, ParameterValue>? outerArguments,
            HashSet<IMethodSymbol> callStack,
            ushort maxCallStackDepth,
            CancellationToken cancellationToken)
        {
            IMethodSymbol targetMethod = invocation.TargetMethod;

            // 1. We can only analyze methods where we have the source code.
            var methodSyntax = GetMethodSyntaxNode(invocation, outerArguments, cancellationToken);
            if (methodSyntax == null)
                yield break;
            if (!semanticModel.HasSameSyntaxTree(methodSyntax))
                semanticModel = semanticModel.GetSemanticModelFor(methodSyntax);

            // 2. Add to callstack
            if (callStack.Count > maxCallStackDepth || !callStack.Add(targetMethod))
                yield break;
            try
            {
                // 3. Inherit variables from outer scope (crucial for local functions capturing variables)
                // And map known argument constants to the method parameters
                var knownArguments = GetArguments(invocation, semanticModel, outerArguments);

                // 4. Get the Operation Tree and CFG for the target method
                var methodOperation = semanticModel.GetOperation(methodSyntax, cancellationToken);
                if (methodOperation == null)
                    yield break;
                cfg ??= methodOperation.GetControlFlowGraph(cancellationToken);
                if (cfg == null || cfg.Blocks.IsDefaultOrEmpty)
                    yield break;

                // 5. Traverse the CFG contextually
                var visited = new HashSet<BasicBlock>();
                var queue = new Queue<BasicBlock>();
                queue.Enqueue(cfg.Blocks[0]); // Start at the Entry block

                while (queue.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var currentBlock = queue.Dequeue();

                    // Prevent infinite loops in `while`/`for` loops
                    if (!visited.Add(currentBlock))
                        continue;

                    // Check for explicit Return operations inside the block's body
                    foreach (var op in currentBlock.Operations)
                    {
                        if (op is IReturnOperation returnOp && returnOp.ReturnedValue != null)
                        {
                            foreach (var res in ResolveReturnedValue(returnOp.ReturnedValue, semanticModel, cfg, knownArguments, callStack, maxCallStackDepth, cancellationToken))
                                yield return res;
                        }
                    }

                    // Check if the branch itself is a return (common in Roslyn CFGs)
                    if (currentBlock.FallThroughSuccessor?.Semantics == ControlFlowBranchSemantics.Return &&
                        currentBlock.BranchValue != null)
                    {
                        foreach (var res in ResolveReturnedValue(currentBlock.BranchValue, semanticModel, cfg, knownArguments, callStack, maxCallStackDepth, cancellationToken))
                            yield return res;
                    }

                    // 6. Evaluate branching based on our known arguments
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
                    }
                }
            }
            finally
            {
                // Done, remove from callstack
                callStack.Remove(targetMethod);
            }
        }
        /// <summary>
        /// Internal resolver for <see cref="GetPossibleOutParameterValues(IInvocationOperation, IArgumentOperation, SemanticModel, CancellationToken)"/>
        /// </summary>
        /// <remarks>Also traversed local function</remarks>
        private static IEnumerable<IOperation> GetPossibleOutParameterValuesInternal(
            IInvocationOperation invocation,
            IArgumentOperation outArgument,
            SemanticModel semanticModel,
            ControlFlowGraph? cfg,
            IReadOnlyDictionary<IParameterSymbol, ParameterValue>? outerArguments,
            HashSet<IMethodSymbol> callStack,
            ushort maxCallStackDepth,
            CancellationToken cancellationToken)
        {
            var targetMethod = invocation.TargetMethod;
            var targetParameter = outArgument.Parameter;
            if (targetParameter == null)
                yield break;

            // 1. We can only analyze methods where we have the source code.
            var methodSyntax = GetMethodSyntaxNode(invocation, outerArguments, cancellationToken);
            if (methodSyntax == null)
                yield break;
            if (!semanticModel.HasSameSyntaxTree(methodSyntax))
                semanticModel = semanticModel.GetSemanticModelFor(methodSyntax);

            // 2. Add to callstack
            if (callStack.Count > maxCallStackDepth || !callStack.Add(targetMethod))
                yield break;
            try
            {
                // 3. Inherit variables from outer scope (crucial for local functions capturing variables)
                // And map known argument constants to the method parameters
                var knownArguments = GetArguments(invocation, semanticModel, outerArguments);

                // 4. Get the Operation Tree and CFG for the target method
                var methodOperation = semanticModel.GetOperation(methodSyntax, cancellationToken);
                if (methodOperation == null)
                    yield break;
                cfg ??= methodOperation.GetControlFlowGraph(cancellationToken);
                if (cfg == null || cfg.Blocks.IsDefaultOrEmpty)
                    yield break;

                // 5. Traverse the CFG contextually
                // Track visited states. We track BOTH the block and the current operation 
                // to prevent infinite loops while still allowing different paths to reach 
                // the same block with different assigned out-values.
                var visited = new HashSet<BasicBlock>();
                // Queue tracks the block AND the last assigned value to our out parameter on this path
                var queue = new Queue<(BasicBlock Block, IOperation? CurrentOutValue)>();
                queue.Enqueue((cfg.Blocks[0], null)); // Start at the Entry block

                while (queue.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var (currentBlock, currentOutValue) = queue.Dequeue();

                    // If we reached the method exit, yield the last assigned value for this path
                    if (currentBlock.Kind == BasicBlockKind.Exit)
                    {
                        if (currentOutValue != null)
                        {
                            foreach (var result in ResolveReturnedValue(currentOutValue, semanticModel, cfg, knownArguments, callStack, maxCallStackDepth, cancellationToken))
                                yield return result;
                        }
                        continue;
                    }

                    // Prevent infinite loops in `while`/`for` loops
                    if (!visited.Add(currentBlock))
                        continue;

                    // Check for assignments to our target out-parameter in this block
                    foreach (IOperation op in currentBlock.Operations)
                    {
                        // Unwrap the assignment if it's inside a statement
                        var assignment = op.FindAssignmentTarget(targetParameter);
                        if (assignment?.Target is IParameterReferenceOperation paramRef &&
                            SymbolEqualityComparer.Default.Equals(paramRef.Parameter, targetParameter))
                        {
                            // Update our tracked value for this path
                            currentOutValue = assignment.Value.Value;
                        }
                    }

                    // 6. Evaluate branching based on our known arguments
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
                                queue.Enqueue((currentBlock.ConditionalSuccessor.Destination, currentOutValue));
                            if (currentBlock.FallThroughSuccessor?.Destination != null)
                                queue.Enqueue((currentBlock.FallThroughSuccessor.Destination, currentOutValue));
                        }
                        else if (conditionResult == jumpWhen)
                        {
                            // Condition matches the jump requirement
                            if (currentBlock.ConditionalSuccessor.Destination != null)
                                queue.Enqueue((currentBlock.ConditionalSuccessor.Destination, currentOutValue));
                        }
                        else
                        {
                            // Condition fails the jump requirement, fall through
                            if (currentBlock.FallThroughSuccessor?.Destination != null)
                                queue.Enqueue((currentBlock.FallThroughSuccessor.Destination, currentOutValue));
                        }
                    }
                    else if (currentBlock.FallThroughSuccessor?.Destination != null)
                    {
                        // Unconditional branch
                        queue.Enqueue((currentBlock.FallThroughSuccessor.Destination, currentOutValue));
                    }
                }
            }
            finally
            {
                // Done, remove from callstack
                callStack.Remove(targetMethod);
            }
        }
        /// <summary>
        /// Internal resolver for <see cref="GetPossibleLastAssignment(IOperation, SemanticModel, CancellationToken)"/>
        /// </summary>
        /// <remarks>Also traversed local function</remarks>
        private static IEnumerable<IOperation> GetPossibleLastAssignmentInternal(
            IOperation variable,
            SemanticModel semanticModel,
            ControlFlowGraph? cfg,
            IReadOnlyDictionary<IParameterSymbol, ParameterValue>? outerArguments,
            HashSet<IMethodSymbol> callStack,
            ushort maxCallStackDepth,
            CancellationToken cancellationToken)
        {
            if (variable.Syntax is not ExpressionSyntax expression)
                yield break;
            expression = expression.IgnoreCasts().IgnoreNullSuppression();

            // 1.  We can only analyze methods where we have the source code.
            var methodBody = expression.GetContainingMethodDeclarationBlock();
            if (methodBody == null)
                yield break;
            if (!semanticModel.HasSameSyntaxTree(methodBody))
                semanticModel = semanticModel.GetSemanticModelFor(methodBody);

            // 2. Get symbol to check for assignments
            var targetSymbol = semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol;
            if (targetSymbol == null)
                yield break;

            // 3. Get the Operation Tree and CFG for the target method
            var methodOperation = semanticModel.GetOperation(methodBody, cancellationToken);
            if (methodOperation == null)
                yield break;
            cfg ??= methodOperation.GetControlFlowGraph(cancellationToken);
            if (cfg == null || cfg.Blocks.IsDefaultOrEmpty)
                yield break;

            // 4. Locate the starting BasicBlock and our index within it
            FindStartingBlock(cfg, variable.Syntax,
                out var startBlock,
                out var usageIndex);
            // 4.1 Did not find index, so we scan the entire method
            if (startBlock == null || usageIndex == null)
            {
                startBlock = cfg.Blocks.Last();
                usageIndex = startBlock.Operations.Length - 1;
            }
            //if (startBlock == null || usageIndex == null)
            //    yield break;

            // 5. Traverse backwards
            var visited = new HashSet<BasicBlock>();
            var queue = new Queue<(BasicBlock Block, int StartIndex)>();
            queue.Enqueue((startBlock, usageIndex.Value));

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (currentBlock, startIndex) = queue.Dequeue();

                // Prevent infinite loops in `while`/`for` loops
                if (!visited.Add(currentBlock))
                    continue;

                bool assignmentFoundInCurrentBlock = false;

                // Walk backwards through the current block's operations
                if (currentBlock.IsReachable)
                {
                    for (int i = startIndex; i >= 0; i--)
                    {
                        var topLevelOp = currentBlock.Operations[i];
                        var nestedAssignments = topLevelOp.DescendantsAndSelf()
                            .Reverse()
                            .Select(x => (Operation: x, Assignment: x.FindAssignmentTarget(targetSymbol)))
                            .Select(x =>
                            {
                                if (x.Assignment != null)
                                    return x;

                                // Special checks for properties/fields
                                if (targetSymbol is IPropertySymbol or IFieldSymbol)
                                {
                                    // Block for instance method invocations, as they could protentially alter the property
                                    if (x.Operation is IInstanceReferenceOperation { Parent: IInvocationOperation } instanceInvocation &&
                                        (instanceInvocation.Type?.IsDerivedFrom(targetSymbol.ContainingType) ?? false))
                                    {
                                        // Block via empty assignment
                                        return (x.Operation, (null, null));
                                    }
                                    // Block for calls that pass 'this' parameter, as they could protentially alter a public property
                                    if (targetSymbol.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedAndInternal &&
                                        x.Operation is IInvocationOperation thisPassedInvocation &&
                                        thisPassedInvocation.Arguments
                                            .Where(x => x.Value.Type?.IsDerivedFrom(targetSymbol.ContainingType) ?? false)
                                            .Any(x => x.Value is IInstanceReferenceOperation))
                                    {
                                        // Block via empty assignment
                                        return (x.Operation, (null, null));
                                    }
                                }

                                // Also check for local functions, as they might also alter the target without explicit assignments
                                if (x.Operation is IInvocationOperation invocation &&
                                    invocation.TargetMethod.MethodKind is MethodKind.LocalFunction or MethodKind.LambdaMethod or MethodKind.DelegateInvoke)
                                {
                                    // 1. Get CFG for the target method
                                    var localCfg = cfg.GetLocalFunctionCfg(invocation.TargetMethod, cancellationToken);
                                    if (localCfg == null)
                                        return x;

                                    // 2. Add to callstack
                                    if (callStack.Count > maxCallStackDepth || !callStack.Add(invocation.TargetMethod))
                                        return x;
                                    try
                                    {
                                        var possibledAssignments = GetPossibleLastAssignmentInternal(variable, semanticModel, localCfg, outerArguments, callStack, maxCallStackDepth, cancellationToken);
                                        var possibledAssignment = possibledAssignments.ExactlyOneOrDefault();
                                        if (possibledAssignment == null)
                                            return x;
                                        return (x.Operation, (possibledAssignment, possibledAssignment));
                                    }
                                    finally
                                    {
                                        callStack.Remove(invocation.TargetMethod);
                                    }
                                }
                                return x;
                            })
                            .Where(x => x.Assignment != null)
                            .Select(x => x.Assignment!.Value.Value);
                        foreach (var assignment in nestedAssignments)
                        {
                            foreach (var res in ResolveReturnedValue(assignment, semanticModel, cfg, outerArguments, callStack, maxCallStackDepth, cancellationToken))
                                yield return res;
                            assignmentFoundInCurrentBlock = true;
                            break; // Stop looking at earlier assignments in this statement
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
        }
        /// <summary>
        /// Internal resolver for <see cref= TraverseAssignments(IOperation, SemanticModel, CancellationToken)"/>
        /// </summary>
        private static IOperation TraverseAssignmentsInternal(
            IOperation operation,
            SemanticModel semanticModel,
            ControlFlowGraph? cfg,
            IReadOnlyDictionary<IParameterSymbol, ParameterValue>? outerArguments,
            HashSet<IMethodSymbol> callStack,
            Stack<TraversalStackInfo> callStackInfo,
            ushort maxCallStackDepth,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 1. Check if current call traversal is finished
            while (callStackInfo.Count > 0)
            {
                var previousCallStackInfo = callStackInfo.Peek();
                if (previousCallStackInfo.MethodSyntax.Contains(operation.Syntax))
                    break;

                callStack.Remove(previousCallStackInfo.Method);
                semanticModel = previousCallStackInfo.PreviousSemanticModel;
                outerArguments = previousCallStackInfo.PreviousParameters;
                cfg = previousCallStackInfo.PreviousControlFlowGraph;
                callStackInfo.Pop();
            }

            // 2. Step into invocations
            if (operation is IInvocationOperation invocation)
            {
                // 1. We can only analyze methods where we have the source code.
                var methodSyntax = GetMethodSyntaxNode(invocation, outerArguments, cancellationToken);
                if (methodSyntax == null)
                    return operation;

                // 2. Check for possible return values
                var lastAssignments = GetPossibleReturnValuesInternal(invocation, semanticModel, null, outerArguments, callStack, maxCallStackDepth, cancellationToken);
                var lastAssignment = lastAssignments.ExactlyOneOrDefault();
                if (lastAssignment == null)
                    return operation;

                // 3. Check if last assignment is inside the invocation method
                if (methodSyntax.Contains(lastAssignment.Syntax))
                {
                    var currentCallStackInfo = new TraversalStackInfo(methodSyntax, invocation.TargetMethod, semanticModel, outerArguments, cfg);

                    if (!semanticModel.HasSameSyntaxTree(methodSyntax))
                        semanticModel = semanticModel.GetSemanticModelFor(methodSyntax);

                    // 4. Inherit variables from outer scope (crucial for local functions capturing variables)
                    // And map known argument constants to the method parameters
                    outerArguments = GetArguments(invocation, semanticModel, outerArguments);

                    // 5. Get the Operation Tree and CFG for the target method
                    var methodOperation = semanticModel.GetOperation(methodSyntax, cancellationToken);
                    if (methodOperation == null)
                        return operation;
                    cfg = methodOperation.GetControlFlowGraph(cancellationToken);
                    if (cfg == null || cfg.Blocks.IsDefaultOrEmpty)
                        return operation;

                    // 6. Add to callstack 
                    if (callStack.Count > maxCallStackDepth || !callStack.Add(currentCallStackInfo.Method))
                        return operation;
                    callStackInfo.Push(currentCallStackInfo);
                }

                // 7. Traverse function call with all the currently known parameters
                return TraverseAssignmentsInternal(lastAssignment, semanticModel, cfg, outerArguments, callStack, callStackInfo, maxCallStackDepth, cancellationToken);
            }
            else if (operation.Parent is IArgumentOperation argument &&
                argument.Parameter?.RefKind is RefKind.Out or RefKind.Ref &&
                argument.Parent is IInvocationOperation outInvocation)
            {
                // 1. We can only analyze methods where we have the source code.
                var methodSyntax = GetMethodSyntaxNode(outInvocation, outerArguments, cancellationToken);
                if (methodSyntax == null)
                    return operation;

                // 2. Check for possible out values
                var lastAssignments = GetPossibleOutParameterValuesInternal(outInvocation, argument, semanticModel, cfg, outerArguments, callStack, maxCallStackDepth, cancellationToken);
                var lastAssignment = lastAssignments.ExactlyOneOrDefault();
                if (lastAssignment == null)
                    return operation;

                // 3. Check if last assignment is inside the invocation method
                if (methodSyntax.Contains(lastAssignment.Syntax))
                {
                    var currentCallStackInfo = new TraversalStackInfo(methodSyntax, outInvocation.TargetMethod, semanticModel, outerArguments, cfg);

                    if (!semanticModel.HasSameSyntaxTree(methodSyntax))
                        semanticModel = semanticModel.GetSemanticModelFor(methodSyntax);

                    // 4. Inherit variables from outer scope (crucial for local functions capturing variables)
                    // And map known argument constants to the method parameters
                    outerArguments = GetArguments(outInvocation, semanticModel, outerArguments);

                    // 5. Get the Operation Tree and CFG for the target method
                    var methodOperation = semanticModel.GetOperation(methodSyntax, cancellationToken);
                    if (methodOperation == null)
                        return operation;
                    cfg = methodOperation.GetControlFlowGraph(cancellationToken);
                    if (cfg == null || cfg.Blocks.IsDefaultOrEmpty)
                        return operation;

                    // 6. Add to callstack 
                    if (callStack.Count > maxCallStackDepth || !callStack.Add(currentCallStackInfo.Method))
                        return operation;
                    callStackInfo.Push(currentCallStackInfo);
                }

                // 7. Traverse function call with all the currently known parameters
                return TraverseAssignmentsInternal(lastAssignment, semanticModel, cfg, outerArguments, callStack, callStackInfo, maxCallStackDepth, cancellationToken);
            }
            else
            {
                var lastAssignments = GetPossibleLastAssignmentInternal(operation, semanticModel, cfg, outerArguments, callStack, maxCallStackDepth, cancellationToken);
                var lastAssignment = lastAssignments.ExactlyOneOrDefault();
                if (lastAssignment == null)
                {
                    // We hit an end, possibly a parameter reference? If so check if we can move up into the caller (if we are currenly inside a local function/delegate)
                    if (operation is not IParameterReferenceOperation parameterReference)
                        return operation;
                    var possibleCallingOperations = GetCallingOperations(parameterReference, semanticModel, cancellationToken);
                    var possibleCallingOperation = possibleCallingOperations.ExactlyOneOrDefault();
                    if (possibleCallingOperation.CallerParamRef == null)
                        return operation;

                    // Check if we are inside the caller body
                    if (possibleCallingOperation.ParentInvocation is IInvocationOperation parentInvocation)
                    {
                        // 1. We can only analyze methods where we have the source code.
                        var methodSyntax = GetMethodSyntaxNode(parentInvocation, outerArguments, cancellationToken);
                        if (methodSyntax == null)
                            return operation;

                        var currentCallStackInfo = new TraversalStackInfo(methodSyntax, parentInvocation.TargetMethod, semanticModel, outerArguments, cfg);

                        if (!semanticModel.HasSameSyntaxTree(methodSyntax))
                            semanticModel = semanticModel.GetSemanticModelFor(methodSyntax);

                        // 2 Inherit variables from outer scope (crucial for local functions capturing variables)
                        // And map known argument constants to the method parameters
                        outerArguments = GetArguments(parentInvocation, semanticModel, outerArguments);

                        // 3. Add to callstack 
                        if (callStack.Count > maxCallStackDepth || !callStack.Add(currentCallStackInfo.Method))
                            return operation;
                        callStackInfo.Push(currentCallStackInfo);
                    }

                    lastAssignment = possibleCallingOperation.CallerParamRef;
                }
                return TraverseAssignmentsInternal(lastAssignment, semanticModel, cfg, outerArguments, callStack, callStackInfo, maxCallStackDepth, cancellationToken);
            }
        }


        /// <summary>
        /// Checks the returned <paramref name="op"/> for possible local function usage.
        /// Will traverse local function if found
        /// </summary>
        private static IEnumerable<IOperation> ResolveReturnedValue(
            IOperation? op,
            SemanticModel semanticModel,
            ControlFlowGraph currentCfg,
            IReadOnlyDictionary<IParameterSymbol, ParameterValue>? currentArguments,
            HashSet<IMethodSymbol> callStack,
            ushort maxCallStackDepth,
            CancellationToken cancellationToken)
        {
            if (op == null)
                yield break;

            // Return of a known argument?
            {
                if (currentArguments != null &&
                    op.IgnoreCasts() is IParameterReferenceOperation parameter &&
                    parameter.Parameter.RefKind is not RefKind.Out and not RefKind.Ref &&
                    currentArguments.TryGetValue(parameter.Parameter, out var parameterValue))
                {
                    foreach (var result in ResolveReturnedValue(parameterValue.Parameter, semanticModel, currentCfg, currentArguments, callStack, maxCallStackDepth, cancellationToken))
                        yield return result;
                    yield break; // We successfully stepped in, bail out here.
                }
            }

            // Return is set via a local function?
            {
                if (op.IgnoreCasts() is IInvocationOperation invocation &&
                    invocation.TargetMethod.MethodKind is MethodKind.LocalFunction or MethodKind.LambdaMethod or MethodKind.DelegateInvoke)
                {
                    // Find the local function's CFG starting from our current CFG
                    var localCfg = currentCfg.GetLocalFunctionCfg(invocation.TargetMethod, cancellationToken);
                    if (localCfg != null)
                    {
                        foreach (var result in GetPossibleReturnValuesInternal(invocation, semanticModel, localCfg, currentArguments, callStack, maxCallStackDepth, cancellationToken))
                            yield return result;
                        yield break; // We successfully stepped in, bail out here.
                    }
                }
            }
            // Parameter is set via a local function out parameter?
            {
                if (op.IgnoreCasts() is IParameterReferenceOperation parameter &&
                    parameter.Parameter.RefKind is RefKind.Out or RefKind.Ref &&
                    parameter.Parent is IArgumentOperation argument &&
                    argument.Parent is IInvocationOperation invocation &&
                    invocation.TargetMethod.MethodKind is MethodKind.LocalFunction or MethodKind.LambdaMethod or MethodKind.DelegateInvoke)
                {
                    // Find the local function's CFG starting from our current CFG
                    var localCfg = currentCfg.GetLocalFunctionCfg(invocation.TargetMethod, cancellationToken);
                    if (localCfg != null)
                    {
                        foreach (var result in GetPossibleOutParameterValuesInternal(invocation, argument, semanticModel, localCfg, currentArguments, callStack, maxCallStackDepth, cancellationToken))
                            yield return result;
                        yield break; // We successfully stepped in, bail out here.
                    }
                }
            }
            yield return op;
        }

        /// <summary>
        /// Tries to get the <see cref="SyntaxNode"/> the given <paramref name="invocation"/> body
        /// </summary>
        private static SyntaxNode? GetMethodSyntaxNode(
            IInvocationOperation invocation,
            IReadOnlyDictionary<IParameterSymbol, ParameterValue>? currentArguments,
            CancellationToken cancellationToken)
        {
            // 1. Normal invocation
            if (!invocation.TargetMethod.DeclaringSyntaxReferences.IsDefaultOrEmpty)
            {
                var syntaxRef = invocation.TargetMethod.DeclaringSyntaxReferences.ExactlyOneOrDefault();
                if (syntaxRef == null)
                    return null;
                return syntaxRef.GetSyntax(cancellationToken);
            }

            // 2. A delegate invocation has the delegate variable as its 'Instance' 
            var delegateInstance = invocation.Instance?.IgnoreCasts();
            if (delegateInstance == null)
                return null;

            // 3. Is the delegate referencing a parameter?
            if (delegateInstance is IParameterReferenceOperation paramRef &&
                paramRef.Parameter != null)
            {
                if (currentArguments == null)
                    return null;
                if (!currentArguments.TryGetValue(paramRef.Parameter, out var parameterValue))
                    return null;
                delegateInstance = parameterValue.Parameter.IgnoreCasts();
            }

            // 4. Check delegate creation
            if (delegateInstance is IDelegateCreationOperation delegateCreation)
            {
                delegateInstance = delegateCreation.Target;
            }

            // 5. Get lambda syntax
            if (delegateInstance is IAnonymousFunctionOperation anonymousFunction)
            {
                return anonymousFunction.Syntax;
            }
            if (delegateInstance is IFlowAnonymousFunctionOperation flowAnonymousFunction)
            {
                return flowAnonymousFunction.Syntax;
            }

            // 6. Handle methods passed as delegates
            if (delegateInstance is IMethodReferenceOperation methodReference)
            {
                var syntaxRef = methodReference.Method.DeclaringSyntaxReferences.ExactlyOneOrDefault();
                if (syntaxRef == null)
                    return null;
                return syntaxRef.GetSyntax(cancellationToken);
            }
            if (delegateInstance is ILocalFunctionOperation localFunction)
            {
                return localFunction.Syntax;
            }

            return null;
        }

        /// <summary>
        /// Inherit variables from outer scope (crucial for local functions capturing variables)
        /// And map known argument constants to the method parameters
        /// </summary>
        private static Dictionary<IParameterSymbol, ParameterValue> GetArguments(
            IInvocationOperation invocation,
            SemanticModel semanticModel,
            IReadOnlyDictionary<IParameterSymbol, ParameterValue>? currentArguments)
        {
            var knownArguments = new Dictionary<IParameterSymbol, ParameterValue>(SymbolEqualityComparer.Default);
            if (currentArguments != null)
            {
                foreach (var arg in currentArguments)
                {
                    knownArguments.Add(arg.Key, arg.Value);
                }
            }
            foreach (var arg in invocation.Arguments)
            {
                if (arg.Parameter != null)
                {
                    knownArguments[arg.Parameter] = new ParameterValue(arg.Value);
                }
            }

            // Unwrap delegated via knownArguments
            if (invocation.Instance is IParameterReferenceOperation parameterReference &&
                knownArguments.ContainsKey(parameterReference.Parameter))
            {
                if (GetMethodSyntaxNode(invocation, knownArguments, default) is SyntaxNode delegateDefinition)
                {
                    if (semanticModel.GetSymbolInfo(delegateDefinition).Symbol is IMethodSymbol delegateSymbol)
                    {
                        foreach (var arg in delegateSymbol.Parameters
                            .Zip(invocation.Arguments, (x, y) => (ActualParameter: x, DelegateParameter: y.Parameter)))
                        {
                            if (arg.DelegateParameter == null)
                                continue;
                            // Move parameter value to actual parameter
                            var parameterValue = knownArguments[arg.DelegateParameter];
                            knownArguments[arg.ActualParameter] = parameterValue;
                            knownArguments.Remove(arg.DelegateParameter);
                        }
                    }
                }
            }

            return knownArguments;
        }

        /// <summary>
        /// Tries to evaluate the <paramref name="condition"/> using the <paramref name="knownArguments"/> values
        /// </summary>
        /// <remarks>Only works for compiler constants</remarks>
        private static bool? EvaluateCondition(
            IOperation? condition,
            IReadOnlyDictionary<IParameterSymbol, ParameterValue> knownArguments)
        {
            // (Use the exact same EvaluateCondition logic from the previous snippet)
            if (condition == null)
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
        /// Tries to find the <paramref name="startBlock"/> and <paramref name="usageIndex"/> of <paramref name="variableUsageNode"/> insise the given <paramref name="cfg"/>
        /// </summary>
        private static void FindStartingBlock(
            ControlFlowGraph cfg,
            SyntaxNode variableUsageNode,
            out BasicBlock? startBlock,
            out int? usageIndex)
        {
            startBlock = null;
            usageIndex = null;
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
                        // Start looking just before the variable usage
                        usageIndex = i - 1;
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

        /// <summary>
        /// Finds all curresponding local caller parameter reference for the given <paramref name="paramRef"/>
        /// </summary>
        /// <returns>CallerParamRef and ParentInvocation, if the invocation happended inside another parent invocation</returns>
        private static IEnumerable<(IOperation CallerParamRef, IInvocationOperation? ParentInvocation)> GetCallingOperations(
            IParameterReferenceOperation paramRef,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            var targetParam = paramRef.Parameter;
            if (targetParam.ContainingSymbol is not IMethodSymbol methodSymbol)
                yield break;

            // 1. Get the index of our parameter (e.g. '(a,b)=>...', 'a' is index 0)
            int paramIndex = methodSymbol.Parameters.IndexOf(targetParam);
            if (paramIndex == -1)
                yield break;

            // 2. Walk up the Operation Tree to find the Lambda or Local Function block
            IOperation? functionOp = null;
            IOperation? currentOp = paramRef;
            while (currentOp != null)
            {
                if (currentOp is IAnonymousFunctionOperation || currentOp is ILocalFunctionOperation)
                {
                    functionOp = currentOp;
                    break;
                }
                currentOp = currentOp.Parent;
            }
            if (functionOp == null)
                yield break;

            switch (functionOp)
            {
                // Local function invoked directly 
                case ILocalFunctionOperation localFunction:
                {
                    var localCfg = localFunction.GetControlFlowGraph(default);
                    if (localCfg == null)
                        yield break;
                    var callerCfg = localCfg.Parent;
                    if (callerCfg == null)
                        yield break;

                    // 1. Find all direct calls to this local function in the caller CFG
                    foreach (var invocation in callerCfg.ListAllOperations().OfType<IInvocationOperation>())
                    {
                        if (SymbolEqualityComparer.Default.Equals(invocation.TargetMethod, methodSymbol))
                        {
                            // 2. Found, return argument caller value
                            yield return (invocation.Arguments[paramIndex].Value, null);
                        }
                    }
                }
                break;
                // Invocation of a passed delegate
                case IAnonymousFunctionOperation anonymousFunction:
                {
                    var usage = anonymousFunction.Parent?.IgnoreCasts();
                    // 1. Unwrap implicit delegate creation
                    if (usage is IDelegateCreationOperation delegateCreation)
                    {
                        usage = delegateCreation.Parent;
                    }
                    if (usage is not IArgumentOperation argument || argument.Parent is not IInvocationOperation parentInvocation)
                        yield break;

                    // 2. Get the delegate parameter 
                    var delegateParam = argument.Parameter; // This is `selector`
                    if (delegateParam == null)
                        yield break;

                    // 3. We can only analyze methods where we have the source code.
                    var methodSyntax = GetMethodSyntaxNode(parentInvocation, null, cancellationToken);
                    if (methodSyntax == null)
                        yield break;

                    // 5. Get the Operation Tree and CFG for the target method
                    var methodOperation = semanticModel.GetOperation(methodSyntax, cancellationToken);
                    if (methodOperation == null)
                        yield break;
                    var cfg = methodOperation.GetControlFlowGraph(cancellationToken);
                    if (cfg == null || cfg.Blocks.IsDefaultOrEmpty)
                        yield break;

                    // 6. Find delegate invocation inside CFG
                    foreach (var invocation in cfg.ListAllOperations().OfType<IInvocationOperation>())
                    {
                        if (invocation.TargetMethod.MethodKind != MethodKind.DelegateInvoke)
                            continue;
                        var instance = invocation.Instance?.IgnoreCasts();
                        if (instance == null)
                            continue;
                        // 7. Check the delegate invocation parameters to match the delegate parameter
                        if (instance is IParameterReferenceOperation delParamRef &&
                            SymbolEqualityComparer.Default.Equals(delParamRef.Parameter, delegateParam))
                        {
                            // 8. Found, return argument caller value
                            yield return (invocation.Arguments[paramIndex].Value, parentInvocation);
                        }
                    }
                }
                break;
            }
            yield break;
        }

        #endregion
    }
}

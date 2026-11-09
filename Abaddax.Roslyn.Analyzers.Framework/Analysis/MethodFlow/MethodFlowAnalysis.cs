using Abaddax.Roslyn.Analyzers.Framework.Analysis.MethodFlow.Helper;
using Abaddax.Roslyn.Analyzers.Framework.Analysis.MethodFlow.Models;
using Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow;
using Abaddax.Roslyn.Analyzers.Framework.Extensions;
using Abaddax.Roslyn.Analyzers.Framework.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.MethodFlow
{
    public static class MethodFlowAnalysis
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
            CancellationToken cancellationToken,
            TraverseCallback? onTraverseCallback = null)
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
                onTraverseCallback: onTraverseCallback,
                cancellationToken);
        }

        #region Internal
        /// <summary>
        /// Internal resolver for <see cref="GetPossibleReturnValues(IInvocationOperation, SemanticModel, CancellationToken)"/>
        /// </summary>
        /// <remarks>Also traverses local function</remarks>
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

            // We can only analyze methods where we have the source code.
            var methodSyntax = GetMethodSyntaxNode(invocation, outerArguments, cancellationToken);
            if (methodSyntax == null)
                yield break;
            if (!semanticModel.HasSameSyntaxTree(methodSyntax))
            {
                var newSemanticModel = semanticModel.TryGetSemanticModelFor(methodSyntax);
                if (newSemanticModel == null)
                    yield break;
                semanticModel = newSemanticModel;
            }

            // Add to callstack
            if (callStack.Count > maxCallStackDepth || !callStack.Add(targetMethod))
                yield break;
            try
            {
                // Inherit variables from outer scope (crucial for local functions capturing variables)
                // And map known argument constants to the method parameters
                var knownArguments = GetArguments(invocation, semanticModel, outerArguments, cancellationToken);

                // Get the Operation Tree and CFG for the target method
                var methodOperation = semanticModel.GetOperation(methodSyntax, cancellationToken);
                if (methodOperation == null)
                    yield break;
                cfg ??= methodOperation.GetControlFlowGraph(cancellationToken);
                if (cfg == null || cfg.Blocks.IsDefaultOrEmpty)
                    yield break;

                foreach (var block in cfg.TraverseCfg(knownArguments, cancellationToken))
                {
                    // Check for explicit Return operations inside the block's body
                    foreach (var op in block.Operations)
                    {
                        if (op is IReturnOperation returnOp && returnOp.ReturnedValue != null)
                        {
                            foreach (var res in ResolveReturnedValue(returnOp.ReturnedValue, semanticModel, cfg, knownArguments, callStack, maxCallStackDepth, cancellationToken))
                                yield return res;
                        }
                    }

                    // Check if the branch itself is a return (common in Roslyn CFGs)
                    if (block.FallThroughSuccessor?.Semantics == ControlFlowBranchSemantics.Return &&
                        block.BranchValue != null)
                    {
                        foreach (var res in ResolveReturnedValue(block.BranchValue, semanticModel, cfg, knownArguments, callStack, maxCallStackDepth, cancellationToken))
                            yield return res;
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
        /// <remarks>Also traverses local function</remarks>
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

            // We can only analyze methods where we have the source code.
            var methodSyntax = GetMethodSyntaxNode(invocation, outerArguments, cancellationToken);
            if (methodSyntax == null)
                yield break;
            if (!semanticModel.HasSameSyntaxTree(methodSyntax))
            {
                var newSemanticModel = semanticModel.TryGetSemanticModelFor(methodSyntax);
                if (newSemanticModel == null)
                    yield break;
                semanticModel = newSemanticModel;
            }

            // Add to callstack
            if (callStack.Count > maxCallStackDepth || !callStack.Add(targetMethod))
                yield break;
            try
            {
                // Inherit variables from outer scope (crucial for local functions capturing variables)
                // And map known argument constants to the method parameters
                var knownArguments = GetArguments(invocation, semanticModel, outerArguments, cancellationToken);

                // Get the Operation Tree and CFG for the target method
                var methodOperation = semanticModel.GetOperation(methodSyntax, cancellationToken);
                if (methodOperation == null)
                    yield break;
                cfg ??= methodOperation.GetControlFlowGraph(cancellationToken);
                if (cfg == null || cfg.Blocks.IsDefaultOrEmpty)
                    yield break;

                var variableFlow = VariableFlowAnalysis.RunAnalysis(
                    targetMethod,
                    cfg,
                    semanticModel,
                    knownArguments,
                    cancellationToken);
                if (variableFlow == null)
                    yield break;

                var possibleOutValues = variableFlow.GetPossibleExitValues(outArgument)
                    .ToArray();
                // Only continue if all last assignment operations are know
                if (possibleOutValues.Any(x => x == null))
                    yield break;

                foreach (var possibleOutValue in possibleOutValues)
                {
                    foreach (var res in ResolveReturnedValue(possibleOutValue, semanticModel, cfg, outerArguments, callStack, maxCallStackDepth, cancellationToken))
                        yield return res;
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
        /// <remarks>Also traverses local function</remarks>
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

            // We can only analyze methods where we have the source code.
            var methodBody = expression.GetContainingMethodDeclarationBlock();
            if (methodBody == null)
                yield break;
            if (!semanticModel.HasSameSyntaxTree(methodBody))
            {
                var newSemanticModel = semanticModel.TryGetSemanticModelFor(methodBody);
                if (newSemanticModel == null)
                    yield break;
                semanticModel = newSemanticModel;
            }

            // Get the Operation Tree and CFG for the target method
            var methodOperation = semanticModel.GetOperation(methodBody, cancellationToken);
            if (methodOperation == null)
                yield break;
            cfg ??= methodOperation.GetControlFlowGraph(cancellationToken);
            if (cfg == null || cfg.Blocks.IsDefaultOrEmpty)
                yield break;

            if (semanticModel.GetDeclaredSymbol(methodBody, cancellationToken) is not IMethodSymbol methodSymbol)
                yield break;

            var variableFlow = VariableFlowAnalysis.RunAnalysis(
                methodSymbol,
                cfg,
                semanticModel,
                outerArguments,
                cancellationToken);
            if (variableFlow == null)
                yield break;

            var possibleValues = variableFlow.GetPossibleLastValues(variable)
                .ToArray();
            // Only continue if all last assignment operations are know
            if (possibleValues.Any(x => x == null))
                yield break;

            foreach (var possibleValue in possibleValues)
            {
                foreach (var res in ResolveReturnedValue(possibleValue, semanticModel, cfg, outerArguments, callStack, maxCallStackDepth, cancellationToken))
                    yield return res;
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
            TraverseCallback? onTraverseCallback,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if current call traversal is finished
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

            // Check for abort condition
            if (onTraverseCallback != null && !onTraverseCallback.Invoke(operation))
                return operation;

            // Step into invocations
            if (operation is IInvocationOperation invocation)
            {
                // We can only analyze methods where we have the source code.
                var methodSyntax = GetMethodSyntaxNode(invocation, outerArguments, cancellationToken);
                if (methodSyntax == null)
                    return operation;

                // Check for possible return values
                var lastAssignments = GetPossibleReturnValuesInternal(invocation, semanticModel, cfg: null, outerArguments, callStack, maxCallStackDepth, cancellationToken);
                var lastAssignment = lastAssignments.ExactlyOneOrDefault();
                if (lastAssignment == null)
                    return operation;

                // Check if last assignment is inside the invocation method
                if (methodSyntax.Contains(lastAssignment.Syntax))
                {
                    var currentCallStackInfo = new TraversalStackInfo(methodSyntax, invocation.TargetMethod, semanticModel, outerArguments, cfg);

                    if (!semanticModel.HasSameSyntaxTree(methodSyntax))
                    {
                        var newSemanticModel = semanticModel.TryGetSemanticModelFor(methodSyntax);
                        if (newSemanticModel == null)
                            return operation;
                        semanticModel = newSemanticModel;
                    }

                    // Inherit variables from outer scope (crucial for local functions capturing variables)
                    // And map known argument constants to the method parameters
                    outerArguments = GetArguments(invocation, semanticModel, outerArguments, cancellationToken);

                    // Get the Operation Tree and CFG for the target method
                    var methodOperation = semanticModel.GetOperation(methodSyntax, cancellationToken);
                    if (methodOperation == null)
                        return operation;
                    cfg = methodOperation.GetControlFlowGraph(cancellationToken);
                    if (cfg == null || cfg.Blocks.IsDefaultOrEmpty)
                        return operation;

                    // Add to callstack 
                    if (callStack.Count > maxCallStackDepth || !callStack.Add(currentCallStackInfo.Method))
                        return operation;
                    callStackInfo.Push(currentCallStackInfo);
                }

                // Traverse function call with all the currently known parameters
                return TraverseAssignmentsInternal(lastAssignment, semanticModel, cfg, outerArguments, callStack, callStackInfo, maxCallStackDepth, onTraverseCallback, cancellationToken);
            }
            else if (operation.Parent is IArgumentOperation argument &&
                argument.Parameter?.RefKind is RefKind.Out or RefKind.Ref &&
                argument.Parent is IInvocationOperation outInvocation)
            {
                // We can only analyze methods where we have the source code.
                var methodSyntax = GetMethodSyntaxNode(outInvocation, outerArguments, cancellationToken);
                if (methodSyntax == null)
                    return operation;

                // Check for possible out values
                var lastAssignments = GetPossibleOutParameterValuesInternal(outInvocation, argument, semanticModel, cfg, outerArguments, callStack, maxCallStackDepth, cancellationToken);
                var lastAssignment = lastAssignments.ExactlyOneOrDefault();
                if (lastAssignment == null)
                    return operation;

                // Check if last assignment is inside the invocation method
                if (methodSyntax.Contains(lastAssignment.Syntax))
                {
                    var currentCallStackInfo = new TraversalStackInfo(methodSyntax, outInvocation.TargetMethod, semanticModel, outerArguments, cfg);

                    if (!semanticModel.HasSameSyntaxTree(methodSyntax))
                    {
                        var newSemanticModel = semanticModel.TryGetSemanticModelFor(methodSyntax);
                        if (newSemanticModel == null)
                            return operation;
                        semanticModel = newSemanticModel;
                    }

                    // Inherit variables from outer scope (crucial for local functions capturing variables)
                    // And map known argument constants to the method parameters
                    outerArguments = GetArguments(outInvocation, semanticModel, outerArguments, cancellationToken);

                    // Get the Operation Tree and CFG for the target method
                    var methodOperation = semanticModel.GetOperation(methodSyntax, cancellationToken);
                    if (methodOperation == null)
                        return operation;
                    cfg = methodOperation.GetControlFlowGraph(cancellationToken);
                    if (cfg == null || cfg.Blocks.IsDefaultOrEmpty)
                        return operation;

                    // Add to callstack 
                    if (callStack.Count > maxCallStackDepth || !callStack.Add(currentCallStackInfo.Method))
                        return operation;
                    callStackInfo.Push(currentCallStackInfo);
                }

                // Traverse function call with all the currently known parameters
                return TraverseAssignmentsInternal(lastAssignment, semanticModel, cfg, outerArguments, callStack, callStackInfo, maxCallStackDepth, onTraverseCallback, cancellationToken);
            }
            else
            {
                var lastAssignments = GetPossibleLastAssignmentInternal(operation, semanticModel, cfg, outerArguments, callStack, maxCallStackDepth, cancellationToken);
                var lastAssignment = lastAssignments.ExactlyOneOrDefault();
                if (lastAssignment == null)
                {
                    // We hit an end, possibly a parameter reference? If so check if we can move up into the caller (if we are currently inside a local function/delegate)
                    if (operation is not IParameterReferenceOperation parameterReference)
                        return operation;
                    var possibleCallingOperations = GetCallingOperations(parameterReference, semanticModel, cancellationToken);
                    var possibleCallingOperation = possibleCallingOperations.ExactlyOneOrDefault();
                    if (possibleCallingOperation.CallerParamRef == null)
                        return operation;

                    // Check if we are inside the caller body
                    if (possibleCallingOperation.ParentInvocation is IInvocationOperation parentInvocation)
                    {
                        // We can only analyze methods where we have the source code.
                        var methodSyntax = GetMethodSyntaxNode(parentInvocation, outerArguments, cancellationToken);
                        if (methodSyntax == null)
                            return operation;

                        var currentCallStackInfo = new TraversalStackInfo(methodSyntax, parentInvocation.TargetMethod, semanticModel, outerArguments, cfg);

                        if (!semanticModel.HasSameSyntaxTree(methodSyntax))
                        {
                            var newSemanticModel = semanticModel.TryGetSemanticModelFor(methodSyntax);
                            if (newSemanticModel == null)
                                return operation;
                            semanticModel = newSemanticModel;
                        }

                        // Inherit variables from outer scope (crucial for local functions capturing variables)
                        // And map known argument constants to the method parameters
                        outerArguments = GetArguments(parentInvocation, semanticModel, outerArguments, cancellationToken);

                        // Add to callstack 
                        if (callStack.Count > maxCallStackDepth || !callStack.Add(currentCallStackInfo.Method))
                            return operation;
                        callStackInfo.Push(currentCallStackInfo);
                    }

                    lastAssignment = possibleCallingOperation.CallerParamRef;
                }
                return TraverseAssignmentsInternal(lastAssignment, semanticModel, cfg, outerArguments, callStack, callStackInfo, maxCallStackDepth, onTraverseCallback, cancellationToken);
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
            // Normal invocation
            if (!invocation.TargetMethod.DeclaringSyntaxReferences.IsDefaultOrEmpty)
            {
                var syntaxRef = invocation.TargetMethod.DeclaringSyntaxReferences.ExactlyOneOrDefault();
                if (syntaxRef == null)
                    return null;
                return syntaxRef.GetSyntax(cancellationToken);
            }

            // A delegate invocation has the delegate variable as its 'Instance' 
            var delegateInstance = invocation.Instance?.IgnoreCasts();
            if (delegateInstance == null)
                return null;

            // Is the delegate referencing a parameter?
            if (delegateInstance is IParameterReferenceOperation paramRef &&
                paramRef.Parameter != null)
            {
                if (currentArguments == null)
                    return null;
                if (!currentArguments.TryGetValue(paramRef.Parameter, out var parameterValue))
                    return null;
                delegateInstance = parameterValue.Parameter.IgnoreCasts();
            }

            // Check delegate creation
            if (delegateInstance is IDelegateCreationOperation delegateCreation)
                delegateInstance = delegateCreation.Target;

            // Get lambda syntax
            if (delegateInstance is IAnonymousFunctionOperation anonymousFunction)
                return anonymousFunction.Syntax;
            if (delegateInstance is IFlowAnonymousFunctionOperation flowAnonymousFunction)
                return flowAnonymousFunction.Syntax;

            // Handle methods passed as delegates
            if (delegateInstance is IMethodReferenceOperation methodReference)
            {
                var syntaxRef = methodReference.Method.DeclaringSyntaxReferences.ExactlyOneOrDefault();
                if (syntaxRef == null)
                    return null;
                return syntaxRef.GetSyntax(cancellationToken);
            }
            if (delegateInstance is ILocalFunctionOperation localFunction)
                return localFunction.Syntax;

            return null;
        }

        /// <summary>
        /// Inherit variables from outer scope (crucial for local functions capturing variables)
        /// And map known argument constants to the method parameters
        /// </summary>
        private static Dictionary<IParameterSymbol, ParameterValue> GetArguments(
            IInvocationOperation invocation,
            SemanticModel semanticModel,
            IReadOnlyDictionary<IParameterSymbol, ParameterValue>? currentArguments,
            CancellationToken cancellationToken)
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
                    knownArguments[arg.Parameter] = new ParameterValue(arg.Value);
            }

            // Unwrap delegated via knownArguments
            if (invocation.Instance is IParameterReferenceOperation parameterReference &&
                knownArguments.ContainsKey(parameterReference.Parameter))
            {
                if (GetMethodSyntaxNode(invocation, knownArguments, cancellationToken) is SyntaxNode delegateDefinition)
                {
                    if (semanticModel.GetSymbolInfo(delegateDefinition, cancellationToken).Symbol is IMethodSymbol delegateSymbol)
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
        /// Finds all corresponding local caller parameter reference for the given <paramref name="paramRef"/>
        /// </summary>
        /// <returns>CallerParamRef and ParentInvocation, if the invocation happened inside another parent invocation</returns>
        private static IEnumerable<CallerInfo> GetCallingOperations(
            IParameterReferenceOperation paramRef,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            var targetParam = paramRef.Parameter;
            if (targetParam.ContainingSymbol is not IMethodSymbol methodSymbol)
                yield break;

            // Get the index of our parameter (e.g. '(a,b)=>...', 'a' is index 0)
            int paramIndex = methodSymbol.Parameters.IndexOf(targetParam, 0, methodSymbol.Parameters.Length, SymbolEqualityComparer.Default);
            if (paramIndex == -1)
                yield break;

            // Walk up the Operation Tree to find the Lambda or Local Function block
            IOperation? functionOp = null;
            IOperation? currentOp = paramRef;
            while (currentOp != null)
            {
                if (currentOp is IAnonymousFunctionOperation or ILocalFunctionOperation)
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

                    // Find all direct calls to this local function in the caller CFG
                    foreach (var invocation in callerCfg.ListAllOperations().OfType<IInvocationOperation>())
                    {
                        if (SymbolEqualityComparer.Default.Equals(invocation.TargetMethod, methodSymbol))
                            // Found, return argument caller value
                            yield return new CallerInfo(invocation.Arguments[paramIndex].Value, null, false);
                    }
                }
                break;
                // Invocation of a passed delegate
                case IAnonymousFunctionOperation anonymousFunction:
                {
                    var usage = anonymousFunction.Parent?.IgnoreCasts();

                    // Unwrap implicit delegate creation
                    if (usage is IDelegateCreationOperation delegateCreation)
                        usage = delegateCreation.Parent;

                    if (usage is not IArgumentOperation argument || argument.Parent is not IInvocationOperation parentInvocation)
                        yield break;

                    // Shortcut for known LINQ methods
                    var linqParameter = LambdaTraversalHelper.GetForwardedParameter(parentInvocation, argument, paramIndex);
                    if (linqParameter != null)
                    {
                        yield return new CallerInfo(linqParameter, null, true);
                        yield break;
                    }

                    // Get the delegate parameter 
                    var delegateParam = argument.Parameter; // This is `selector`
                    if (delegateParam == null)
                        yield break;

                    // We can only analyze methods where we have the source code.
                    var methodSyntax = GetMethodSyntaxNode(parentInvocation, currentArguments: null, cancellationToken);
                    if (methodSyntax == null)
                        yield break;

                    // Get the Operation Tree and CFG for the target method
                    var methodOperation = semanticModel.GetOperation(methodSyntax, cancellationToken);
                    if (methodOperation == null)
                        yield break;
                    var cfg = methodOperation.GetControlFlowGraph(cancellationToken);
                    if (cfg == null || cfg.Blocks.IsDefaultOrEmpty)
                        yield break;

                    // Find delegate invocation inside CFG
                    foreach (var invocation in cfg.ListAllOperations().OfType<IInvocationOperation>())
                    {
                        if (invocation.TargetMethod.MethodKind != MethodKind.DelegateInvoke)
                            continue;
                        var instance = invocation.Instance?.IgnoreCasts();
                        if (instance == null)
                            continue;
                        // Check the delegate invocation parameters to match the delegate parameter
                        if (instance is IParameterReferenceOperation delParamRef &&
                            SymbolEqualityComparer.Default.Equals(delParamRef.Parameter, delegateParam))
                        {
                            // Found, return argument caller value
                            yield return new CallerInfo(invocation.Arguments[paramIndex].Value, parentInvocation, false);
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

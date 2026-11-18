using Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Extensions;
using Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Helper;
using Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Models;
using Abaddax.Roslyn.Analyzers.Framework.Extensions;
using Abaddax.Roslyn.Analyzers.Framework.Helper;
using Abaddax.Roslyn.Analyzers.Framework.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow
{
    public static class VariableFlowAnalysis
    {
        public static VariableFlowState? RunAnalysis(
            MethodDeclarationSyntax methodBody,
            SemanticModel semanticModel,
            IReadOnlyDictionary<IParameterSymbol, ParameterValue>? knownArguments,
            CancellationToken cancellationToken)
        {
            var methodOperation = semanticModel.GetOperation(methodBody, cancellationToken);
            if (methodOperation == null)
                return null;
            if (semanticModel.GetDeclaredSymbol(methodBody, cancellationToken) is not IMethodSymbol methodSymbol)
                return null;
            var cfg = methodOperation.GetControlFlowGraph(cancellationToken);
            if (cfg == null)
                return null;
            return RunAnalysis(methodSymbol, cfg, semanticModel, knownArguments, cancellationToken);
        }
        public static VariableFlowState? RunAnalysis(
            IMethodSymbol methodSymbol,
            ControlFlowGraph cfg,
            SemanticModel semanticModel,
            IReadOnlyDictionary<IParameterSymbol, ParameterValue>? knownArguments,
            CancellationToken cancellationToken)
        {
            try
            {
                // Prefill "empty" state with the static and "this" instance
                var emptyState = ProgramState.Empty;
                emptyState = emptyState.Allocate(AccessPathRoot.Static(methodSymbol.ContainingType), methodSymbol.ContainingType, null);
                if (!methodSymbol.IsStatic)
                    emptyState = emptyState.Allocate(AccessPathRoot.This, methodSymbol.ContainingType, null);

                // Inject known arguments
                if (knownArguments != null)
                    emptyState = InjectKnownArguments(emptyState, knownArguments);

                return RunAnalysisInternal(
                    methodSymbol,
                    new VariableFlowAnalysisContext()
                    {
                        CFG = cfg,
                        SemanticModel = semanticModel,
                        KnownArguments = knownArguments ?? ImmutableDictionary<IParameterSymbol, ParameterValue>.Empty,
                        CallStack = new HashSet<IMethodSymbol>([methodSymbol], SymbolEqualityComparer.Default),
                        MaxCallStackDepth = 10,
                        EmptyState = emptyState
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
#if DEBUG
                throw;
#else
                return null;
#endif
            }
        }

        #region Internal
        /// <summary>
        /// Internal resolver for <see cref="RunAnalysis(IMethodSymbol, ControlFlowGraph, SemanticModel, IReadOnlyDictionary{IParameterSymbol, ParameterValue}?, CancellationToken)"/>
        /// </summary>
        private static VariableFlowState? RunAnalysisInternal(
            IMethodSymbol methodSymbol,
            VariableFlowAnalysisContext analysisContext,
            CancellationToken cancellationToken)
        {
            // Track In-States and Out-States for every basic block
            var blockInStates = new Dictionary<BasicBlock, ProgramState>();
            var blockOutStates = new Dictionary<BasicBlock, ProgramState>();
            var exitStates = new Dictionary<BasicBlock, ProgramState>();

            var states = new Dictionary<IOperation, ProgramState>(OperationEqualityComparer.Default);

            // Running only once is not enough, because not all predecessor blocks can always be executed before
            // (In some complex branching scenarios)
            // Keep running until the analysis reaches a stable state
            int iteration = 0;
            bool hasChanges = true;
            while (hasChanges)
            {
                if (iteration++ > 10)
                    throw new Exception("Failed to reach stable state");
                hasChanges = false;
                exitStates.Clear();

                foreach (var block in analysisContext.CFG.TraverseCfg(
                    knownArguments: analysisContext.KnownArguments,
                    cancellationToken: cancellationToken))
                {
                    // Compute IN state by MERGING Out-states of all incoming predecessor blocks
                    ProgramState? currentState = null;
                    foreach (var predecessor in block.GetPredecessors(analysisContext.CFG))
                    {
                        if (!blockOutStates.TryGetValue(predecessor, out var predecessorOutState))
                            continue;
                        currentState = currentState == null
                            ? predecessorOutState
                            : ProgramState.Merge(currentState, predecessorOutState);
                    }
                    currentState ??= analysisContext.EmptyState;
                    blockInStates[block] = currentState;

                    // Execute transfer functions for every operation inside this basic block
                    VariableFlowTransferState transferState = VariableFlowTransferState.Empty;
                    foreach (var operation in block.GetOperations())
                    {
                        currentState = TransferOperation(analysisContext, currentState, operation, cancellationToken, out var nestedTransferState);
                        states[operation] = currentState;
                        transferState = VariableFlowTransferState.Merge(transferState, nestedTransferState);
                    }
                    // Merge transfer state into states
                    foreach (var nestedOperationState in transferState.NestedOperationStates)
                    {
                        var nestedState = nestedOperationState.Value;
                        if (states.TryGetValue(nestedOperationState.Key, out var currentNestedState))
                        {
                            nestedState = ProgramState.Merge(currentNestedState, nestedState);
                        }
                        states[nestedOperationState.Key] = nestedState;
                    }

                    // Check if Out-state changed. If yes, add successors back to the worklist.
                    if (!blockOutStates.TryGetValue(block, out var oldOutState))
                        oldOutState = analysisContext.EmptyState;

                    if (!oldOutState.IsEquivalentTo(currentState))
                    {
                        blockOutStates[block] = currentState;
                        hasChanges = true;
                    }

                    // Store exit blocks
                    if (block.Kind == BasicBlockKind.Exit)
                    {
                        if (exitStates.TryGetValue(block, out var currentExitState))
                            currentState = ProgramState.Merge(currentExitState, currentState);
                        exitStates[block] = currentState;
                    }
                }
            }

            return new VariableFlowState(analysisContext.CFG, states, exitStates.Values);
        }

        private static ProgramState TransferOperation(
            VariableFlowAnalysisContext analysisContext,
            ProgramState state,
            IOperation operation,
            CancellationToken cancellationToken,
            out VariableFlowTransferState transferState)
        {
            cancellationToken.ThrowIfCancellationRequested();
            transferState = VariableFlowTransferState.Empty;
            return operation switch
            {
                // Unwrap statement
                IExpressionStatementOperation statement => TransferOperation(analysisContext, state, statement.Operation, cancellationToken, out transferState),
                // Deconstruct Assignments (x, y) = (1, 2);
                IDeconstructionAssignmentOperation deconstruct => HandleDeconstructAssignment(analysisContext, state, deconstruct, cancellationToken, out transferState),
                // Variable Declaration or Assignment (e.g., x = new Foo(); or y = x;)
                IAssignmentOperation assign => HandleAssignment(analysisContext, state, assign, cancellationToken, out transferState),
                // Invocation (e.g. x.Func(); x.Func(out var y);)
                IInvocationOperation invocation => HandleInvocation(analysisContext, state, invocation, cancellationToken, out transferState),
                // FlowCaptures (automatic CFG temporary values)
                IFlowCaptureOperation flowCapture => HandleFlowCapture(analysisContext, state, flowCapture, cancellationToken, out transferState),
                // Postfix/Prefix increment/decrement (e.g x++;)
                IIncrementOrDecrementOperation incrementOrDecrement => HandleIncrementOrDecrement(analysisContext, state, incrementOrDecrement, cancellationToken, out transferState),
                // TODO: Add handlers for other operation types if needed
                _ => state
            };
        }

        private static ProgramState GetAbstractValue(
            VariableFlowAnalysisContext analysisContext,
            ProgramState state,
            IOperation value,
            ITypeSymbol? targetType,
            CancellationToken cancellationToken,
            out IAbstractValue abstractValue,
            out VariableFlowTransferState transferState)
        {
            transferState = VariableFlowTransferState.Empty;
            // Source (Right-hand side, e.g., 'new Foo()', 'y', or 'y.Prop')
            switch (value.IgnoreCasts())
            {
                // Assignment to new value
                case IObjectCreationOperation: //x = new MyClass();
                case IArrayCreationOperation: //x = new int[10];
                case IDelegateCreationOperation: //x = Func;
                case ILiteralOperation: //x = 1;
                case IDefaultValueOperation: //x = default;
                case ICollectionExpressionOperation: //x = [1,2,3];
                case IOperation { Syntax: StackAllocArrayCreationExpressionSyntax }: //x = stackalloc new int[10];
                case IBinaryOperation: //x = a + b;
                case IOperation arrayOperation
                    when arrayOperation.FindChilds<IArrayElementReferenceOperation>().Any(): //x = arr[0]; x = arr[0].X;
                case ICaughtExceptionOperation: // catch(Exception ex)
                {
                    var creation = value.IgnoreCasts();
                    var creationType = value.Type; //Type with casts!
                    if (creationType == null)
                    {
                        // Deduce type from target for '= null'
                        if (creation.Syntax is LiteralExpressionSyntax literal && literal.Token.IsKind(SyntaxKind.NullKeyword))
                            creationType = targetType;
                        if (creationType == null)
                            throw new Exception("Unknown type of the creation operation");
                    }
                    abstractValue = ProgramStateExtensions.AllocateDefaultValueForType(ref state, creationType, creation);
                    return state;
                }
                case ITupleOperation { Type: INamedTypeSymbol { IsValueType: true } tupleType } tuple:
                {
                    var currentTransferState = transferState;
                    // Evaluate source values
                    var sourceTupleValues = tuple.Elements.Select((tupleElement, index) =>
                    {
                        var tupleElementType = tupleType.TupleElements[index];
                        //state = TransferOperation(state, tupleElement, cancellationToken);
                        state = GetAbstractValue(analysisContext, state, tupleElement, tupleElement.Type, cancellationToken,
                            out var tupleElementValue,
                            out var nestedTransferState);
                        currentTransferState = VariableFlowTransferState.Merge(currentTransferState, nestedTransferState);
                        return (tupleElement, tupleElementType, tupleElementValue);
                    });
                    abstractValue = new ValueTypeValue(sourceTupleValues.ToImmutableDictionary(
                            x => new AccessSymbol(x.tupleElementType, x.tupleElement),
                            y => y.tupleElementValue));
                    transferState = VariableFlowTransferState.Merge(transferState, currentTransferState);
                    return state;
                }
                // Multiple inline chained assignments: x = y = 1;
                case IAssignmentOperation nestedAssignment:
                {
                    state = HandleAssignment(analysisContext, state, nestedAssignment, cancellationToken, out var nestedTransferState);
                    transferState = VariableFlowTransferState.Merge(transferState, nestedTransferState);
                    value = nestedAssignment.Target;
                    goto default; //Proceed with assignment
                }
                // Assignment to the result of an invocation: x = Func(out var y)
                case IInvocationOperation invocation:
                {
                    state = HandleInvocation(analysisContext, state, invocation, cancellationToken, out var nestedTransferState);
                    transferState = VariableFlowTransferState.Merge(transferState, nestedTransferState);
                    goto default; //Proceed with assignment
                }
                // Postfix or Prefix increment/decrement: x++
                case IIncrementOrDecrementOperation incrementOrDecrement:
                {
                    state = HandleIncrementOrDecrement(analysisContext, state, incrementOrDecrement, cancellationToken, out var nestedTransferState);
                    transferState = VariableFlowTransferState.Merge(transferState, nestedTransferState);
                    value = incrementOrDecrement.Target;
                    goto default;
                }
                // Path-To-Path assignment: y = x or y = x.Prop;
                default:
                {
                    var sourcePath = BuildAccessPath(value);
                    abstractValue = ProgramStateExtensions.EvaluatePath(ref state, sourcePath);
                    return state;
                }
            }
        }

        private static ProgramState HandleAssignment(
            VariableFlowAnalysisContext analysisContext,
            ProgramState state,
            IAssignmentOperation assign,
            CancellationToken cancellationToken,
            out VariableFlowTransferState transferState)
        {
            transferState = VariableFlowTransferState.Empty;
            //Ignore array index assignments as we can not track them
            if (assign.Target.FindChilds<IArrayElementReferenceOperation>().Any())
                return state;

            // Target (Left-hand side, e.g., 'x' or 'x.Prop')
            var targetPath = BuildAccessPath(assign.Target);

            // Get right side value 
            state = GetAbstractValue(analysisContext, state, assign.Value, assign.Target.Type, cancellationToken,
                out var sourceValue,
                out var nestedTransferState);
            transferState = VariableFlowTransferState.Merge(transferState, nestedTransferState);
            transferState = transferState.WithPathWritten(targetPath);
            return state.Write(targetPath, sourceValue);
        }
        private static ProgramState HandleDeconstructAssignment(
            VariableFlowAnalysisContext analysisContext,
            ProgramState state,
            IDeconstructionAssignmentOperation deconstruct,
            CancellationToken cancellationToken,
            out VariableFlowTransferState transferState)
        {
            if (deconstruct.Target.IgnoreCasts() is not ITupleOperation tupleTarget)
                throw new Exception("Unknown deconstruct assignment target");
            if (deconstruct.Value.IgnoreCasts() is not ITupleOperation { Type: INamedTypeSymbol tupleSourceType } tupleSource)
                throw new Exception("Unknown tuple assignment values");
            if (tupleTarget.Elements.Length != tupleSource.Elements.Length)
                throw new Exception("Tuple length mismatch");

            //Pre-Evaluate all current source tuple values
            state = GetAbstractValue(analysisContext, state, deconstruct.Value, deconstruct.Target.Type, cancellationToken,
               out var abstractSourceTupleValue,
               out transferState);
            if (abstractSourceTupleValue is not ValueTypeValue sourceTupleValue || sourceTupleValue.Fields.Count != tupleSource.Elements.Length)
                throw new Exception("Unexpected abstract tuple value");

            var targetPaths = new List<AccessPath>();
            //Assign each value 
            for (int i = 0; i < tupleTarget.Elements.Length; i++)
            {
                //Write values to the target tuple                
                var targetPath = BuildAccessPath(tupleTarget.Elements[i]);
                var sourceValue = sourceTupleValue.Fields[new AccessSymbol(tupleSourceType.TupleElements[i], tupleSource.Elements[i])];
                targetPaths.Add(targetPath);
                state = state.Write(targetPath, sourceValue);
            }
            transferState = transferState.WithPathsWritten(targetPaths);
            return state;
        }
        private static ProgramState HandleInvocation(
            VariableFlowAnalysisContext analysisContext,
            ProgramState state,
            IInvocationOperation invocation,
            CancellationToken cancellationToken,
            out VariableFlowTransferState transferState)
        {
            var outParams = invocation.Arguments.Where(x => x.Parameter?.RefKind is RefKind.Out or RefKind.Ref)
                .ToArray();
            var argumentPathsWritten = new Dictionary<IArgumentOperation, IEnumerable<AccessPath>>();

            transferState = VariableFlowTransferState.Empty;
            // Check parameters for inline assignments
            foreach (var argument in invocation.Arguments.Except(outParams))
            {
                state = TransferOperation(analysisContext, state, argument.Value, cancellationToken, out var nestedTransferState);
                argumentPathsWritten.Add(argument, nestedTransferState.PathsWritten);
                // Save each state so we can later access the state at with the arguments got passed to the method
                transferState = transferState.WithNestedState(argument, state);
                transferState = VariableFlowTransferState.Merge(transferState, nestedTransferState);
            }

            // Traverse into local function, because the might have capture clause
            state = TraverseLocalFunction(analysisContext, state, invocation, cancellationToken);

            // Invalidated passed arguments
            foreach (var argument in invocation.Arguments.Except(outParams).Where(x => !x.IsConstLikeArgument()))
            {
                state = InvalidateAccessPath(state, argument, argumentPathsWritten[argument]);
            }

            // Assign out/ref params
            foreach (var outParam in outParams)
            {
                if (outParam.Parameter == null)
                    throw new Exception("Unknown out parameter");
                var sourcePath = AccessPath.FromRoot(outParam.Parameter, outParam.Value);
                var targetPath = BuildAccessPath(outParam.Value);
                state = state.Assign(targetPath, sourcePath);
            }

            // Invalidate nested fields
            if (invocation.Instance != null && !invocation.IsConstLikeMethod())
            {
                var instancePath = BuildAccessPath(invocation.Instance);
                state = state.InvalidateNested(instancePath);
            }

            return state;

            static ProgramState TraverseLocalFunction(VariableFlowAnalysisContext analysisContext, ProgramState state, IInvocationOperation invocation, CancellationToken cancellationToken)
            {
                if (invocation.TargetMethod.IsStatic)
                    return state;
                if (invocation.TargetMethod.MethodKind is not (MethodKind.LocalFunction or MethodKind.LambdaMethod or MethodKind.DelegateInvoke))
                    return state;
                // Get CFG for the target method
                var localCfg = analysisContext.CFG.GetLocalFunctionCfg(invocation.TargetMethod, cancellationToken);
                if (localCfg == null)
                    return state;

                // Add to callstack
                if (analysisContext.CallStack.Count > analysisContext.MaxCallStackDepth || !analysisContext.CallStack.Add(invocation.TargetMethod))
                    return state;
                try
                {
                    var flowAnalysis = RunAnalysisInternal(invocation.TargetMethod, new VariableFlowAnalysisContext()
                    {
                        CFG = localCfg,
                        SemanticModel = analysisContext.SemanticModel,
                        KnownArguments = analysisContext.KnownArguments, //TODO: Inject current state
                        CallStack = analysisContext.CallStack,
                        MaxCallStackDepth = analysisContext.MaxCallStackDepth,
                        EmptyState = state,
                    }, cancellationToken);
                    if (flowAnalysis == null)
                        return state;

                    var combinedExitState = ProgramState.Empty;
                    foreach (var exitState in flowAnalysis.ExitStates)
                    {
                        combinedExitState = ProgramState.Merge(combinedExitState, exitState);
                    }
                    return combinedExitState;
                }
                finally
                {
                    analysisContext.CallStack.Remove(invocation.TargetMethod);
                }
            }
            static ProgramState InvalidateAccessPath(ProgramState state, IArgumentOperation argument, IEnumerable<AccessPath> accessPaths)
            {
                bool any = false;
                foreach (var path in accessPaths)
                {
                    state = state.InvalidateNested(path, keepUntilFirstRevType: true);
                    any = true;
                }
                if (any)
                    return state;
                var accessPath = TryBuildAccessPath(argument.Value, out _);
                if (accessPath == null)
                    return state;
                state = state.InvalidateNested(accessPath.Value, keepUntilFirstRevType: true);
                return state;
            }
        }
        private static ProgramState HandleFlowCapture(
            VariableFlowAnalysisContext analysisContext,
            ProgramState state,
            IFlowCaptureOperation flowCapture,
            CancellationToken cancellationToken,
            out VariableFlowTransferState transferState)
        {
            transferState = VariableFlowTransferState.Empty;
            var flowCaptureRoot = new AccessPathRoot(new AccessSymbol(flowCapture.Id, flowCapture));
            if (flowCapture.Value.Type == null)
            {
                //Skip null captures
                if (flowCapture.Value.Syntax is LiteralExpressionSyntax literal && literal.Token.IsKind(SyntaxKind.NullKeyword))
                    return state;
                throw new Exception("Unknown type of the creation operation");
            }
            transferState = transferState.WithPathWritten(AccessPath.FromRoot(flowCaptureRoot));
            return state.Allocate(flowCaptureRoot, flowCapture.Value.Type, flowCapture.Value);
        }
        private static ProgramState HandleIncrementOrDecrement(
            VariableFlowAnalysisContext analysisContext,
            ProgramState state,
            IIncrementOrDecrementOperation incrementOrDecrement,
            CancellationToken cancellationToken,
            out VariableFlowTransferState transferState)
        {
            transferState = VariableFlowTransferState.Empty;
            var targetPath = BuildAccessPath(incrementOrDecrement.Target);
            if (incrementOrDecrement.Type == null)
                throw new Exception("Unknown type of the increment or decrement operation");

            var value = ProgramStateExtensions.AllocateDefaultValueForType(ref state, incrementOrDecrement.Type, incrementOrDecrement);
            transferState = transferState.WithPathWritten(targetPath);
            return state.Write(targetPath, value);
        }

        /// <summary>
        /// Converts a Roslyn IOperation (like x.Prop1.Prop2) into your AccessPath abstraction.
        /// </summary>
        internal static AccessPath BuildAccessPath(IOperation operation)
        {
            var accessPath = TryBuildAccessPath(operation, out var current);
            if (accessPath == null)
                throw new Exception($"Failed to resolve access path for '{current?.IgnoreCasts().GetType()}'");
            return accessPath.Value;
        }
        internal static AccessPath? TryBuildAccessPath(IOperation operation,
            out IOperation? current)
        {
            // Unroll member reference chains from Roslyn's syntax tree or operation tree
            List<AccessSymbol> members = new();
            current = operation;
            while (true)
            {
                switch (current?.IgnoreCasts())
                {
                    case IMemberReferenceOperation memberRef:
                    {
                        members.Insert(0, new AccessSymbol(memberRef.Member, current));
                        if (memberRef.Member.IsStatic)
                            return AccessPath.FromRoot(AccessPathRoot.Static(memberRef.Member.ContainingType)).AppendRange(members);
                        current = memberRef.Instance;
                        break;
                    }
                    case IInvocationOperation invocation:
                    {
                        members.Insert(0, new AccessSymbol(invocation.TargetMethod, current));
                        //Static method
                        if (invocation.TargetMethod.IsStatic)
                            return AccessPath.FromRoot(AccessPathRoot.Static(invocation.TargetMethod.ContainingType)).AppendRange(members);
                        current = invocation.Instance;
                        break;
                    }
                    case IFlowCaptureReferenceOperation flowCapture:
                    {
                        return AccessPath.FromRoot(new AccessPathRoot(new AccessSymbol(flowCapture.Id, current))).AppendRange(members);
                    }
                    case IAwaitOperation awaitOperation:
                    {
                        current = awaitOperation.Operation;
                        //Fake 'x = await Task' as sync 'x = Task.Result'
                        if (current.Type?.GetGenericParameter(0) is not null)
                        {
                            var resultParam = current.Type.GetMembers().First(x => x.Name == "Result");
                            members.Insert(0, new AccessSymbol(resultParam, awaitOperation));
                        }
                        break;
                    }
                    case ILocalReferenceOperation localRef:
                    {
                        return AccessPath.FromRoot(localRef.Local).AppendRange(members);
                    }
                    case IInstanceReferenceOperation instanceRef:
                    {
                        return AccessPath.FromRoot(AccessPathRoot.This).AppendRange(members);
                    }
                    case IParameterReferenceOperation paramRef:
                    {
                        return AccessPath.FromRoot(paramRef.Parameter).AppendRange(members);
                    }
                    case IDeclarationExpressionOperation decl:
                    {
                        current = decl.Expression;
                        break;
                    }
                    // Caller argument passed to the current method -> unwrap to local parameter
                    case IArgumentOperation { Parameter: not null } argument:
                    {
                        return AccessPath.FromRoot(argument.Parameter, argument).AppendRange(members);
                    }
                    default:
                    {
                        return null;
                    }
                }
            }
        }


        private static ProgramState InjectKnownArguments(ProgramState state, IReadOnlyDictionary<IParameterSymbol, ParameterValue> knownArguments)
        {
            foreach (var knownArgument in knownArguments)
            {
                var path = AccessPath.FromRoot(new AccessPathRoot(new AccessSymbol(knownArgument.Key)));
                if (knownArgument.Value.Parameter.Type == null)
                    throw new Exception("Unknown outer parameter type");
                state = state.AllocateOrWriteDefault(path, knownArgument.Value.Parameter.Type, knownArgument.Value.Parameter);
            }
            return state;
        }
        #endregion
    }
}

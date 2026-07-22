using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Abaddax.Roslyn.Analyzers.Extensions
{
    internal static class OperationExtensions
    {
        public static IOperation IgnoreCasts(this IOperation operation)
        {
            if (operation is IConversionOperation cast)
                return IgnoreCasts(cast.Operand);
            return operation;
        }

        /// <summary>
        /// Tries to create a <see cref="ControlFlowGraph"> for the given <paramref name="operation"/>
        /// </summary>
        public static ControlFlowGraph? GetControlFlowGraph(this IOperation operation,
            CancellationToken cancellationToken)
        {
            return operation switch
            {
                IMethodBodyOperation methodBody => ControlFlowGraph.Create(methodBody, cancellationToken),
                IConstructorBodyOperation constructorBody => ControlFlowGraph.Create(constructorBody, cancellationToken),
                IAnonymousFunctionOperation anonymousFunction => Create(anonymousFunction, anonymousFunction.Symbol, cancellationToken),
                ILocalFunctionOperation localFunctionOperation => Create(localFunctionOperation, localFunctionOperation.Symbol, cancellationToken),
                _ => null
            };

            static ControlFlowGraph? Create(IOperation operation, IMethodSymbol targetMethod, CancellationToken cancellationToken)
            {
                var originalOperation = operation;
                while (operation.Parent != null)
                    operation = operation.Parent;
                // Prevent Stackoverlow
                if (originalOperation == operation)
                    return null;

                var cfg = GetControlFlowGraph(operation, cancellationToken);
                if (cfg == null)
                    return null;

                return cfg.GetLocalFunctionCfg(targetMethod, cancellationToken);
            }
        }


        /// <summary>
        /// Helper method to verify the <paramref name="assignment"/> target matches <paramref name="targetSymbol"/>
        /// </summary>
        public static bool IsTargetMatch(this IOperation assignment, ISymbol targetSymbol)
        {
            ISymbol? symbol = GetTarget(assignment);
            if (symbol == null)
                return false;
            return SymbolEqualityComparer.Default.Equals(symbol, targetSymbol);

            static ISymbol? GetTarget(IOperation operation)
            {
                return operation switch
                {
                    IAssignmentOperation assignment => GetTarget(assignment.Target),
                    ILocalReferenceOperation localRef => localRef.Local,
                    IFieldReferenceOperation fieldRef => fieldRef.Field,
                    IPropertyReferenceOperation propRef => propRef.Property,
                    IParameterReferenceOperation paramRef => paramRef.Parameter,
                    _ => null
                };
            }
        }

        public static (IOperation Target, IOperation Value)? FindAssignmentTarget(this IOperation operation, ISymbol targetSymbol)
        {
            // Unwrap the assignment if it's inside a statement
            (IOperation? target, IOperation? value) = operation switch
            {
                IAssignmentOperation a => (a.Target, a.Value),
                IExpressionStatementOperation exprStmt => exprStmt.Operation.FindAssignmentTarget(targetSymbol) ?? (null!, null!),
                IInvocationOperation invocation => invocation.Arguments
                    .Where(x => x.Parameter?.RefKind is RefKind.Out or RefKind.Ref)
                    .Where(x => x.Value.IsTargetMatch(targetSymbol))
                    .Select(x => (x.Value, x.Value))
                    .LastOrDefault(),
                _ => (null, null)
            };
            if (target == null || value == null)
                return null;
            if (target.IsTargetMatch(targetSymbol))
                return (target, value);
            return null;
        }




    }
}

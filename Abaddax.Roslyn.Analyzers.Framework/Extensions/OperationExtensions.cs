using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Abaddax.Roslyn.Analyzers.Framework.Extensions
{
    public static class OperationExtensions
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
                // Prevent stack overflow
                if (originalOperation == operation)
                    return null;

                var cfg = GetControlFlowGraph(operation, cancellationToken);
                if (cfg == null)
                    return null;

                return cfg.GetLocalFunctionCfg(targetMethod, cancellationToken);
            }
        }

        public static TOperation? FindParent<TOperation>(this IOperation operation, bool includeSelf = true)
            where TOperation : class, IOperation
        {
            if (operation is TOperation self && includeSelf)
                return self;
            IOperation? current = operation;
            while (current != null)
            {
                if (current is TOperation parent)
                    return parent;
                current = current.Parent;
            }
            return null;
        }
        public static IEnumerable<TOperation> FindChilds<TOperation>(this IOperation operation, bool includeSelf = true)
             where TOperation : class, IOperation
        {
            if (operation is TOperation self && includeSelf)
                yield return self;
            foreach (var child in operation.ChildOperations)
            {
                foreach (var foundChild in child.FindChilds<TOperation>())
                    yield return foundChild;
            }
        }

    }
}

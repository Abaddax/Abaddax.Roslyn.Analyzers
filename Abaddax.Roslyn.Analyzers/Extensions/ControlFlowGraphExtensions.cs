using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Abaddax.Roslyn.Analyzers.Extensions
{
    internal static class ControlFlowGraphExtensions
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

    }
}

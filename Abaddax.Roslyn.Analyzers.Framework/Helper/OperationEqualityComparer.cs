using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;

namespace Abaddax.Roslyn.Analyzers.Framework.Helper
{
    public sealed class OperationEqualityComparer : IEqualityComparer<IOperation?>
    {
        /// <summary>
        /// Check for reference equality of the operations
        /// </summary>
        /// <remarks>
        /// This comparer might me unsuitable when the operations are retrieved from different sources.
        /// In this case prefer <see cref="SyntaxEquality"/>
        /// </remarks>
        public static readonly IEqualityComparer<IOperation?> Default = new OperationEqualityComparer(CompareMode.ReferenceEquality);
        /// <summary>
        /// Compares the operations bases on their syntax and child-operation syntax
        /// </summary>
        public static readonly IEqualityComparer<IOperation?> SyntaxEquality = new OperationEqualityComparer(CompareMode.SyntaxEquality | CompareMode.ChildOperationsEquality);


        private readonly CompareMode _compareMode;
        private OperationEqualityComparer(CompareMode compareMode)
        {
            _compareMode = compareMode;
        }

        public bool Equals(IOperation? x, IOperation? y)
        {
            if (x is null || y is null)
                return x is null && y is null;
            if (_compareMode == CompareMode.ReferenceEquality)
                return ReferenceEquals(x, y);
            if (x.Kind != y.Kind)
                return false;
            if (x is IFlowCaptureOperation flow1 && y is IFlowCaptureOperation flow2 && !flow1.Id.Equals(flow2.Id))
                return false;
            if (!SymbolEqualityComparer.Default.Equals(x.Type, y.Type))
                return false;
            if (_compareMode.HasFlag(CompareMode.SyntaxEquality))
            {
                var comparer = _compareMode.HasFlag(CompareMode.ParentSyntaxEquality)
                    ? SyntaxNodeEqualityComparer.IncludeParents
                    : SyntaxNodeEqualityComparer.Default;
                if (!comparer.Equals(x.Syntax, y.Syntax))
                    return false;
            }
            if (_compareMode.HasFlag(CompareMode.ChildOperationsEquality))
            {
                if (x.ChildOperations.Count != y.ChildOperations.Count)
                    return false;
                if (!x.ChildOperations.SequenceEqual(y.ChildOperations, this))
                    return false;
            }
            return true;
        }
        public int GetHashCode(IOperation? obj)
        {
            if (_compareMode == CompareMode.ReferenceEquality)
                return obj?.GetHashCode() ?? 0;
            if (obj is IFlowCaptureOperation flow)
                return flow.Id.GetHashCode();
            return obj?.Syntax.GetHashCode() ?? 0;
        }

        #region Helpers
        [Flags]
        private enum CompareMode
        {
            ReferenceEquality = 0b0000,
            SyntaxEquality = 0b0001,
            ParentSyntaxEquality = 0b0011,
            ChildOperationsEquality = 0b100
        }
        #endregion
    }
}

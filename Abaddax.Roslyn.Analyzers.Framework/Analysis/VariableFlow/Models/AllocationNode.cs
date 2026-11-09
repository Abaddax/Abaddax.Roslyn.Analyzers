using Abaddax.Roslyn.Analyzers.Framework.Helper;
using Microsoft.CodeAnalysis;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Models
{
    file sealed class AllocationNodeEqualityComparer : IEqualityComparer<AllocationNode>
    {
        public bool Equals(AllocationNode? x, AllocationNode? y)
        {
            if (x is null && y is null)
                return true;
            if (x is null || y is null)
                return false;
            if (x.Id != y.Id)
                return false;
            if (!SymbolEqualityComparer.Default.Equals(x.Type, y.Type))
                return false;
            return OperationEqualityComparer.Default.Equals(x.AllocationOperation, y.AllocationOperation);
        }
        public int GetHashCode(AllocationNode? obj)
        {
            if (obj == null)
                return 0;
            //Generate hash using ValueTuple
            return (
                    obj.Id.GetHashCode(),
                    SymbolEqualityComparer.Default.GetHashCode(obj.Type),
                    OperationEqualityComparer.Default.GetHashCode(obj.AllocationOperation)
                ).GetHashCode();
        }
    }

    /// <summary>
    /// A unique heap allocation.
    /// </summary>
    internal sealed class AllocationNode
    {
        public static readonly IEqualityComparer<AllocationNode> EqualityComparer = new AllocationNodeEqualityComparer();

        public int Id { get; }
        public ITypeSymbol Type { get; }
        public IOperation? AllocationOperation { get; }
        public AllocationNode(int id, ITypeSymbol type, IOperation? allocationOperation)
        {
            Id = id;
            Type = type ?? throw new ArgumentNullException(nameof(type));
            AllocationOperation = allocationOperation;
        }
        public override string ToString() => $"Alloc_{Id}[{Type.ToDisplayString()}] ({AllocationOperation})";
    }
}

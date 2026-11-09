using System.Text;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Models
{
    /// <summary>
    /// Reference-Type value: Points to one or more heap allocation sites.
    /// </summary>
    /// <remarks>
    /// Assignment copies the pointer (aliasing).
    /// </remarks>
    internal sealed class ReferenceTypeValue : IAbstractValue
    {
        public static readonly ReferenceTypeValue Empty = new(PointsToSet.Empty);

        public PointsToSet Targets { get; }
        public ReferenceTypeValue(PointsToSet targets)
        {
            Targets = targets;
        }

        /// <summary>
        /// Reference copy -> shares the underlying heap node
        /// </summary>
        public IAbstractValue Copy() => this;
        public IAbstractValue Merge(IAbstractValue other)
        {
            if (other is not ReferenceTypeValue otherRef)
            {
                if (other is null)
                    return this;
                if (IsEquivalentTo(Empty))
                    return other;
                if (other.IsEquivalentTo(ValueTypeValue.Empty))
                    return this;
                throw new Exception($"Unable to merge {other.GetType()} and {GetType()}");
            }
            return new ReferenceTypeValue(Targets.Union(otherRef.Targets));
        }
        public bool IsEquivalentTo(IAbstractValue other)
        {
            if (other is not ReferenceTypeValue otherRef)
                return false;
            return Targets.Nodes.SetEquals(otherRef.Targets.Nodes);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("ReferenceType: ");
            sb.Append(Targets.ToString());
            return sb.ToString();
        }
    }
}

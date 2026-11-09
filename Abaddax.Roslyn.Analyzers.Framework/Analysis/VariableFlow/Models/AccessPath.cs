using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Models
{
    /// <summary>
    /// The chain of member accesses starting from a root variable symbol.
    /// </summary>
    /// <remarks>
    /// Example: x -> .Prop1 -> .Prop2
    /// </remarks>
    internal readonly struct AccessPath
    {
        public AccessPathRoot Root { get; }
        public ImmutableArray<AccessSymbol> Members { get; }

        private AccessPath(AccessPathRoot root, ImmutableArray<AccessSymbol> members)
        {
            Root = root;
            Members = members;
        }

        public static AccessPath FromRoot(ISymbol root, IOperation? accessOperation = null)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            return new(new AccessPathRoot(new AccessSymbol(root, accessOperation)), ImmutableArray<AccessSymbol>.Empty);
        }
        public static AccessPath FromRoot(AccessPathRoot root)
        {
            return new(root, ImmutableArray<AccessSymbol>.Empty);
        }

        public AccessPath Append(AccessSymbol member) => new(Root, Members.Add(member));
        public AccessPath AppendRange(IEnumerable<AccessSymbol> members) => new(Root, Members.AddRange(members));

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Root);
            foreach (var member in Members)
            {
                sb.Append('.');
                sb.Append(member.ToString());
            }
            return sb.ToString();
        }
    }
}

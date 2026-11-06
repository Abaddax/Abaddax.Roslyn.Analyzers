using Abaddax.Roslyn.Analyzers.Framework.Helper;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Models
{
    /// <summary>
    /// Value-Type (Struct) value: Holds its own independent fields.
    /// </summary>
    /// <remarks>
    /// Assignment performs a structural clone (no aliasing).
    /// </remarks>
    internal sealed class ValueTypeValue : IAbstractValue
    {
        public static readonly ValueTypeValue Empty = new(ImmutableDictionary<AccessSymbol, IAbstractValue>.Empty);

        public ImmutableHashSet<IOperation> PossibleValues { get; }
        public ImmutableDictionary<AccessSymbol, IAbstractValue> Fields { get; }

        private ValueTypeValue(ImmutableHashSet<IOperation> possibleValues, ImmutableDictionary<AccessSymbol, IAbstractValue> fields)
        {
            PossibleValues = possibleValues?.WithComparer(OperationEqualityComparer.Default) ?? throw new ArgumentNullException(nameof(possibleValues));
            Fields = fields?.WithComparers(AccessSymbol.EqualityComparer) ?? throw new ArgumentNullException(nameof(fields));
        }
        public ValueTypeValue(IOperation value)
            : this([value ?? throw new ArgumentNullException(nameof(value))], ImmutableDictionary<AccessSymbol, IAbstractValue>.Empty) { }
        public ValueTypeValue(ImmutableDictionary<AccessSymbol, IAbstractValue> fields)
            : this(ImmutableHashSet<IOperation>.Empty, fields) { }

        /// <summary>
        /// Recursively clone all fields so mutations to 'y' don't affect 'x'
        /// </summary>
        /// <returns></returns>
        public IAbstractValue Copy()
        {
            var copiedFields = Fields.ToImmutableDictionary(
                x => x.Key,
                y => y.Value.Copy(),
                AccessSymbol.EqualityComparer);
            return new ValueTypeValue(PossibleValues, copiedFields);
        }
        public IAbstractValue Merge(IAbstractValue other)
        {
            if (other is not ValueTypeValue otherVal)
            {
                if (other is null)
                    return this;
                if (IsEquivalentTo(Empty))
                    return other;
                if (other.IsEquivalentTo(ReferenceTypeValue.Empty))
                    return this;
                throw new Exception($"Unable to merge {other.GetType()} and {GetType()}");
            }
            var allKeys = Fields.Keys.Union(otherVal.Fields.Keys, AccessSymbol.EqualityComparer);
            var mergeFields = Fields.ToBuilder();
            foreach (var key in allKeys)
            {
                if (otherVal.Fields.TryGetValue(key, out var field2))
                {
                    if (Fields.TryGetValue(key, out var field1))
                        mergeFields[key] = field1!.Merge(field2);
                    else
                    {
                        mergeFields[key] = field2;
                    }
                }
                //else ... nothing to do, its already in the current mergeFields state
            }
            return new ValueTypeValue(PossibleValues.Union(otherVal.PossibleValues), mergeFields.ToImmutable());
        }
        public bool IsEquivalentTo(IAbstractValue other)
        {
            if (other is not ValueTypeValue otherVal)
                return false;
            if (!PossibleValues.SetEquals(otherVal.PossibleValues))
                return false;
            if (Fields.Count != otherVal.Fields.Count)
                return false;
            foreach (var field in Fields)
            {
                if (!otherVal.Fields.TryGetValue(field.Key, out var otherField))
                    return false;
                var equalField = (field.Value, otherField) switch
                {
                    (not null, not null) => field.Value.IsEquivalentTo(otherField),
                    (null, null) => true,
                    _ => false
                };
                if (!equalField)
                    return false;
            }
            return true;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("ValueType: ");
            sb.Append("{");
            sb.Append(string.Join(", ", Fields.Select(x => $"{x.Key}:{x.Value}")));
            sb.Append("}");

            sb.Append(" (");
            sb.Append(string.Join(", ", PossibleValues.Select(x => x.Syntax.ToFullString())));
            sb.Append(")");
            return sb.ToString();
        }
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using System.Diagnostics.CodeAnalysis;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Models
{
    file sealed class AccessSymbolEqualityComparer : IEqualityComparer<AccessSymbol>
    {
        public bool Equals(AccessSymbol x, AccessSymbol y)
        {
            if (x.IsFlowCapture || y.IsFlowCapture)
            {
                return (x.CaptureId, y.CaptureId) switch
                {
                    (not null, not null) => x.CaptureId.Value.Equals(y.CaptureId.Value),
                    _ => false
                };
            }
            else
            {
                return SymbolEqualityComparer.Default.Equals(x.Symbol, y.Symbol);
            }
        }
        public int GetHashCode(AccessSymbol obj)
        {
            //Generate hash using ValueTuple
            return (
                    SymbolEqualityComparer.Default.GetHashCode(obj.Symbol),
                    obj.CaptureId?.GetHashCode() ?? 0
                ).GetHashCode();
        }
    }
    /// <summary>
    /// The <see cref="ISymbol"/> and <see cref="IOperation"/> of an access to a variable symbol.
    /// </summary>
    internal readonly struct AccessSymbol
    {
        public static readonly IEqualityComparer<AccessSymbol> EqualityComparer = new AccessSymbolEqualityComparer();

        [MemberNotNullWhen(true, nameof(CaptureId))]
        [MemberNotNullWhen(false, nameof(Symbol))]
        public bool IsFlowCapture => CaptureId != null;

        public ISymbol? Symbol { get; }
        public CaptureId? CaptureId { get; }
        public IOperation? AccessOperation { get; }

        public AccessSymbol(ISymbol symbol, IOperation? accessOperation = null)
        {
            Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
            AccessOperation = accessOperation;
        }
        public AccessSymbol(CaptureId captureId, IOperation? accessOperation = null)
        {
            CaptureId = captureId;
            AccessOperation = accessOperation;
        }

        public override string ToString()
        {
            if (IsFlowCapture)
                return $"FlowCapture_{CaptureId.Value.GetHashCode()}";
            return Symbol.Name;
        }
    }
}

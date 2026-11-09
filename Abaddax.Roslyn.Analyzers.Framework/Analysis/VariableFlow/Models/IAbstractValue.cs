namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.VariableFlow.Models
{
    /// <summary>
    /// The abstract value of a symbol.
    /// </summary>
    internal interface IAbstractValue
    {
        IAbstractValue Copy();
        IAbstractValue Merge(IAbstractValue other);
        bool IsEquivalentTo(IAbstractValue other);
    }
}

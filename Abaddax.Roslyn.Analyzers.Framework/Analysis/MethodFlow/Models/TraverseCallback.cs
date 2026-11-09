using Microsoft.CodeAnalysis;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.MethodFlow.Models
{
    /// <summary>
    /// Check if the traversal should be continued or aborted
    /// </summary>
    /// <returns><see langword="true"/> if the traversal should be continued, <see langword="false"/> to abort traversal</returns>
    public delegate bool TraverseCallback(IOperation currentOperation);

}

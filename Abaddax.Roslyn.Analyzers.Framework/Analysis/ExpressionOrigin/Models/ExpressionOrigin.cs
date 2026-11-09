using System.Diagnostics;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.ExpressionOrigin.Models
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public abstract class ExpressionOrigin
    {
        public abstract string ToFullString();

        private string DebuggerDisplay
        {
            get
            {
                return ToFullString().Replace("\r", "").Replace("\n", "");
            }
        }
    }
}

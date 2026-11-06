using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.MethodFlow.Models
{
    internal readonly struct CallerInfo
    {
        public IOperation CallerParamRef { get; }
        public IInvocationOperation? ParentInvocation { get; }
        public bool IsForwardedParam { get; }

        public CallerInfo(IOperation callerParamRef, IInvocationOperation? parentInvocation, bool isForwardedParam)
        {
            CallerParamRef = callerParamRef ?? throw new ArgumentNullException(nameof(callerParamRef));
            ParentInvocation = parentInvocation;
            IsForwardedParam = isForwardedParam;
        }
    }
}

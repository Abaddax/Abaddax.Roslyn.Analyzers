using Microsoft.CodeAnalysis;

namespace Abaddax.Roslyn.Analyzers.Framework.Analysis.MethodFlow.Models
{
    internal readonly struct AssignmentTargetInfo
    {
        public IOperation Target { get; }
        public IOperation Value { get; }

        public bool IsBlocked => Target == null || Value == null;

        public static AssignmentTargetInfo Blocked => default;

        public AssignmentTargetInfo(IOperation target, IOperation value)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }
        public static explicit operator AssignmentTargetInfo((IOperation Target, IOperation Value) info)
        {
            return new AssignmentTargetInfo(info.Target, info.Value);
        }
    }
}

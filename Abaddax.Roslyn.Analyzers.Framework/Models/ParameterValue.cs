using Microsoft.CodeAnalysis;

namespace Abaddax.Roslyn.Analyzers.Framework.Models
{
    public readonly struct ParameterValue
    {
        public IOperation Parameter { get; }
        public Optional<object?> ConstValue => Parameter.ConstantValue;

        public ParameterValue(IOperation parameterValue)
        {
            Parameter = parameterValue ?? throw new ArgumentNullException(nameof(parameterValue));
        }
    }
}

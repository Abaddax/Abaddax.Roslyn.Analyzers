using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Abaddax.Roslyn.Analyzers.Extensions
{
    internal static class OptionsExtensions
    {
        public static AnalyzerConfigOptions GetGlobalOptions(this AnalyzerOptions options, SyntaxTree sourceTree)
        {
            return options.AnalyzerConfigOptionsProvider.GetOptions(sourceTree);
        }
        public static bool IsSet(this AnalyzerConfigOptions options, string diagnostic, string property, bool defaultValue = false)
        {
            if (options.TryGetValue($"dotnet_code_quality.{diagnostic}.{property}", out var valueStr) &&
               bool.TryParse(valueStr, out var value))
            {
                return value;
            }
            return defaultValue;
        }
        public static bool IsEnabled(this AnalyzerConfigOptions options, string diagnostic, bool defaultValue = false)
        {
            return options.IsSet(diagnostic, "enabled", defaultValue);
        }
    }
}

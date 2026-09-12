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


        public delegate bool TryParseDelegate<T>(string s, out T value);

        public static bool TryGetValue<T>(this AnalyzerConfigOptions options, string key, TryParseDelegate<T> parser, out T value)
        {
            value = default!;
            if (!options.TryGetValue(key, out var strValue))
                return false;
            return parser.Invoke(strValue, out value);
        }
        public static T GetValueOrDefault<T>(this AnalyzerConfigOptions options, string key, TryParseDelegate<T> parser, T defaultValue)
        {
            if (options.TryGetValue(key, parser, out var value))
                return value;
            return defaultValue;
        }

        public static bool IsSet(this AnalyzerConfigOptions options, string diagnostic, string property, bool defaultValue = false)
        {
            if (options.TryGetValue<bool>($"build_property.AbaddaxRoslynAnalyzers{diagnostic}{property.Replace("_", "")}".ToLowerInvariant(), bool.TryParse, out var value))
            {
                return value;
            }
            if (options.TryGetValue<bool>($"dotnet_code_quality.{diagnostic}.{property}", bool.TryParse, out value))
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

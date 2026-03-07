// DictionaryExtensions.cs
// Polyfill for Dictionary.GetValueOrDefault which is unavailable on .NET Framework 4.8.
// On .NET 8+ this method exists natively; this extension is only compiled for net48.
#if REVIT2023_2024
using System.Collections.Generic;

namespace ToolsByGimhan.RebarGenerator.Helpers
{
    internal static class DictionaryExtensions
    {
        /// <summary>Returns the value for <paramref name="key"/>, or <paramref name="defaultValue"/> if not found.</summary>
        public static TValue GetValueOrDefault<TKey, TValue>(
            this Dictionary<TKey, TValue> dict, TKey key, TValue defaultValue)
        {
            return dict != null && dict.TryGetValue(key, out var val) ? val : defaultValue;
        }

        /// <summary>Returns the value for <paramref name="key"/>, or default(TValue) if not found.</summary>
        public static TValue GetValueOrDefault<TKey, TValue>(
            this Dictionary<TKey, TValue> dict, TKey key)
        {
            return dict != null && dict.TryGetValue(key, out var val) ? val : default(TValue);
        }

        /// <summary>Returns the value for <paramref name="key"/>, or default(TValue) if not found. (IDictionary overload)</summary>
        public static TValue GetValueOrDefault<TKey, TValue>(
            this IDictionary<TKey, TValue> dict, TKey key)
        {
            return dict != null && dict.TryGetValue(key, out var val) ? val : default(TValue);
        }
    }
}
#endif


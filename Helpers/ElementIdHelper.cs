// ──────────────────────────────────────────────────────────────────────────
// ElementIdHelper.cs
// Abstracts the breaking change between Revit ≤ 2024 (IntegerValue : int)
// and Revit 2025+ (Value : long) on ElementId.
// Use this helper everywhere instead of calling IntegerValue / Value directly.
// ──────────────────────────────────────────────────────────────────────────
using Autodesk.Revit.DB;

#pragma warning disable CS0618 // ElementId.IntegerValue is obsolete in Revit 2024

namespace ToolsByGimhan.RebarGenerator.Helpers
{
    internal static class ElementIdHelper
    {
        /// <summary>
        /// Returns true when <paramref name="id"/> corresponds to
        /// <paramref name="cat"/>, using the correct accessor for the
        /// current Revit version.
        /// </summary>
        public static bool IsCategory(ElementId id, BuiltInCategory cat)
        {
#if REVIT2025_2026
            return id.Value == (long)cat;
#else
            return id.IntegerValue == (int)cat;
#endif
        }

        /// <summary>Null-safe overload that also checks element.Category.</summary>
        public static bool IsCategory(Element? element, BuiltInCategory cat)
        {
            if (element?.Category?.Id is null) return false;
            return IsCategory(element.Category.Id, cat);
        }

        /// <summary>
        /// Returns the raw numeric value of an ElementId as <see langword="long"/>
        /// regardless of Revit version.
        /// </summary>
        public static long GetValue(ElementId id)
        {
#if REVIT2025_2026
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }

        /// <summary>
        /// Returns InvalidElementId for any Revit version.
        /// </summary>
        public static ElementId InvalidElementId
        {
            get
            {
#if REVIT2025_2026
                return ElementId.InvalidElementId;
#else
                return ElementId.InvalidElementId;
#endif
            }
        }
    }
}

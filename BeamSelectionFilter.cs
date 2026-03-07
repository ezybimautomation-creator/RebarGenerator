// BeamSelectionFilter.cs — mirrors Python BeamSelectionFilter & FootingSelectionFilter
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using ToolsByGimhan.RebarGenerator.Helpers;

namespace ToolsByGimhan.RebarGenerator
{
    /// <summary>Filters Revit selections strictly by a specific category.</summary>
    public sealed class CategorySelectionFilter : ISelectionFilter
    {
        private readonly BuiltInCategory _targetCategory;

        public CategorySelectionFilter(BuiltInCategory targetCategory)
        {
            _targetCategory = targetCategory;
        }

        public bool AllowElement(Element elem)
        {
            if (elem?.Category == null) return false;
            return ElementIdHelper.IsCategory(elem.Category.Id, _targetCategory);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }
}

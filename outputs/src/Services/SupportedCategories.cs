using Autodesk.Revit.DB;

namespace IFCInfo
{
    internal static class SupportedCategories
    {
        internal static bool Contains(long id) => id == (long)BuiltInCategory.OST_DuctCurves
            || id == (long)BuiltInCategory.OST_DuctFitting
            || id == (long)BuiltInCategory.OST_PipeCurves
            || id == (long)BuiltInCategory.OST_Conduit
            || id == (long)BuiltInCategory.OST_CableTray;
    }
}

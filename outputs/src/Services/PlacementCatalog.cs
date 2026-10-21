using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Electrical;
namespace IFCInfo
{
    internal static class PlacementCatalog
    {
        internal static void Configure(IFCInfoWindow window,Document doc)
        {
            window.LoadPlacementFamily = (path, sourceCategoryId) =>
            {
                if (!SupportedCategories.Contains(sourceCategoryId))
                    throw new InvalidOperationException("This category is not supported.");
                DuctCreation.RunTransaction(doc, "IFC - Load placement family", () =>
                {
                    if (!doc.LoadFamily(path, out Family loaded))
                        throw new InvalidOperationException("Could not load the family. It may already exist; choose an existing type or another .rfa file.");
                    if (sourceCategoryId == 0 || loaded.FamilyCategory?.Id.Number() != sourceCategoryId)
                        throw new InvalidOperationException("The selected family does not match the IFC source category. Loading was rolled back; select a family in the correct category.");
                });
                Configure(window, doc);
                return window.ReplacementTypes;
            };
            var types=new List<ReplacementTypeOption>();
            foreach (ElementType type in new FilteredElementCollector(doc).WhereElementIsElementType().Cast<ElementType>())
            {
                if (type.Category==null || !SupportedCategories.Contains(type.Category.Id.Number())) continue;
                var option=new ReplacementTypeOption { Id=type.Id.Number(),CategoryId=type.Category.Id.Number(),CategoryName=type.Category.Name,
                    Label=type.FamilyName+" : "+type.Name,Kind="Unsupported",Placement="This category requires a specialized builder that is not available in this version." };
                if (type is FamilySymbol symbol)
                {
                    var placement=symbol.Family.FamilyPlacementType;
                    option.Kind="Family";
                    option.PlacementMode=placement.ToString();
                    option.Supported=placement==FamilyPlacementType.OneLevelBased || placement==FamilyPlacementType.OneLevelBasedHosted ||
                        placement==FamilyPlacementType.WorkPlaneBased || placement==FamilyPlacementType.TwoLevelsBased ||
                        placement==FamilyPlacementType.CurveBased || placement==FamilyPlacementType.CurveDrivenStructural || placement==FamilyPlacementType.Adaptive;
                    option.NeedsHost=placement==FamilyPlacementType.OneLevelBasedHosted || placement==FamilyPlacementType.WorkPlaneBased;
                    option.Placement=placement==FamilyPlacementType.Adaptive ? "Adaptive: pick control points in the model for each source." :
                        option.NeedsHost ? "Select a planar host face in the host model after confirming." :
                        placement==FamilyPlacementType.TwoLevelsBased ? "Two-level family: select the base and top levels." :
                        placement==FamilyPlacementType.CurveBased || placement==FamilyPlacementType.CurveDrivenStructural ? "Line-based family: use the source path, or pick two points if it cannot be read." :
                        option.Supported ? "Place at the source point; use the geometry center if no insertion point exists." : "Unsupported placement type: "+placement;
                }
                else if (type is DuctType) option.Kind="Duct";
                else if (type is PipeType) option.Kind="Pipe";
                else if (type is CableTrayType) option.Kind="CableTray";
                else if (type is ConduitType) option.Kind="Conduit";
                else if (type is WallType wall && wall.Kind==WallKind.Basic) option.Kind="Wall";
                else if (type is FloorType) option.Kind="Floor";
                else if (type is RoofType) option.Kind="Roof";
                if (option.Kind!="Family" && option.Kind!="Unsupported")
                {
                    option.Supported=true;
                    option.Placement=option.Kind=="Wall" ? "Native wall: a uniform vertical wall solid; thickness must match the wall type." :
                        option.Kind=="Floor" || option.Kind=="Roof" ? "Native element from horizontal floor/roof boundaries; geometry is verified after creation." :
                        "Native run from the source centerline and dimensions; a new length can be entered.";
                }
                types.Add(option);
            }
            foreach (var category in new FilteredElementCollector(doc).WhereElementIsNotElementType().Where(e=>e.Category!=null && SupportedCategories.Contains(e.Category.Id.Number()))
                .Select(e=>e.Category).GroupBy(c=>c.Id.Number()).Select(g=>g.First()))
                if (!types.Any(t=>t.CategoryId==category.Id.Number())) types.Add(new ReplacementTypeOption { CategoryId=category.Id.Number(),CategoryName=category.Name,
                    Label="No placeable type",Kind="Unsupported",Placement="This category contains elements but has no type available for placement." });
            window.ReplacementTypes=types.OrderBy(t=>t.CategoryName).ThenBy(t=>t.Label).ToList();
            window.ReplacementLevels=new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l=>l.ProjectElevation)
                .Select(l=>new ReplacementLevelOption { Id=l.Id.Number(),Label=l.Name }).ToList();
            window.ReplacementSystems=new FilteredElementCollector(doc).OfClass(typeof(MEPSystemType)).Cast<MEPSystemType>()
                .Where(t=>t is MechanicalSystemType || t is PipingSystemType)
                .Select(t=>new ReplacementTypeOption { Id=t.Id.Number(),Label=t.Name,Kind=t is PipingSystemType ? "Pipe" : "Duct" }).ToList();
        }
    }
}

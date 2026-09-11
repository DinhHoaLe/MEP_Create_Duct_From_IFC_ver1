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
            var types=new List<ReplacementTypeOption>();
            foreach (ElementType type in new FilteredElementCollector(doc).WhereElementIsElementType().Cast<ElementType>())
            {
                if (type.Category==null || type.Category.CategoryType!=CategoryType.Model) continue;
                var option=new ReplacementTypeOption { Id=type.Id.Value,CategoryId=type.Category.Id.Value,CategoryName=type.Category.Name,
                    Label=type.FamilyName+" : "+type.Name,Kind="Unsupported",Placement="Category này cần cách dựng chuyên biệt chưa có trong bản này." };
                if (type is FamilySymbol symbol)
                {
                    var placement=symbol.Family.FamilyPlacementType;
                    option.Kind="Family";
                    option.PlacementMode=placement.ToString();
                    option.Supported=placement==FamilyPlacementType.OneLevelBased || placement==FamilyPlacementType.OneLevelBasedHosted ||
                        placement==FamilyPlacementType.WorkPlaneBased || placement==FamilyPlacementType.TwoLevelsBased ||
                        placement==FamilyPlacementType.CurveBased || placement==FamilyPlacementType.CurveDrivenStructural || placement==FamilyPlacementType.Adaptive;
                    option.NeedsHost=placement==FamilyPlacementType.OneLevelBasedHosted || placement==FamilyPlacementType.WorkPlaneBased;
                    option.Placement=placement==FamilyPlacementType.Adaptive ? "Adaptive: chọn điểm điều khiển trong model cho từng nguồn." :
                        option.NeedsHost ? "Chọn một mặt host phẳng trong model chính sau khi xác nhận." :
                        placement==FamilyPlacementType.TwoLevelsBased ? "Family hai Level: chọn Level dưới và Level trên." :
                        placement==FamilyPlacementType.CurveBased || placement==FamilyPlacementType.CurveDrivenStructural ? "Family theo đường: dùng đường nguồn; nếu không đọc được sẽ yêu cầu chọn 2 điểm." :
                        option.Supported ? "Đặt theo điểm nguồn; dùng tâm hình học nếu nguồn không có điểm đặt." : "Chưa hỗ trợ kiểu đặt: "+placement;
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
                    option.Placement=option.Kind=="Wall" ? "Wall native: khối tường thẳng đứng, tiết diện đều; bề dày phải khớp Wall Type." :
                        option.Kind=="Floor" || option.Kind=="Roof" ? "Native từ biên khối sàn/mái phẳng nằm ngang; kiểm tra hình học sau tạo." :
                        "Tuyến native theo đường tim và kích thước nguồn; có thể nhập chiều dài mới.";
                }
                types.Add(option);
            }
            foreach (var category in new FilteredElementCollector(doc).WhereElementIsNotElementType().Where(e=>e.Category?.CategoryType==CategoryType.Model)
                .Select(e=>e.Category).GroupBy(c=>c.Id.Value).Select(g=>g.First()))
                if (!types.Any(t=>t.CategoryId==category.Id.Value)) types.Add(new ReplacementTypeOption { CategoryId=category.Id.Value,CategoryName=category.Name,
                    Label="Không có type có thể đặt",Kind="Unsupported",Placement="Category hiện có phần tử nhưng không có type để đặt mới." });
            window.ReplacementTypes=types.OrderBy(t=>t.CategoryName).ThenBy(t=>t.Label).ToList();
            window.ReplacementLevels=new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l=>l.ProjectElevation)
                .Select(l=>new ReplacementLevelOption { Id=l.Id.Value,Label=l.Name }).ToList();
            window.ReplacementSystems=new FilteredElementCollector(doc).OfClass(typeof(MEPSystemType)).Cast<MEPSystemType>()
                .Where(t=>t is PipingSystemType || t is MechanicalSystemType)
                .Select(t=>new ReplacementTypeOption { Id=t.Id.Value,Label=t.Name,Kind=t is PipingSystemType ? "Pipe" : "Duct" }).ToList();
        }
    }
}

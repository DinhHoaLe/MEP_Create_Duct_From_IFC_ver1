using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;



namespace IFCInfo
{
    /// <summary>Đọc Category, parameter hệ thống và ghép dữ liệu IFC theo GUID.</summary>
    internal static class CategoryReader
    {
        public static void Configure(IFCInfoWindow window, Document linkedDoc, string ifcPath)
        {
            if (linkedDoc == null)
                return;
            using (var collector = new FilteredElementCollector(linkedDoc))
                window.Categories = collector.WhereElementIsNotElementType().ToElements()
                    .Where(element => element.Category != null && element.Category.CategoryType == CategoryType.Model)
                    .GroupBy(element => element.Category.Id.Value)
                    .Select(group => new CategoryOption { Id = group.Key, Name = group.First().Category.Name })
                    .OrderBy(category => category.Name).ToList();
            window.LoadCategory = category => SetCategoryCount(window, linkedDoc, ifcPath, category);
        }

        private static void SetCategoryCount(IFCInfoWindow window, Document linkedDoc, string ifcPath, CategoryOption category)
        {
            window.AirTerminalCount = null;
            window.AirTerminalError = null;
            window.AirTerminals = new List<AirTerminalRow>();
            window.CanReplaceCategory = category.Id == (long)BuiltInCategory.OST_DuctTerminal;
            bool isDuct = category.Id == (long)BuiltInCategory.OST_DuctCurves ||
                category.Id == (long)BuiltInCategory.OST_FlexDuctCurves;
            window.CanCreateDucts = isDuct;
            bool readIfc = window.CanReplaceCategory || isDuct;
            window.IfcSourceStatus = "Dữ liệu parameter trong Revit link.";
            if (linkedDoc == null)
            {
                window.AirTerminalError = "Không đọc được model link. Hãy load link rồi chạy lại tool.";
                return;
            }
            try
            {
                IfcSourceReader ifc = null;
                if (readIfc && !File.Exists(ifcPath))
                {
                    var picker = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "Chọn IFC gốc của link để đọc hệ thống và cao độ",
                        Filter = "IFC STEP (*.ifc)|*.ifc",
                        CheckFileExists = true,
                        Multiselect = false
                    };
                    if (picker.ShowDialog() == true)
                        ifcPath = picker.FileName;
                }
                if (readIfc && File.Exists(ifcPath))
                {
                    try
                    {
                        ifc = IfcSourceReader.Read(ifcPath);
                    }
                    catch (Exception ex) { window.IfcSourceStatus = "Không đọc được IFC gốc: " + ex.Message; }
                }
                else if (readIfc)
                    window.IfcSourceStatus = "Chưa chọn IFC gốc. Chỉ đọc parameter trong Revit; chưa có cao độ IFC.";
                var sourceElements = ifc == null ? null : (isDuct ? ifc.Ducts : ifc.Terminals);
                int matched = 0;
                // Count instances in the selected linked document, not the host/view.
                using (var collector = new FilteredElementCollector(linkedDoc))
                {
                    var terminals = collector
                        .OfCategoryId(new ElementId(category.Id))
                        .WhereElementIsNotElementType()
                        .ToElements();
                    window.AirTerminalCount = terminals.Count;
                    var rows = new List<AirTerminalRow>();
                    foreach (Element element in terminals)
                    {
                        string guid = ReadIfcGuid(element);
                        var row = new AirTerminalRow
                        {
                            ElementId = element.Id.ToString(),
                            Name = element.Name,
                            IfcGuid = guid,
                            SystemType = ReadSystemValue(element, BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM, "System Type"),
                            SystemName = ReadSystemValue(element, BuiltInParameter.RBS_SYSTEM_NAME_PARAM, "System Name"),
                            Elevation = "Không có thông tin",
                            DataSource = ifc == null ? "Parameter Revit" : "Chưa khớp IFC GUID; parameter Revit"
                        };
                        IfcTerminalSource sourceRow;
                        if (sourceElements != null && sourceElements.TryGetValue(guid, out sourceRow))
                        {
                            matched++;
                            if (isDuct)
                                row.DuctSource = sourceRow;
                            if (!string.IsNullOrWhiteSpace(sourceRow.SystemName))
                                row.SystemName = sourceRow.SystemName;
                            if (!string.IsNullOrWhiteSpace(sourceRow.SystemType))
                                row.SystemType = sourceRow.SystemType;
                            row.Elevation = sourceRow.ElevationMm.HasValue
                                ? sourceRow.ElevationMm.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                                : "Không có thông tin";
                            row.DataSource = "Khớp IFC GUID; ưu tiên IFC gốc";
                        }
                        rows.Add(row);
                    }
                    window.AirTerminals = rows.OrderBy(row => row.SystemType).ThenBy(row => row.SystemName)
                      .ThenBy(row => row.ElementId).ToList();
                    if (ifc != null)
                        window.IfcSourceStatus = string.Format("IFC gốc: {0}\nKhớp GUID: {1}/{2} phần tử trong link. IFC có {3} phần tử nguồn thuộc nhóm {4}. " +
                            "Dữ liệu lấy từ phiên bản IFC trên đĩa; hãy reload link nếu IFC đã thay đổi.",
                            ifcPath, matched, terminals.Count, sourceElements.Count, isDuct ? "Duct" : "Air Terminal");
                }
            }
            catch (Exception ex)
            {
                window.AirTerminalError = "Không thể đọc Category: " + ex.Message;
            }
        }

        private static string ReadIfcGuid(Element element)
        {
            foreach (Parameter parameter in element.Parameters)
            {
                string name = parameter.Definition.Name.Replace(" ", "").Replace("_", "");
                if (name.Equals("IfcGUID", StringComparison.OrdinalIgnoreCase))
                {
                    string value = ParameterText(parameter, element.Document);
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
            }
            var shape = element as DirectShape;
            // Use exact GUID matching only; never infer identity from display names or IDs.
            return shape == null ? "" : (shape.ApplicationDataId ?? "");
        }

        private static string ReadSystemValue(Element element, BuiltInParameter builtin, string label)
        {
            string value = ParameterText(element.get_Parameter(builtin), element.Document);
            if (!string.IsNullOrWhiteSpace(value))
                return value;

            // IFC properties may be stored on the instance or its type, with a pset prefix.
            foreach (Element candidate in new[] { element, element.Document.GetElement(element.GetTypeId()) })
            {
                if (candidate == null)
                    continue;
                var values = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Parameter parameter in candidate.Parameters)
                {
                    string name = parameter.Definition.Name;
                    string compact = name.Replace(" ", "").Replace("_", "");
                    string expected = label.Replace(" ", "");
                    if (!compact.Equals(expected, StringComparison.OrdinalIgnoreCase) &&
                        !compact.EndsWith("." + expected, StringComparison.OrdinalIgnoreCase) &&
                        !compact.EndsWith(":" + expected, StringComparison.OrdinalIgnoreCase))
                        continue;
                    value = ParameterText(parameter, element.Document);
                    if (!string.IsNullOrWhiteSpace(value))
                        values.Add(value);
                }
                if (values.Count > 0)
                    return string.Join("; ", values);
            }
            return "Không có thông tin";
        }

        private static string ParameterText(Parameter parameter, Document doc)
        {
            if (parameter == null || !parameter.HasValue)
                return null;
            if (parameter.StorageType == StorageType.String)
                return parameter.AsString()?.Trim();
            if (parameter.StorageType == StorageType.ElementId)
            {
                Element referenced = doc.GetElement(parameter.AsElementId());
                return referenced == null ? null : referenced.Name;
            }
            return parameter.AsValueString()?.Trim();
        }


    }
}

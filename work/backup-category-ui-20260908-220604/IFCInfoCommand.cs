using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace IFCInfo
{
    [Transaction(TransactionMode.Manual)]
    public class IFCInfoCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData,
            ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null)
            {
                TaskDialog.Show("IFC Info", "Hãy mở một project Revit trước.");
                return Result.Cancelled;
            }

            try
            {
                Reference picked = uiDoc.Selection.PickObject(
                    ObjectType.Element, new LinkFilter(),
                    "Click chọn IFC link (Esc để hủy)");

                Document doc = uiDoc.Document;
                var link = (RevitLinkInstance)doc.GetElement(picked.ElementId);
                var linkType = (RevitLinkType)doc.GetElement(link.GetTypeId());
                Document linkedDoc = link.GetLinkDocument();
                string linkPath = GetLinkPath(linkType, linkedDoc);
                string original = null;

                if (linkedDoc != null && linkedDoc.ProjectInformation != null)
                {
                    Parameter p = linkedDoc.ProjectInformation
                        .LookupParameter("Original IFC File Name");
                    if (p != null && p.StorageType == StorageType.String)
                        original = p.AsString();
                }

                string ifcPath;
                string source;
                string note;
                if (!string.IsNullOrWhiteSpace(original))
                {
                    ifcPath = original.Trim();
                    source = "Project Information → Original IFC File Name";
                    note = "Thông tin lưu khi import/link; đường dẫn có thể đã thay đổi.";
                }
                else if (linkPath.EndsWith(".ifc.rvt", StringComparison.OrdinalIgnoreCase))
                {
                    ifcPath = linkPath.Substring(0, linkPath.Length - 4);
                    source = "Suy luận từ tên file trung gian .ifc.RVT";
                    note = "Tên và đường dẫn IFC là suy luận, chưa xác minh file nguồn.";
                }
                else if (linkPath.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase))
                {
                    ifcPath = linkPath;
                    source = "Đường dẫn tham chiếu của link";
                    note = "Chưa kiểm tra file IFC nguồn có còn tồn tại hay không.";
                }
                else
                {
                    var emptyWindow = new IFCInfoWindow("Chưa xác định được IFC", "",
                        DisplayPath(linkPath), link.Id.ToString(), "Thiếu thông tin IFC",
                        "Link có thể là RVT thường, file trung gian đã đổi tên hoặc thiếu metadata IFC.", false);
                    new System.Windows.Interop.WindowInteropHelper(emptyWindow).Owner =
                        commandData.Application.MainWindowHandle;
                    SetAirTerminalCount(emptyWindow, linkedDoc, "");
                    AirTerminalReplacement.Configure(emptyWindow, doc);
                    emptyWindow.ShowDialog();
                    AirTerminalReplacement.Execute(uiDoc, link, emptyWindow);
                    return Result.Succeeded;
                }

                // Split manually to also support server paths containing '/'.
                string normalized = ifcPath.Replace('\\', '/').TrimEnd('/');
                string fileName = normalized.Substring(normalized.LastIndexOf('/') + 1);

                var dialog = new IFCInfoWindow(fileName, ifcPath, DisplayPath(linkPath),
                    link.Id.ToString(), source, note, true);
                new System.Windows.Interop.WindowInteropHelper(dialog).Owner =
                    commandData.Application.MainWindowHandle;
                SetAirTerminalCount(dialog, linkedDoc, ifcPath);
                AirTerminalReplacement.Configure(dialog, doc);
                dialog.ShowDialog();
                AirTerminalReplacement.Execute(uiDoc, link, dialog);
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = "IFC Info: " + ex.Message;
                return Result.Failed;
            }
        }

        private static void SetAirTerminalCount(IFCInfoWindow window, Document linkedDoc, string ifcPath)
        {
            if (linkedDoc == null)
            {
                window.AirTerminalError = "Không đọc được model link. Hãy load link rồi chạy lại tool.";
                return;
            }
            try
            {
                IfcSourceReader ifc = null;
                if (!File.Exists(ifcPath))
                {
                    var picker = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "Chọn IFC gốc của link để đọc hệ thống và cao độ",
                        Filter = "IFC STEP (*.ifc)|*.ifc", CheckFileExists = true, Multiselect = false
                    };
                    if (picker.ShowDialog() == true) ifcPath = picker.FileName;
                }
                if (File.Exists(ifcPath))
                {
                    try { ifc = IfcSourceReader.Read(ifcPath); }
                    catch (Exception ex) { window.IfcSourceStatus = "Không đọc được IFC gốc: " + ex.Message; }
                }
                else window.IfcSourceStatus = "Chưa chọn IFC gốc. Chỉ đọc parameter trong Revit; chưa có cao độ IFC.";
                int matched = 0;
                // Count instances in the selected linked document, not the host/view.
                using (var collector = new FilteredElementCollector(linkedDoc))
                {
                    var terminals = collector
                        .OfCategory(BuiltInCategory.OST_DuctTerminal)
                        .WhereElementIsNotElementType()
                        .ToElements();
                    window.AirTerminalCount = terminals.Count;
                    var rows = new List<AirTerminalRow>();
                    foreach (Element element in terminals)
                    {
                        string guid = ReadIfcGuid(element);
                        var row = new AirTerminalRow
                        {
                            ElementId = element.Id.ToString(), Name = element.Name, IfcGuid = guid,
                            SystemType = ReadSystemValue(element, BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM, "System Type"),
                            SystemName = ReadSystemValue(element, BuiltInParameter.RBS_SYSTEM_NAME_PARAM, "System Name"),
                            Elevation = "Không có thông tin",
                            DataSource = ifc == null ? "Parameter Revit" : "Chưa khớp IFC GUID; parameter Revit"
                        };
                        IfcTerminalSource sourceRow;
                        if (ifc != null && ifc.Terminals.TryGetValue(guid, out sourceRow))
                        {
                            matched++;
                            if (!string.IsNullOrWhiteSpace(sourceRow.SystemName)) row.SystemName = sourceRow.SystemName;
                            if (!string.IsNullOrWhiteSpace(sourceRow.SystemType)) row.SystemType = sourceRow.SystemType;
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
                        window.IfcSourceStatus = string.Format("IFC gốc: {0}\nKhớp GUID: {1}/{2} phần tử trong link. IFC có {3} Air Terminals. " +
                            "Dữ liệu lấy từ phiên bản IFC trên đĩa; hãy reload link nếu IFC đã thay đổi.",
                            ifcPath, matched, terminals.Count, ifc.Terminals.Count);
                }
            }
            catch (Exception ex)
            {
                window.AirTerminalError = "Không thể đếm Air Terminals: " + ex.Message;
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
                    if (!string.IsNullOrWhiteSpace(value)) return value;
                }
            }
            var shape = element as DirectShape;
            // Use exact GUID matching only; never infer identity from display names or IDs.
            return shape == null ? "" : (shape.ApplicationDataId ?? "");
        }

        private static string ReadSystemValue(Element element, BuiltInParameter builtin, string label)
        {
            string value = ParameterText(element.get_Parameter(builtin), element.Document);
            if (!string.IsNullOrWhiteSpace(value)) return value;

            // IFC properties may be stored on the instance or its type, with a pset prefix.
            foreach (Element candidate in new[] { element, element.Document.GetElement(element.GetTypeId()) })
            {
                if (candidate == null) continue;
                var values = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Parameter parameter in candidate.Parameters)
                {
                    string name = parameter.Definition.Name;
                    string compact = name.Replace(" ", "").Replace("_", "");
                    string expected = label.Replace(" ", "");
                    if (!compact.Equals(expected, StringComparison.OrdinalIgnoreCase) &&
                        !compact.EndsWith("." + expected, StringComparison.OrdinalIgnoreCase) &&
                        !compact.EndsWith(":" + expected, StringComparison.OrdinalIgnoreCase)) continue;
                    value = ParameterText(parameter, element.Document);
                    if (!string.IsNullOrWhiteSpace(value)) values.Add(value);
                }
                if (values.Count > 0) return string.Join("; ", values);
            }
            return "Không có thông tin";
        }

        private static string ParameterText(Parameter parameter, Document doc)
        {
            if (parameter == null || !parameter.HasValue) return null;
            if (parameter.StorageType == StorageType.String)
                return parameter.AsString()?.Trim();
            if (parameter.StorageType == StorageType.ElementId)
            {
                Element referenced = doc.GetElement(parameter.AsElementId());
                return referenced == null ? null : referenced.Name;
            }
            return parameter.AsValueString()?.Trim();
        }

        private static string GetLinkPath(RevitLinkType linkType, Document linkedDoc)
        {
            try
            {
                ExternalFileReference fileRef = linkType.GetExternalFileReference();
                if (fileRef != null)
                {
                    string path = ModelPathUtils.ConvertModelPathToUserVisiblePath(
                        fileRef.GetAbsolutePath());
                    if (!string.IsNullOrWhiteSpace(path))
                        return path;
                }
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                // Server/cloud links may not expose an external file reference.
            }
            return linkedDoc == null ? string.Empty : linkedDoc.PathName ?? string.Empty;
        }

        private static string DisplayPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? "Không đọc được" : path;
        }

        private sealed class LinkFilter : ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                return element is RevitLinkInstance;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}

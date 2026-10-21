using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;



namespace IFCInfo
{
    /// <summary>Read categories and system parameters, then match IFC data by GUID.</summary>
    internal static class CategoryReader
    {
        public static void Configure(IFCInfoWindow window, Document linkedDoc, LinkOption linkOption)
        {
            if (linkedDoc == null)
                return;
            using (var collector = new FilteredElementCollector(linkedDoc))
                window.Categories = collector.WhereElementIsNotElementType().ToElements()
                    .Where(element => element.Category != null && SupportedCategories.Contains(element.Category.Id.Number()))
                    .GroupBy(element => element.Category.Id.Number())
                    .Select(group => new CategoryOption { Id = group.Key, Name = group.First().Category.Name })
                    .OrderBy(category => category.Name).ToList();
            var previewCache=new Dictionary<long,IfcPreviewMesh>();
            window.LoadPreview = row =>
            {
                if (row==null || !long.TryParse(row.ElementId,out long id)) return null;
                if (previewCache.TryGetValue(id,out var cached)) return cached;
                if (previewCache.Count>=24) previewCache.Clear();
                var preview=IfcPreviewGeometryBuilder.Build(linkedDoc.GetElement(ElementIds.Create(id)));
                if (preview!=null) previewCache[id]=preview;
                return preview;
            };
            window.LoadCategory = category => SetCategoryCount(window, linkedDoc, linkOption, category);
        }

        private static void SetCategoryCount(IFCInfoWindow window, Document linkedDoc, LinkOption linkOption, CategoryOption category)
        {
            string ifcPath = linkOption.IfcPath;
            window.AirTerminalCount = null;
            window.AirTerminalError = null;
            window.AirTerminals = new List<AirTerminalRow>();
            window.CanReplaceCategory = category != null && SupportedCategories.Contains(category.Id);
            window.CanCreateDucts = false;
            if (!window.CanReplaceCategory)
                throw new InvalidOperationException("This category is not supported.");
            bool isDuct = category.Id == (long)BuiltInCategory.OST_DuctCurves;
            window.CanCreateDucts = isDuct;
            bool readIfc = window.CanReplaceCategory || isDuct;
            window.IfcSourceStatus = "Parameter data from the Revit link.";
            if (linkedDoc == null)
            {
                window.AirTerminalError = "Could not read the linked model. Load the link and run the tool again.";
                return;
            }
            try
            {
                IfcSourceReader ifc = null;
                if (readIfc && !File.Exists(ifcPath))
                {
                    var picker = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "Select the original linked IFC to read systems and elevations",
                        Filter = "IFC STEP (*.ifc)|*.ifc",
                        CheckFileExists = true,
                        Multiselect = false
                    };
                    if (!string.IsNullOrWhiteSpace(ifcPath))
                    {
                        string directory = Path.GetDirectoryName(ifcPath);
                        if (Directory.Exists(directory)) picker.InitialDirectory = directory;
                        picker.FileName = Path.GetFileName(ifcPath);
                    }
                    if (picker.ShowDialog() == true)
                        ifcPath = linkOption.IfcPath = picker.FileName;
                }
                if (readIfc && File.Exists(ifcPath))
                {
                    try
                    {
                        ifc = IfcSourceReader.Read(ifcPath);
                    }
                    catch (Exception ex) { window.IfcSourceStatus = "Could not read original IFC: " + ex.Message; }
                }
                else if (readIfc)
                    window.IfcSourceStatus = "No original IFC was selected. Only Revit parameters are available; IFC elevations are unavailable.";
                var sourceElements = ifc?.Products;
                int matched = 0;
                // Count instances in the selected linked document, not the host/view.
                using (var collector = new FilteredElementCollector(linkedDoc))
                {
                    var terminals = collector
                        .OfCategoryId(ElementIds.Create(category.Id))
                        .WhereElementIsNotElementType()
                        .ToElements();
                    window.AirTerminalCount = terminals.Count;
                    var rows = new List<AirTerminalRow>();
                    foreach (Element element in terminals)
                    {
                        string guid = ReadIfcGuid(element);
                        var connector = (element as MEPCurve)?.ConnectorManager.Connectors.Cast<Connector>()
                            .FirstOrDefault(c => c.ConnectorType == ConnectorType.End);
                        var row = new AirTerminalRow
                        {
                            ElementId = element.Id.ToString(),
                            Name = element.Name,
                            IfcGuid = guid,
                            NativeShape = connector == null ? null : connector.Shape == ConnectorProfileType.Round ? "Round" :
                                connector.Shape == ConnectorProfileType.Oval ? "Oval" : connector.Shape == ConnectorProfileType.Rectangular ?
                                (Math.Abs(connector.Width - connector.Height) <= 0.1 / 304.8 ? "Square" : "Rectangular") : null,
                            SystemType = ReadSystemValue(element, category.Id==(long)BuiltInCategory.OST_PipeCurves
                                ? BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM : BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM, "System Type"),
                            SystemName = ReadSystemValue(element, BuiltInParameter.RBS_SYSTEM_NAME_PARAM, "System Name"),
                            Elevation = "No information",
                            DataSource = ifc == null ? "Parameter Revit" : "No matching IFC GUID; using Revit parameters"
                        };
                        IfcTerminalSource sourceRow;
                        if (sourceElements != null && sourceElements.TryGetValue(guid, out sourceRow))
                        {
                            matched++;
                            row.DuctSource = sourceRow;
                            row.IfcProperties = sourceRow.Properties;
                            if (!string.IsNullOrWhiteSpace(sourceRow.SystemName))
                                row.SystemName = sourceRow.SystemName;
                            if (!string.IsNullOrWhiteSpace(sourceRow.SystemType))
                                row.SystemType = sourceRow.SystemType;
                            row.Elevation = sourceRow.ElevationMm.HasValue
                                ? sourceRow.ElevationMm.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                                : "No information";
                            row.DataSource = "IFC GUID matched; original IFC takes precedence";
                        }
                        if (row.IfcProperties.Count==0)
                        foreach (var candidate in new[] { element,linkedDoc.GetElement(element.GetTypeId()) }.Where(e=>e!=null))
                        foreach (Parameter parameter in candidate.Parameters)
                        {
                            string name=parameter.Definition.Name;
                            if (!name.StartsWith("Pset",StringComparison.OrdinalIgnoreCase) && !name.StartsWith("Qto",StringComparison.OrdinalIgnoreCase)) continue;
                            string value=ParameterText(parameter,linkedDoc); if (string.IsNullOrEmpty(value)) continue;
                            int split=name.IndexOf('.'); if (split<0) split=name.IndexOf(':');
                            row.IfcProperties.Add(new IfcPropertyValue { Scope=candidate.Id==element.Id ? "Revit link / Instance" : "Revit link / Type",
                                SetName=split<0 ? "IFC parameter" : name.Substring(0,split),Name=split<0 ? name : name.Substring(split+1),Value=value });
                        }
                        rows.Add(row);
                    }
                    window.AirTerminals = rows.OrderBy(row => row.SystemType).ThenBy(row => row.SystemName)
                      .ThenBy(row => row.ElementId).ToList();
                    if (ifc != null)
                        window.IfcSourceStatus = string.Format("Original IFC: {0}\nGUID matches: {1}/{2} linked elements. IFC contains {3} source elements in group {4}. " +
                            "Data comes from the IFC file on disk; reload the link if the IFC has changed.",
                            ifcPath, matched, terminals.Count, sourceElements.Count, "IFC Product");
                }
            }
            catch (Exception ex)
            {
                window.AirTerminalError = "Could not read category: " + ex.Message;
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
            return "No information";
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

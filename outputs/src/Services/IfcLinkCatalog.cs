using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;



namespace IFCInfo
{
    /// <summary>Tìm IFC link, trạng thái load và đường dẫn nguồn cho dropdown.</summary>
    internal static class IfcLinkCatalog
    {
        public static List<LinkOption> GetLinks(Document doc)
        {
            var result = new List<LinkOption>();
            var links = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().ToList();
            foreach (var link in links)
            {
                var type = doc.GetElement(link.GetTypeId()) as RevitLinkType;
                if (type == null)
                    continue;
                Document linkedDoc = link.GetLinkDocument();
                string original = linkedDoc?.ProjectInformation?.LookupParameter("Original IFC File Name")?.AsString();
                string path = GetLinkPath(type, linkedDoc);
                bool ifcPath = path.EndsWith(".ifc.rvt", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase);
                bool ifcName = type.Name.EndsWith(".ifc.rvt", StringComparison.OrdinalIgnoreCase) || type.Name.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase);
                if (string.IsNullOrWhiteSpace(original) && !ifcPath && !ifcName)
                    continue;
                string sourcePath = string.IsNullOrWhiteSpace(original) ? path : original.Trim();
                if (sourcePath.EndsWith(".ifc.rvt", StringComparison.OrdinalIgnoreCase))
                    sourcePath = sourcePath.Substring(0, sourcePath.Length - 4);
                result.Add(new LinkOption { Id = link.Id.Value, Name = type.Name, IfcPath = sourcePath, IsLoaded = linkedDoc != null });
            }
            foreach (var group in result.GroupBy(l => l.Name).Where(g => g.Count() > 1))
            {
                int number = 0;
                foreach (var item in group.OrderBy(l => l.Id))
                    item.Name += " · Bản " + (++number);
            }
            return result.OrderBy(l => l.Name).ToList();
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


    }
}

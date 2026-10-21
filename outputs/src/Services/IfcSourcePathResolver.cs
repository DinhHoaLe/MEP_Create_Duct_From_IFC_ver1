using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IFCInfo
{
    internal static class IfcSourcePathResolver
    {
        public static string Resolve(string original, string linkPath, string linkedModelPath,
            string hostModelPath, string linkName)
        {
            var candidates = new List<string>();
            Add(candidates, original);
            foreach (string modelPath in new[] { linkPath, linkedModelPath, hostModelPath })
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(modelPath) || !Path.IsPathRooted(modelPath))
                        continue;
                    string directory = Path.GetDirectoryName(modelPath);
                    if (string.IsNullOrWhiteSpace(directory)) continue;
                    if (!string.IsNullOrWhiteSpace(original))
                    {
                        if (!Path.IsPathRooted(original.Trim()))
                            Add(candidates, Path.Combine(directory, original.Trim()));
                        Add(candidates, Path.Combine(directory, Path.GetFileName(original.Trim())));
                    }
                    // A Revit IFC cache named X.ifc.RVT belongs to X.ifc beside it.
                    if (modelPath != hostModelPath) Add(candidates, modelPath);
                    if (!string.IsNullOrWhiteSpace(linkName))
                        Add(candidates, Path.Combine(directory, Path.GetFileName(linkName)));
                }
                catch (ArgumentException) { }
                catch (NotSupportedException) { }
                catch (PathTooLongException) { }
            }
            return candidates.FirstOrDefault(File.Exists)
                ?? candidates.FirstOrDefault(path => Directory.Exists(Path.GetDirectoryName(path)))
                ?? candidates.FirstOrDefault() ?? string.Empty;
        }

        private static void Add(List<string> candidates, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            path = path.Trim();
            if (path.EndsWith(".ifc.rvt", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(0, path.Length - 4);
            if (!path.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                if (!Path.IsPathRooted(path)) return;
                path = Path.GetFullPath(path);
                if (!candidates.Contains(path, StringComparer.OrdinalIgnoreCase)) candidates.Add(path);
            }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }
            catch (PathTooLongException) { }
        }
    }
}

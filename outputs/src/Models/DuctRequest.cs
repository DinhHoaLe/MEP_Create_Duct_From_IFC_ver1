using System.Collections.Generic;

namespace IFCInfo
{
    /// <summary>Duct creation request after type selection and system mapping.</summary>
    public sealed class DuctRequest
    {
        // Map profiles separately so each group can select its own system.
        public static string SystemKey(bool round, string sourceType) => (round ? "Round|" : "Rectangular|") + (sourceType ?? "");
        public static string SystemKey(string shape, string sourceType) => shape + "|" + (sourceType ?? "");
        public static string ShapeLabel(string shape) => shape == "Round" ? "Round" : shape == "Square" ? "Square" : shape == "Oval" ? "Oval" : shape == "Rectangular" ? "Rectangular" : "Unknown";
        public List<DuctPlanItem> Items;
        public long RoundTypeId, RectangularTypeId;
        public Dictionary<string, long> SystemTypes = new Dictionary<string, long>();
        public Dictionary<string, long> DuctTypes = new Dictionary<string, long>();
        public Dictionary<string, NewDuctSystem> NewSystems = new Dictionary<string, NewDuctSystem>();
        public Dictionary<string, long> Levels = new Dictionary<string, long>();
        public Dictionary<string, long> Worksets = new Dictionary<string, long>();
        public bool KeepSuccessful, CreateFittings, ConnectTerminals, SaveSettings = true;
        public double FittingGapMm = 1;
        public List<DuctRunRow> Skipped = new List<DuctRunRow>();
    }
    public sealed class NewDuctSystem
    {
        public string Name;
        public long TemplateId;
        public string Classification;
    }
}


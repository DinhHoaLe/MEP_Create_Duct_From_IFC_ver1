using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace IFCInfo
{
    internal static class SupportedCategories
    {
        private static readonly HashSet<long> Lines=new HashSet<long>{
            (long)BuiltInCategory.OST_DuctCurves,(long)BuiltInCategory.OST_PipeCurves,
            (long)BuiltInCategory.OST_Conduit,(long)BuiltInCategory.OST_CableTray};
        private static readonly HashSet<long> Families=new HashSet<long>{
            (long)BuiltInCategory.OST_DuctFitting,(long)BuiltInCategory.OST_DuctAccessory,
            (long)BuiltInCategory.OST_DuctTerminal,(long)BuiltInCategory.OST_MechanicalEquipment,
            (long)BuiltInCategory.OST_PipeFitting,(long)BuiltInCategory.OST_PipeAccessory,
            (long)BuiltInCategory.OST_PlumbingFixtures,(long)BuiltInCategory.OST_Sprinklers,
            (long)BuiltInCategory.OST_CableTrayFitting,(long)BuiltInCategory.OST_ConduitFitting,
            (long)BuiltInCategory.OST_ElectricalEquipment,(long)BuiltInCategory.OST_ElectricalFixtures,
            (long)BuiltInCategory.OST_LightingFixtures,(long)BuiltInCategory.OST_LightingDevices,
            (long)BuiltInCategory.OST_DataDevices,(long)BuiltInCategory.OST_CommunicationDevices,
            (long)BuiltInCategory.OST_FireAlarmDevices,(long)BuiltInCategory.OST_SecurityDevices,
            (long)BuiltInCategory.OST_TelephoneDevices,(long)BuiltInCategory.OST_NurseCallDevices,
            (long)BuiltInCategory.OST_GenericModel};
        internal static bool IsLine(long id)=>Lines.Contains(id);
        internal static bool Contains(long id)=>Lines.Contains(id)||Families.Contains(id);
    }
}

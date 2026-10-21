using System;
using Autodesk.Revit.DB;

namespace IFCInfo
{
    internal static class ElementIds
    {
        private static readonly System.Reflection.PropertyInfo LongValue = typeof(ElementId).GetProperty("Value");
        private static readonly System.Reflection.ConstructorInfo LongConstructor = typeof(ElementId).GetConstructor(new[] { typeof(long) });
        internal static long Number(this ElementId id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            return LongValue != null ? (long)LongValue.GetValue(id) : (int)typeof(ElementId).GetProperty("IntegerValue").GetValue(id);
        }
        internal static ElementId Create(long value) => LongConstructor != null
            ? (ElementId)LongConstructor.Invoke(new object[] { value }) : (ElementId)Activator.CreateInstance(typeof(ElementId), checked((int)value));
    }
}

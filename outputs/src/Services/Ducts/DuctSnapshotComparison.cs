using System;
using System.Globalization;
using System.Linq;

namespace IFCInfo
{
    public static class DuctSnapshotComparison
    {
        public static bool Equal(string a,string b)
        {
            if (a==null || b==null) return false;
            var aa=a.Split('|'); var bb=b.Split('|');
            if (aa.Length!=bb.Length || !aa.Skip(1).SequenceEqual(bb.Skip(1))) return false;
            var av=aa[0].Split(';'); var bv=bb[0].Split(';');
            if (av.Length!=12 || bv.Length!=12) return false;
            for (int n=0;n<12;n++)
            {
                double x,y;
                if (!double.TryParse(av[n],NumberStyles.Float,CultureInfo.InvariantCulture,out x) ||
                    !double.TryParse(bv[n],NumberStyles.Float,CultureInfo.InvariantCulture,out y) ||
                    double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y) ||
                    Math.Abs(x-y)>(n>=6 && n<=8 ? 1e-6 : 0.1/304.8)) return false;
            }
            return true;
        }
        public static string Describe(string before,string after)
        {
            if (before==null) return "Chưa có trạng thái gốc";
            var a=before.Split('|'); var b=after.Split('|');
            if (a.Length<3 || b.Length<3) return "Thông tin IFC đã thay đổi";
            var x=a[0].Split(';'); var y=b[0].Split(';');
            if (x.Length!=12 || y.Length!=12) return "Hình học IFC đã thay đổi";
            var changes=new System.Collections.Generic.List<string>();
            if (!x.Take(6).SequenceEqual(y.Take(6))) changes.Add("Đường tim (mm): "+Point(x,0)+" → "+Point(x,3)+" thành "+Point(y,0)+" → "+Point(y,3));
            if (!x.Skip(6).Take(3).SequenceEqual(y.Skip(6).Take(3))) changes.Add("Đổi hướng tiết diện");
            if (!x.Skip(9).SequenceEqual(y.Skip(9))) changes.Add("W/H/D (mm): "+Point(x,9)+" → "+Point(y,9));
            if (a[1]!=b[1]) changes.Add("System Type: "+a[1]+" → "+b[1]);
            if (a[2]!=b[2]) changes.Add("System Name: "+a[2]+" → "+b[2]);
            return string.Join("; ",changes);
        }
        private static string Point(string[] values,int offset) => string.Join(", ",values.Skip(offset).Take(3)
            .Select(v=>(double.Parse(v,CultureInfo.InvariantCulture)*304.8).ToString("0.##",CultureInfo.InvariantCulture)));
    }
}

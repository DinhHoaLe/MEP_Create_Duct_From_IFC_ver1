using System;
using System.Collections.Generic;
using System.Linq;

namespace IFCInfo
{
    // Inputs are in host coordinates and feet. Tests can run without Revit.
    public static class DuctCoverage
    {
        public sealed class Segment
        {
            public long Id; public double[] Start, End;
        }
        public sealed class Result
        {
            public string Status; public List<long> Ids = new List<long>();
        }
        public static Result Check(double[] start, double[] end, IEnumerable<Segment> ducts, double tolerance)
        {
            var vector = Sub(end, start);
            double length = Norm(vector);
            if (length <= tolerance)
                return new Result { Status = "Chưa xác định" };
            var axis = vector.Select(v => v / length).ToArray();
            var intervals = new List<double[]>();
            var result = new Result();
            foreach (var duct in ducts)
            {
                var a = Sub(duct.Start, start);
                var b = Sub(duct.End, start);
                double t0 = Dot(a, axis), t1 = Dot(b, axis);
                if (DistanceToAxis(a, axis, t0) > tolerance || DistanceToAxis(b, axis, t1) > tolerance)
                    continue;
                double low = Math.Max(0, Math.Min(t0, t1)), high = Math.Min(length, Math.Max(t0, t1));
                if (high - low <= tolerance)
                    continue;
                intervals.Add(new[] { low, high });
                result.Ids.Add(duct.Id);
            }
            if (intervals.Count == 0)
            {
                result.Status = "Chưa tồn tại";
                return result;
            }
            double covered = 0;
            foreach (var span in intervals.OrderBy(i => i[0]))
            {
                if (span[0] > covered + tolerance)
                {
                    result.Status = "Trùng một phần";
                    return result;
                }
                covered = Math.Max(covered, span[1]);
            }
            result.Status = covered >= length - tolerance ? "Đã tồn tại" : "Trùng một phần";
            return result;
        }
        private static double[] Sub(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };
        private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        private static double Norm(double[] a) => Math.Sqrt(Dot(a, a));
        private static double DistanceToAxis(double[] p, double[] axis, double t) =>
            Norm(new[] { p[0] - axis[0] * t, p[1] - axis[1] * t, p[2] - axis[2] * t });
    }
}

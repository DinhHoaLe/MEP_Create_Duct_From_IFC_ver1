using System;
using System.Linq;
namespace IFCInfo
{
    public static class DuctConnectionGeometry
    {
        private static double Dot(double[] a,double[] b) => a.Zip(b,(x,y)=>x*y).Sum();
        private static double[] Sub(double[] a,double[] b) => a.Zip(b,(x,y)=>x-y).ToArray();
        // Both outward connector rays must meet ahead, within the explicit gap limit.
        public static bool CanBridge(double[] p,double[] u,double[] q,double[] v,double limit,double tolerance)
        {
            if (limit<=tolerance) return false;
            var delta=Sub(q,p); double distance=Math.Sqrt(Dot(delta,delta));
            if (distance<=tolerance) return false;
            double dot=Dot(u,v), a=Dot(delta,u), b=Dot(delta,v);
            if (dot < -0.999999)
            {
                var offset=delta.Zip(u,(d,x)=>d-a*x).ToArray();
                return a>0 && distance<=limit && Math.Sqrt(Dot(offset,offset))<=tolerance;
            }
            if (Math.Abs(dot)>0.999999) return false;
            double t=(a-dot*b)/(1-dot*dot), s=(dot*a-b)/(1-dot*dot);
            if (t < 0 || s < 0 || t>limit || s>limit) return false;
            var mismatch=p.Select((x,i)=>x+t*u[i]-q[i]-s*v[i]).ToArray();
            return Math.Sqrt(Dot(mismatch,mismatch))<=tolerance;
        }
    }
}

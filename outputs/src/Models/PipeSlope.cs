using System;
namespace IFCInfo
{
    public static class PipeSlope
    {
        public static void Set(AirTerminalRow row,double dx,double dy,double dz)
        {
            row.SlopePercent=null;row.IsVerticalPipe=false;row.SlopeIssue="";
            if(double.IsNaN(dx)||double.IsNaN(dy)||double.IsNaN(dz)||double.IsInfinity(dx)||double.IsInfinity(dy)||double.IsInfinity(dz)){row.SlopeIssue="Invalid centerline";return;}
            double horizontal=Math.Sqrt(dx*dx+dy*dy);
            if(horizontal<1e-9){row.IsVerticalPipe=Math.Abs(dz)>1e-9;row.SlopeIssue=row.IsVerticalPipe?"":"Zero length centerline";return;}
            row.SlopePercent=100*Math.Abs(dz)/horizontal;
        }
    }
}

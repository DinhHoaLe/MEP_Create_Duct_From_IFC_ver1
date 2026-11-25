using System;
using System.Collections.Generic;
using System.Linq;
namespace IFCInfo
{
    public static class CurveLevelResolver
    {
        public static ReplacementLevelOption Resolve(AirTerminalRow row,IEnumerable<ReplacementLevelOption> levels)
        {
            var choices=levels.Where(l=>l.Id>0).ToList();
            var named=choices.Where(l=>!string.IsNullOrWhiteSpace(row.SourceLevelName) && string.Equals(l.Label,row.SourceLevelName,StringComparison.OrdinalIgnoreCase)).ToList();
            if(named.Count>0)return named.Count==1?named[0]:null;
            double? z=row.HostLevelElevationMm??row.HostCenterElevationMm;
            if(!z.HasValue || double.IsNaN(z.Value) || double.IsInfinity(z.Value))return null;
            var exact=choices.Where(l=>l.ElevationMm.HasValue && Math.Abs(l.ElevationMm.Value-z.Value)<=0.6).ToList();
            if(exact.Count>0)return exact.Count==1?exact[0]:null;
            // A known storey elevation must match; do not silently assign another floor.
            if(row.HostLevelElevationMm.HasValue)return null;
            var below=choices.Where(l=>l.ElevationMm.HasValue && l.ElevationMm.Value<=z.Value+0.6).OrderByDescending(l=>l.ElevationMm).ToList();
            if(below.Count==0)return null;
            return below.Count(l=>Math.Abs(l.ElevationMm.Value-below[0].ElevationMm.Value)<=0.6)==1?below[0]:null;
        }
    }
}

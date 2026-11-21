using System;
namespace RynthCore.Plugin.RynthAi.Meta;
internal static class MaraeMapWalkMath
{
    internal static bool TryGoal(double ns, double ew, double heading, double step, out double goalNs, out double goalEw)
    {
        goalNs=goalEw=0;
        if (!double.IsFinite(ns) || !double.IsFinite(ew) || !double.IsFinite(heading) || !double.IsFinite(step) || step<=0) return false;
        double radians=(heading%360)*Math.PI/180;
        step=Math.Clamp(step,4,24);
        goalNs=ns+Math.Cos(radians)*step/240;
        goalEw=ew+Math.Sin(radians)*step/240;
        return Math.Abs(goalNs)<=102.85 && Math.Abs(goalEw)<=102.85;
    }
}

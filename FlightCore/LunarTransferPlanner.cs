using System;
using System.Threading;

namespace FlightCore
{
    public enum LunarManeuver { FreeReturn, LunarOrbit }

    /// <summary>A preliminary, gravity-only transfer; burns are later executed by the vehicle.</summary>
    public sealed class LunarTransferPlan
    {
        public LunarManeuver Maneuver { get; internal set; }
        public double DepartureTime { get; internal set; }
        public Vector3d DeltaV { get; internal set; }
        public double MoonClosestTime { get; internal set; }
        public double MoonClosestRadius { get; internal set; }
        public double EarthReturnRadius { get; internal set; }
        public double CaptureDeltaV { get; internal set; }
        internal Vector3d MoonClosestOffset { get; set; }
        internal double MoonEntryTime { get; set; }
        internal Vector3d MoonEntryOffset { get; set; }
    }

    /// <summary>
    /// Searches Lambert transfers against the moving Moon and scores coasts using
    /// the flight core's patched-conic acceleration. SI units in launch ECI.
    /// Preliminary impulses are estimates; live guidance executes finite burns.
    /// </summary>
    public static class LunarTransferPlanner
    {
        const double Mu = Gravity.EarthGravitationalParameter;
        const double Day = 86400;
        const double Surface = EarthFrames.SemiMajorAxisMeters;
        static double Dot(Vector3d a,Vector3d b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        static Vector3d Cross(Vector3d a,Vector3d b)=>new Vector3d(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
        static Vector3d Unit(Vector3d a)=>a*(1/a.Magnitude());

        /// <summary>Searches a bounded set of parking-orbit departure times, flight times, and lunar aim points.</summary>
        public static bool TryPlan(LunarEphemeris ephemeris, Vector3d position, Vector3d velocity, double epoch,
            LunarManeuver maneuver, double availableDeltaV, CancellationToken cancellation,
            Action<int>? progress, out LunarTransferPlan? plan, out string reason)
        {
            if(ephemeris==null) throw new ArgumentNullException(nameof(ephemeris));
            plan=null;
            var elements=OrbitDiagnostics.Compute(position,velocity);
            if(!elements.IsBound || elements.MinimumWgs84AltitudeBoundMeters<120000)
            { reason="A bound Earth orbit above 120 km is required"; return false; }
            if(elements.PeriodSeconds>2*Day)
            { reason="Parking orbit is too large for the lunar transfer search"; return false; }
            if(availableDeltaV<=0) { reason="No propellant is available"; return false; }
            Vector3d plane=Cross(position,velocity);
            if(plane.Magnitude()<1) { reason="Parking orbit has no stable plane"; return false; }
            plane=Unit(plane);
            double best=double.PositiveInfinity;
            int checkedCount=0,encounters=0,solutions=0,affordable=0,notDownward=0; double bestDeparturePeri=0;
            // A burn needs time to slew and, for low-thrust vehicles, to straddle
            // its computed impulse epoch. One orbit of phasing offers alternatives.
            int phaseCount=maneuver==LunarManeuver.FreeReturn ? 64 : 48;
            for(int phase=0;phase<phaseCount;phase++)
            {
                cancellation.ThrowIfCancellationRequested();
                double wait=600+phase*elements.PeriodSeconds/phaseCount;
                CoastEarth(position,velocity,wait,out var departureP,out var departureV);
                foreach(double days in new[]{2.75,3.25,3.75,4.25,4.75,5.5,6.25})
                {
                    double arrival=epoch+wait+days*Day;
                    var moon=ephemeris.MoonPosition(arrival);var moonV=ephemeris.MoonVelocity(arrival);
                    var moonNormal=Cross(moon,moonV);
                    if(moonNormal.Magnitude()<1) continue;
                    moonNormal=Unit(moonNormal);var tangent=Unit(Cross(moonNormal,moon));
                    int aimCount=24;
                    for(int aim=0;aim<aimCount;aim++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        double distance=aim<8?5000000:aim<16?12000000:25000000;
                        double theta=(aim%8)*Math.PI/4;
                        Vector3d offset=(tangent*Math.Cos(theta)+moonNormal*Math.Sin(theta))*distance;
                        if(!Lambert(departureP,moon+offset,days*Day,plane,out var transferVelocity)) continue;
                        solutions++;
                        var dv=transferVelocity-departureV;
                        if(dv.Magnitude()>Math.Min(availableDeltaV,12000)) continue;
                        affordable++;
                        // An impulsive Lambert solution can point through Earth.
                        // A finite departure burn must not start by diving into it.
                        if(Dot(dv,departureP)<-100*departureP.Magnitude()) continue;
                        notDownward++;
                        var departureOrbit=OrbitDiagnostics.Compute(departureP,transferVelocity);
                        bestDeparturePeri=Math.Max(bestDeparturePeri,departureOrbit.PerigeeRadiusMeters);
                        // A below-surface osculating periapsis can lie in the
                        // past on an already-outbound escape branch. Reject it
                        // only when radial motion still points toward Earth.
                        if(departureOrbit.PerigeeRadiusMeters<Surface+100000 && Dot(departureP,transferVelocity)<0) continue;
                        var trace=Trace(ephemeris,departureP,transferVelocity,epoch+wait,maneuver,cancellation);
                        checkedCount++; progress?.Invoke(checkedCount);
                        if(trace.Encounter) encounters++;
                        if(!trace.Encounter || trace.MoonRadius<LunarEphemeris.MoonRadius+50000 || trace.MoonRadius>LunarEphemeris.MoonSoi*.85) continue;
                        double capture=0,score;
                        if(maneuver==LunarManeuver.LunarOrbit)
                        {
                            capture=Math.Max(0,trace.MoonSpeed-Math.Sqrt(LunarEphemeris.MoonMu/trace.MoonRadius));
                            if(dv.Magnitude()+capture>availableDeltaV) continue;
                            // Prefer a safe, relatively low capture orbit over a faster but distant flyby.
                            score=dv.Magnitude()+capture+trace.MoonRadius/20000;
                        }
                        else
                        {
                            // Keep the unpowered return above modeled atmosphere;
                            // this maneuver does not also perform Earth reentry.
                            if(!trace.Returned || trace.EarthRadius<Surface+130000 || trace.EarthRadius>40000000) continue;
                            // Aim for a return near Earth; a surviving perigee is preferable.
                            score=dv.Magnitude()+Math.Abs(trace.EarthRadius-(Surface+150000))/4000;
                        }
                        if(score<best)
                        {
                            best=score;plan=new LunarTransferPlan { Maneuver=maneuver, DepartureTime=epoch+wait,
                                DeltaV=dv, MoonClosestTime=trace.MoonTime, MoonClosestRadius=trace.MoonRadius,
                                EarthReturnRadius=trace.EarthRadius, CaptureDeltaV=capture, MoonClosestOffset=trace.MoonOffset,
                                MoonEntryTime=trace.MoonEntryTime, MoonEntryOffset=trace.MoonEntryOffset };
                        }
                    }
                }
            }
            reason=plan==null ? "No safe lunar trajectory was found ("+solutions+" transfers, "+affordable+" within fuel, "+notDownward+" outward, "+checkedCount+" coasts, "+encounters+" lunar encounters; best departure periapsis "+(bestDeparturePeri/1000).ToString("F0")+" km)" : "";
            return plan!=null;
        }

        struct EncounterTrace { public bool Encounter,Returned; public double MoonRadius,MoonSpeed,MoonTime,EarthRadius,MoonEntryTime; public Vector3d MoonOffset,MoonEntryOffset; }
        static EncounterTrace Trace(LunarEphemeris e, Vector3d p, Vector3d v, double t, LunarManeuver mode, CancellationToken ct)
        {
            double start=t,bestMoon=double.PositiveInfinity,moonSpeed=0,moonTime=0,bestEarth=double.PositiveInfinity;
            bool entered=false,passed=false,returned=false; var bestOffset=Vector3d.Zero; var entryOffset=Vector3d.Zero; double entryTime=0;
            while(t-start<18*Day)
            {
                if(((int)(t-start))%8192<900) ct.ThrowIfCancellationRequested();
                var moon=e.MoonPosition(t);var rel=p-moon;double mr=rel.Magnitude();
                var relV=v-e.MoonVelocity(t);double mv=relV.Magnitude();
                if(!entered && mr<LunarEphemeris.MoonSoi) { entered=true;entryTime=t;entryOffset=rel; }
                if(entered && mr<bestMoon){bestMoon=mr;moonTime=t;moonSpeed=mv;bestOffset=rel;}
                if(entered && mr>bestMoon+5000 && Dot(rel,relV)>0) passed=true;
                if(passed && mode==LunarManeuver.LunarOrbit && mr>Math.Max(bestMoon*1.3,bestMoon+1000000)) break;
                if(mr<LunarEphemeris.MoonRadius) break;
                double er=p.Magnitude();
                if(!entered && er<Surface) break;
                if(passed && er<bestEarth) bestEarth=er;
                if(passed && er<100000000 && Dot(p,v)>=0){returned=true;break;}
                if(passed && er<Surface){returned=true;break;}
                double h=Math.Min(900,Math.Max(2,.025*er/Math.Max(1,v.Magnitude())));
                if(mr<150000000) h=Math.Min(h,Math.Max(2,.025*mr/Math.Max(1,mv)));
                if(entered && mr<LunarEphemeris.MoonRadius+2000000) h=Math.Min(h,Math.Max(1,.3*Math.Max(0,mr-LunarEphemeris.MoonRadius)/Math.Max(1,mv)));
                if(passed && er<Surface+50000000) h=Math.Min(h,Math.Max(1,.3*Math.Max(0,er-Surface)/Math.Max(1,v.Magnitude())));
                Step(e,ref p,ref v,t,h); t+=h;
            }
            return new EncounterTrace{Encounter=entered&&passed,Returned=returned,MoonRadius=bestMoon,MoonSpeed=moonSpeed,MoonTime=moonTime,EarthRadius=bestEarth,MoonOffset=bestOffset,MoonEntryTime=entryTime,MoonEntryOffset=entryOffset};
        }

        /// <summary>Corrects finite-burn errors against the planned lunar encounter.
        /// Uses finite differences of the same gravity-only predictor. Returns false
        /// instead of issuing a large or divergent correction.</summary>
        public static bool TryCourseCorrection(LunarEphemeris e, Vector3d position, Vector3d velocity,
            double time, LunarTransferPlan plan, out Vector3d correction)
        {
            correction=Vector3d.Zero;
            if(plan.MoonEntryTime-time < 3600) return false;
            Vector3d target=e.MoonPosition(plan.MoonEntryTime)+plan.MoonEntryOffset;
            Vector3d dv=Vector3d.Zero;
            for(int iteration=0;iteration<6;iteration++)
            {
                var current=PredictAt(e,position,velocity+dv,time,plan.MoonEntryTime);
                var residual=target-current;
                if(residual.Magnitude()<10000) { correction=dv; return dv.Magnitude()<500; }
                const double probe=.5;
                var x=(PredictAt(e,position,velocity+dv+new Vector3d(probe,0,0),time,plan.MoonEntryTime)-current)*(1/probe);
                var y=(PredictAt(e,position,velocity+dv+new Vector3d(0,probe,0),time,plan.MoonEntryTime)-current)*(1/probe);
                var z=(PredictAt(e,position,velocity+dv+new Vector3d(0,0,probe),time,plan.MoonEntryTime)-current)*(1/probe);
                double det=Dot(x,Cross(y,z));if(Math.Abs(det)<1e-9)return false;
                var change=new Vector3d(Dot(residual,Cross(y,z))/det,Dot(x,Cross(residual,z))/det,Dot(x,Cross(y,residual))/det);
                double size=change.Magnitude();if(size>150)change=change*(150/size);
                dv+=change;if(dv.Magnitude()>500)return false;
            }
            return false;
        }
        static Vector3d PredictAt(LunarEphemeris e,Vector3d p,Vector3d v,double t,double end)
        {
            while(t<end)
            {
                double er=p.Magnitude(),mr=(p-e.MoonPosition(t)).Magnitude();
                double h=Math.Min(900,Math.Max(2,.025*er/Math.Max(1,v.Magnitude())));
                if(mr<150000000)h=Math.Min(h,Math.Max(2,.025*mr/Math.Max(1,(v-e.MoonVelocity(t)).Magnitude())));
                h=Math.Min(h,end-t);Step(e,ref p,ref v,t,h);t+=h;
            }
            return p;
        }
        static void Step(LunarEphemeris e,ref Vector3d p,ref Vector3d v,double t,double h)
        {
            var a=e.Acceleration(p,t);var b=e.Acceleration(p+v*(h*.5),t+h*.5);
            var c=e.Acceleration(p+(v+a*(h*.5))*(h*.5),t+h*.5);
            var d=e.Acceleration(p+(v+b*(h*.5))*h,t+h);
            p=p+(v+(v+a*(h*.5))*2+(v+b*(h*.5))*2+(v+c*h))*(h/6);
            v=v+(a+b*2+c*2+d)*(h/6);
        }
        static void CoastEarth(Vector3d p,Vector3d v,double duration,out Vector3d outP,out Vector3d outV)
        {
            double t=0;while(t<duration){double h=Math.Min(20,duration-t);EarthStep(ref p,ref v,h);t+=h;}outP=p;outV=v;
        }
        static Vector3d EarthGravity(Vector3d p){double r=p.Magnitude();return p*(-Mu/(r*r*r));}
        static void EarthStep(ref Vector3d p,ref Vector3d v,double h)
        {
            var a=EarthGravity(p);var b=EarthGravity(p+v*(h*.5));var c=EarthGravity(p+(v+a*(h*.5))*(h*.5));var d=EarthGravity(p+(v+b*(h*.5))*h);
            p=p+(v+(v+a*(h*.5))*2+(v+b*(h*.5))*2+(v+c*h))*(h/6);v=v+(a+b*2+c*2+d)*(h/6);
        }
        static void Stumpff(double z,out double c,out double s)
        {
            if(z>1e-7){double q=Math.Sqrt(z);c=(1-Math.Cos(q))/z;s=(q-Math.Sin(q))/(q*q*q);}
            else if(z< -1e-7){double q=Math.Sqrt(-z);c=(Math.Cosh(q)-1)/(-z);s=(Math.Sinh(q)-q)/(q*q*q);}
            else {c=.5-z/24+z*z/720;s=1.0/6-z/120+z*z/5040;}
        }
        // Universal-variable single-revolution Lambert transfer, selecting the
        // direction consistent with the supplied parking-orbit angular momentum.
        internal static bool Lambert(Vector3d r1,Vector3d r2,double duration,Vector3d plane,out Vector3d velocity)
        {
            velocity=Vector3d.Zero;double a=r1.Magnitude(),b=r2.Magnitude();
            double cos=Math.Max(-1,Math.Min(1,Dot(r1,r2)/(a*b)));double sin=Math.Sqrt(Math.Max(0,1-cos*cos));
            if(Dot(Cross(r1,r2),plane)<0)sin=-sin;
            if(Math.Abs(sin)<1e-8||1-cos<1e-12)return false;
            double A=sin*Math.Sqrt(a*b/(1-cos)),lo=-39,hi=39,z=0,y=0,c=0,s=0;
            for(int i=0;i<90;i++)
            {
                z=(lo+hi)*.5;Stumpff(z,out c,out s);if(c<=0)return false;
                y=a+b+A*(z*s-1)/Math.Sqrt(c);
                if(y<=0){lo=z;continue;}
                double x=Math.Sqrt(y/c),tof=(x*x*x*s+A*Math.Sqrt(y))/Math.Sqrt(Mu);
                if(tof<duration)lo=z;else hi=z;
            }
            if(y<=0)return false;
            double f=1-y/a,g=A*Math.Sqrt(y/Mu);
            if(Math.Abs(g)<1e-8)return false;
            velocity=(r2-r1*f)*(1/g);return true;
        }
    }
}

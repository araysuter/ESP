using System;

namespace FlightCore
{
    /// <summary>Approximate, offline geocentric celestial positions in the launch frame.
    /// Low precision orbital elements and lunar perturbations, not a navigation ephemeris.
    /// The launch frame is aligned with ECEF at launch and does not rotate thereafter.
    /// </summary>
    public sealed class LunarEphemeris
    {
        public const double MoonMu = 4.902800118e12;
        public const double SunMu = 1.32712440018e20;
        public const double MoonRadius = 1737400;
        public const double SunRadius = 695700000;
        public const double EarthSoi = 924000000;
        public const double MoonSoi = 66100000;
        const double Rad = Math.PI / 180;
        static readonly DateTime Epoch = new DateTime(1999, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        static EphemerisTable? moonTable, sunTable;
        /// <summary>Loads immutable offline reference vectors. Invalid data is rejected before either table changes.</summary>
        public static void ConfigureReferenceData(byte[] moonBytes, byte[] sunBytes)
        {
            var moon = new EphemerisTable(moonBytes); var sun = new EphemerisTable(sunBytes);
            moonTable=moon; sunTable=sun;
        }
        static readonly System.Collections.Generic.Dictionary<CelestialPrimary, EphemerisTable> planets = new System.Collections.Generic.Dictionary<CelestialPrimary, EphemerisTable>();
        public static void ConfigurePlanet(CelestialPrimary body, byte[] bytes)
        {
            if (Array.IndexOf(InnerPlanets, body) < 0)
                throw new ArgumentOutOfRangeException(nameof(body), "Expected a supported planet or dwarf planet.");
            var table = new EphemerisTable(bytes);
            planets[body] = table;
        }
        public static readonly CelestialPrimary[] InnerPlanets = { CelestialPrimary.Mercury, CelestialPrimary.Venus, CelestialPrimary.Mars, CelestialPrimary.Jupiter, CelestialPrimary.Saturn, CelestialPrimary.Ceres, CelestialPrimary.Uranus, CelestialPrimary.Neptune, CelestialPrimary.Pluto, CelestialPrimary.Eris, CelestialPrimary.Haumea, CelestialPrimary.Makemake };
        public static double Radius(CelestialPrimary body) => body == CelestialPrimary.Eris ? 1200000 : body == CelestialPrimary.Haumea ? 715000 : body == CelestialPrimary.Makemake ? 714000 : body == CelestialPrimary.Uranus ? 25362000 : body == CelestialPrimary.Neptune ? 24622000 : body == CelestialPrimary.Pluto ? 1188300 : body == CelestialPrimary.Jupiter ? 69911000 : body == CelestialPrimary.Saturn ? 58232000 : body == CelestialPrimary.Ceres ? 469720 : body == CelestialPrimary.Mercury ? 2439400 : body == CelestialPrimary.Venus ? 6051800 : body == CelestialPrimary.Mars ? 3389500 : body == CelestialPrimary.Moon ? MoonRadius : body == CelestialPrimary.Sun ? SunRadius : EarthFrames.SemiMajorAxisMeters;
        public static double Mu(CelestialPrimary body) => body == CelestialPrimary.Eris ? 1.1077338e12 : body == CelestialPrimary.Haumea ? 2.673529e11 : body == CelestialPrimary.Makemake ? 2.069033e11 : body == CelestialPrimary.Uranus ? 5.7939513e15 : body == CelestialPrimary.Neptune ? 6.83509997e15 : body == CelestialPrimary.Pluto ? 8.693e11 : body == CelestialPrimary.Jupiter ? 1.26686534196e17 : body == CelestialPrimary.Saturn ? 3.793120623e16 : body == CelestialPrimary.Ceres ? 6.262905e10 : body == CelestialPrimary.Mercury ? 2.2031868551e13 : body == CelestialPrimary.Venus ? 3.24858592e14 : body == CelestialPrimary.Mars ? 4.2828375816e13 : body == CelestialPrimary.Moon ? MoonMu : body == CelestialPrimary.Sun ? SunMu : Gravity.EarthGravitationalParameter;
        public double Soi(CelestialPrimary body, double t)
        {
            if (body == CelestialPrimary.Earth) return EarthSoi;
            if (body == CelestialPrimary.Moon) return MoonSoi;
            if (body == CelestialPrimary.Sun) return double.PositiveInfinity;
            return (Position(body,t)-SunPosition(t)).Magnitude()*Math.Pow(Mu(body)/SunMu,.4);
        }
        public bool TryPlanet(CelestialPrimary body, double t, out Vector3d p, out Vector3d v, out Vector3d a) => Reference(planets.TryGetValue(body,out var table) ? table : null,t,out p,out v,out a);
        public Vector3d Position(CelestialPrimary body,double t) { if(body==CelestialPrimary.Earth)return Vector3d.Zero; if(body==CelestialPrimary.Moon)return MoonPosition(t); if(body==CelestialPrimary.Sun)return SunPosition(t); if(TryPlanet(body,t,out var p,out _,out _))return p; throw new InvalidOperationException("Planet ephemeris unavailable"); }
        public Vector3d Velocity(CelestialPrimary body,double t) { if(body==CelestialPrimary.Earth)return Vector3d.Zero; if(body==CelestialPrimary.Moon)return MoonVelocity(t); if(body==CelestialPrimary.Sun)return SunVelocity(t); if(TryPlanet(body,t,out _,out var v,out _))return v; throw new InvalidOperationException("Planet ephemeris unavailable"); }
        readonly DateTime launch;
        internal DateTime LaunchUtc => launch;
        readonly double launchSidereal;
        public LunarEphemeris(DateTime launchUtc)
        {
            if (launchUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Launch time must be UTC.");
            launch = launchUtc;
            launchSidereal = CelestialFrames.GreenwichSiderealAngle(launchUtc);
        }
        static double Sin(double degrees) => Math.Sin(degrees * Rad);
        static double Cos(double degrees) => Math.Cos(degrees * Rad);
        static double Atan(double y, double x) => Math.Atan2(y, x) / Rad;
        static double Solve(double m, double e)
        {
            double mean = (m % 360) * Rad, eccentric = mean;
            for (int i = 0; i < 8; i++) eccentric -= (eccentric - e * Math.Sin(eccentric) - mean) / (1 - e * Math.Cos(eccentric));
            return eccentric;
        }
        Vector3d RotateToLaunch(double x, double y, double z, double d)
        {
            double obliquity = (23.4393 - 3.563e-7 * d) * Rad;
            return EarthFrames.InertialToEcef(new Vector3d(x, y * Math.Cos(obliquity) - z * Math.Sin(obliquity), y * Math.Sin(obliquity) + z * Math.Cos(obliquity)), launchSidereal);
        }
        bool Reference(EphemerisTable? table,double seconds,out Vector3d p,out Vector3d v,out Vector3d a)
        {
            if(table!=null && table.Sample(launch,seconds,out var rp,out var rv,out var ra))
            {
                p=CelestialFrames.J2000ToEcef(rp,launch); v=CelestialFrames.J2000ToEcef(rv,launch); a=CelestialFrames.J2000ToEcef(ra,launch);
                // Smoothly join the finite table to the approximate model over one week.
                const double blend=7*86400.0;
                double offset=table.OffsetSeconds(launch,seconds), remaining=table.DurationSeconds-offset;
                double distance=Math.Min(offset,remaining);
                if(distance<blend && (ReferenceEquals(table,moonTable) || ReferenceEquals(table,sunTable)))
                {
                    bool moon=ReferenceEquals(table,moonTable);
                    Vector3d f=moon ? ApproxMoonPosition(seconds) : ApproxSunPosition(seconds);
                    Vector3d fm=moon ? ApproxMoonPosition(seconds-2) : ApproxSunPosition(seconds-2);
                    Vector3d fp=moon ? ApproxMoonPosition(seconds+2) : ApproxSunPosition(seconds+2);
                    Vector3d am=moon ? ApproxMoonPosition(seconds-60) : ApproxSunPosition(seconds-60);
                    Vector3d ap=moon ? ApproxMoonPosition(seconds+60) : ApproxSunPosition(seconds+60);
                    Vector3d fv=(fp-fm)*.25, fa=(ap-f*2+am)*(1.0/3600);
                    double x=Math.Max(0,Math.Min(1,distance/blend)), x2=x*x, x3=x2*x;
                    double w=x3*(x*(6*x-15)+10), direction=offset<remaining ? 1 : -1;
                    double wd=direction*30*x2*(x-1)*(x-1)/blend;
                    double wdd=60*x*(2*x2-3*x+1)/(blend*blend);
                    a=fa+(a-fa)*w+(v-fv)*(2*wd)+(p-f)*wdd;
                    v=fv+(v-fv)*w+(p-f)*wd; p=f+(p-f)*w;
                }
                return true;
            }
            p=v=a=Vector3d.Zero; return false;
        }
        public bool HasReferenceData(double seconds) => Reference(moonTable,seconds,out _,out _,out _) && Reference(sunTable,seconds,out _,out _,out _);
        public Vector3d SunPosition(double seconds)
        {
            if(Reference(sunTable,seconds,out var p,out _,out _)) return p;
            return ApproxSunPosition(seconds);
        }
        Vector3d ApproxSunPosition(double seconds)
        {
            double d = (launch - Epoch).TotalDays + seconds / 86400;
            double w = 282.9404 + 4.70935e-5 * d, e = .016709 - 1.151e-9 * d, m = 356.0470 + .9856002585 * d;
            double E = Solve(m, e), x = Math.Cos(E) - e, y = Math.Sqrt(1-e*e) * Math.Sin(E);
            double r = Math.Sqrt(x*x+y*y) * 149597870700, lon = Atan(y,x) + w;
            return RotateToLaunch(r * Cos(lon), r * Sin(lon), 0, d);
        }
        public Vector3d MoonPosition(double seconds)
        {
            if(Reference(moonTable,seconds,out var p,out _,out _)) return p;
            return ApproxMoonPosition(seconds);
        }
        Vector3d ApproxMoonPosition(double seconds)
        {
            double d = (launch - Epoch).TotalDays + seconds / 86400;
            double n = 125.1228 - .0529538083*d, i = 5.1454, w = 318.0634 + .1643573223*d;
            double e = .054900, m = 115.3654 + 13.0649929509*d, E = Solve(m,e);
            double x = 60.2666*(Math.Cos(E)-e), y = 60.2666*Math.Sqrt(1-e*e)*Math.Sin(E);
            double r = Math.Sqrt(x*x+y*y), v = Atan(y,x);
            double xe = r*(Cos(n)*Cos(v+w)-Sin(n)*Sin(v+w)*Cos(i));
            double ye = r*(Sin(n)*Cos(v+w)+Cos(n)*Sin(v+w)*Cos(i));
            double ze = r*Sin(v+w)*Sin(i);
            double lon = Atan(ye,xe), lat = Atan(ze,Math.Sqrt(xe*xe+ye*ye));
            double ms = 356.0470 + .9856002585*d, ws = 282.9404 + 4.70935e-5*d;
            double ls = ms + ws, lm = n + w + m, D = lm-ls, F = lm-n;
            lon += -1.274*Sin(m-2*D)+.658*Sin(2*D)-.186*Sin(ms)-.059*Sin(2*m-2*D)-.057*Sin(m-2*D+ms)+.053*Sin(m+2*D)+.046*Sin(2*D-ms)+.041*Sin(m-ms)-.035*Sin(D)-.031*Sin(m+ms)-.015*Sin(2*F-2*D)+.011*Sin(m-4*D);
            lat += -.173*Sin(F-2*D)-.055*Sin(m-F-2*D)-.046*Sin(m+F-2*D)+.033*Sin(F+2*D)+.017*Sin(2*m+F);
            r += -.58*Cos(m-2*D)-.46*Cos(2*D);
            r *= EarthFrames.SemiMajorAxisMeters;
            return RotateToLaunch(r*Cos(lon)*Cos(lat),r*Sin(lon)*Cos(lat),r*Sin(lat),d);
        }
        public Vector3d MoonVelocity(double seconds) => Reference(moonTable,seconds,out _,out var v,out _) ? v : (MoonPosition(seconds+2)-MoonPosition(seconds-2))*.25;
        public Vector3d SunVelocity(double seconds) => Reference(sunTable,seconds,out _,out var v,out _) ? v : (SunPosition(seconds+2)-SunPosition(seconds-2))*.25;
        public Vector3d MoonAcceleration(double seconds) => Reference(moonTable,seconds,out _,out _,out var a) ? a : (MoonPosition(seconds+60)-MoonPosition(seconds)*2+MoonPosition(seconds-60))*(1.0/3600);
        public Vector3d SunAcceleration(double seconds) => Reference(sunTable,seconds,out _,out _,out var a) ? a : (SunPosition(seconds+60)-SunPosition(seconds)*2+SunPosition(seconds-60))*(1.0/3600);
        public CelestialPrimary Primary(Vector3d position, double seconds)
        {
            double earthDistance = position.Magnitude();
            // The Moon never comes within 250,000 km of Earth. Avoid ephemeris
            // evaluation for the common near-Earth launch and orbit cases.
            if (earthDistance < 250000000) return CelestialPrimary.Earth;
            if ((position-MoonPosition(seconds)).Magnitude() < MoonSoi) return CelestialPrimary.Moon;
            if (earthDistance < EarthSoi) return CelestialPrimary.Earth;
            foreach(var body in InnerPlanets) if(TryPlanet(body,seconds,out var p,out _,out _) && (position-p).Magnitude()<Soi(body,seconds)) return body;
            return CelestialPrimary.Sun;
        }
        static Vector3d Attraction(Vector3d relative, double mu, double radius = 0)
        {
            double r = relative.Magnitude();
            // RK trial stages can enter a body before the swept surface event
            // truncates the step. A uniform-sphere interior extension is finite
            // at the center and continuous at the surface. It is only used for
            // those rejected trial states; exterior gravity is unchanged.
            if (r < radius) return relative * (-mu/(radius*radius*radius));
            if (r < 1) throw new InvalidOperationException("Singular celestial gravity.");
            return relative * (-mu/(r*r*r));
        }
        /// <summary>Geocentric inertial acceleration. Moving-primary acceleration is included to preserve frame transformations.</summary>
        public Vector3d Acceleration(Vector3d position, double seconds, double gravityMultiplier = 1)
        {
            switch (Primary(position,seconds))
            {
                case CelestialPrimary.Moon: return MoonAcceleration(seconds)+Attraction(position-MoonPosition(seconds),MoonMu*gravityMultiplier,MoonRadius);
                case CelestialPrimary.Sun: return SunAcceleration(seconds)+Attraction(position-SunPosition(seconds),SunMu*gravityMultiplier,SunRadius);
                case CelestialPrimary.Earth: return Attraction(position,Gravity.EarthGravitationalParameter*gravityMultiplier);
                default: var body=Primary(position,seconds); if(TryPlanet(body,seconds,out var p,out _,out var a)) return a+Attraction(position-p,Mu(body)*gravityMultiplier,Radius(body)); throw new InvalidOperationException("Planet ephemeris unavailable");
            }
        }
    }
    public enum CelestialPrimary { Earth, Moon, Sun, Mercury, Venus, Mars, Jupiter, Saturn, Ceres, Uranus, Neptune, Pluto, Eris, Haumea, Makemake }
}

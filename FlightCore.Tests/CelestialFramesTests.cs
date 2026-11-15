using System;
using FlightCore;
namespace FlightCore.Tests
{
    internal static class CelestialFramesTests
    {
        public static void Run()
        {
            void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
            double Dot(Vector3d a, Vector3d b) => a.X*b.X+a.Y*b.Y+a.Z*b.Z;
            var epoch = new DateTime(2000,1,1,12,0,0,DateTimeKind.Utc);
            var x = new Vector3d(1,0,0); var y = new Vector3d(0,1,0); var z = new Vector3d(0,0,1);
            var greenwich = CelestialFrames.J2000ToEcef(x, epoch);
            Check(Math.Abs(greenwich.X - .18155965295) < 1e-9 && Math.Abs(greenwich.Y - .98337993290) < 1e-9, "J2000 GMST orientation");
            var quarter = CelestialFrames.J2000ToEcef(x, epoch.AddSeconds(86164.0905/4));
            Check(Math.Abs(quarter.X-greenwich.Y)<1e-6 && Math.Abs(quarter.Y+greenwich.X)<1e-6, "Sky turns west at sidereal rate");
            foreach (var utc in new[] { epoch, new DateTime(2026,11,15,0,0,0,DateTimeKind.Utc), epoch.AddYears(50) })
            {
                var a=CelestialFrames.J2000ToEcef(x,utc); var b=CelestialFrames.J2000ToEcef(y,utc); var c=CelestialFrames.J2000ToEcef(z,utc);
                Check(Math.Abs(a.Magnitude()-1)<1e-12 && Math.Abs(b.Magnitude()-1)<1e-12 && Math.Abs(c.Magnitude()-1)<1e-12, "Celestial unit axes");
                Check(Math.Abs(Dot(a,b))+Math.Abs(Dot(a,c))+Math.Abs(Dot(b,c))<1e-12, "Celestial orthogonal axes and transpose inverse");
            }
            // Meeus/IAU1976: precession of RA=0, Dec=0 to 2050 has positive ~0.278 degree declination.
            var future=CelestialFrames.J2000ToEcef(x,epoch.AddDays(36525*.5));
            Check(Math.Abs(Math.Asin(future.Z)*180/Math.PI-.27834)<.0001, "Precession sign and scale");
            try { CelestialFrames.J2000ToEcef(x, DateTime.SpecifyKind(epoch,DateTimeKind.Unspecified)); throw new Exception("Accepted unspecified UTC"); }
            catch (ArgumentException) { }
            Console.WriteLine("Celestial frames: epoch, sidereal direction/rate, orthogonality, precession and UTC passed.");
        }
    }
}

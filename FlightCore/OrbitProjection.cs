using System;
using System.Collections.Generic;

namespace FlightCore
{
    public enum OrbitMarkerKind { Apoapsis, Periapsis, Impact }

    public readonly struct OrbitMapMarker
    {
        public bool IsAvailable { get; }
        public Vector3d EcefPosition { get; }
        /// <summary>WGS84 height at this radial apsis, not the orbit's minimum geodetic height.</summary>
        public double Wgs84AltitudeMeters { get; }
        internal OrbitMapMarker(Vector3d ecef)
        {
            EcefPosition = ecef;
            Wgs84AltitudeMeters = EarthFrames.EcefToGeodetic(ecef).AltitudeMeters;
            IsAvailable = Wgs84AltitudeMeters >= -.02;
        }
    }

    public sealed class OrbitProjectionResult
    {
        public bool IsValid { get; internal set; }
        public bool IsClosed { get; internal set; }
        public bool HasImpact => Impact.IsAvailable;
        public string Status { get; internal set; } = "Projection unavailable";
        public IReadOnlyList<Vector3d> EcefPoints { get; internal set; } = Array.Empty<Vector3d>();
        public OrbitMapMarker Apoapsis { get; internal set; }
        public OrbitMapMarker Periapsis { get; internal set; }
        public OrbitMapMarker Impact { get; internal set; }
        public OrbitMapMarker Marker(OrbitMarkerKind kind) => kind == OrbitMarkerKind.Apoapsis ? Apoapsis : kind == OrbitMarkerKind.Periapsis ? Periapsis : Impact;
    }

    /// <summary>
    /// Gravity-only instantaneous osculating conic. ECI input is SI; ALL output points
    /// use the supplied CURRENT Earth angle (an orbit curve, never a future ground track).
    /// No dynamics are advanced. Open conics stop at 20 Earth radii or just beyond the
    /// current radius if already farther out. Radial degenerate states return unavailable.
    /// Conic basis: JPL NAIF oscelt, https://naif.jpl.nasa.gov/pub/naif/toolkit_docs/C/cspice/oscelt_c.html
    /// </summary>
    public static class OrbitProjection
    {
        public const int MaximumSamples = 512;
        private const double Tau = 2 * Math.PI;
        private const double A = EarthFrames.SemiMajorAxisMeters;
        private const double B = A * (1 - EarthFrames.Flattening);

        public static OrbitProjectionResult Build(Vector3d position, Vector3d velocity, double currentEarthAngle, int maxSamples = MaximumSamples)
        {
            var result = new OrbitProjectionResult();
            if (!Finite(currentEarthAngle)) return result;
            maxSamples = Math.Max(16, Math.Min(MaximumSamples, maxSamples));
            try
            {
                double r = position.Magnitude(), v = velocity.Magnitude();
                if (!Finite(r) || !Finite(v) || r < B || r > 1e12 || v > 1e7) return result;
                if (Surface(position) < 1 - 1e-10) { result.Status = "Below WGS84 surface"; return result; }
                Vector3d h = Cross(position, velocity);
                double hm = h.Magnitude();
                if (hm < r * Math.Max(1, v) * 1e-9) { result.Status = "Radial coast — no orbital plane"; return result; }
                double mu = Gravity.EarthGravitationalParameter, p = hm * hm / mu;
                var ev = Cross(velocity, h) * (1 / mu) - position * (1 / r);
                double e = ev.Magnitude();
                Vector3d x = e > 1e-8 ? ev * (1 / e) : position * (1 / r);
                // Preserve the actual tiny eccentricity vector rather than inventing an apsis.
                if (e <= 1e-8) e = 0;
                Vector3d y = Cross(h * (1 / hm), x);
                double start = Math.Atan2(Dot(position, y), Dot(position, x));
                bool bound = v * v / 2 - mu / r < 0 && e < 1;
                double end = start + Tau;
                if (!bound)
                {
                    double limitRadius = Math.Max(20 * A, r * 1.05);
                    double cosine = (p / limitRadius - 1) / Math.Max(e, 1e-30);
                    end = Math.Acos(Math.Max(-1, Math.Min(1, cosine)));
                    // Open-conic physical anomaly is in (-acos(-1/e), +acos(-1/e)).
                    if (end <= start + 1e-10) { result.Status = "Open coast beyond map range"; return result; }
                }

                // In the orbit plane the ellipsoid intersection is a quartic in tan(nu/2).
                // Solve in two bounded hemispheres, including tangent roots, rather than
                // looking only at line vertices (which can skip a shallow ground crossing).
                double ax = EllipsoidDot(x, x), xy = EllipsoidDot(x, y), ay = EllipsoidDot(y, y);
                var roots = new List<double>();
                AddSurfaceRoots(roots, p, e, ax, xy, ay, false);
                AddSurfaceRoots(roots, p, e, ax, xy, ay, true);
                double initialSurface = Surface(position);
                double initialSurfaceRate = EllipsoidDot(position, velocity);
                bool startsOnSurface = initialSurface <= 1 + 1e-10;
                double impactAt = startsOnSurface && initialSurfaceRate <= 1e-12 ? start : double.PositiveInfinity;
                foreach (double root in roots)
                {
                    double delta = Forward(root - start);
                    // A root just behind the epoch can be the same surface contact after
                    // floating-point roundoff, not a different crossing one orbit later.
                    if (delta < 1e-9 || Tau - delta < 1e-9) delta = 0;
                    double anomaly = start + delta;
                    if (1 + e * Math.Cos(anomaly) <= 0 || anomaly > end + 1e-9) continue;
                    // A surface start moving away is allowed; an inward start impacts now.
                    if (delta == 0 && startsOnSurface && initialSurfaceRate > 1e-12) continue;
                    impactAt = Math.Min(impactAt, anomaly);
                }
                bool impact = Finite(impactAt);
                if (impact) end = Math.Min(end, impactAt);
                var points = new Vector3d[maxSamples];
                for (int i = 0; i < points.Length; i++)
                {
                    var inertial = i == 0 ? position : Point(start + (end - start) * i / (points.Length - 1), p, e, x, y);
                    points[i] = EarthFrames.InertialToEcef(inertial, currentEarthAngle);
                }
                result.IsValid = true;
                result.IsClosed = bound && !impact;
                result.EcefPoints = Array.AsReadOnly(points);
                result.Status = impact ? "Coast intersects WGS84" : bound ? "Bound coast" : "Open escape coast";
                if (impact) result.Impact = new OrbitMapMarker(points[points.Length - 1]);
                // Tiny circular eccentricities have no meaningful radial apsis orientation.
                if (e > 1e-8)
                {
                    result.Periapsis = Apsis(0, start, end, p, e, x, y, currentEarthAngle);
                    if (bound) result.Apoapsis = Apsis(Math.PI, start, end, p, e, x, y, currentEarthAngle);
                }
                return result;
            }
            catch (ArgumentException) { return new OrbitProjectionResult(); }
            catch (InvalidOperationException) { return new OrbitProjectionResult(); }
        }

        private static OrbitMapMarker Apsis(double angle, double start, double end, double p, double e, Vector3d x, Vector3d y, double earthAngle)
        {
            double forward = start + Forward(angle - start);
            if (forward > end + 1e-8) return default;
            var point = Point(angle, p, e, x, y);
            if (Surface(point) < 1 - 1e-10) return default;
            return new OrbitMapMarker(EarthFrames.InertialToEcef(point, earthAngle));
        }

        private static void AddSurfaceRoots(List<double> anomalies, double p, double e, double ax, double xy, double ay, bool opposite)
        {
            double sign = opposite ? -1 : 1, q = p * p;
            double u = 1 + sign * e, w = 1 - sign * e;
            double[] coefficients = { q * ax - u * u, 4 * q * xy, q * (-2 * ax + 4 * ay) - 2 * u * w, -4 * q * xy, q * ax - w * w };
            foreach (double t in PolynomialRoots(coefficients)) anomalies.Add(2 * Math.Atan(t) + (opposite ? Math.PI : 0));
        }

        // Real-root isolation by recursively finding derivative roots. Degree <= 4,
        // t in [-1,1]; at most 4*56 bisections per hemisphere, no unbounded solver.
        private static List<double> PolynomialRoots(double[] c)
        {
            double scale = 0;
            for (int i = 0; i < c.Length; i++) scale = Math.Max(scale, Math.Abs(c[i]));
            if (!Finite(scale) || scale == 0) return new List<double>();
            for (int i = 0; i < c.Length; i++) c[i] /= scale;
            int degree = c.Length - 1;
            while (degree > 0 && Math.Abs(c[degree]) < 1e-15) degree--;
            if (degree == 0) return new List<double>();
            var derivative = new double[degree];
            for (int i = 1; i <= degree; i++) derivative[i - 1] = i * c[i];
            var bounds = PolynomialRoots(derivative);
            bounds.Insert(0, -1); bounds.Add(1);
            bounds.Sort();
            var roots = new List<double>();
            foreach (double t in bounds) if (Math.Abs(Evaluate(c, degree, t)) < 1e-12) roots.Add(t);
            for (int i = 1; i < bounds.Count; i++)
            {
                double lo = bounds[i - 1], hi = bounds[i], flo = Evaluate(c, degree, lo), fhi = Evaluate(c, degree, hi);
                if (flo * fhi >= 0) continue;
                for (int n = 0; n < 56; n++)
                {
                    double mid = (lo + hi) / 2, fm = Evaluate(c, degree, mid);
                    if ((fm > 0) == (flo > 0)) { lo = mid; flo = fm; } else hi = mid;
                }
                roots.Add((lo + hi) / 2);
            }
            roots.Sort(); return roots;
        }
        private static double Evaluate(double[] c, int degree, double t) { double v = c[degree]; for (int i = degree - 1; i >= 0; i--) v = v * t + c[i]; return v; }
        private static Vector3d Point(double nu, double p, double e, Vector3d x, Vector3d y) => (x * Math.Cos(nu) + y * Math.Sin(nu)) * (p / (1 + e * Math.Cos(nu)));
        private static double Forward(double a) { a %= Tau; return a < 0 ? a + Tau : a; }
        private static double EllipsoidDot(Vector3d x, Vector3d y) => (x.X * y.X + x.Y * y.Y) / (A * A) + x.Z * y.Z / (B * B);
        private static double Surface(Vector3d x) => EllipsoidDot(x, x);
        private static double Dot(Vector3d x, Vector3d y) => x.X * y.X + x.Y * y.Y + x.Z * y.Z;
        private static Vector3d Cross(Vector3d x, Vector3d y) => new Vector3d(x.Y * y.Z - x.Z * y.Y, x.Z * y.X - x.X * y.Z, x.X * y.Y - x.Y * y.X);
        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}

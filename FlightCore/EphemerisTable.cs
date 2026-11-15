using System;
using System.IO;
namespace FlightCore
{
    /// <summary>UTC-sampled JPL ICRF position and velocity with cubic Hermite interpolation.</summary>
    internal sealed class EphemerisTable
    {
        readonly DateTime start;
        readonly double interval;
        readonly double[][] values;
        public EphemerisTable(byte[] bytes)
        {
            using (var reader=new BinaryReader(new MemoryStream(bytes,false)))
            {
                if(new string(reader.ReadChars(4))!="ESP1") throw new InvalidDataException("Bad ephemeris header");
                int year=reader.ReadInt32(), hours=reader.ReadInt32(), count=reader.ReadInt32();
                if(year<1900 || year>2100 || hours<1 || hours>48 || count<2 || count>200000 || bytes.Length!=16+count*48) throw new InvalidDataException("Bad ephemeris size");
                start=new DateTime(year,1,1,0,0,0,DateTimeKind.Utc); interval=hours*3600.0;
                values=new double[count][];
                for(int i=0;i<count;i++)
                {
                    var row=new double[6];
                    for(int j=0;j<6;j++) { row[j]=reader.ReadDouble(); if(double.IsNaN(row[j])||double.IsInfinity(row[j])) throw new InvalidDataException("Invalid ephemeris value"); }
                    values[i]=row;
                }
            }
        }
        public double OffsetSeconds(DateTime launch, double seconds) => (launch-start).TotalSeconds+seconds;
        public double DurationSeconds => (values.Length-1)*interval;
        public bool Sample(DateTime launch, double seconds, out Vector3d position, out Vector3d velocity, out Vector3d acceleration)
        {
            if (launch.Kind != DateTimeKind.Utc) throw new ArgumentException("Ephemeris time must be UTC.", nameof(launch));
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            double t=(launch-start).TotalSeconds+seconds, index=t/interval;
            if(index<0 || index>values.Length-1) { position=velocity=acceleration=Vector3d.Zero; return false; }
            int k=Math.Min(values.Length-2,(int)Math.Floor(index)); double u=index-k, u2=u*u, u3=u2*u;
            var a=values[k]; var b=values[k+1];
            Axis(a,b,0,u,u2,u3,out double px,out double vx,out double ax);
            Axis(a,b,1,u,u2,u3,out double py,out double vy,out double ay);
            Axis(a,b,2,u,u2,u3,out double pz,out double vz,out double az);
            position=new Vector3d(px,py,pz); velocity=new Vector3d(vx,vy,vz); acceleration=new Vector3d(ax,ay,az); return true;
        }
        void Axis(double[] a,double[] b,int j,double u,double u2,double u3,out double p,out double v,out double acc)
        {
            p=(2*u3-3*u2+1)*a[j]+(u3-2*u2+u)*interval*a[j+3]+(-2*u3+3*u2)*b[j]+(u3-u2)*interval*b[j+3];
            v=((6*u2-6*u)*a[j]+(3*u2-4*u+1)*interval*a[j+3]+(-6*u2+6*u)*b[j]+(3*u2-2*u)*interval*b[j+3])/interval;
            acc=((12*u-6)*a[j]+(6*u-4)*interval*a[j+3]+(-12*u+6)*b[j]+(6*u-2)*interval*b[j+3])/(interval*interval);
        }
    }
}

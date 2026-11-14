using System;
namespace FlightCore
{
    public readonly struct RocketSnapshot
    {
        public Vector3d Position {get;} public Vector3d Velocity {get;} public double MissionTime {get;} public double PropellantKg {get;} public int StageIndex {get;} public double MassKg {get;} public double AltitudeMeters {get;} public double SpeedRelativeGround {get;} public double DynamicPressurePascals {get;} public bool IsLanded {get;} public bool IsLaunched {get;} public bool IsComplete {get;} public string Status {get;}
        internal RocketSnapshot(Vector3d p,Vector3d v,double time,double fuel,int stage,double mass,double alt,double speed,double q,bool landed,bool launched,bool complete,string status){Position=p;Velocity=v;MissionTime=time;PropellantKg=fuel;StageIndex=stage;MassKg=mass;AltitudeMeters=alt;SpeedRelativeGround=speed;DynamicPressurePascals=q;IsLanded=landed;IsLaunched=launched;IsComplete=complete;Status=status;}
    }
    /// <summary>Point-mass inertial flight. WGS84 pad-altitude surface is a terrain approximation; impact time is linearly interpolated within a fixed step.</summary>
    public sealed class RocketSimulation
    {
        const double G0=9.80665, Omega=Aerodynamics.EarthAngularVelocityRadiansPerSecond;
        readonly double step,lat,lon,pad; readonly SolidBoosterDefinition? booster; bool boosterAttached; double boosterFuel; readonly StageDefinition[] stages; readonly WeatherAtmosphere atmosphere;
        Vector3d position,velocity; double time,fuel,actualPitch,actualHeading,throttle,pitch=90,heading=90; int stage; bool commanded,launched,landed;
        public bool BoosterAttached => boosterAttached;
        public double BoosterPropellantKg => boosterFuel;
        public bool BoosterBurning => commanded && !landed && boosterAttached && boosterFuel > 0;
        public double ActualPitchDegrees => actualPitch;
        public double ActualHeadingDegrees => actualHeading;
        public double Throttle {get=>throttle;set {Check(value,0,1);throttle=value;}}
        public double PitchDegrees {get=>pitch;set {Check(value,0,90);pitch=value;}}
        public double HeadingDegrees {get=>heading;set {Check(value,0,360);heading=value;}}
        public RocketSimulation(double step,StageDefinition[] stages,WeatherAtmosphere atmosphere,double latitudeRadians,double longitudeRadians,double padAltitudeMeters=0, SolidBoosterDefinition? booster=null)
        {
            if(!Finite(step)||step<=0||step>10)throw new ArgumentOutOfRangeException(nameof(step)); if(stages==null||stages.Length==0||Array.Exists(stages,x=>x==null))throw new ArgumentException("At least one valid stage required",nameof(stages));
            Check(latitudeRadians,-Math.PI/2,Math.PI/2);if(!Finite(longitudeRadians)||!Finite(padAltitudeMeters))throw new ArgumentOutOfRangeException(nameof(longitudeRadians));
            this.step=step;this.stages=(StageDefinition[])stages.Clone();this.atmosphere=atmosphere??throw new ArgumentNullException(nameof(atmosphere));lat=latitudeRadians;lon=longitudeRadians;pad=padAltitudeMeters;this.booster=booster;Reset();
        }
        public void Reset(){position=EarthFrames.GeodeticToEcef(lat,lon,pad);velocity=Rotation(position);time=0;stage=0;fuel=stages[0].PropellantKg;commanded=launched=landed=false;boosterAttached=booster!=null;boosterFuel=booster?.PropellantKg??0;actualPitch=90;actualHeading=90;throttle=0;pitch=90;heading=90;}
        public void Launch(){if(!landed)commanded=true;}
        public bool Stage(){if(landed||stage>=stages.Length-1)return false;stage++;fuel=stages[stage].PropellantKg;return true;}
        /// <summary>Discard spent booster dry mass without a separation impulse. Burning boosters cannot be jettisoned.</summary>
        public bool JettisonBoosters(){if(!boosterAttached||boosterFuel>0||landed)return false;boosterAttached=false;return true;}
        public RocketSnapshot State {get {var geo=Geo(position,time);var air=Air(position,time,geo);var drag=Aerodynamics.Compute(velocity,air.velocity,air.sample.DensityKgPerCubicMeter,stages[stage].DragCoefficient,stages[stage].ReferenceAreaSquareMeters,Mass(fuel,boosterFuel));return new RocketSnapshot(position,velocity,time,fuel,stage,Mass(fuel,boosterFuel),geo.AltitudeMeters,(velocity-Rotation(position)).Magnitude(),drag.DynamicPressurePascals,landed,launched,landed,landed?"Landed":launched?(fuel<=0&&!BoosterBurning?"Coasting: fuel depleted":"Flight"):commanded?"Holding on pad":"Ready");}}
        public void Step()
        {
            if(landed||!commanded)return;
            actualPitch=Move(actualPitch,pitch,5*step);double delta=((heading-actualHeading+540)%360)-180;actualHeading=(actualHeading+Math.Max(-5*step,Math.Min(5*step,delta))+360)%360;
            if(!launched){var air=Air(position,time,Geo(position,time));var engine=Engine(air.sample.PressurePascals);double coreThrust=fuel>0?engine.thrust:0;double solidThrust=BoosterBurning?booster!.ThrustNewtons:0;double thrust=coreThrust+solidThrust;var up=Direction(position,time,90,0);double gravity=-(Gravity.Acceleration(position).X*up.X+Gravity.Acceleration(position).Y*up.Y+Gravity.Acceleration(position).Z*up.Z);if(thrust*Math.Sin(actualPitch*Math.PI/180)<=Mass(fuel,boosterFuel)*gravity){
                // Hold-down burns both propellants; a mass-driven liftoff is checked next tick.
                fuel=Math.Max(0,fuel-coreThrust/(engine.isp*G0)*step);
                boosterFuel=Math.Max(0,boosterFuel-(BoosterBurning?booster!.MassFlowKgPerSecond:0)*step);
                time+=step;position=EarthFrames.EcefToInertial(EarthFrames.GeodeticToEcef(lat,lon,pad),Omega*time);velocity=Rotation(position);return;}launched=true;}
            double remaining=step;
            while(remaining>1e-10&&!landed){
                bool coreBurn=fuel>0, solidBurn=BoosterBurning;
                var start=new Y(position,velocity,fuel,boosterFuel);
                // Solid mass flow is constant: split exactly at its independent burnout event.
                double solidDuration=solidBurn?boosterFuel/booster!.MassFlowKgPerSecond:double.PositiveInfinity;
                double duration=Math.Min(remaining,solidDuration);
                var end=Integrate(start,time,duration,coreBurn,solidBurn);
                if(coreBurn&&end.f<0){double lo=0,hi=duration;for(int i=0;i<50;i++){double mid=(lo+hi)*.5;if(Integrate(start,time,mid,true,solidBurn).f>0)lo=mid;else hi=mid;}duration=hi;end=Integrate(start,time,duration,true,solidBurn);end.f=0;}
                if(solidBurn&&duration>=solidDuration)end.b=0;
                double oldAlt=Geo(position,time).AltitudeMeters-pad,newAlt=Geo(end.p,time+duration).AltitudeMeters-pad;
                if(oldAlt>=0&&newAlt<0){double fraction=oldAlt/(oldAlt-newAlt);position=start.p+(end.p-start.p)*fraction;time+=duration*fraction;var geo=Geo(position,time);position=EarthFrames.EcefToInertial(EarthFrames.GeodeticToEcef(geo.LatitudeRadians,geo.LongitudeRadians,pad),Omega*time);velocity=Rotation(position);fuel=Math.Max(0,start.f+(end.f-start.f)*fraction);boosterFuel=Math.Max(0,start.b+(end.b-start.b)*fraction);landed=true;break;}
                position=end.p;velocity=end.v;fuel=Math.Max(0,end.f);boosterFuel=Math.Max(0,end.b);time+=duration;remaining-=duration;
            }
        }
        struct Y {public Vector3d p,v;public double f,b;public Y(Vector3d p,Vector3d v,double f,double b){this.p=p;this.v=v;this.f=f;this.b=b;} public static Y operator +(Y a,Y b)=>new Y(a.p+b.p,a.v+b.v,a.f+b.f,a.b+b.b);public static Y operator *(Y a,double n)=>new Y(a.p*n,a.v*n,a.f*n,a.b*n);}
        Y Integrate(Y y,double t,double h,bool burn,bool solidBurn){var k1=Derivative(y,t,burn,solidBurn);var k2=Derivative(y+k1*(h*.5),t+h*.5,burn,solidBurn);var k3=Derivative(y+k2*(h*.5),t+h*.5,burn,solidBurn);var k4=Derivative(y+k3*h,t+h,burn,solidBurn);return y+(k1+k2*2+k3*2+k4)*(h/6);}
        Y Derivative(Y y,double t,bool burn,bool solidBurn){var geo=Geo(y.p,t);var air=Air(y.p,t,geo);var engine=Engine(air.sample.PressurePascals);double coreThrust=burn?engine.thrust:0;double thrust=coreThrust+(solidBurn?booster!.ThrustNewtons:0);double mass=Mass(Math.Max(0,y.f),Math.Max(0,y.b));var drag=Aerodynamics.Compute(y.v,air.velocity,air.sample.DensityKgPerCubicMeter,stages[stage].DragCoefficient,stages[stage].ReferenceAreaSquareMeters,mass);return new Y(y.v,Gravity.Acceleration(y.p)+Direction(y.p,t,actualPitch,actualHeading)*(thrust/mass)+drag.AccelerationMetersPerSecondSquared,burn?-coreThrust/(engine.isp*G0):0,solidBurn?-booster!.MassFlowKgPerSecond:0);}
        (double thrust,double isp) Engine(double pressure){double f=Math.Max(0,Math.Min(1,pressure/101325));var s=stages[stage];return ((s.ThrustVacuumNewtons+(s.ThrustSeaLevelNewtons-s.ThrustVacuumNewtons)*f)*throttle,s.IspVacuumSeconds+(s.IspSeaLevelSeconds-s.IspVacuumSeconds)*f);}
        double Mass(double currentFuel,double currentBoosterFuel){double m=stages[stage].DryMassKg+currentFuel+(boosterAttached?booster!.DryMassKg+currentBoosterFuel:0);for(int i=stage+1;i<stages.Length;i++)m+=stages[i].DryMassKg+stages[i].PropellantKg;return m;}
        GeodeticPosition Geo(Vector3d p,double t)=>EarthFrames.EcefToGeodetic(EarthFrames.InertialToEcef(p,Omega*t));
        (AtmosphereSample sample,Vector3d velocity) Air(Vector3d p,double t,GeodeticPosition g){// Clamp only intermediate impact-bracketing samples below the atmosphere domain.
            var a=atmosphere.Sample(Math.Max(StandardAtmosphere.MinimumGeometricAltitudeMeters,g.AltitudeMeters));var w=Aerodynamics.WindToInertial(a.WindEastNorthUpMetersPerSecond,g.LatitudeRadians,g.LongitudeRadians+Omega*t);return (a,Aerodynamics.CorotatingAirVelocity(p,w));}
        Vector3d Direction(Vector3d p,double t,double pitchDegrees,double headingDegrees){var g=Geo(p,t);double a=pitchDegrees*Math.PI/180,h=headingDegrees*Math.PI/180;return Aerodynamics.WindToInertial(new Vector3d(Math.Cos(a)*Math.Sin(h),Math.Cos(a)*Math.Cos(h),Math.Sin(a)),g.LatitudeRadians,g.LongitudeRadians+Omega*t);}
        static Vector3d Rotation(Vector3d p)=>new Vector3d(-Omega*p.Y,Omega*p.X,0);
        static double Move(double a,double b,double d)=>a+Math.Max(-d,Math.Min(d,b-a)); static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);static void Check(double v,double min,double max){if(!Finite(v)||v<min||v>max)throw new ArgumentOutOfRangeException(nameof(v));}
    }
}

using System;
namespace FlightCore
{
    public readonly struct RocketSnapshot
    {
        public Vector3d Position {get;} public Vector3d Velocity {get;} public double MissionTime {get;} public double PropellantKg {get;} public int StageIndex {get;} public double MassKg {get;} public double AltitudeMeters {get;} public double SpeedRelativeGround {get;} public double DynamicPressurePascals {get;} public bool IsLanded {get;} public bool IsLaunched {get;} public bool IsComplete {get;} public bool IsDestroyed {get;} public string Status {get;}
        internal RocketSnapshot(Vector3d p,Vector3d v,double time,double fuel,int stage,double mass,double alt,double speed,double q,bool landed,bool launched,bool complete,bool destroyed,string status){Position=p;Velocity=v;MissionTime=time;PropellantKg=fuel;StageIndex=stage;MassKg=mass;AltitudeMeters=alt;SpeedRelativeGround=speed;DynamicPressurePascals=q;IsLanded=landed;IsLaunched=launched;IsComplete=complete;IsDestroyed=destroyed;Status=status;}
    }
    /// <summary>Point-mass inertial flight. WGS84 pad-altitude surface is a terrain approximation; impact time is linearly interpolated within a fixed step.</summary>
    public sealed class RocketSimulation
    {
        const double G0=9.80665, Omega=Aerodynamics.EarthAngularVelocityRadiansPerSecond;
        /// <summary>Point-mass gameplay damage threshold, not a structural or crew survivability model.</summary>
        public const double DestructiveImpactSpeedMetersPerSecond = 10;
        readonly double step,lat,lon,pad; readonly SolidBoosterDefinition? booster; bool boosterAttached; double boosterFuel; readonly StageDefinition[] stages; readonly WeatherAtmosphere atmosphere;
        Vector3d position,velocity; double time,fuel,actualPitch,actualHeading,throttle,pitch=90,heading=90; int stage; bool commanded,launched,landed,destroyed;
        double gravityMultiplier = 1;
        internal bool ReturnMode;
        internal Vector3d ReturnCommand, ReturnAxis;
        internal double ReturnBankRadians;
        public Vector3d ReturnAttitudeDirection => ReturnAxis;
        public double ReturnBankAngleRadians => ReturnBankRadians;
        public Vector3d AirRelativeVelocity { get { var geo=Geo(position,time);return velocity-Air(position,time,geo).velocity; } }
        public double ReturnHeatFluxWattsPerSquareMeter => CurrentForces().HeatFlux;
        public bool IsCaught { get; private set; }
        internal double FixedStep => step;
        internal bool SupportsStarshipReturn => stages[stage].SupportsStarshipReturn;
        internal double ReturnAvailableThrust { get { var s=stages[stage]; var pressure=CurrentForces().Atmosphere.PressurePascals;double f=Math.Max(0,Math.Min(1,pressure/101325));return s.ThrustVacuumNewtons+(s.ThrustSeaLevelNewtons-s.ThrustVacuumNewtons)*f; } }
        internal RocketSimulation ReturnClone(double newStep)
        {
            var c=new RocketSimulation(newStep,stages,atmosphere,lat,lon,pad,booster);
            c.position=position;c.velocity=velocity;c.time=time;c.fuel=fuel;c.stage=stage;c.boosterFuel=boosterFuel;c.boosterAttached=boosterAttached;
            c.gravityMultiplier=gravityMultiplier;c.infiniteFuel=infiniteFuel;c.crashDestructionEnabled=crashDestructionEnabled;c.HasUsedCheats=HasUsedCheats;
            c.commanded=commanded;c.launched=launched;c.landed=landed;c.destroyed=destroyed;c.actualPitch=actualPitch;c.actualHeading=actualHeading;
            c.ReturnBankRadians=ReturnBankRadians;c.ReturnMode=ReturnMode;c.ReturnAxis=ReturnAxis;c.ReturnCommand=ReturnCommand;c.throttle=throttle;
            return c;
        }
        internal void EnableReturn() { ReturnMode=true;ReturnAxis=Direction(Geo(position,time),time,actualPitch,actualHeading);ReturnCommand=ReturnAxis;throttle=0; }
        internal void CatchAtCurrentPosition() { IsCaught=true;landed=true;throttle=0;velocity=Rotation(position); }

        bool infiniteFuel, crashDestructionEnabled = true;
        /// <summary>Scales the same inverse-square acceleration used by integration and liftoff. Scientific default: 1.</summary>
        public double GravityMultiplier { get => gravityMultiplier; set { Check(value,0,5); gravityMultiplier=value; if(value!=1) HasUsedCheats=true; } }
        /// <summary>Freezes remaining liquid and attached solid propellant; never refills an empty tank.</summary>
        public bool InfiniteFuel { get => infiniteFuel; set { infiniteFuel=value; if(value) HasUsedCheats=true; } }
        /// <summary>Disabling damage still stops the vehicle at the surface; it does not disable collision.</summary>
        public bool CrashDestructionEnabled { get => crashDestructionEnabled; set { crashDestructionEnabled=value; if(!value) HasUsedCheats=true; } }
        public bool HasUsedCheats { get; private set; }
        public bool IsDestroyed => destroyed;
        public double ImpactSpeedMetersPerSecond { get; private set; }
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
        public void Reset(){position=EarthFrames.GeodeticToEcef(lat,lon,pad);velocity=Rotation(position);time=0;stage=0;fuel=stages[0].PropellantKg;commanded=launched=landed=destroyed=false;boosterAttached=booster!=null;boosterFuel=booster?.PropellantKg??0;actualPitch=90;actualHeading=90;throttle=0;pitch=90;heading=90;gravityMultiplier=1;infiniteFuel=false;crashDestructionEnabled=true;HasUsedCheats=false;ImpactSpeedMetersPerSecond=0;ReturnMode=false;IsCaught=false;ReturnBankRadians=0;ReturnAxis=ReturnCommand=Vector3d.Zero;}
        public void Launch(){if(!landed)commanded=true;}
        public bool Stage(){if(landed||stage>=stages.Length-1)return false;stage++;fuel=stages[stage].PropellantKg;return true;}
        /// <summary>Discard spent booster dry mass without a separation impulse. Burning boosters cannot be jettisoned.</summary>
        public bool JettisonBoosters(){if(!boosterAttached||boosterFuel>0||landed)return false;boosterAttached=false;return true;}
        public RocketSnapshot State {get {
            var forces=CurrentForces();
            string status=destroyed?"Destroyed: ground impact":IsCaught?"Caught at Starbase":landed?"Landed":launched?(fuel<=0&&!BoosterBurning?"Coasting: fuel depleted":"Flight"):commanded?"Holding on pad":"Ready";
            return new RocketSnapshot(position,velocity,time,fuel,stage,forces.Mass,forces.Geodetic.AltitudeMeters,(velocity-Rotation(position)).Magnitude(),forces.Drag.DynamicPressurePascals,landed,launched,landed,destroyed,status);
        }}
        /// <summary>Instantaneous shared force evaluation at the current state. Net acceleration excludes pad/ground support; label it free-flight acceleration when constrained.</summary>
        public RocketPhysicsTelemetry Physics {get {
            var forces=CurrentForces();
            double totalFuel=Math.Max(0,fuel)+(boosterAttached?Math.Max(0,boosterFuel):0);
            for(int i=stage+1;i<stages.Length;i++)totalFuel+=stages[i].PropellantKg;
            return new RocketPhysicsTelemetry(forces.Gravity.Magnitude(),forces.Atmosphere.PressurePascals,forces.Atmosphere.DensityKgPerCubicMeter,
                forces.Drag.ForceNewtons.Magnitude(),forces.Thrust,forces.Acceleration.Magnitude(),forces.Drag.AirSpeedMetersPerSecond,
                totalFuel,forces.Atmosphere.TemperatureKelvin,forces.Drag.DynamicPressurePascals);
        }}
        ForceSample CurrentForces()=>EvaluateForces(new Y(position,velocity,fuel,boosterFuel),time,commanded&&!landed&&fuel>0,BoosterBurning);
        public void Step()
        {
            if(landed||!commanded)return;
            if(ReturnMode) ReturnAxis=StarshipReturnController.Slew(ReturnAxis,ReturnCommand,10*Math.PI/180*step);
            actualPitch=Move(actualPitch,pitch,5*step);double delta=((heading-actualHeading+540)%360)-180;actualHeading=(actualHeading+Math.Max(-5*step,Math.Min(5*step,delta))+360)%360;
            if(!launched){var padGeo=Geo(position,time);var air=Air(position,time,padGeo);var engine=Engine(air.sample.PressurePascals);double coreThrust=fuel>0?engine.thrust:0;double solidThrust=BoosterBurning?booster!.ThrustNewtons:0;double thrust=coreThrust+solidThrust;var up=Direction(padGeo,time,90,0);var gravityVector=Gravity.Acceleration(position)*gravityMultiplier;double gravity=-(gravityVector.X*up.X+gravityVector.Y*up.Y+gravityVector.Z*up.Z);if(thrust*Math.Sin(actualPitch*Math.PI/180)<=Mass(fuel,boosterFuel)*gravity){
                // Hold-down burns both propellants; a mass-driven liftoff is checked next tick.
                if(!infiniteFuel){fuel=Math.Max(0,fuel-coreThrust/(engine.isp*G0)*step);
                boosterFuel=Math.Max(0,boosterFuel-(BoosterBurning?booster!.MassFlowKgPerSecond:0)*step);}
                time+=step;position=EarthFrames.EcefToInertial(EarthFrames.GeodeticToEcef(lat,lon,pad),Omega*time);velocity=Rotation(position);return;}launched=true;}
            double remaining=step;
            while(remaining>1e-10&&!landed){
                bool coreBurn=fuel>0, solidBurn=BoosterBurning;
                var start=new Y(position,velocity,fuel,boosterFuel);
                // Solid mass flow is constant: split exactly at its independent burnout event.
                double solidDuration=solidBurn&&!infiniteFuel?boosterFuel/booster!.MassFlowKgPerSecond:double.PositiveInfinity;
                double duration=Math.Min(remaining,solidDuration);
                var end=Integrate(start,time,duration,coreBurn,solidBurn);
                if(coreBurn&&!infiniteFuel&&end.f<0){double lo=0,hi=duration;for(int i=0;i<50;i++){double mid=(lo+hi)*.5;if(Integrate(start,time,mid,true,solidBurn).f>0)lo=mid;else hi=mid;}duration=hi;end=Integrate(start,time,duration,true,solidBurn);end.f=0;}
                if(solidBurn&&duration>=solidDuration)end.b=0;
                double oldAlt=Geo(position,time).AltitudeMeters-pad,newAlt=Geo(end.p,time+duration).AltitudeMeters-pad;
                if(oldAlt>=0&&newAlt<0){double fraction=oldAlt/(oldAlt-newAlt);position=start.p+(end.p-start.p)*fraction;time+=duration*fraction;var geo=Geo(position,time);position=EarthFrames.EcefToInertial(EarthFrames.GeodeticToEcef(geo.LatitudeRadians,geo.LongitudeRadians,pad),Omega*time);
                    var impactVelocity=start.v+(end.v-start.v)*fraction;
                    ImpactSpeedMetersPerSecond=(impactVelocity-Rotation(position)).Magnitude();
                    destroyed=crashDestructionEnabled&&ImpactSpeedMetersPerSecond>DestructiveImpactSpeedMetersPerSecond;
                    velocity=Rotation(position);fuel=Math.Max(0,start.f+(end.f-start.f)*fraction);boosterFuel=Math.Max(0,start.b+(end.b-start.b)*fraction);landed=true;break;}
                position=end.p;velocity=end.v;fuel=Math.Max(0,end.f);boosterFuel=Math.Max(0,end.b);time+=duration;remaining-=duration;
            }
        }
        struct Y {public Vector3d p,v;public double f,b;public Y(Vector3d p,Vector3d v,double f,double b){this.p=p;this.v=v;this.f=f;this.b=b;} public static Y operator +(Y a,Y b)=>new Y(a.p+b.p,a.v+b.v,a.f+b.f,a.b+b.b);public static Y operator *(Y a,double n)=>new Y(a.p*n,a.v*n,a.f*n,a.b*n);}
        Y Integrate(Y y,double t,double h,bool burn,bool solidBurn){var k1=Derivative(y,t,burn,solidBurn);var k2=Derivative(y+k1*(h*.5),t+h*.5,burn,solidBurn);var k3=Derivative(y+k2*(h*.5),t+h*.5,burn,solidBurn);var k4=Derivative(y+k3*h,t+h,burn,solidBurn);return y+(k1+k2*2+k3*2+k4)*(h/6);}
        Y Derivative(Y y,double t,bool burn,bool solidBurn){var forces=EvaluateForces(y,t,burn,solidBurn);return new Y(y.v,forces.Acceleration,burn&&!infiniteFuel?-forces.CoreThrust/(forces.Isp*G0):0,solidBurn&&!infiniteFuel?-booster!.MassFlowKgPerSecond:0);}
        readonly struct ForceSample
        {
            public readonly GeodeticPosition Geodetic;
            public readonly AtmosphereSample Atmosphere;
            public readonly Vector3d Gravity,Acceleration;
            public readonly DragResult Drag;
            public readonly double Thrust,CoreThrust,Isp,Mass,HeatFlux;
            public ForceSample(GeodeticPosition geo,AtmosphereSample air,Vector3d gravity,Vector3d acceleration,DragResult drag,double thrust,double coreThrust,double isp,double mass,double heatFlux)
            {Geodetic=geo;Atmosphere=air;Gravity=gravity;Acceleration=acceleration;Drag=drag;Thrust=thrust;CoreThrust=coreThrust;Isp=isp;Mass=mass;HeatFlux=heatFlux;}
        }
        // Integration and telemetry share this exact atmosphere, mass and force path.
        ForceSample EvaluateForces(Y y,double t,bool burn,bool solidBurn)
        {
            var geo=Geo(y.p,t);var air=Air(y.p,t,geo);var engine=Engine(air.sample.PressurePascals);
            double coreThrust=burn?engine.thrust:0;
            double thrust=coreThrust+(solidBurn?booster!.ThrustNewtons:0);
            double mass=Mass(Math.Max(0,y.f),Math.Max(0,y.b));
            var drag=Aerodynamics.Compute(y.v,air.velocity,air.sample.DensityKgPerCubicMeter,stages[stage].DragCoefficient,stages[stage].ReferenceAreaSquareMeters,mass);
            var gravity=Gravity.Acceleration(y.p)*gravityMultiplier;
            double heatFlux=0;
            var acceleration=gravity+(ReturnMode?ReturnAxis:Direction(geo,t,actualPitch,actualHeading))*(thrust/mass)+drag.AccelerationMetersPerSecondSquared;
            if(ReturnMode)
            {
                var relative=y.v-air.velocity;double speed=relative.Magnitude();
                // Broadside 9 m by 50 m body area; slender end-on area. Coefficients are game estimates.
                double cosine=speed>0?Math.Abs(StarshipReturnController.Dot(ReturnAxis,relative*(1/speed))):1;
                double area=63.62+386.38*Math.Sqrt(Math.Max(0,1-cosine*cosine));
                drag=Aerodynamics.Compute(y.v,air.velocity,air.sample.DensityKgPerCubicMeter,1.2,area,mass);
                acceleration=gravity+ReturnAxis*(thrust/mass)+drag.AccelerationMetersPerSecondSquared;
                if(speed>1)
                {
                    var flow=relative*(1/speed);var up=y.p.Normalize();var lift=up-flow*StarshipReturnController.Dot(up,flow);
                    if(lift.Magnitude()>1e-8) {
                        lift=lift.Normalize();var side=StarshipReturnController.Cross(flow,lift);
                        lift=lift*Math.Cos(ReturnBankRadians)+side*Math.Sin(ReturnBankRadians);
                        acceleration+=lift*(.25*drag.ForceNewtons.Magnitude()/mass);
                    }
                }
                // Sutton-Graves Earth entry heating proxy, 4.5 m nose radius; not TPS temperature/damage.
                heatFlux=1.74153e-4*Math.Sqrt(air.sample.DensityKgPerCubicMeter/4.5)*speed*speed*speed;
            }
            return new ForceSample(geo,air.sample,gravity,acceleration,drag,thrust,coreThrust,engine.isp,mass,heatFlux);
        }
        (double thrust,double isp) Engine(double pressure){double f=Math.Max(0,Math.Min(1,pressure/101325));var s=stages[stage];return ((s.ThrustVacuumNewtons+(s.ThrustSeaLevelNewtons-s.ThrustVacuumNewtons)*f)*throttle,s.IspVacuumSeconds+(s.IspSeaLevelSeconds-s.IspVacuumSeconds)*f);}
        double Mass(double currentFuel,double currentBoosterFuel){double m=stages[stage].DryMassKg+currentFuel+(boosterAttached?booster!.DryMassKg+currentBoosterFuel:0);for(int i=stage+1;i<stages.Length;i++)m+=stages[i].DryMassKg+stages[i].PropellantKg;return m;}
        GeodeticPosition Geo(Vector3d p,double t)=>EarthFrames.EcefToGeodetic(EarthFrames.InertialToEcef(p,Omega*t));
        (AtmosphereSample sample,Vector3d velocity) Air(Vector3d p,double t,GeodeticPosition g){// Clamp only intermediate impact-bracketing samples below the atmosphere domain.
            var a=atmosphere.Sample(Math.Max(StandardAtmosphere.MinimumGeometricAltitudeMeters,g.AltitudeMeters));var w=Aerodynamics.WindToInertial(a.WindEastNorthUpMetersPerSecond,g.LatitudeRadians,g.LongitudeRadians+Omega*t);return (a,Aerodynamics.CorotatingAirVelocity(p,w));}
        static Vector3d Direction(GeodeticPosition location, double t, double pitchDegrees, double headingDegrees)
        {
            double pitchRadians = pitchDegrees * Math.PI / 180;
            double headingRadians = headingDegrees * Math.PI / 180;
            var localDirection = new Vector3d(Math.Cos(pitchRadians) * Math.Sin(headingRadians),
                Math.Cos(pitchRadians) * Math.Cos(headingRadians), Math.Sin(pitchRadians));
            return Aerodynamics.WindToInertial(localDirection, location.LatitudeRadians, location.LongitudeRadians + Omega * t);
        }
        static Vector3d Rotation(Vector3d p)=>new Vector3d(-Omega*p.Y,Omega*p.X,0);
        static double Move(double a,double b,double d)=>a+Math.Max(-d,Math.Min(d,b-a)); static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);static void Check(double v,double min,double max){if(!Finite(v)||v<min||v>max)throw new ArgumentOutOfRangeException(nameof(v));}
    }
}

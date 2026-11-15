using System;

namespace FlightCore
{
    public enum StarshipReturnPhase { Idle, Planning, CoastToBurn, FlipRetrograde, DeorbitBurn, EntryCoast, AtmosphericEntry, LandingFlip, PoweredLanding, Caught, Landed, Failed }

    /// <summary>Experimental point-mass return guidance. No position/velocity changes except normal forces and a qualified tower constraint.
    /// Uses a bounded numerical search over actual finite burns and atmospheric trajectories, not a fixed burn clock.
    /// Coefficients, attitude rate, engine throttling and catch envelope are gameplay estimates, not flight-qualified Starship data.</summary>
    public sealed class StarshipReturnController
    {
        const double Omega = Aerodynamics.EarthAngularVelocityRadiansPerSecond;
        readonly double latitude, longitude, catchAltitude;
        RocketSimulation? seed;
        StarshipReturnController? trialController;
        RocketSimulation? trial;
        int candidate, refinementRound;
        double refineWait, refineDv, refineNormal, bestNormal, requestedNormal;
        double epoch, period, bestWait, bestDv, bestMiss=double.PositiveInfinity, burnTime, requestedDv, deliveredDv;
        bool refining, polishing, validating, hasApproachPoint;
        Vector3d approachPosition,previousAxis;double approachTime,previousAxisTime;bool hasPreviousAxis;
        int polishIteration;
        readonly Vector3d[] residuals=new Vector3d[3];
        public StarshipReturnPhase Phase { get; private set; }
        public string Status { get; private set; } = "Return idle";
        public double PredictedMissMeters => bestMiss;
        public double CatchArmClosure01 { get; private set; }
        public bool PredictedCatchFeasible { get; private set; }
        public double CatchLugDistanceMeters { get; private set; }
        public double CatchLugAltitudeErrorMeters { get; private set; }
        public double CatchLugSpeedMetersPerSecond { get; private set; }
        public double TargetDistanceMeters { get; private set; }
        public double HeatFluxWattsPerSquareMeter { get; private set; }
        public double PeakHeatFluxWattsPerSquareMeter { get; private set; }
        public double PeakDynamicPressurePascals { get; private set; }
        public double BurnStartMissionTimeSeconds => burnTime;
        public double PlannedDeltaVMetersPerSecond => Math.Sqrt(requestedDv*requestedDv+requestedNormal*requestedNormal);
        public bool IsComplete => Phase==StarshipReturnPhase.Caught||Phase==StarshipReturnPhase.Landed||Phase==StarshipReturnPhase.Failed;
        public StarshipReturnController(double targetLatitudeRadians, double targetLongitudeRadians, double catchAltitudeMeters=70)
        {
            // Reuse WGS84 input validation.
            EarthFrames.GeodeticToEcef(targetLatitudeRadians,targetLongitudeRadians,catchAltitudeMeters);
            latitude=targetLatitudeRadians;longitude=targetLongitudeRadians;catchAltitude=catchAltitudeMeters;
        }
        public bool TryStart(RocketSimulation simulation, out string reason)
        {
            if(simulation==null) throw new ArgumentNullException(nameof(simulation));
            if(simulation.Primary!=CelestialPrimary.Earth){reason="Starship return requires Earth as the current primary";return false;}
            var s=simulation.State;var orbit=OrbitDiagnostics.Compute(s.Position,s.Velocity);
            if(!simulation.SupportsStarshipReturn){reason="This stage does not support Starship return";return false;}
            if(Phase!=StarshipReturnPhase.Idle) { reason="Return already requested";return false; }
            if(!s.IsLaunched||s.IsComplete||s.StageIndex!=1||!orbit.IsBound||orbit.MinimumWgs84AltitudeBoundMeters<120000)
            { reason="Return requires the Starship upper stage in a stable orbit above 120 km";return false; }
            if(s.PropellantKg<1) { reason="No propellant remains for return";return false; }
            simulation.EnableReturn();seed=simulation.ReturnClone(4);epoch=s.MissionTime;
            double radius=s.Position.Magnitude();double energy=s.Velocity.Magnitude()*s.Velocity.Magnitude()/2-Gravity.EarthGravitationalParameter/radius;double semi=-Gravity.EarthGravitationalParameter/(2*energy);period=2*Math.PI*Math.Sqrt(semi*semi*semi/Gravity.EarthGravitationalParameter);
            Phase=StarshipReturnPhase.Planning;Status="Searching finite-burn return trajectories";reason="";return true;
        }
        public void Update(RocketSimulation simulation)
        {
            if(simulation==null) throw new ArgumentNullException(nameof(simulation));
            if(Phase==StarshipReturnPhase.Idle||IsComplete)return;
            if(!RequireEarth(simulation))return;
            if(Phase==StarshipReturnPhase.Planning) { simulation.Throttle=0;PlanSlice();return; }
            AdvanceGuidance(simulation);
        }
        /// <summary>Advance private prediction clones only. The caller must give this controller one owner until planning completes.</summary>
        public void AdvancePlanning() { if(Phase==StarshipReturnPhase.Planning)PlanSlice(); }
        void PlanSlice()
        {
            // At most 48 prediction integration steps per caller tick; planning never owns an unbounded UI loop.
            for(int work=0;work<48;work++)
            {
                if(trial==null)
                {
                    int count=validating?1:polishing?3:refining?(refinementRound==0?125:25):600;
                    if(candidate>=count)
                    {
                        if(!refining){refining=true;candidate=0;refineWait=bestWait;refineDv=bestDv;refineNormal=bestNormal;continue;}
                        if(refining&&refinementRound<4){refinementRound++;candidate=0;refineWait=bestWait;refineDv=bestDv;refineNormal=bestNormal;bestMiss=double.PositiveInfinity;continue;}
                        if(!polishing){polishing=true;candidate=0;refineWait=bestWait;refineDv=bestDv;refineNormal=bestNormal;continue;}
                        if(!validating&&polishIteration++<8&&bestMiss>.1) {
                            double a=(residuals[1].X-residuals[0].X)/.5,b=residuals[2].X-residuals[0].X;
                            double c=(residuals[1].Y-residuals[0].Y)/.5,d=residuals[2].Y-residuals[0].Y,det=a*d-b*c;
                            if(Math.Abs(det)>1e-6) {
                                double dw=(-d*residuals[0].X+b*residuals[0].Y)/det;
                                double dn=(c*residuals[0].X-a*residuals[0].Y)/det;
                                refineWait=Math.Max(60,refineWait+Math.Max(-period/200,Math.Min(period/200,dw)));
                                refineNormal+=Math.Max(-100,Math.Min(100,dn));candidate=0;continue;
                            }
                        }
                        if(!validating){validating=true;candidate=0;continue;}
                        if(double.IsInfinity(bestMiss)){Phase=StarshipReturnPhase.Failed;Status="No finite return impact trajectory found";seed=null;return;}
                        burnTime=epoch+bestWait;requestedDv=bestDv;requestedNormal=bestNormal;Phase=StarshipReturnPhase.CoastToBurn;
                        Status="Return trajectory selected; predicted miss "+(bestMiss/1000).ToString("F1")+" km";
                        seed=null;return;
                    }
                    double wait,dv,normal;
                    if(validating){wait=bestWait;dv=bestDv;normal=bestNormal;}
                    else if(polishing){wait=refineWait+(candidate==1?.5:0);dv=refineDv;normal=refineNormal+(candidate==2?1:0);}
                    else if(!refining) { wait=60+(candidate/25)*period/24;dv=70+((candidate/5)%5)*90;normal=((candidate%5)-2)*400; }
                    else if(refinementRound==0) {wait=Math.Max(60,refineWait+((candidate/25)-2)*period/96);dv=Math.Max(25,refineDv+(((candidate/5)%5)-2)*24);normal=refineNormal+((candidate%5)-2)*100;}
                    else {double scale=Math.Pow(4,refinementRound);wait=Math.Max(60,refineWait+((candidate/5)-2)*period/(96*scale));dv=refineDv;normal=refineNormal+((candidate%5)-2)*100/scale;}
                    trial=seed!.ReturnClone(4);trial.EnableReturn();trialController=new StarshipReturnController(latitude,longitude,catchAltitude);
                    trialController.Phase=StarshipReturnPhase.CoastToBurn;trialController.burnTime=epoch+wait;trialController.requestedDv=dv;trialController.requestedNormal=normal;
                }
                double predictionStep=(trial.State.AltitudeMeters<120000||trialController!.Phase==StarshipReturnPhase.DeorbitBurn||trialController.Phase==StarshipReturnPhase.FlipRetrograde)?(refinementRound>=3?.02:.2):4;
                if(Math.Abs(trial.FixedStep-predictionStep)>.001)trial=trial.ReturnClone(predictionStep);
                trialController!.AdvanceGuidance(trial);trial.Step();
                if((!validating&&trialController.hasApproachPoint)||trial.State.IsComplete||trialController.IsComplete||trial.State.MissionTime>epoch+period+10000)
                {
                    var point=trialController.hasApproachPoint?trialController.approachPosition:trial.State.Position;double pointTime=trialController.hasApproachPoint?trialController.approachTime:trial.State.MissionTime;
                    if(polishing&&!validating)residuals[candidate]=LandingResidual(point,pointTime);
                    double miss=trialController.SurfaceDistance(point,pointTime);
                    if(!trialController.hasApproachPoint)miss=double.PositiveInfinity;
                    if(validating)PredictedCatchFeasible=trial.IsCaught;
                    if(!validating&&miss<bestMiss){bestMiss=miss;bestWait=trialController.burnTime-epoch;bestDv=trialController.requestedDv;bestNormal=trialController.requestedNormal;PredictedCatchFeasible=trial.IsCaught;}
                    candidate++;trial=null;trialController=null;
                }
            }
        }
        bool RequireEarth(RocketSimulation sim)
        {
            if(sim.Primary==CelestialPrimary.Earth)return true;
            sim.Throttle=0;Phase=StarshipReturnPhase.Failed;Status="Starship return stopped: current primary is not Earth";return false;
        }
        void AdvanceGuidance(RocketSimulation sim)
        {
            if(!RequireEarth(sim))return;
            var s=sim.State;var up=s.Position.Normalize();
            var axisRate=hasPreviousAxis&&s.MissionTime>previousAxisTime?(sim.ReturnAxis-previousAxis)*(1/(s.MissionTime-previousAxisTime)):Vector3d.Zero;
            previousAxis=sim.ReturnAxis;previousAxisTime=s.MissionTime;hasPreviousAxis=true;var air=s.Velocity-Aerodynamics.CorotatingAirVelocity(s.Position,Vector3d.Zero);
            double speed=air.Magnitude();var retro=speed>1?air*(-1/speed):up;
            var normal=Cross(s.Position,s.Velocity).Normalize();
            double totalDv=Math.Sqrt(requestedDv*requestedDv+requestedNormal*requestedNormal);
            var burnAxis=totalDv>0?(retro*requestedDv+normal*requestedNormal).Normalize():retro;
            TargetDistanceMeters=SurfaceDistance(s.Position,s.MissionTime);
            HeatFluxWattsPerSquareMeter=sim.ReturnHeatFluxWattsPerSquareMeter;
            PeakHeatFluxWattsPerSquareMeter=Math.Max(PeakHeatFluxWattsPerSquareMeter,HeatFluxWattsPerSquareMeter);
            PeakDynamicPressurePascals=Math.Max(PeakDynamicPressurePascals,s.DynamicPressurePascals);
            if(s.IsComplete) { Phase=sim.IsCaught?StarshipReturnPhase.Caught:s.IsDestroyed?StarshipReturnPhase.Failed:StarshipReturnPhase.Landed;Status=s.Status;sim.Throttle=0;return; }
            sim.Throttle=0;
            if(Phase==StarshipReturnPhase.CoastToBurn||Phase==StarshipReturnPhase.FlipRetrograde)
            {
                sim.ReturnCommand=burnAxis;
                if(s.MissionTime>=burnTime-30)Phase=StarshipReturnPhase.FlipRetrograde;
                if(s.MissionTime>=burnTime&&Dot(sim.ReturnAxis,burnAxis)>.995)Phase=StarshipReturnPhase.DeorbitBurn;
            }
            if(Phase==StarshipReturnPhase.DeorbitBurn)
            {
                sim.ReturnCommand=burnAxis;
                if(deliveredDv>=totalDv-1e-8||s.PropellantKg<=0){
                    sim.Throttle=0;
                    if(s.PropellantKg<=0&&deliveredDv<totalDv-1e-8&&OrbitDiagnostics.Compute(s.Position,s.Velocity).MinimumWgs84AltitudeBoundMeters>120000)
                    {Phase=StarshipReturnPhase.Failed;Status="Return burn incomplete: propellant exhausted without atmospheric intercept";return;}
                    Phase=StarshipReturnPhase.EntryCoast;
                }
                else {
                    sim.Throttle=Dot(sim.ReturnAxis,burnAxis)>.98?Math.Min(1,Math.Max(0,totalDv-deliveredDv)*s.MassKg/(sim.ReturnAvailableThrust*sim.FixedStep)):0;
                    deliveredDv+=sim.ReturnAvailableThrust*sim.Throttle/s.MassKg*sim.FixedStep;
                }
            }
            if(Phase==StarshipReturnPhase.EntryCoast||Phase==StarshipReturnPhase.AtmosphericEntry)
            {
                // Broadside attitude maintains an estimated lift/drag ratio of 0.25. Bank reversal extends or shortens range.
                sim.ReturnCommand=up;
                if(speed>1) {
                    double timeToGround=Math.Min(1200,Math.Max(0,s.AltitudeMeters/Math.Max(50,-Dot(air,up))));
                    var futureTarget=EarthFrames.EcefToInertial(EarthFrames.GeodeticToEcef(latitude,longitude,catchAltitude),Omega*(s.MissionTime+timeToGround));
                    var side=Cross(air*(1/speed),up);
                    if(side.Magnitude()>1e-8) {
                        double lateral=Dot(futureTarget-s.Position,side.Normalize());
                        double bank=Math.Max(-Math.PI/3,Math.Min(Math.PI/3,lateral/150000));
                        sim.ReturnBankRadians+=Math.Max(-.0872665*sim.FixedStep,Math.Min(.0872665*sim.FixedStep,bank-sim.ReturnBankRadians));
                        var flow=air*(1/speed);var body=up-flow*Dot(up,flow);
                        if(body.Magnitude()>1e-8) {
                            body=body.Normalize();sim.ReturnCommand=body*Math.Cos(sim.ReturnBankRadians)+Cross(flow,body)*Math.Sin(sim.ReturnBankRadians);
                        }
                    }
                }
                if(s.AltitudeMeters<120000)Phase=StarshipReturnPhase.AtmosphericEntry;
                double vertical=Dot(air,up), gravity=Gravity.Acceleration(s.Position).Magnitude();
                double net=sim.ReturnAvailableThrust/s.MassKg-gravity;
                double stop=net>0?vertical*vertical/(2*net):double.PositiveInfinity;
                if(s.AltitudeMeters<Math.Max(3000,stop+Math.Abs(vertical)*15+catchAltitude+300)&&vertical<0)
                    { Phase=StarshipReturnPhase.LandingFlip;hasApproachPoint=true;approachPosition=s.Position;approachTime=s.MissionTime; }
            }
            if(Phase==StarshipReturnPhase.LandingFlip||Phase==StarshipReturnPhase.PoweredLanding)
            {
                // Aim the physical lug, including its offset under the actual finite body tilt.
                var targetLug=EarthFrames.EcefToInertial(EarthFrames.GeodeticToEcef(latitude,longitude,catchAltitude+21),Omega*s.MissionTime);
                var lug=s.Position+sim.ReturnAxis*21;
                var lugRelativeVelocity=s.Velocity+axisRate*21-Aerodynamics.CorotatingAirVelocity(lug,Vector3d.Zero);
                var target=targetLug-sim.ReturnAxis*21;
                TargetDistanceMeters=SurfaceDistance(lug,s.MissionTime);
                if(TargetDistanceMeters<50&&s.AltitudeMeters>=catchAltitude-3&&s.AltitudeMeters<=catchAltitude+100&&lugRelativeVelocity.Magnitude()<30)CatchArmClosure01=Math.Min(1,CatchArmClosure01+.18*sim.FixedStep);
                var offset=target-s.Position;double h=Dot(s.Position-target,up);
                // Velocity feedback uses COM motion. Differenced attitude motion would feed
                // an unsmoothed derivative back into the attitude command itself.
                double vertical=Dot(air,up);var horizontal=air-up*vertical;var horizontalOffset=offset-up*Dot(offset,up);
                // Spend real fuel holding a feasible approach height while correcting lateral error.
                // This may run out of fuel; no target or descent-speed assignment is made.
                double approachHeight=Math.Min(5000,Math.Max(0,(TargetDistanceMeters-20)*.12));
                double descentHeight=Math.Max(0,h-approachHeight);
                double brakingAcceleration=Math.Max(1,sim.ReturnAvailableThrust/s.MassKg-Gravity.Acceleration(s.Position).Magnitude());
                double desiredDown=h>approachHeight?-Math.Min(70,Math.Sqrt(2*brakingAcceleration*descentHeight)*.45):Math.Min(40,(approachHeight-h)/8);
                var omega=new Vector3d(0,0,Omega);
                var rotatingFeedForward=Cross(omega,Cross(omega,s.Position))+Cross(omega,air)*2;
                var acceleration=Gravity.Acceleration(s.Position)*(-1)-sim.ReturnAerodynamicAcceleration+rotatingFeedForward+up*((desiredDown-vertical)/1.2)
                    +horizontalOffset*.02-horizontal*.28;
                // Limit steering to a 30 degree cone while landing; no unlimited lateral acceleration.
                double axial=Math.Max(0,Dot(acceleration,up));var lateral=acceleration-up*axial;
                if(lateral.Magnitude()>axial*.577&&lateral.Magnitude()>0)lateral=lateral.Normalize()*(axial*.577);
                acceleration=up*axial+lateral;sim.ReturnCommand=acceleration.Magnitude()>0?acceleration.Normalize():up;
                if(Dot(sim.ReturnAxis,sim.ReturnCommand)>.9) {Phase=StarshipReturnPhase.PoweredLanding;sim.Throttle=Math.Min(1,acceleration.Magnitude()*s.MassKg/sim.ReturnAvailableThrust);}
                var lugGeo=EarthFrames.EcefToGeodetic(EarthFrames.InertialToEcef(lug,Omega*s.MissionTime));
                CatchLugDistanceMeters=SurfaceDistance(lug,s.MissionTime);CatchLugAltitudeErrorMeters=lugGeo.AltitudeMeters-(catchAltitude+21);CatchLugSpeedMetersPerSecond=lugRelativeVelocity.Magnitude();
                if(CatchArmClosure01>=.99&&Math.Abs(CatchLugAltitudeErrorMeters)<.5&&CatchLugDistanceMeters<.35&&CatchLugSpeedMetersPerSecond<3&&Dot(sim.ReturnAxis,up)>Math.Cos(5*Math.PI/180))
                {sim.CatchAtCurrentPosition();Phase=StarshipReturnPhase.Caught;Status="Caught at Starbase within physical envelope";return;}
            }
            Status=PhaseLabel(Phase)+" | Starbase "+(TargetDistanceMeters/1000).ToString("F1")+" km";
            if(s.PropellantKg<=0&&(Phase==StarshipReturnPhase.PoweredLanding||Phase==StarshipReturnPhase.LandingFlip))Status="Return propellant exhausted; ballistic descent";
        }
        static string PhaseLabel(StarshipReturnPhase phase)
        {
            switch(phase) {
                case StarshipReturnPhase.CoastToBurn:return "Coasting to deorbit burn";
                case StarshipReturnPhase.FlipRetrograde:return "Turning retrograde";
                case StarshipReturnPhase.DeorbitBurn:return "Deorbit burn";
                case StarshipReturnPhase.EntryCoast:return "Coasting to atmosphere";
                case StarshipReturnPhase.AtmosphericEntry:return "Atmospheric entry";
                case StarshipReturnPhase.LandingFlip:return "Landing flip";
                case StarshipReturnPhase.PoweredLanding:return "Powered landing";
                default:return phase.ToString();
            }
        }
        Vector3d LandingResidual(Vector3d position,double time)
        {
            var ecef=EarthFrames.InertialToEcef(position,Omega*time);
            var target=EarthFrames.GeodeticToEcef(latitude,longitude,catchAltitude);var delta=ecef-target;
            var east=new Vector3d(-Math.Sin(longitude),Math.Cos(longitude),0);
            var north=new Vector3d(-Math.Sin(latitude)*Math.Cos(longitude),-Math.Sin(latitude)*Math.Sin(longitude),Math.Cos(latitude));
            return new Vector3d(Dot(delta,east),Dot(delta,north),0);
        }
        double SurfaceDistance(Vector3d position,double time)
        {
            var g=EarthFrames.EcefToGeodetic(EarthFrames.InertialToEcef(position,Omega*time));
            double a=Math.Sin((g.LatitudeRadians-latitude)/2),b=Math.Sin((g.LongitudeRadians-longitude)/2);
            return 2*6371000*Math.Asin(Math.Sqrt(Math.Min(1,a*a+Math.Cos(latitude)*Math.Cos(g.LatitudeRadians)*b*b)));
        }
        internal static Vector3d Cross(Vector3d a,Vector3d b)=>new Vector3d(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
        internal static double Dot(Vector3d a,Vector3d b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        internal static Vector3d Slew(Vector3d a,Vector3d b,double angle)
        {
            double dot=Math.Max(-1,Math.Min(1,Dot(a,b)));double arc=Math.Acos(dot);
            if(arc<=angle)return b;
            var perpendicular=b-a*dot;
            if(perpendicular.Magnitude()<1e-8){var basis=Math.Abs(a.Z)<.9?new Vector3d(0,0,1):new Vector3d(1,0,0);perpendicular=basis-a*Dot(a,basis);}
            return a*Math.Cos(angle)+perpendicular.Normalize()*Math.Sin(angle);
        }
    }
}

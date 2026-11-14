using System;
namespace FlightCore
{
    public sealed class StageDefinition
    {
        public double DryMassKg { get; } public double PropellantKg { get; } public double ThrustSeaLevelNewtons { get; } public double ThrustVacuumNewtons { get; } public double IspSeaLevelSeconds { get; } public double IspVacuumSeconds { get; } public double DragCoefficient { get; } public double ReferenceAreaSquareMeters { get; }
        public StageDefinition(double dryMassKg,double propellantKg,double thrustSeaLevelNewtons,double thrustVacuumNewtons,double ispSeaLevelSeconds,double ispVacuumSeconds,double dragCoefficient,double referenceAreaSquareMeters)
        {
            foreach(var x in new[]{dryMassKg,propellantKg,thrustSeaLevelNewtons,thrustVacuumNewtons,ispSeaLevelSeconds,ispVacuumSeconds,dragCoefficient,referenceAreaSquareMeters}) if(double.IsNaN(x)||double.IsInfinity(x)||x<0) throw new ArgumentOutOfRangeException(nameof(dryMassKg));
            if(dryMassKg<=0||ispSeaLevelSeconds<=0||ispVacuumSeconds<=0)throw new ArgumentOutOfRangeException(nameof(dryMassKg));
            DryMassKg=dryMassKg;PropellantKg=propellantKg;ThrustSeaLevelNewtons=thrustSeaLevelNewtons;ThrustVacuumNewtons=thrustVacuumNewtons;IspSeaLevelSeconds=ispSeaLevelSeconds;IspVacuumSeconds=ispVacuumSeconds;DragCoefficient=dragCoefficient;ReferenceAreaSquareMeters=referenceAreaSquareMeters;
        }
        // Fictional gameplay values; not specifications of an actual vehicle.
        public static StageDefinition[] DemoStages()=>new[]{new StageDefinition(40000,400000,7.6e6,8.2e6,285,310,.5,12.6),new StageDefinition(10000,100000,.8e6,1.1e6,280,345,.5,12.6)};
    }
}

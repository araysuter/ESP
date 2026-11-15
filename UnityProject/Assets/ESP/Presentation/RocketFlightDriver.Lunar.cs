using System;
using System.Threading;
using System.Threading.Tasks;
using FlightCore;
using UnityEngine;

namespace ESP.Presentation
{
    public sealed partial class RocketFlightDriver
    {
        private LunarMissionGuidance lunarGuidance;
        private Task<(bool found, string reason, Exception error)> lunarPlanning;
        private CancellationTokenSource lunarCancellation;
        private string lunarNotice;
        private float lunarPlanningStarted;
        public bool IsPlanningLunar => lunarPlanning != null;
        public bool LunarActive => lunarGuidance != null && lunarGuidance.IsActive;
        public bool HasLunarStatus => lunarGuidance != null || lunarNotice != null;
        public bool LunarNeedsRealTime => LunarActive && !IsPlanningLunar && simulation != null && lunarGuidance.NeedsRealTime(simulation);
        public bool CanRequestLunar => !IsPaused && !MissionSession.BlocksInput && !ReentryActive && !LunarActive && simulation != null && simulation.State.IsLaunched && !simulation.State.IsComplete;
        public string LunarStatus => IsPlanningLunar ? string.Format("Searching lunar trajectories · {0} checked · {1:F0}s\nSimulation time paused", lunarGuidance.PlanningCandidates, Time.realtimeSinceStartup-lunarPlanningStarted) :
            LunarActive ? lunarGuidance.Status : lunarNotice ?? (lunarGuidance != null ? lunarGuidance.Status : "Start from a bound Earth orbit with throttle cut.");

        public void BeginLunarManeuver(LunarManeuver maneuver)
        {
            if(!CanRequestLunar)return;
            var controller=new LunarMissionGuidance();
            if(!controller.TryStart(simulation,maneuver,out var reason)){lunarNotice=reason;return;}
            lunarNotice=null;lunarGuidance=controller;AssistanceEnabled=false;
            TimeRate=1;accumulator=0;actualTimeRate=0;
            lunarPlanningStarted=Time.realtimeSinceStartup;lunarCancellation=new CancellationTokenSource();
            var token=lunarCancellation.Token;
            // The worker reads only immutable snapshot data and ephemerides; the
            // simulation is paused until the plan is committed on the main thread.
            lunarPlanning=Task.Run(() => {
                try { bool found=controller.Calculate(token,out var why);return (found,why,(Exception)null); }
                catch(Exception error) { return (false,"",error); }
            });
        }
        private bool AdvanceLunarPlanning()
        {
            if(lunarPlanning==null)return false;
            if(!lunarPlanning.IsCompleted)return true;
            var result=lunarPlanning.GetAwaiter().GetResult();lunarPlanning=null;
            lunarCancellation.Dispose();lunarCancellation=null;
            if(result.error!=null)
            {
                Debug.LogException(result.error);lunarNotice="Lunar planning failed; choose another maneuver";
                lunarGuidance=null;return true;
            }
            if(!result.found){lunarNotice=result.reason;lunarGuidance=null;return true;}
            if(!lunarGuidance.Activate(simulation,out var reason)){lunarNotice=reason;lunarGuidance=null;}
            return true;
        }
        public void CancelLunarManeuver()
        {
            lunarCancellation?.Cancel();lunarCancellation?.Dispose();lunarCancellation=null;lunarPlanning=null;
            if(lunarGuidance!=null && simulation!=null && lunarGuidance.IsActive) lunarGuidance.Cancel(simulation);
            lunarNotice="Lunar guidance cancelled";lunarGuidance=null;
            TimeRate=1;accumulator=0;actualTimeRate=0;
        }
        private void ClearLunarGuidance()
        {
            lunarCancellation?.Cancel();lunarCancellation?.Dispose();lunarCancellation=null;lunarPlanning=null;
            if(lunarGuidance!=null && simulation!=null && lunarGuidance.IsActive) lunarGuidance.Cancel(simulation);
            lunarGuidance=null;lunarNotice=null;
        }
    }
}

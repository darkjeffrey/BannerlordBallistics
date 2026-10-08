using ProjectileLandingTracker.Tracking;
using TaleWorlds.MountAndBlade;

namespace ProjectileLandingTracker
{
    /// <summary>
    /// Module entry point. Attaches the tracker to every mission (battles, sieges, tournaments, ...).
    /// </summary>
    public class SubModule : MBSubModuleBase
    {
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            mission.AddMissionBehavior(new ProjectileTrackerBehavior());
        }
    }
}

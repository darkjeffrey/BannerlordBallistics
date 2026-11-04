using System;
using HarmonyLib;
using ProjectileLandingTracker.Tracking;
using TaleWorlds.MountAndBlade;

namespace ProjectileLandingTracker
{
    /// <summary>
    /// Module entry point. Attaches the tracker to every mission (battles, sieges, tournaments, ...).
    /// </summary>
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                new Harmony("darkjeffrey.ProjectileLandingTracker").PatchAll(typeof(SubModule).Assembly);
            }
            catch (Exception)
            {
                // Without the patch, siege release points just aren't learned.
            }
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            mission.AddMissionBehavior(new ProjectileTrackerBehavior());
        }
    }
}

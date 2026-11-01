using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace LeviathansPacingPatch
{
    // The leviathan mods (LEVIATHANS:SANDWORM, HiveLord) try to offer their quest every 600 ticks once
    // colony wealth passes 500k. Each attempt runs the expensive world-tile search first and only then
    // calls QuestUtility.GenerateQuestAndMakeAvailable, which Pacing Manager's era gate turns into a
    // silent null. While the era gate is closed that means a full world scan every 10 seconds, forever.
    //
    // Fix: ask Pacing Manager the same question up front and skip the attempt while it would refuse.
    // The leviathan components keep retrying, so the quest still arrives once the era range allows it.
    public class LeviathansPacingPatchMod : Mod
    {
        private static Func<string, bool> isQuestGeneratorAllowed;

        public LeviathansPacingPatchMod(ModContentPack content) : base(content)
        {
            MethodInfo allowed = AccessTools.Method("ProgressionPacing.ProgressionPacingModSettings:IsQuestGeneratorAllowed");
            if (allowed == null)
            {
                Log.Warning("[Leviathans Pacing Patch] Pacing Manager's IsQuestGeneratorAllowed not found; patch inactive.");
                return;
            }
            isQuestGeneratorAllowed = AccessTools.MethodDelegate<Func<string, bool>>(allowed);

            Harmony harmony = new Harmony("DanZinagri.LeviathansPacingPatch");
            // Both targets are optional: either leviathan mod may be absent.
            TryPatch(harmony, "SandWormLib.SandWormQuestUtility:TryCreateLeviathanQuest", nameof(SandwormPrefix));
            TryPatch(harmony, "HiveLordLib.HiveLordQuestUtility:TryOfferQuest", nameof(HiveLordPrefix));
        }

        private static void TryPatch(Harmony harmony, string target, string prefix)
        {
            MethodInfo method = AccessTools.Method(target);
            if (method == null)
            {
                return;
            }
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(LeviathansPacingPatchMod), prefix));
        }

        // Keys are the QuestScriptDef defNames Pacing Manager lists in its FixedQuestGenerators.
        private static bool SandwormPrefix(ref bool __result)
        {
            return Allow("SandWorm_LeviathanQuest", ref __result);
        }

        private static bool HiveLordPrefix(ref bool __result)
        {
            return Allow("HiveLord_SyndicateHunt", ref __result);
        }

        private static bool Allow(string key, ref bool __result)
        {
            if (isQuestGeneratorAllowed(key))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }
}

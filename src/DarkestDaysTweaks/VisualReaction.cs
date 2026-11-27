using HarmonyLib;
using Il2CppDRS.Game.AI.Alertness;

namespace DarkestDaysTweaks;

internal static class VisualReaction
{
    [ThreadStatic] private static (IntPtr Sight, IntPtr Aggro) active;

    internal static void Install(HarmonyLib.Harmony harmony)
    {
        var simulate = AccessTools.Method(typeof(SensorChannelAlertnessSystem), "DRS_Utils_Framework_ISimulatable_Simulate")
            ?? throw new MissingMethodException("SensorChannelAlertnessSystem.Simulate");
        var check = AccessTools.Method(typeof(SensorChannelAlertnessStateTransition), "CheckIfIncreasing")
            ?? throw new MissingMethodException("SensorChannelAlertnessStateTransition.CheckIfIncreasing");
        harmony.Patch(simulate, prefix: Hook(nameof(BeginAlertness)), finalizer: Hook(nameof(EndAlertness)));
        harmony.Patch(check, prefix: Hook(nameof(BeforeCheck)), postfix: Hook(nameof(AfterCheck)));
    }

    private static HarmonyMethod Hook(string name) => new(AccessTools.Method(typeof(VisualReaction), name));

    private static void BeginAlertness(SensorChannelAlertnessSystem __instance,
        out (IntPtr Sight, IntPtr Aggro) __state)
    {
        __state = active;
        active = default;
        if (Mod.Current.VisualReactionSteps == 0) return;
        var owner = __instance.m_owner;
        if (owner?.m_data is not { } data ||
            (data.TryCast<Il2CppDRS.Database.AI.Wrappers.ZombieAiEntityData>() == null &&
             data.TryCast<Il2CppDRS.Database.AI.Wrappers.ZombieAiEntityDataVariant>() == null)) return;

        var state = __instance.m_currentAlertnessSet?.CurrentStateType;
        var config = owner.m_systemParameters?.AiConfigs;
        var sight = config?.SensoryConfigs?.SightSensorType;
        var aggro = config?.AlertnessConfigs?.AggroAlertnessState;
        // Suspicion may already be active by the time the transition to pursuit starts.
        if (state != null && sight != null && aggro != null && state.Pointer != aggro.Pointer)
            active = (sight.Pointer, aggro.Pointer);
    }

    private static void EndAlertness((IntPtr Sight, IntPtr Aggro) __state) => active = __state;

    private static void BeforeCheck(SensorChannelAlertnessStateTransition __instance, out bool __state)
    {
        __state = active.Sight != IntPtr.Zero && __instance.m_toState?.Pointer == active.Aggro &&
            !__instance.m_isIncreasing;
    }

    private static void AfterCheck(SensorChannelAlertnessStateTransition __instance, bool __state)
    {
        if (!__state || !__instance.m_isIncreasing || __instance.IsReady) return;
        // A transition can list several possible senses even when this attempt uses only sight.
        var contributors = __instance.m_currentlyContributingSensorTypes;
        if (contributors == null || contributors.Count != 1) return;
        foreach (var sensor in contributors)
            if (sensor == null || sensor.Pointer != active.Sight) return;

        __instance.AddIncreasePauseTime(Mod.Current.VisualReactionSteps * .5f, true);
    }
}

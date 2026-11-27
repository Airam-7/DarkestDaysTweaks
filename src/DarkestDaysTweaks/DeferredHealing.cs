using HarmonyLib;
using Il2CppDRS.Game.Actions.ShelterCharacter;
using Il2CppDRS.Game.ShelterCharacter;
using Il2CppDRS.Game.ShelterCharacter.Traits.EventEffectListeners;
using Il2CppDRS.Game.Survivor;

namespace DarkestDaysTweaks;

internal static class DeferredHealing
{
    [ThreadStatic] private static IntPtr resolvingSurvivor;

    internal static void Install(HarmonyLib.Harmony harmony)
    {
        var listener = typeof(CyclesPassedEventEffectListener);
        Patch(harmony, listener, "IncrementCycles", nameof(BeginResolution), nameof(EndResolution));
        Patch(harmony, typeof(AAlterHealthAction), "Apply", nameof(ScaleResolvedHealth));
    }

    private static void Patch(HarmonyLib.Harmony harmony, Type type, string method, string prefix, string? finalizer = null)
    {
        var target = AccessTools.Method(type, method) ?? throw new MissingMethodException(type.FullName, method);
        harmony.Patch(target, prefix: Hook(prefix), finalizer: finalizer == null ? null : Hook(finalizer));
    }

    private static HarmonyMethod Hook(string method) => new(AccessTools.Method(typeof(DeferredHealing), method));

    private static void BeginResolution(CyclesPassedEventEffectListener __instance, out IntPtr __state)
    {
        __state = resolvingSurvivor;
        resolvingSurvivor = IntPtr.Zero;
        if (Mod.Current.ItemHealingMode == 1) return;
        var trait = __instance.m_trait;
        if (trait?.CharacterTraitDefinition?.name is not ("State_Healing" or "State_ImprovedHealing")) return;
        resolvingSurvivor = trait.Owner?.TryCast<Survivor>()?.Pointer ?? IntPtr.Zero;
    }

    private static void EndResolution(IntPtr __state) => resolvingSurvivor = __state;

    private static void ScaleResolvedHealth(IShelterCharacter __0, ref float __1)
    {
        if (resolvingSurvivor == IntPtr.Zero || __0?.Pointer != resolvingSurvivor || __1 <= 0) return;
        var health = __0.TryCast<Survivor>()?.Model?.Health;
        if (health == null) return;
        float original = __1;
        int mode = Mod.Current.ItemHealingMode;
        __1 = Settings.RecoveryAmount(original, mode, health.Value, health.MaxValue);
        if (__1 != original)
            MelonLoader.MelonLogger.Msg($"Deferred item healing ({(mode == 4 ? "Max" : $"{mode}x")}): vanilla {original:0.##}, adjusted {__1:0.##}.");
    }
}

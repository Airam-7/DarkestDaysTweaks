using HarmonyLib;
using Il2CppDRS.Database.Character.CharacterData.ShelterCharacter.Needs;
using Il2CppDRS.Database.Configs;
using Il2CppDRS.Database.Items.ComponentData;
using Il2CppDRS.Game.Items.Components;
using Il2CppInterop.Runtime;

namespace DarkestDaysTweaks;

internal static class FoodShelfLife
{
    private static DrsConfigsData? configs;
    private static int lastLoggedMultiplier;
    [ThreadStatic] private static IntPtr freshFoodData;

    internal static void Install(HarmonyLib.Harmony harmony)
    {
        var add = AccessTools.Method(typeof(DegradationComponent), "AddDegradeCycles")
            ?? throw new MissingMethodException("DegradationComponent.AddDegradeCycles");
        var calculate = AccessTools.Method(typeof(DegradationComponentData), "TryGetDegradation")
            ?? throw new MissingMethodException("DegradationComponentData.TryGetDegradation");
        harmony.Patch(add, prefix: Hook(nameof(BeginFreshCycles)), finalizer: Hook(nameof(EndFreshCycles)));
        harmony.Patch(calculate, postfix: Hook(nameof(ExtendExpiry)));
    }

    private static HarmonyMethod Hook(string name) => new(AccessTools.Method(typeof(FoodShelfLife), name));

    private static CharacterNeedType? GetHungerNeed()
    {
        if (configs != null) return configs.ShelterCharacterConfig?.HungerNeedType;
        var loaded = UnityEngine.Resources.FindObjectsOfTypeAll(Il2CppType.Of<DrsConfigsData>());
        foreach (var asset in loaded)
        {
            var candidate = asset?.TryCast<DrsConfigsData>();
            var hunger = candidate?.ShelterCharacterConfig?.HungerNeedType;
            if (hunger == null) continue;
            configs = candidate;
            return hunger;
        }
        return null;
    }

    private static void BeginFreshCycles(DegradationComponent __instance, out IntPtr __state)
    {
        __state = freshFoodData;
        freshFoodData = IntPtr.Zero;
        if (Mod.Current.FoodShelfLifeMultiplier == 1) return;
        var hunger = GetHungerNeed();
        if (hunger == null) return;
        var components = __instance.m_parent?.Definition?.Components;
        if (components == null) return;
        foreach (var component in components)
        {
            if (component?.TryCast<AlterCharacterNeedComponentData>()?.NeedType?.Pointer != hunger.Pointer) continue;
            freshFoodData = __instance.m_data?.Pointer ?? IntPtr.Zero;
            break;
        }
    }

    private static void EndFreshCycles(IntPtr __state) => freshFoodData = __state;

    private static void ExtendExpiry(DegradationComponentData __instance, int __1,
        ref DegradationRuleSet.Degradation __2, bool __result)
    {
        if (!__result || freshFoodData == IntPtr.Zero || __instance.Pointer != freshFoodData) return;
        int multiplier = Mod.Current.FoodShelfLifeMultiplier;
        int vanillaEnd = __2.EndCycle;
        __2.EndCycle = Settings.FreshFoodExpiry(__1, __2.StartCycle, vanillaEnd, multiplier);
        if (__2.EndCycle != vanillaEnd && lastLoggedMultiplier != multiplier)
        {
            lastLoggedMultiplier = multiplier;
            MelonLoader.MelonLogger.Msg($"Food shelf life x{multiplier}: starts at phase {__2.StartCycle}, expires at phase {__2.EndCycle} (vanilla: {vanillaEnd}).");
        }
    }
}

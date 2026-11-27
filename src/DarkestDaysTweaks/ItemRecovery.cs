using HarmonyLib;
using Il2CppDRS.Database.Character.CharacterData.ShelterCharacter.Needs;
using Il2CppDRS.Game.Survivor;
using Il2CppDRS.Model;

namespace DarkestDaysTweaks;

internal static class ItemRecovery
{
    internal static void Install(HarmonyLib.Harmony harmony)
    {
        Patch(harmony, "DRS_Game_Inventory_ItemUser_IItemUserAlterNeed_AlterNeed", nameof(ScaleNeed));
        Patch(harmony, "DRS_Game_Inventory_ItemUser_IItemUserAlterHealth_AlterHealth", nameof(ScaleHealth));
    }

    private static void Patch(HarmonyLib.Harmony harmony, string target, string prefix)
    {
        var original = AccessTools.Method(typeof(Survivor), target) ?? throw new MissingMethodException(target);
        harmony.Patch(original, prefix: new HarmonyMethod(AccessTools.Method(typeof(ItemRecovery), prefix)));
    }

    private static void ScaleNeed(Survivor __instance, CharacterNeedType __0, ref float __1)
    {
        if (__0 == null || __1 <= 0 || !float.IsFinite(__1) ||
            (Mod.Current.SatietyMode == 1 && Mod.Current.MoraleRecoveryMode == 1)) return;
        var config = __instance.m_inventory?.m_configs?.ShelterCharacterConfig;
        if (__0.Pointer == config?.HungerNeedType?.Pointer)
            Scale(ref __1, Mod.Current.SatietyMode, __instance.Model?.Hunger);
        else if (__0.Pointer == config?.MoraleNeedType?.Pointer)
            Scale(ref __1, Mod.Current.MoraleRecoveryMode, __instance.Model?.Morale);
    }

    private static void ScaleHealth(Survivor __instance, ref float __0)
    {
        Scale(ref __0, Mod.Current.ItemHealingMode, __instance.Model?.Health);
    }

    private static void Scale(ref float amount, int mode, ClampedObservableFloat? meter)
    {
        if (mode == 1 || meter == null || amount <= 0 || !float.IsFinite(amount)) return;
        amount = Settings.RecoveryAmount(amount, mode, meter.Value, meter.MaxValue);
    }
}

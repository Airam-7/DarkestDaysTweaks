using HarmonyLib;
using Il2CppDRS.Database.Items;
using Il2CppDRS.Database.Items.ComponentData;
using Il2CppDRS.Game.Environment.Interactable;
using Il2CppDRS.Game.Environment.Interactable.Response;
using ItemId = Il2CppDRS.SerializableGuid.SerializableGuid;

namespace DarkestDaysTweaks;

internal static class LocationLoot
{
    [ThreadStatic] private static IntPtr firstInteraction;

    internal static void Install(HarmonyLib.Harmony harmony)
    {
        var respond = AccessTools.Method(typeof(Interactable), "DoResponse")
            ?? throw new MissingMethodException("Interactable.DoResponse");
        var openLoot = AccessTools.Method(typeof(LootPointResponse), "DoResponseInternal")
            ?? throw new MissingMethodException("LootPointResponse.DoResponseInternal");
        harmony.Patch(respond, prefix: Hook(nameof(BeginInteraction)), finalizer: Hook(nameof(EndInteraction)));
        harmony.Patch(openLoot, prefix: Hook(nameof(ScaleFirstOpening)));
    }

    private static HarmonyMethod Hook(string name) => new(AccessTools.Method(typeof(LocationLoot), name));

    private static void BeginInteraction(Interactable __instance, bool __1, out IntPtr __state)
    {
        __state = firstInteraction;
        // Vanilla marks the interactable before dispatching its loot response.
        firstInteraction = Mod.Current.LootMultiplier > 1 && !__1 &&
            __instance.Model?.HasBeenInteractedWith == false ? __instance.Pointer : IntPtr.Zero;
    }

    private static void EndInteraction(IntPtr __state) => firstInteraction = __state;

    private static void ScaleFirstOpening(LootPointResponse __instance)
    {
        if (firstInteraction == IntPtr.Zero || __instance.m_parent?.Pointer != firstInteraction) return;
        firstInteraction = IntPtr.Zero;
        var inventory = __instance.m_inventory;
        var stacks = inventory?.m_contents;
        if (inventory == null || stacks == null) return;

        // Snapshot the rolled contents before native insertion changes the stacks.
        int extras = Mod.Current.LootMultiplier - 1;
        var additions = new List<(ItemId Id, int Count)>();
        for (int i = 0; i < stacks.Count; i++)
        {
            var stack = stacks[i];
            var definition = stack?.ItemDefinition;
            if (stack == null || definition == null || stack.Count <= 0 || !CanMultiply(definition)) continue;
            if (stack.Count <= int.MaxValue / extras)
                additions.Add((definition.Guid, stack.Count * extras));
        }

        foreach (var (id, count) in additions)
            inventory.TryAddItemFromData(id, count);
    }

    private static bool CanMultiply(InventoryItemDefinition definition)
    {
        bool consumable = false;
        var components = definition.Components;
        if (components != null)
        {
            foreach (var component in components)
            {
                if (component == null) continue;
                if (component.TryCast<ConsumeOnUnlockComponentData>() != null ||
                    component.TryCast<OnReceivedActionsComponentData>() != null ||
                    component.TryCast<OnTransferConditionComponentData>() != null)
                    return false;
                consumable |= component.TryCast<ConsumeOnUseComponentData>() != null;
            }
        }
        return consumable || definition.DefaultMaxStack > 1;
    }
}

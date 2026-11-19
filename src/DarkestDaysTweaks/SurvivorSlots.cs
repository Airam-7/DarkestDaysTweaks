using Il2CppDRS.Game.Inventory;
using Il2CppDRS.Game.Survivor;

namespace DarkestDaysTweaks;

internal static class SurvivorSlots
{
    // Keep the game's requested size separate from the expanded, serialized size.
    private static readonly Dictionary<IntPtr, int> nativeSizes = new();
    [ThreadStatic] private static bool resizing;

    internal static void BeforeResize(Inventory __instance, ref int __0)
    {
        if (resizing || __0 <= 0 || __instance.m_owner?.TryCast<Survivor>() == null) return;
        nativeSizes[__instance.Pointer] = __0;
        __0 = Target(__instance, __0);
    }

    internal static void Update(Survivor __instance)
    {
        if (UnityEngine.Time.frameCount % 15 != 0) return;
        var inventory = __instance.m_inventory;
        if (inventory?.m_model == null) return;
        int current = inventory.m_model.MaxSize;
        if (current <= 0) return;
        if (!nativeSizes.TryGetValue(inventory.Pointer, out int native))
        {
            // An existing save may include capacity from traits or equipment.
            native = current;
            nativeSizes[inventory.Pointer] = native;
        }
        if (Math.Max(native, Mod.Current.SurvivorSlots) == current) return;
        int target = Target(inventory, native);
        if (target == current) return;
        resizing = true;
        try { inventory.SetMaxSize(target); }
        finally { resizing = false; }
    }

    private static int Target(Inventory inventory, int native)
    {
        int target = Math.Max(native, Mod.Current.SurvivorSlots);
        var model = inventory.m_model;
        if (model == null || target >= model.MaxSize) return target;
        var stacks = model.Contents;
        if (stacks == null) return model.MaxSize;
        for (int i = 0; i < stacks.Count; i++)
        {
            var stack = stacks[i];
            if (stack != null && stack.Count > 0)
                target = Math.Max(target, Math.Max(i, stack.Index) + 1);
        }
        return target;
    }

    internal static void Forget(Inventory __instance) => nativeSizes.Remove(__instance.Pointer);
}

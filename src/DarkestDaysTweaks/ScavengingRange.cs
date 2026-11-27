using HarmonyLib;
using Il2CppDRS.Database.Levels.Map;
using Il2CppDRS.Game.Location;
using Il2CppDRS.Presentation.UI.Screens.Map;
using Il2CppDRS.SerializableGuid;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace DarkestDaysTweaks;

internal static class ScavengingRange
{
    private static MapData? map;
    private static MapShelterLocationData? cachedShelter;
    [ThreadStatic] private static int vanillaReachabilityScope;

    internal static void Install(HarmonyLib.Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(MapShelterLocationData), nameof(MapShelterLocationData.IsReachable)),
            postfix: Hook(nameof(ExtendReachability)));
        harmony.Patch(AccessTools.Method(typeof(MapIconCurrentShelterUI), nameof(MapIconCurrentShelterUI.PopulateShelter)),
            prefix: Hook(nameof(BeginRadiusDisplay)), finalizer: Hook(nameof(EndRadiusDisplay)));
        var vanillaCallers = new[]
        {
            AccessTools.Method(typeof(LocationsManager), nameof(LocationsManager.InitialiseAccessibleLocations)),
            AccessTools.Method(typeof(LocationsManager), nameof(LocationsManager.MoveShelter)),
            AccessTools.Method(typeof(MapHud), nameof(MapHud.DoMoveShelter))
        };
        foreach (var caller in vanillaCallers)
            harmony.Patch(caller, prefix: Hook(nameof(BeginVanillaReachability)), finalizer: Hook(nameof(EndVanillaReachability)));
    }

    private static HarmonyMethod Hook(string name) => new(typeof(ScavengingRange), name);

    private static void ExtendReachability(MapShelterLocationData __instance, SerializableGuid __0, ref bool __result)
    {
        double multiplier = Mod.Current.ScavengingRangeMultiplier;
        if (__result || multiplier <= 1 || vanillaReachabilityScope != 0) return;
        float radius = __instance.ScavengeRadius;
        if (radius <= 0 || !float.IsFinite(radius)) return;

        if (map == null || cachedShelter?.Pointer != __instance.Pointer)
        {
            map = FindOwningMap(__instance);
            cachedShelter = __instance;
        }
        var locations = map?.m_mapLocationsDictionary;
        if (locations == null || !locations.TryGetValue(__0, out var destination) || destination == null) return;

        float distanceSquared = (destination.MapPosition - __instance.MapPosition).sqrMagnitude;
        double expanded = radius * multiplier;
        __result = distanceSquared > radius * radius && distanceSquared <= expanded * expanded;
    }

    private static MapData? FindOwningMap(MapShelterLocationData shelter)
    {
        foreach (var asset in Resources.FindObjectsOfTypeAll(Il2CppType.Of<MapData>()))
        {
            var candidate = asset.TryCast<MapData>();
            var locations = candidate?.m_mapLocationsDictionary;
            if (locations != null && locations.TryGetValue(shelter.LocationReference, out var registered) &&
                registered?.Pointer == shelter.Pointer) return candidate;
        }
        return null;
    }

    private static void BeginRadiusDisplay(MapShelterLocationData __0, out float? __state)
    {
        __state = null;
        double multiplier = Mod.Current.ScavengingRangeMultiplier;
        if (multiplier <= 1 || __0 == null || __0.m_scavengeRadius <= 0) return;
        __state = __0.m_scavengeRadius;
        __0.m_scavengeRadius = (float)(__state.Value * multiplier);
    }

    private static void EndRadiusDisplay(MapShelterLocationData __0, float? __state)
    {
        if (__state.HasValue) __0.m_scavengeRadius = __state.Value;
    }

    private static void BeginVanillaReachability() => vanillaReachabilityScope++;
    private static void EndVanillaReachability() => vanillaReachabilityScope--;
}

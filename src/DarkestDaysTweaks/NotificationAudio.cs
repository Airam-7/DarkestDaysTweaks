using HarmonyLib;
using Il2CppDRS.Presentation.Audio;
using Il2CppDRS.Presentation.UI.Screens;
using Il2CppFMOD;
using Il2CppFMOD.Studio;

namespace DarkestDaysTweaks;

internal static class NotificationAudio
{
    private static InjectableSceneAudioEvent? notification;
    [ThreadStatic] private static bool startingNotification;
    private static readonly (int, int, int, int)[] StateCueIds =
    {
        EventId("0556209b-698b-4941-b5f0-b41230234ec8"), // Neutral
        EventId("a530d8b9-c6f3-4bac-9f49-21bafd0ded17"), // Good
        EventId("ea945b74-44b1-432b-a0dc-5c4625053420")  // Bad
    };

    internal static void Install(HarmonyLib.Harmony harmony)
    {
        // Native callers inline ShowStateGain. The event handler provides the owning screen.
        harmony.Patch(AccessTools.Method(typeof(StateGainedScreen), nameof(StateGainedScreen.StateChange)),
            prefix: new HarmonyMethod(typeof(NotificationAudio), nameof(IdentifyNotification)));
        harmony.Patch(AccessTools.Method(typeof(ASceneAudioEvent), nameof(ASceneAudioEvent.StartEvent)),
            prefix: new HarmonyMethod(typeof(NotificationAudio), nameof(BeginEvent)),
            finalizer: new HarmonyMethod(typeof(NotificationAudio), nameof(EndEvent)));
        harmony.Patch(AccessTools.Method(typeof(EventDescription), nameof(EventDescription.createInstance)),
            postfix: new HarmonyMethod(typeof(NotificationAudio), nameof(AdjustBeforePlayback)));
    }

    private static void IdentifyNotification(StateGainedScreen __instance) =>
        notification = __instance.m_stateGainedSceneAudio;

    private static bool BeginEvent(ASceneAudioEvent __instance, out bool __state)
    {
        __state = startingNotification;
        int percent = Mod.Current.NotificationVolume;
        if (percent >= 100)
        {
            startingNotification = false;
            return true;
        }

        startingNotification = IsStateCue(__instance.m_audioEventReference.Guid) ||
            (notification != null && notification.Pointer == __instance.Pointer);
        if (percent != 0 || !startingNotification) return true;

        // The StateGained events control music parameters. Muting the triggering instance
        // can still let the other event play, so skip the trigger when muted.
        if (__instance.m_triggerOnce) __instance.m_hasTriggered = true;
        return false;
    }

    private static (int, int, int, int) EventId(string value)
    {
        byte[] bytes = Guid.Parse(value).ToByteArray();
        return (BitConverter.ToInt32(bytes, 0), BitConverter.ToInt32(bytes, 4),
            BitConverter.ToInt32(bytes, 8), BitConverter.ToInt32(bytes, 12));
    }

    private static void EndEvent(bool __state) => startingNotification = __state;

    private static bool IsStateCue(GUID id) =>
        Array.IndexOf(StateCueIds, (id.Data1, id.Data2, id.Data3, id.Data4)) >= 0;

    private static void AdjustBeforePlayback(ref EventInstance __0, RESULT __result)
    {
        int percent = Mod.Current.NotificationVolume;
        if (percent >= 100 || __result != RESULT.OK || !__0.isValid()) return;
        if (!startingNotification &&
            (__0.getDescription(out var description) != RESULT.OK ||
             description.getID(out GUID id) != RESULT.OK || !IsStateCue(id))) return;
        if (__0.getVolume(out float volume) == RESULT.OK)
            __0.setVolume(volume * percent / 100f);
    }

}

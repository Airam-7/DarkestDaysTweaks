using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;
using Il2CppDRS.Input;
using Il2CppDRS.Game.Inventory;
using Il2CppDRS.Game.Items.Components;
using Il2CppDRS.Game.Character;
using Il2CppDRS.Game.Character.States.ConcreteStates;
using Il2CppDRS.Database.Noise;
using Guid = Il2CppDRS.SerializableGuid.SerializableGuid;

[assembly: MelonInfo(typeof(DarkestDaysTweaks.Mod), "Darkest Days Tweaks", "0.1.7", "Airam")]
[assembly: MelonGame("PikPok", "IntoTheDeadOurDarkestDays")]

namespace DarkestDaysTweaks;

public sealed class Mod : MelonMod
{
    internal static Settings Current = new();
    internal static long StackCalls, MeleeCalls, FirearmCalls, WalkCalls, RunCalls;
    private string configPath = "";
    private SaveBackups? backups;
    private SaveBackups.Slot[]? saveSlots;
    private SaveBackups.Entry? confirmBackup;
    private bool confirmCurrentLoad;
    private bool archiveView;
    private bool ConfirmingSaveLoad => confirmBackup != null || confirmCurrentLoad;
    private int backupCampaign, backupSelection, backupPhaseIndex;
    private enum BackupControl { SwitchView, CurrentSave, Phase, Previous, Next, Initial, Manual, CreateCopy, Recovery, Back }
    private string backupStatus = "";
    private float nextConfigCheck;
    private (DateTime Written, long Length)? loadedConfigStamp, observedConfigStamp;
    private bool open, keyWasDown;
    private bool sticksWereDown;
    private static bool capturing;
    private ActionSetSwitcher? blockedSwitcher;
    private int blockHandle;
    private int selected, heldDirection;
    private float repeatAt;
    private bool wasConfirm, wasBack, padFocus;
    private int draggedSlider = -1;
    private string[] edits = new string[6];
    private static readonly string[] SettingLabels = {"Inventory stack size", "Melee weapon durability", "Firearm durability", "Survivor inventory slots", "Experience gain", "Location loot amount"};
    private static readonly int[] ShelfLifeSteps = {1, 2, 3, 5};
    private static readonly int[] RecoverySteps = {1, 2, 3, 4};
    private int visualReaction, shelfLife, itemHealing, satiety, moraleRecovery;
    private int notificationVolumeStep, scavengingRangeStep;
    private static readonly int[] SettingsFocusOrder = { 0, 1, 2, 3, 4, 5, 6, 7, 12, 20, 13, 14, 15, 22, 23, 8, 9, 10, 11 };
    private int? windowScaleDrag;
    private int panelOpacity, panelTextSize, panelScale;
    private bool walk, run, showSettingsButton;
    private string status = "";
    private bool guiErrorLogged;
    private bool updateLogged, guiLogged;
    private int activeField=-1;
    private bool selectAll;
    private GUIStyle? labelStyle, buttonStyle, smallStyle, titleStyle, panelStyle, editStyle;
    private bool oldCursorVisible;
    private CursorLockMode oldCursorLock;
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);

    public override void OnInitializeMelon()
    {
        configPath = Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "DarkestDaysTweaks.txt");
        Current = Settings.Load(configPath);
        Current.Save(configPath);
        RememberConfigStamp();
        SyncEdits();
        try
        {
            PatchSlots(typeof(Inventory), "SetMaxSize", nameof(SurvivorSlots.BeforeResize), true);
            PatchSlots(typeof(Inventory), "System_IDisposable_Dispose", nameof(SurvivorSlots.Forget), true);
            PatchSlots(typeof(Il2CppDRS.Game.Survivor.Survivor), "DRS_Utils_Framework_ISimulatable_Simulate", nameof(SurvivorSlots.Update), false);
            Patch(typeof(Il2CppDRS.Game.Survivor.Progression.SurvivorProgressionSystem), "DRS_Game_Survivor_Progression_IProgressionHandler_AddXP", nameof(ScaleExperience), null);
            LocationLoot.Install(HarmonyInstance);
            FoodShelfLife.Install(HarmonyInstance);
            ItemRecovery.Install(HarmonyInstance);
            DeferredHealing.Install(HarmonyInstance);
            VisualReaction.Install(HarmonyInstance);
            Patch(typeof(UnityEngine.EventSystems.EventSystem), "Update", nameof(AllowGameUI), null);
            foreach (var calculator in new[] {typeof(Inventory), typeof(InventoryStack), typeof(Il2CppDRS.Database.Balancing.InventoryBalancingData)})
                Patch(calculator, "GetAdjustedMaxStack", nameof(StackPrefix), nameof(StackPostfix));
            Patch(typeof(InventoryGameUtils), "GetMaxStacksForDifficulty", nameof(StackPrefix), nameof(StackPostfix));
            Patch(typeof(ADepleteWeaponDurabilityComponent), "DepleteModifiedDurability", nameof(WearPrefix), null);
            Patch(typeof(ACharacterNoise), "TryCreateNoise", nameof(FootstepPrefix), null);
            LoggerInstance.Msg("READY: stacks, weapon wear and survivor footstep detection hooks installed. F6 = settings.");
        }
        catch (Exception ex)
        {
            HarmonyInstance.UnpatchSelf();
            status = "ERROR: Features could not be enabled. Check MelonLoader/Latest.log.";
            LoggerInstance.Error(ex);
        }
        try
        {
            var feature = new SaveBackups(Path.GetDirectoryName(configPath)!, LoggerInstance);
            feature.Install(HarmonyInstance);
            backups = feature;
        }
        catch (Exception ex) { LoggerInstance.Warning($"Phase backups unavailable: {ex.Message}"); }
        try { NotificationAudio.Install(HarmonyInstance); }
        catch (Exception ex) { LoggerInstance.Warning($"Notification volume unavailable: {ex.Message}"); }
        try { ScavengingRange.Install(HarmonyInstance); }
        catch (Exception ex) { LoggerInstance.Warning($"Scavenging range unavailable: {ex.Message}"); }
    }
    private static void ScaleExperience(ref float __0)
    {
        if (__0 > 0 && float.IsFinite(__0) && Current.ExperienceMultiplier > 1)
            __0 = (float)Math.Min(float.MaxValue, __0 * Current.ExperienceMultiplier);
    }

    private static bool AllowGameUI() => !capturing;
    private void CaptureGameplay()
    {
        var next = InputManager.Exists ? InputManager.Instance.ActionSetSwitcher : null;
        if (next?.Pointer == blockedSwitcher?.Pointer) return;
        ReleaseGameplay();
        if (next == null) return;
        blockHandle = next.PushActionSetAffector(ActionSetSwitcher.ActionSet.Disabled, ActionSetSwitcher.ActionSetPriority.Critical);
        blockedSwitcher = next;
        next.PostSimulate();
    }
    private void ReleaseGameplay()
    {
        if (blockedSwitcher == null) return;
        blockedSwitcher.PopActionSetAffector(blockHandle);
        blockedSwitcher = null;
    }
    public override void OnDeinitializeMelon() { capturing = false; ReleaseGameplay(); }
    private void PatchSlots(Type type, string method, string callback, bool before)
    {
        var hook = new HarmonyMethod(AccessTools.Method(typeof(SurvivorSlots), callback));
        HarmonyInstance.Patch(AccessTools.Method(type, method), before ? hook : null, before ? null : hook);
    }
    private void Patch(Type type, string method, string? prefix, string? postfix)
    {
        var target = AccessTools.Method(type, method) ?? throw new MissingMethodException(type.FullName, method);
        HarmonyMethod? H(string? name) => name == null ? null : new HarmonyMethod(typeof(Mod).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic));
        HarmonyInstance.Patch(target, H(prefix), H(postfix));
    }
    [ThreadStatic] private static long scaledCalculations;
    private static void StackPrefix(out long __state) => __state = scaledCalculations;
    private static void StackPostfix(long __state, ref int __result)
    {
        // A nested calculator may already have scaled this result.
        if (__state != scaledCalculations) return;
        scaledCalculations++;
        StackCalls++;
        __result = Settings.StackLimit(__result, Current.StackMultiplier);
    }
    private static void WearPrefix(ADepleteWeaponDurabilityComponent __instance, ref float __0)
    {
        if (__0 == 0) return;
        var components = __instance.m_parent?.m_components;
        if (components == null) return;
        bool gun = false, melee = false;
        for (int i = 0; i < components.Count; i++)
        {
            var c = components[i];
            if (c.TryCast<LoadedAmmoComponent>() != null) gun = true;
            if (c.TryCast<DepleteDurabilityOnMeleeAttackComponent>() != null) melee = true;
        }
        // Do not change armour, food degradation, repair costs, or current/max saved durability.
        if (gun) { FirearmCalls++; __0 = (float)(__0 / Current.FirearmDurability); }
        else if (melee) { MeleeCalls++; __0 = (float)(__0 / Current.MeleeDurability); }
    }
    private static bool FootstepPrefix(ACharacterNoise __instance, ANoiseData noiseData, ref Guid eventGuid, ref bool __result)
    {
        // Match only the current footstep asset of living survivors, excluding shots, impacts,
        // executions, stance changes, environment noises and zombie footsteps.
        if (__instance.TryCast<SurvivorNoise>() == null) return true;
        var step = __instance.m_currentFootstepNoiseSet?.Footstep;
        if (step == null || noiseData == null || step.Pointer != noiseData.Pointer) return true;
        var machine = __instance.m_stateMachine;
        bool running = machine != null && (machine.IsStateActive<SpeedingState>() || machine.IsStateActive<RushingState>());
        if (running) RunCalls++; else WalkCalls++;
        bool silent = running ? Current.SilentRun : Current.SilentWalk;
        if (silent) { eventGuid = default; __result = false; return false; }
        return true;
    }
    public override void OnUpdate()
    {
        backups?.Update();
        CheckConfigFile();
        if (!updateLogged) { LoggerInstance.Msg("Update callback active."); updateLogged=true; }
        bool down = (GetAsyncKeyState(0x75) & 0x8000) != 0;
        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (open) CaptureGameplay();
        else if (capturing && (pad == null || (!pad.buttonEast.isPressed && !pad.buttonSouth.isPressed &&
                 !pad.leftStickButton.isPressed && !pad.rightStickButton.isPressed &&
                 !pad.dpad.up.isPressed && !pad.dpad.down.isPressed && !pad.dpad.left.isPressed &&
                 !pad.dpad.right.isPressed && pad.leftStick.ReadValue().sqrMagnitude < .04f)))
        {
            ReleaseGameplay();
            capturing = false;
        }
        bool sticks = pad != null && pad.leftStickButton.isPressed && pad.rightStickButton.isPressed;
        bool toggled = ((down && !keyWasDown) || (sticks && !sticksWereDown)) && Application.isFocused;
        if (toggled) { Toggle(); padFocus = sticks; }
        bool confirm = pad != null && pad.buttonSouth.isPressed;
        bool back = pad != null && pad.buttonEast.isPressed;
        if (open && !toggled && Application.isFocused && pad != null && !sticks)
        {
            if (back && !wasBack) BackOrClose();
            else if (confirm && !wasConfirm) { padFocus = true; ActivateSelected(); }
            else Navigate(pad);
        }
        wasConfirm = confirm;
        wasBack = back;
        sticksWereDown = sticks;
        keyWasDown = down;
        if (open) { Cursor.visible = true; Cursor.lockState = CursorLockMode.None; }
    }
    private void Toggle()
    {
        LoggerInstance.Msg($"F6: menu {(!open ? "open" : "closed")}");
        if (!open)
        {
            oldCursorVisible = Cursor.visible; oldCursorLock = Cursor.lockState;
            SyncEdits(); selected = heldDirection = 0; padFocus = false; capturing = open = true;
            CaptureGameplay();
        }
        else Close();
    }
    private void Close()
    {
        saveSlots = null;
        confirmBackup = null;
        confirmCurrentLoad = false;
        draggedSlider = -1;
        windowScaleDrag = null;
        open = false; Cursor.visible = oldCursorVisible; Cursor.lockState = oldCursorLock;
    }
    private void SyncEdits()
    {
        activeField=-1;
        edits = new[] {Current.StackMultiplier, Current.MeleeDurability, Current.FirearmDurability, Current.SurvivorSlots, Current.ExperienceMultiplier, Current.LootMultiplier}
            .Select(x => x.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        visualReaction = Current.VisualReactionSteps;
        shelfLife = Current.FoodShelfLifeMultiplier;
        itemHealing = Current.ItemHealingMode;
        satiety = Current.SatietyMode;
        moraleRecovery = Current.MoraleRecoveryMode;
        notificationVolumeStep = Current.NotificationVolume / Settings.NotificationVolumeStep;
        scavengingRangeStep = (int)Math.Round((Current.ScavengingRangeMultiplier - 1) / Settings.ScavengingRangeStep);
        showSettingsButton = Current.ShowSettingsButton;
        panelOpacity = Current.PanelOpacity;
        panelTextSize = Current.PanelTextSize;
        panelScale = Current.PanelScale;
        walk = Current.SilentWalk; run = Current.SilentRun;
    }
    public override void OnGUI()
    {
        if (!guiLogged) { LoggerInstance.Msg("GUI callback active."); guiLogged=true; }
        if (!open && Current.ShowSettingsButton && GUI.Button(new Rect(Screen.width-260,45,245,32), "Mod settings - F6 / L3+R3")) Toggle();
        if (!open) return;
        try
        {
            HandleKeyboard();
            if (open) Draw();
        }
        catch (Exception ex) { if (!guiErrorLogged) { LoggerInstance.Error(ex); guiErrorLogged = true; } }
    }
    private void Draw()
    {
        const float panelHeight = 744;
        float available = Math.Min(Screen.width * .92f / 1000f, Screen.height * .90f / panelHeight);
        float automatic = Math.Min(1.25f, available / 1.12f);
        float scale = Math.Max(.01f, Math.Min(available, automatic * panelScale / 100f));
        float textScale = scale * panelTextSize / 100f;
        float x=(Screen.width-1000*scale)/2,y=(Screen.height-panelHeight*scale)/2;
        Rect R(float px,float py,float w,float h)=>new(x+px*scale,y+py*scale,w*scale,h*scale);
        if (labelStyle == null) InitializeStyles();
        labelStyle!.fontSize=buttonStyle!.fontSize=editStyle!.fontSize=(int)(20*textScale);
        smallStyle!.fontSize=(int)(16*textScale);titleStyle!.fontSize=(int)(26*textScale);
        var previous=GUI.color;
        GUI.color=new Color(0.045f,0.055f,0.07f,0.98f * panelOpacity / 100f);
        GUI.Box(R(0,0,1000,panelHeight),"",panelStyle);
        GUI.color=previous;
        if (saveSlots != null)
        {
            DrawBackups(R);
            return;
        }
        GUI.Label(R(28,12,930,36),"DARKEST DAYS TWEAKS  |  F6 or L3 + R3",titleStyle);
        if (backups?.CanList == true &&
            GUI.Button(R(760,12,212,36), "Saved games", buttonStyle)) OpenBackups();
        GUI.Label(R(28,52,945,24),"Arrows / D-pad / left stick: navigate. Enter / A / Cross: select. Esc / B / Circle: close.",smallStyle);
        GUI.Label(R(28,76,945,24),"Use Left / Right to adjust a value, or click a numeric field to type.",smallStyle);
        if (Event.current.type == EventType.MouseDown) padFocus = false;
        GUI.Label(R(28,104,450,26), "Core modifiers", labelStyle);
        GUI.Label(R(508,104,450,26), "Situational modifiers", labelStyle);
        if (padFocus)
        {
            var (column, row) = FocusMarker(selected);
            GUI.Box(R(column,row,6,25),"",editStyle);
        }
        EditNumber();
        for (int i=0;i<SettingLabels.Length;i++)
        {
            float row = CoreRowY(i);
            GUI.Label(R(28,row,460,28),SettingLabels[i],labelStyle);
            var oldBackground=GUI.backgroundColor;
            GUI.backgroundColor=activeField==i ? new Color(0.35f,0.85f,1,1) : new Color(0.72f,0.9f,1,1);
            string value = edits[i];
            if (activeField == i) value += " |";
            else if (Settings.TryNumber(value, out double number) && number == 1) value = "Vanilla";
            if (GUI.Button(R(28,row+33,130,26),value,editStyle)) {activeField=i;selectAll=true;}
            GUI.backgroundColor=oldBackground;
            DrawSlider(i, R(176,row+33,300,26));
        }
        if (GUI.Button(R(508,137,220,31), $"Silent walking: {(walk ? "ON" : "OFF")}",buttonStyle)) walk = !walk;
        if (GUI.Button(R(744,137,228,31), $"Silent running: {(run ? "ON" : "OFF")}",buttonStyle)) run = !run;
        GUI.Label(R(508,173,464,22), "On suppresses footstep detection by zombies.", smallStyle);
        GUI.Label(R(508,197,464,22), "Footsteps are still audible. Off uses vanilla rules.", smallStyle);
        GUI.Label(R(508,226,464,22), $"Food shelf life: {(shelfLife == 1 ? "Vanilla" : $"{shelfLife}x")}", smallStyle);
        DrawChoiceSlider(12, R(508,248,464,18), ref shelfLife, ShelfLifeSteps);
        string reaction = visualReaction == 0 ? "Vanilla (+0 s)" : $"+{visualReaction * .5f:0.#} s";
        GUI.Label(R(508,278,464,22), $"Visual detection grace: {reaction}", smallStyle);
        DrawStepSlider(20, R(508,300,464,18), ref visualReaction, 4);
        GUI.Label(R(508,330,464,22), $"Recovering Health: {RecoveryLabel(itemHealing)}", smallStyle);
        DrawChoiceSlider(13, R(508,352,464,18), ref itemHealing, RecoverySteps);
        GUI.Label(R(508,382,464,22), $"Food satiety: {RecoveryLabel(satiety)}", smallStyle);
        DrawChoiceSlider(14, R(508,404,464,18), ref satiety, RecoverySteps);
        GUI.Label(R(508,434,464,22), $"Restoring Morale: {RecoveryLabel(moraleRecovery)}", smallStyle);
        DrawChoiceSlider(15, R(508,456,464,18), ref moraleRecovery, RecoverySteps);
        GUI.Label(R(508,482,220,22), $"Notification volume: {notificationVolumeStep * Settings.NotificationVolumeStep}%", smallStyle);
        DrawStepSlider(22, R(508,504,220,18), ref notificationVolumeStep, 100 / Settings.NotificationVolumeStep);
        string rangeLabel = scavengingRangeStep == 0 ? "Vanilla" : $"{1 + scavengingRangeStep * Settings.ScavengingRangeStep:0.##}x";
        GUI.Label(R(744,482,228,22), $"Scavenging range: {rangeLabel}", smallStyle);
        DrawStepSlider(23, R(744,504,228,18), ref scavengingRangeStep, Settings.ScavengingRangeSteps);

        GUI.Label(R(28,513,450,28), "Appearance", labelStyle);
        if (GUI.Button(R(28,546,223,32), $"Show button: {(showSettingsButton ? "ON" : "OFF")}", buttonStyle))
            showSettingsButton = !showSettingsButton;
        if (GUI.Button(R(265,546,223,32), $"Background: {panelOpacity}%", buttonStyle)) AdjustAppearance(9, 1);
        GUI.Label(R(508,542,220,22), $"Text size: {panelTextSize}%", smallStyle);
        DrawTextSizeSlider(R(508,568,220,18));
        GUI.Label(R(744,542,228,22), $"Window size: {windowScaleDrag ?? panelScale}%", smallStyle);
        DrawWindowSizeSlider(R(744,568,228,18));
        GUI.Label(R(28,609,945,23),"Pause the game before changing settings during a run.",smallStyle);
        if (GUI.Button(R(28,649,230,42), "Apply and save",buttonStyle)) Apply();
        if (GUI.Button(R(272,649,230,42), "Reload file",buttonStyle))
            Reload();
        if (GUI.Button(R(516,649,230,42), "Restore settings",buttonStyle)) ResetValues();
        if (GUI.Button(R(760,649,212,42), "Close",buttonStyle)) Close();
        GUI.Label(R(28,699,945,27),status,smallStyle);
        GUI.Label(R(28,722,945,21),$"Events: stacks {StackCalls} | melee {MeleeCalls} | firearms {FirearmCalls} | footsteps {WalkCalls}/{RunCalls}",smallStyle);
    }
    private static float CoreRowY(int index) => 137 + index * 62;

    private static (float X, float Y) FocusMarker(int control) => control switch
    {
        < 6 => (17, CoreRowY(control)),
        6 => (497, 137),
        7 => (733, 137),
        8 => (17, 546),
        9 => (254, 546),
        10 => (497, 546),
        11 => (733, 546),
        12 => (497, 226),
        20 => (497, 278),
        21 => (749, 17),
        22 => (497, 482),
        23 => (733, 482),
        >= 13 and <= 15 => (497, 330 + (control - 13) * 52),
        _ => (17 + (control - 16) * 244, 649)
    };

    private void AdjustAppearance(int control, int direction)
    {
        switch (control)
        {
            case 9: panelOpacity = direction > 0 && panelOpacity >= 100 ? 35 : Math.Clamp(panelOpacity + 10 * direction, 35, 100); break;

            case 11: panelScale = direction > 0 && panelScale >= 130 ? 70 : Math.Clamp(panelScale + 10 * direction, 70, 130); break;
        }
    }

    private void CycleShelfLife() => shelfLife = shelfLife switch { 1 => 2, 2 => 3, 3 => 5, _ => 1 };

    private static string RecoveryLabel(int mode) => mode == 4 ? "Max" : mode == 1 ? "Vanilla" : $"{mode}x";

    private void CycleRecovery(int control)
    {
        if (control == 13) itemHealing = itemHealing % 4 + 1;
        else if (control == 14) satiety = satiety % 4 + 1;
        else if (control == 15) moraleRecovery = moraleRecovery % 4 + 1;
    }

    private void Reload()
    {
        try { Current = Settings.Load(configPath); RememberConfigStamp(); SyncEdits(); status="Settings file reloaded."; }
        catch (Exception ex) { status="Could not read the settings file."; LoggerInstance.Error(ex); }
    }

    private (DateTime Written, long Length)? ReadConfigStamp()
    {
        var file = new FileInfo(configPath);
        return file.Exists && file.Length > 0 ? (file.LastWriteTimeUtc, file.Length) : null;
    }

    private void RememberConfigStamp()
    {
        loadedConfigStamp = observedConfigStamp = ReadConfigStamp();
    }

    private void CheckConfigFile()
    {
        float now = Time.realtimeSinceStartup;
        if (now < nextConfigCheck) return;
        nextConfigCheck = now + 1f;
        try
        {
            var stamp = ReadConfigStamp();
            // Give editors that write in place one polling interval to finish.
            if (stamp == null || stamp == loadedConfigStamp || stamp != observedConfigStamp)
            {
                observedConfigStamp = stamp;
                return;
            }

            var settings = Settings.Load(configPath);
            if (ReadConfigStamp() != stamp) return;
            Current = settings;
            loadedConfigStamp = stamp;
            SyncEdits();
            status = "Settings updated from file.";
        }
        catch (IOException) { /* Retry after the editor releases the file. */ }
        catch (UnauthorizedAccessException) { /* Retry when the file becomes readable. */ }
    }
    private void ResetValues() { edits = new[]{"1","1","1","1","1","1"}; walk=run=false; showSettingsButton=true; visualReaction=scavengingRangeStep=0; shelfLife=itemHealing=satiety=moraleRecovery=1; panelOpacity=panelTextSize=panelScale=100; notificationVolumeStep=100 / Settings.NotificationVolumeStep; Apply(); }
    private void ActivateSelected()
    {
        if (saveSlots != null)
        {
            ActivateBackup();
            return;
        }
        switch (selected)
        {
            case 6: walk = !walk; break;
            case 7: run = !run; break;
            case 8: showSettingsButton = !showSettingsButton; break;
            case 9:
            case 11: AdjustAppearance(selected, 1); break;
            case 12: CycleShelfLife(); break;
            case 13:
            case 14:
            case 15: CycleRecovery(selected); break;
            case 16: Apply(); break;
            case 17: Reload(); break;
            case 18: ResetValues(); break;
            case 19: Close(); break;
            case 20: visualReaction = (visualReaction + 1) % 5; break;
            case 21: OpenBackups(); break;
        }
    }
    private void Navigate(UnityEngine.InputSystem.Gamepad pad)
    {
        var stick = pad.leftStick;
        int direction = pad.dpad.up.isPressed || stick.up.isPressed ? 1 :
            pad.dpad.down.isPressed || stick.down.isPressed ? 2 :
            pad.dpad.left.isPressed || stick.left.isPressed ? 3 :
            pad.dpad.right.isPressed || stick.right.isPressed ? 4 : 0;
        float now = Time.realtimeSinceStartup;
        bool repeat = direction == heldDirection;
        heldDirection = direction;
        if (direction == 0 || (repeat && now < repeatAt)) return;
        repeatAt = now + (repeat ? .12f : .4f);
        MoveSelection(direction);
    }

    private void HandleKeyboard()
    {
        var e = Event.current;
        if (e.type != EventType.KeyDown || !Application.isFocused) return;
        switch (e.keyCode)
        {
            case KeyCode.Escape:
                BackOrClose();
                break;
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                padFocus = true;
                if (activeField >= 0) activeField = -1;
                else if (saveSlots == null && selected < SettingLabels.Length)
                {
                    activeField = selected;
                    selectAll = true;
                }
                else ActivateSelected();
                break;
            case KeyCode.UpArrow: MoveSelection(1); break;
            case KeyCode.DownArrow: MoveSelection(2); break;
            case KeyCode.LeftArrow: MoveSelection(3); break;
            case KeyCode.RightArrow: MoveSelection(4); break;
            default: return;
        }
        e.Use();
    }

    private void MoveSelection(int direction)
    {
        padFocus = true;
        activeField = -1;
        if (saveSlots != null)
        {
            MoveBackupSelection(direction);
            return;
        }
        if (selected == 21)
        {
            if (direction == 2) selected = 0;
            return;
        }
        if (selected == 0 && direction == 1 && backups?.CanList == true)
        {
            selected = 21;
            return;
        }
        if (direction <= 2)
        {
            int index = Array.IndexOf(SettingsFocusOrder, selected);
            if (index < 0 && direction == 1) selected = SettingsFocusOrder[^1];
            else if (index == SettingsFocusOrder.Length - 1 && direction == 2) selected = 16;
            else if (index >= 0)
                selected = SettingsFocusOrder[Math.Clamp(index + (direction == 1 ? -1 : 1), 0, SettingsFocusOrder.Length - 1)];
            return;
        }

        int sign = direction == 3 ? -1 : 1;
        switch (selected)
        {
            case < 6:
                double max = selected == 5 ? Settings.MaxLoot : selected == 4 ? Settings.MaxExperience :
                    selected == 3 ? Settings.MaxSlots : selected == 0 ? Settings.MaxStacks : Settings.MaxEase;
                Settings.TryNumber(edits[selected], out var value);
                value = Settings.Clamp(value, max);
                double step = Math.Max(1, Math.Pow(10, Math.Floor(Math.Log10(sign < 0 ? Math.Max(1, value - 1) : value)) - 1));
                edits[selected] = Settings.Clamp(value + sign * step, max).ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
                break;
            case 6 or 7:
                selected = Math.Clamp(selected + sign, 6, 7);
                break;
            case 8 or 9:
                selected = Math.Clamp(selected + sign, 8, 10);
                break;
            case 10:
                panelTextSize = Math.Clamp(panelTextSize + sign * 10, 80, 120);
                break;
            case 11:
                panelScale = Math.Clamp(panelScale + sign * 10, 70, 130);
                break;
            case 12: StepChoice(ref shelfLife, ShelfLifeSteps, sign); break;
            case 13: StepChoice(ref itemHealing, RecoverySteps, sign); break;
            case 14: StepChoice(ref satiety, RecoverySteps, sign); break;
            case 15: StepChoice(ref moraleRecovery, RecoverySteps, sign); break;
            case 20: visualReaction = Math.Clamp(visualReaction + sign, 0, 4); break;
            case 22: notificationVolumeStep = Math.Clamp(notificationVolumeStep + sign, 0, 100 / Settings.NotificationVolumeStep); break;
            case 23: scavengingRangeStep = Math.Clamp(scavengingRangeStep + sign, 0, Settings.ScavengingRangeSteps); break;
            default: selected = Math.Clamp(selected + sign, 16, 19); break;
        }
    }

    private static void StepChoice(ref int value, int[] choices, int direction)
    {
        int step = Math.Max(0, Array.IndexOf(choices, value));
        value = choices[Math.Clamp(step + direction, 0, choices.Length - 1)];
    }

    private void OpenBackups()
    {
        if (backups?.CanList != true) return;
        try
        {
            saveSlots = backups.List();
            archiveView = false;
            backupCampaign = backupPhaseIndex = 0;
            backupSelection = (int)FirstBackupControl;
            confirmBackup = null;
            confirmCurrentLoad = false;
            backupStatus = backups.LoadStatus;
            activeField = -1;
        }
        catch (Exception ex) { status = $"Could not open saved games. {ex.Message}"; }
    }

    private SaveBackups.Slot? CurrentSave => saveSlots!.Length == 0 ? null : saveSlots[backupCampaign];

    private bool CanLoadSave => backups?.CanLoad == true;

    private BackupControl FirstBackupControl => !archiveView && CanLoadSave && CurrentSave != null ? BackupControl.CurrentSave
        : CurrentSave?.Phases.Length > 0 ? BackupControl.Phase : BackupControl.SwitchView;

    private bool CanSelectBackupControl(BackupControl control) => control switch
    {
        BackupControl.SwitchView => true,
        BackupControl.CurrentSave => !archiveView && CanLoadSave && CurrentSave != null,
        BackupControl.Phase => CurrentSave?.Phases.Length > 0,
        BackupControl.Previous => backupPhaseIndex > 0,
        BackupControl.Next => backupPhaseIndex + 1 < CurrentSave?.Phases.Length,
        BackupControl.Initial => archiveView && CanLoadSave && CurrentSave?.Initial != null,
        BackupControl.Manual => archiveView && CanLoadSave && CurrentSave?.Manual != null,
        BackupControl.CreateCopy => !archiveView && CurrentSave != null,
        BackupControl.Recovery => CanLoadSave && CurrentSave?.Recovery != null,
        BackupControl.Back => true,
        _ => false
    };

    private void DrawBackups(Func<float, float, float, float, Rect> rect)
    {
        GUI.Label(rect(28,20,690,40), archiveView ? "LOCAL BACKUPS" : "SAVED GAMES", titleStyle);
        if (ConfirmingSaveLoad)
        {
            var entry = confirmBackup;
            string action = archiveView ? "Load local copy" : entry == null ? "Load save" : "Restore and load";
            string label = entry == null ? $"Save {backupCampaign + 1}" : BackupLabel(entry);
            GUI.Label(rect(28,90,944,35), $"{action} {label}?", labelStyle);
            if (archiveView)
                GUI.Label(rect(28,133,944,35), "The backup opens as a new local save in a free slot.", labelStyle);
            else if (entry != null)
                GUI.Label(rect(28,133,944,35), "The target slot is backed up before the selected phase replaces it.", labelStyle);
            if (backups?.MainMenuAvailable != true)
                GUI.Label(rect(28,170,944,35), "Loading exits this run. Unsaved progress will be lost.", labelStyle);
            if (BackupButton(rect(28,236,290,44), "Cancel", 0)) { BackOrClose(); return; }
            if (BackupButton(rect(344,236,290,44), action, 1)) ConfirmRestore();
        }
        else
        {
            if (BackupButton(rect(712,20,260,40), archiveView ? "Game saves" : "Local backups", (int)BackupControl.SwitchView))
            {
                SwitchBackupView();
                return;
            }
            if (CurrentSave is { } slot)
            {
                int count = saveSlots!.Length;
                if (archiveView)
                {
                    if (GUI.Button(rect(28,75,230,37), "Previous campaign", buttonStyle))
                    { ChangeBackupCampaign(-1); return; }
                    GUI.Label(rect(277,75,190,37), $"Campaign {backupCampaign + 1}/{count}", labelStyle);
                    if (GUI.Button(rect(470,75,230,37), "Next campaign", buttonStyle))
                    { ChangeBackupCampaign(1); return; }
                }
                else for (int i = 0; i < count; i++)
                {
                    var bounds = rect(28+i*190,75,184,37);
                    if (GUI.Toggle(bounds, i == backupCampaign, $"Save {i + 1}", buttonStyle) && i != backupCampaign)
                    { ChangeBackupCampaign(i - backupCampaign); return; }
                }
                GUI.Label(rect(28,121,944,40), slot.Label, labelStyle);
                if (CanLoadSave && !archiveView)
                {
                    if (BackupButton(rect(28,174,944,44), "Load current save", (int)BackupControl.CurrentSave))
                    {
                        LoadCurrent(slot);
                        return;
                    }
                }
                else if (!CanLoadSave) GUI.Label(rect(28,174,944,44), "Waiting for the game to finish loading.", smallStyle);
                if (slot.Phases.Length == 0)
                    GUI.Label(rect(28,234,944,40), "Phase backups appear after a phase is saved with the mod active.", smallStyle);
                else
                {
                    var phase = slot.Phases[backupPhaseIndex];
                    var bounds = rect(28,230,944,42);
                    string label = $"Backup {backupPhaseIndex + 1}/{slot.Phases.Length}  |  {BackupLabel(phase)}";
                    if (CanLoadSave)
                    {
                        if (BackupButton(bounds, label, (int)BackupControl.Phase)) SelectBackup(phase);
                    }
                    else
                    {
                        DrawBackupFocus(bounds, (int)BackupControl.Phase);
                        GUI.Label(bounds, label, labelStyle);
                    }
                    DrawPhaseButton(rect(28,282,210,38), "Previous", BackupControl.Previous, -1);
                    DrawPhaseButton(rect(254,282,210,38), "Next", BackupControl.Next, 1);
                }
                if (CanLoadSave && slot.Initial is { } initial &&
                    BackupButton(rect(28,345,944,42), BackupLabel(initial), (int)BackupControl.Initial)) SelectBackup(initial);
                if (CanLoadSave && slot.Manual is { } manual &&
                    BackupButton(rect(28,396,944,42), BackupLabel(manual), (int)BackupControl.Manual)) SelectBackup(manual);
                if (!archiveView && BackupButton(rect(28,396,420,42), "Back up last game save", (int)BackupControl.CreateCopy))
                    CreateManualBackup(slot);
                if (slot.Recovery is { } recovery)
                {
                    var bounds = rect(28,490,944,42);
                    if (!CanLoadSave) GUI.Label(bounds, BackupLabel(recovery), labelStyle);
                    else if (BackupButton(bounds, BackupLabel(recovery), (int)BackupControl.Recovery)) SelectBackup(recovery);
                }
            }
            else GUI.Label(rect(28,88,944,38), "No saved games found.", labelStyle);

            if (BackupButton(rect(28,565,230,44), "Back", (int)BackupControl.Back)) BackOrClose();
        }
        GUI.Label(rect(28,636,944,42), backupStatus, smallStyle);
    }

    private bool BackupButton(Rect bounds, string text, int index)
    {
        DrawBackupFocus(bounds, index);
        return GUI.Button(bounds, text, buttonStyle);
    }

    private void DrawBackupFocus(Rect bounds, int index)
    {
        if (padFocus && index == backupSelection)
            GUI.Box(new Rect(bounds.x - 10, bounds.y, 5, bounds.height), "", editStyle);
    }

    private void DrawPhaseButton(Rect bounds, string label, BackupControl control, int direction)
    {
        bool enabled = GUI.enabled;
        GUI.enabled = enabled && CanSelectBackupControl(control);
        bool clicked = BackupButton(bounds, label, (int)control);
        GUI.enabled = enabled;
        if (clicked) ChangeBackupPhase(direction);
    }

    private static string BackupLabel(SaveBackups.Entry entry)
    {
        if (entry.BeforeRestore) return $"Before restore  |  {entry.Written.ToLocalTime():g}";
        string[] phase = entry.Phase.Split('-');
        string label = Path.GetFileName(entry.Path) switch
        {
            "first-save.sav" => "First archived save  |  ",
            "manual-save.sav" => "Manual backup  |  ",
            _ => ""
        };
        return $"{label}Day {int.Parse(phase[0])}, phase {int.Parse(phase[1]) + 1}  |  {entry.Written.ToLocalTime():g}";
    }

    private void MoveBackupSelection(int direction)
    {
        int sign = direction is 1 or 3 ? -1 : 1;
        if (ConfirmingSaveLoad) backupSelection = Math.Clamp(backupSelection + sign, 0, 1);
        else if (direction >= 3)
        {
            if ((BackupControl)backupSelection is BackupControl.Phase or BackupControl.Previous or BackupControl.Next)
                ChangeBackupPhase(sign);
            else ChangeBackupCampaign(sign);
        }
        else
        {
            for (int index = backupSelection + sign; index >= 0 && index <= (int)BackupControl.Back; index += sign)
            {
                if (!CanSelectBackupControl((BackupControl)index)) continue;
                backupSelection = index;
                break;
            }
        }
    }

    private void ChangeBackupCampaign(int direction)
    {
        if (saveSlots!.Length == 0) return;
        backupCampaign = Math.Clamp(backupCampaign + direction, 0, saveSlots.Length - 1);
        backupPhaseIndex = 0;
        backupSelection = (int)FirstBackupControl;
        backupStatus = "";
    }

    private void ChangeBackupPhase(int direction)
    {
        if (CurrentSave is not { Phases.Length: > 0 } slot) return;
        backupPhaseIndex = Math.Clamp(backupPhaseIndex + direction, 0, slot.Phases.Length - 1);
        if (!CanSelectBackupControl((BackupControl)backupSelection)) backupSelection = (int)BackupControl.Phase;
    }

    private void ActivateBackup()
    {
        if (ConfirmingSaveLoad)
        {
            if (backupSelection == 0) BackOrClose();
            else ConfirmRestore();
        }
        else if (CurrentSave is { } slot)
        {
            var control = (BackupControl)backupSelection;
            if (!CanSelectBackupControl(control)) return;
            switch (control)
            {
                case BackupControl.SwitchView: SwitchBackupView(); break;
                case BackupControl.CurrentSave: LoadCurrent(slot); break;
                case BackupControl.Phase when CanLoadSave: SelectBackup(slot.Phases[backupPhaseIndex]); break;
                case BackupControl.Previous: ChangeBackupPhase(-1); break;
                case BackupControl.Next: ChangeBackupPhase(1); break;
                case BackupControl.Initial when slot.Initial is { } initial: SelectBackup(initial); break;
                case BackupControl.Manual when slot.Manual is { } manual: SelectBackup(manual); break;
                case BackupControl.CreateCopy: CreateManualBackup(slot); break;
                case BackupControl.Recovery when slot.Recovery is { } recovery: SelectBackup(recovery); break;
                case BackupControl.Back: BackOrClose(); break;
            }
        }
        else if (backupSelection == (int)BackupControl.SwitchView) SwitchBackupView();
        else BackOrClose();
    }

    private void SwitchBackupView()
    {
        try
        {
            saveSlots = archiveView ? backups!.List() : backups!.ListArchive();
            archiveView = !archiveView;
            backupCampaign = backupPhaseIndex = 0;
            backupSelection = (int)FirstBackupControl;
            backupStatus = "";
        }
        catch (Exception error) { backupStatus = $"Could not open saves. {error.Message}"; }
    }

    private void CreateManualBackup(SaveBackups.Slot slot)
    {
        try
        {
            backups!.BackUpCurrent(slot);
            backupStatus = "Manual copy saved. Open Local backups to load it.";
        }
        catch (Exception error) { backupStatus = $"Could not back up the save. {error.Message}"; }
    }

    private void SelectBackup(SaveBackups.Entry entry)
    {
        confirmBackup = entry;
        backupSelection = 0;
        backupStatus = "";
    }

    private void BackOrClose()
    {
        if (ConfirmingSaveLoad)
        {
            backupSelection = confirmBackup == null ? (int)BackupControl.CurrentSave
                : confirmBackup.BeforeRestore ? (int)BackupControl.Recovery
                : confirmBackup == CurrentSave?.Initial ? (int)BackupControl.Initial
                : confirmBackup == CurrentSave?.Manual ? (int)BackupControl.Manual : (int)BackupControl.Phase;
            confirmBackup = null;
            confirmCurrentLoad = false;
        }
        else if (saveSlots != null)
        {
            saveSlots = null;
            selected = backups?.CanList == true ? 21 : 0;
        }
        else Close();
    }

    private void LoadCurrent(SaveBackups.Slot slot)
    {
        if (backups?.MainMenuAvailable != true)
        {
            confirmCurrentLoad = true;
            backupSelection = 0;
            backupStatus = "";
            return;
        }
        try
        {
            backups!.LoadCurrent(slot);
            Close();
        }
        catch (Exception ex) { backupStatus = $"Could not load. {ex.Message}"; LoggerInstance.Error(ex); }
    }

    private void ConfirmRestore()
    {
        if (!ConfirmingSaveLoad || CurrentSave is not { } slot) return;
        try
        {
            if (confirmBackup is { } entry) backups!.RestoreAndLoad(slot, entry);
            else backups!.LoadCurrent(slot);
            Close();
        }
        catch (Exception ex) { backupStatus = $"Could not restore. {ex.Message}"; LoggerInstance.Error(ex); }
    }

    private void EditNumber()
    {
        if(activeField<0)return;
        var e=Event.current;
        if(e.type!=EventType.KeyDown)return;
        if(e.keyCode==KeyCode.Return || e.keyCode==KeyCode.KeypadEnter || e.keyCode==KeyCode.Escape){activeField=-1;e.Use();return;}
        if(e.keyCode==KeyCode.Tab){activeField=(activeField+1)%6;selectAll=true;e.Use();return;}
        if(e.keyCode==KeyCode.A && e.control){selectAll=true;e.Use();return;}
        if(e.keyCode==KeyCode.Backspace || e.keyCode==KeyCode.Delete)
        {edits[activeField]=selectAll || edits[activeField].Length==0 ? "" : edits[activeField][..^1];selectAll=false;e.Use();return;}
        char c=e.character;
        if((c>='0' && c<='9') || c=='.' || c==',')
        {if(selectAll)edits[activeField]="";selectAll=false;if(edits[activeField].Length<18)edits[activeField]+=c;e.Use();}
    }
    private void DrawWindowSizeSlider(Rect bounds)
    {
        float fraction = ((windowScaleDrag ?? panelScale) - 70) / 60f;
        if (ReadSlider(11, bounds, ref fraction))
        {
            windowScaleDrag = 70 + 10 * (int)Math.Round(fraction * 6);
            if (draggedSlider != 11)
            {
                panelScale = windowScaleDrag.Value;
                windowScaleDrag = null;
            }
        }
        PaintSlider(bounds, ((windowScaleDrag ?? panelScale) - 70) / 60f);
    }

    private void DrawTextSizeSlider(Rect bounds)
    {
        float fraction = (panelTextSize - 80) / 40f;
        if (ReadSlider(10, bounds, ref fraction))
            panelTextSize = 80 + 10 * (int)Math.Round(fraction * 4);
        PaintSlider(bounds, (panelTextSize - 80) / 40f);
    }

    private void InitializeStyles()
    {
        labelStyle = CopyStyle(GUI.skin.label);
        buttonStyle = CopyStyle(GUI.skin.button);
        smallStyle = CopyStyle(GUI.skin.label);
        titleStyle = CopyStyle(GUI.skin.label);
        panelStyle = CopyStyle(GUI.skin.box);
        editStyle = CopyStyle(GUI.skin.button);
        var white = Texture2D.whiteTexture;
        editStyle.normal.background = editStyle.hover.background = editStyle.active.background = white;
        editStyle.normal.textColor = editStyle.hover.textColor = editStyle.active.textColor = new Color(0.03f,0.09f,0.14f,1);
        panelStyle.normal.background = white;
    }

    private static GUIStyle CopyStyle(GUIStyle source)
    {
        // The managed GUIStyle copy constructor was stripped by this IL2CPP build.
        var clone=new GUIStyle(IL2CPP.il2cpp_object_new(Il2CppClassPointerStore<GUIStyle>.NativeClassPtr));
        clone.m_Ptr=GUIStyle.Internal_Copy(clone,source);
        return clone;
    }
    private void DrawSlider(int index, Rect bounds)
    {
        double maximum = index == 5 ? Settings.MaxLoot : index == 4 ? Settings.MaxExperience : index == 3 ? Settings.MaxSlots : index == 0 ? Settings.MaxStacks : Settings.MaxEase;
        Settings.TryNumber(edits[index], out var value);
        float fraction = (float)Math.Log(Settings.Clamp(value, maximum), maximum);
        if (ReadSlider(index, bounds, ref fraction))
        {
            value = Math.Round(Math.Pow(maximum, fraction), index == 3 || index == 5 ? 0 : 1);
            edits[index] = value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            fraction = (float)Math.Log(value, maximum);
        }
        PaintSlider(bounds, fraction);
    }

    private bool ReadSlider(int id, Rect bounds, ref float fraction)
    {
        var e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && bounds.Contains(e.mousePosition))
            draggedSlider = id;
        if (draggedSlider != id ||
            (e.type != EventType.MouseDown && e.type != EventType.MouseDrag && e.type != EventType.MouseUp))
            return false;

        float knob = bounds.height * .5f;
        fraction = Math.Clamp((e.mousePosition.x - bounds.x - knob / 2) / (bounds.width - knob), 0, 1);
        activeField = -1;
        if (e.type == EventType.MouseUp) draggedSlider = -1;
        e.Use();
        return true;
    }

    private void DrawChoiceSlider(int id, Rect bounds, ref int value, int[] choices)
    {
        int step = Math.Max(0, Array.IndexOf(choices, value));
        DrawStepSlider(id, bounds, ref step, choices.Length - 1);
        value = choices[step];
    }

    private void DrawStepSlider(int id, Rect bounds, ref int step, int last)
    {
        float fraction = step / (float)last;
        if (ReadSlider(id, bounds, ref fraction))
            step = (int)Math.Round(fraction * last);
        PaintSlider(bounds, step / (float)last);
    }

    private void PaintSlider(Rect bounds, float fraction)
    {
        float knob = bounds.height * .5f;
        GUI.Box(new Rect(bounds.x, bounds.y + bounds.height * .38f, bounds.width, bounds.height * .24f), "", buttonStyle);
        GUI.Box(new Rect(bounds.x + fraction * (bounds.width - knob), bounds.y, knob, bounds.height), "", editStyle);
    }

    private void Apply()
    {
        var numbers = new double[6];
        for(int i=0;i<6;i++)
            if (!Settings.TryNumber(edits[i], out numbers[i]) || numbers[i]<1)
            { status="Enter valid numbers greater than or equal to 1.0."; return; }
        var updated = new Settings {StackMultiplier=numbers[0], MeleeDurability=numbers[1], FirearmDurability=numbers[2], SurvivorSlots=(int)Settings.Clamp(numbers[3],Settings.MaxSlots), ExperienceMultiplier=numbers[4], LootMultiplier=(int)Settings.Clamp(numbers[5],Settings.MaxLoot), PanelOpacity=panelOpacity, PanelTextSize=panelTextSize, PanelScale=panelScale, FoodShelfLifeMultiplier=shelfLife, VisualReactionSteps=visualReaction, ItemHealingMode=itemHealing, SatietyMode=satiety, MoraleRecoveryMode=moraleRecovery, ShowSettingsButton=showSettingsButton, SilentWalk=walk, SilentRun=run};
        updated.NotificationVolume = notificationVolumeStep * Settings.NotificationVolumeStep;
        updated.ScavengingRangeMultiplier = 1 + scavengingRangeStep * Settings.ScavengingRangeStep;
        try
        {
            updated.Save(configPath); Current=updated; RememberConfigStamp(); SyncEdits();
            status="Saved. Changes apply to subsequent actions.";
            LoggerInstance.Msg($"Saved settings: stacks={Current.StackMultiplier}, melee={Current.MeleeDurability}, firearm={Current.FirearmDurability}, silentWalk={walk}, silentRun={run}");
        }
        catch(Exception ex) { status="Could not save. Check file permissions and MelonLoader/Latest.log."; LoggerInstance.Error(ex); }
    }
}

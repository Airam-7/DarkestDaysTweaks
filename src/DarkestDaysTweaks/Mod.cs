using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;
using Il2CppDRS.Game.Inventory;
using Il2CppDRS.Game.Items.Components;
using Il2CppDRS.Game.Character;
using Il2CppDRS.Game.Character.States.ConcreteStates;
using Il2CppDRS.Database.Noise;
using Guid = Il2CppDRS.SerializableGuid.SerializableGuid;

[assembly: MelonInfo(typeof(DarkestDaysTweaks.Mod), "Darkest Days Tweaks", "0.1.3", "Airam")]
[assembly: MelonGame("PikPok", "IntoTheDeadOurDarkestDays")]

namespace DarkestDaysTweaks;

public sealed class Mod : MelonMod
{
    internal static Settings Current = new();
    internal static long StackCalls, MeleeCalls, FirearmCalls, WalkCalls, RunCalls;
    private string configPath = "";
    private bool open, keyWasDown;
    private string[] edits = new string[3];
    private bool walk, run;
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
        SyncEdits();
        try
        {
            // One native stack calculator, shared by inventory and UI: no compounded multiplication.
            Patch(typeof(InventoryGameUtils), "GetMaxStacksForDifficulty", null, nameof(StackPostfix));
            Patch(typeof(DurabilityItemComponent), "DepleteDurability", nameof(WearPrefix), null);
            Patch(typeof(ACharacterNoise), "TryCreateNoise", nameof(FootstepPrefix), null);
            LoggerInstance.Msg("READY: stacks, weapon wear and survivor footstep detection hooks installed. F6 = settings.");
        }
        catch (Exception ex)
        {
            HarmonyInstance.UnpatchSelf();
            status = "ERROR: Features could not be enabled. Check MelonLoader/Latest.log.";
            LoggerInstance.Error(ex);
        }
    }
    private void Patch(Type type, string method, string? prefix, string? postfix)
    {
        var target = AccessTools.Method(type, method) ?? throw new MissingMethodException(type.FullName, method);
        HarmonyMethod? H(string? name) => name == null ? null : new HarmonyMethod(typeof(Mod).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic));
        HarmonyInstance.Patch(target, H(prefix), H(postfix));
    }
    private static void StackPostfix(ref int __result)
    {
        StackCalls++;
        __result = Settings.StackLimit(__result, Current.StackMultiplier);
    }
    private static void WearPrefix(DurabilityItemComponent __instance, ref float amount)
    {
        if (amount <= 0) return;
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
        if (gun) { FirearmCalls++; amount = (float)(amount / Current.FirearmDurability); }
        else if (melee) { MeleeCalls++; amount = (float)(amount / Current.MeleeDurability); }
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
        if (!updateLogged) { LoggerInstance.Msg("Update callback active."); updateLogged=true; }
        bool down = (GetAsyncKeyState(0x75) & 0x8000) != 0;
        if (down && !keyWasDown && Application.isFocused) Toggle();
        keyWasDown = down;
        if (open) { Cursor.visible = true; Cursor.lockState = CursorLockMode.None; }
    }
    private void Toggle()
    {
        LoggerInstance.Msg($"F6: menu {(!open ? "open" : "closed")}");
        if (!open)
        {
            oldCursorVisible = Cursor.visible; oldCursorLock = Cursor.lockState;
            SyncEdits(); open = true;
        }
        else Close();
    }
    private void Close()
    {
        open = false; Cursor.visible = oldCursorVisible; Cursor.lockState = oldCursorLock;
    }
    private void SyncEdits()
    {
        activeField=-1;
        edits = new[] {Current.StackMultiplier, Current.MeleeDurability, Current.FirearmDurability}
            .Select(x => x.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        walk = Current.SilentWalk; run = Current.SilentRun;
    }
    public override void OnGUI()
    {
        if (!guiLogged) { LoggerInstance.Msg("GUI callback active."); guiLogged=true; }
        if (!open && GUI.Button(new Rect(Screen.width-205,45,190,32), "Mod settings - F6")) Toggle();
        if (!open) return;
        try { Draw(); }
        catch (Exception ex) { if (!guiErrorLogged) { LoggerInstance.Error(ex); guiErrorLogged = true; } }
    }
    private void Draw()
    {
        float scale=Math.Min(1.5f,Math.Min((Screen.width-24)/1000f,(Screen.height-24)/580f));
        float x=(Screen.width-1000*scale)/2,y=(Screen.height-580*scale)/2;
        Rect R(float px,float py,float w,float h)=>new(x+px*scale,y+py*scale,w*scale,h*scale);
        labelStyle ??=CopyStyle(GUI.skin.label);
        buttonStyle ??=CopyStyle(GUI.skin.button);
        smallStyle ??=CopyStyle(GUI.skin.label);
        titleStyle ??=CopyStyle(GUI.skin.label);
        panelStyle ??=CopyStyle(GUI.skin.box);
        editStyle ??=CopyStyle(GUI.skin.button);
        editStyle.normal.background=editStyle.hover.background=editStyle.active.background=Texture2D.whiteTexture;
        editStyle.normal.textColor=editStyle.hover.textColor=editStyle.active.textColor=new Color(0.03f,0.09f,0.14f,1);
        panelStyle.normal.background=Texture2D.whiteTexture;
        labelStyle.fontSize=buttonStyle.fontSize=editStyle.fontSize=(int)(20*scale);
        smallStyle.fontSize=(int)(16*scale);titleStyle.fontSize=(int)(26*scale);
        var previous=GUI.color;
        GUI.color=new Color(0.045f,0.055f,0.07f,0.98f);
        GUI.Box(R(0,0,1000,580),"",panelStyle);
        GUI.color=previous;
        GUI.Label(R(28,18,930,40),"DARKEST DAYS TWEAKS  |  F6",titleStyle);
        GUI.Label(R(28,66,945,30),"1.0 = vanilla. Higher values increase capacity or weapon lifespan.",labelStyle);
        GUI.Label(R(28,101,945,30),"Click a value to type a number. +2 / +5 / +10 add to the current value.",smallStyle);
        EditNumber();
        var labels = new[] {"Inventory stack size", "Melee weapon durability", "Firearm durability"};
        for (int i=0;i<3;i++)
        {
            float row=155+i*62;
            GUI.Label(R(28,row,350,34),labels[i],labelStyle);
            var oldBackground=GUI.backgroundColor;
            GUI.backgroundColor=activeField==i ? new Color(0.35f,0.85f,1,1) : new Color(0.72f,0.9f,1,1);
            if (GUI.Button(R(390,row,200,36),edits[i]+(activeField==i ? " |" : ""),editStyle)) {activeField=i;selectAll=true;}
            GUI.backgroundColor=oldBackground;
            if (GUI.Button(R(610,row,85,36),"+2",buttonStyle)) Increment(i,2);
            if (GUI.Button(R(710,row,85,36),"+5",buttonStyle)) Increment(i,5);
            if (GUI.Button(R(810,row,140,36),"+10",buttonStyle)) Increment(i,10);
            GUI.Label(R(390,row+36,215,24),activeField==i ? "Type a new value" : "Click to edit",smallStyle);
            GUI.Label(R(610,row+36,350,24),i==0 ? "Limit: 1,000x (1M per stack)" : "Limit: 1,000,000x",smallStyle);
        }
        if(GUI.Button(R(28,365,460,38),$"Silent walking: {(walk ? "ON" : "OFF")}",buttonStyle))walk=!walk;
        if(GUI.Button(R(512,365,460,38),$"Silent running: {(run ? "ON" : "OFF")}",buttonStyle))run=!run;
        GUI.Label(R(28,415,945,28),"ON: suppress footstep detection. OFF: vanilla behavior. Footstep audio is unchanged.",smallStyle);
        GUI.Label(R(28,441,945,28),"Pause the game before changing settings during a run.",smallStyle);
        if (GUI.Button(R(28,475,230,42), "Apply and save",buttonStyle)) Apply();
        if (GUI.Button(R(272,475,230,42), "Reload file",buttonStyle))
        {
            try { Current = Settings.Load(configPath); SyncEdits(); status="Settings file reloaded."; }
            catch (Exception ex) { status="Could not read the settings file."; LoggerInstance.Error(ex); }
        }
        if (GUI.Button(R(516,475,230,42), "Reset to 1.0",buttonStyle)) { edits = new[]{"1","1","1"}; walk=run=false; Apply(); }
        if (GUI.Button(R(760,475,212,42), "Close",buttonStyle)) Close();
        GUI.Label(R(28,525,945,27),status,smallStyle);
        GUI.Label(R(28,552,945,25),$"Events: stacks {StackCalls} | melee {MeleeCalls} | firearms {FirearmCalls} | footsteps {WalkCalls}/{RunCalls}",smallStyle);
    }
    private void EditNumber()
    {
        if(activeField<0)return;
        var e=Event.current;
        if(e.type!=EventType.KeyDown)return;
        if(e.keyCode==KeyCode.Return || e.keyCode==KeyCode.KeypadEnter || e.keyCode==KeyCode.Escape){activeField=-1;e.Use();return;}
        if(e.keyCode==KeyCode.Tab){activeField=(activeField+1)%3;selectAll=true;e.Use();return;}
        if(e.keyCode==KeyCode.A && e.control){selectAll=true;e.Use();return;}
        if(e.keyCode==KeyCode.Backspace || e.keyCode==KeyCode.Delete)
        {edits[activeField]=selectAll || edits[activeField].Length==0 ? "" : edits[activeField][..^1];selectAll=false;e.Use();return;}
        char c=e.character;
        if((c>='0' && c<='9') || c=='.' || c==',')
        {if(selectAll)edits[activeField]="";selectAll=false;if(edits[activeField].Length<18)edits[activeField]+=c;e.Use();}
    }
    private static GUIStyle CopyStyle(GUIStyle source)
    {
        // The managed GUIStyle copy constructor was stripped by this IL2CPP build.
        var clone=new GUIStyle(IL2CPP.il2cpp_object_new(Il2CppClassPointerStore<GUIStyle>.NativeClassPtr));
        clone.m_Ptr=GUIStyle.Internal_Copy(clone,source);
        return clone;
    }
    private void Increment(int index,double amount)
    {
        if(!Settings.TryNumber(edits[index],out var value) || value<1)
        {status="Enter a valid value of at least 1.0 before adding.";return;}
        double max=index==0 ? Settings.MaxStacks : Settings.MaxEase;
        double next=Settings.Clamp(value+amount,max);
        edits[index]=next.ToString("0.######",System.Globalization.CultureInfo.InvariantCulture);
        activeField=-1;
        status=next>=max ? "This setting has reached its limit." : "Value updated. Click Apply and save to confirm.";
    }
    private void Apply()
    {
        var numbers = new double[3];
        for(int i=0;i<3;i++)
            if (!Settings.TryNumber(edits[i], out numbers[i]) || numbers[i]<1)
            { status="Enter valid numbers greater than or equal to 1.0."; return; }
        var updated = new Settings {StackMultiplier=numbers[0], MeleeDurability=numbers[1], FirearmDurability=numbers[2], SilentWalk=walk, SilentRun=run};
        try
        {
            updated.Save(configPath); Current=updated; SyncEdits();
            status="Saved. Changes apply to subsequent actions.";
            LoggerInstance.Msg($"Saved settings: stacks={Current.StackMultiplier}, melee={Current.MeleeDurability}, firearm={Current.FirearmDurability}, silentWalk={walk}, silentRun={run}");
        }
        catch(Exception ex) { status="Could not save. Check file permissions and MelonLoader/Latest.log."; LoggerInstance.Error(ex); }
    }
}

using System.Globalization;

namespace DarkestDaysTweaks;

public sealed class Settings
{
    public const double MaxStacks = 1000, MaxEase = 1000000;
    public double StackMultiplier = 1, MeleeDurability = 1, FirearmDurability = 1;
    public const int MaxSlots = 32;
    public int SurvivorSlots = 1;
    public const double MaxExperience = 10;
    public double ExperienceMultiplier = 1;
    public const int MaxLoot = 5;
    public int LootMultiplier = 1;
    public int FoodShelfLifeMultiplier = 1;
    public int VisualReactionSteps;
    public int ItemHealingMode = 1, SatietyMode = 1, MoraleRecoveryMode = 1;
    public int NotificationVolume = 100;
    public double ScavengingRangeMultiplier = 1;

    public const int NotificationVolumeStep = 5, ScavengingRangeSteps = 8;
    public const double ScavengingRangeStep = .25, MaxScavengingRange = 3;

    public static int ShelfLifeOption(int multiplier) => multiplier switch
    {
        <= 1 => 1,
        2 => 2,
        3 or 4 => 3,
        _ => 5
    };

    public static int FreshFoodExpiry(int currentCycle, int startCycle, int endCycle, int multiplier)
    {
        if (startCycle != currentCycle || endCycle <= startCycle) return endCycle;
        long duration = (long)endCycle - startCycle;
        return (int)Math.Min(int.MaxValue, startCycle + duration * ShelfLifeOption(multiplier));
    }

    public static float RecoveryAmount(float amount, int mode, float current, float maximum)
    {
        if (mode == 1 || amount <= 0 || !float.IsFinite(amount)) return amount;
        float missing = Math.Max(0, maximum - current);
        return mode == 4 ? missing : Math.Min(missing, amount * mode);
    }
    public int PanelOpacity = 100, PanelTextSize = 100, PanelScale = 100;
    public bool ShowSettingsButton = true;
    public bool SilentWalk, SilentRun;

    public static double Clamp(double value, double max) => double.IsFinite(value) ? Math.Clamp(value, 1, max) : 1;
    public static bool TryNumber(string text, out double value) => double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    public static int StackLimit(int vanilla, double factor)
    {
        if (vanilla <= 0 || factor <= 1) return vanilla;
        // Preserve native sentinels, never reduce the original limit or overflow Int32.
        return Math.Max(vanilla, (int)Math.Min(1000000, Math.Ceiling(vanilla * Clamp(factor, MaxStacks))));
    }
    public void Validate()
    {
        PanelOpacity = Math.Clamp(PanelOpacity, 35, 100);
        PanelTextSize = PanelStep(PanelTextSize, 80, 120);
        PanelScale = PanelStep(PanelScale, 70, 130);
        VisualReactionSteps = Math.Clamp(VisualReactionSteps, 0, 4);
        ItemHealingMode = Math.Clamp(ItemHealingMode, 1, 4);
        SatietyMode = Math.Clamp(SatietyMode, 1, 4);
        MoraleRecoveryMode = Math.Clamp(MoraleRecoveryMode, 1, 4);
        NotificationVolume = ((Math.Clamp(NotificationVolume, 0, 100) + 2) / NotificationVolumeStep) * NotificationVolumeStep;
        ScavengingRangeMultiplier = Math.Round(Clamp(ScavengingRangeMultiplier, MaxScavengingRange) / ScavengingRangeStep) * ScavengingRangeStep;
        FoodShelfLifeMultiplier = ShelfLifeOption(FoodShelfLifeMultiplier);
        LootMultiplier = Math.Clamp(LootMultiplier, 1, MaxLoot);
        ExperienceMultiplier = Clamp(ExperienceMultiplier, MaxExperience);
        SurvivorSlots = Math.Clamp(SurvivorSlots, 1, MaxSlots);
        StackMultiplier = Clamp(StackMultiplier, MaxStacks);
        MeleeDurability = Clamp(MeleeDurability, MaxEase);
        FirearmDurability = Clamp(FirearmDurability, MaxEase);
    }

    private static int PanelStep(int value, int minimum, int maximum) =>
        ((Math.Clamp(value, minimum, maximum) + 5) / 10) * 10;
    public static Settings Load(string path)
    {
        var s = new Settings();
        if (!File.Exists(path)) return s;
        foreach (var line in File.ReadAllLines(path))
        {
            var raw = line.Split('#')[0].Trim();
            var parts = raw.Split('=', 2);
            if (parts.Length != 2) continue;
            var key = parts[0].Trim(); var value = parts[1].Trim();
            if (bool.TryParse(value, out var flag))
            {
                if (key == "ShowSettingsButton") s.ShowSettingsButton = flag;
                if (key == "SilentWalk") s.SilentWalk = flag;
                if (key == "SilentRun") s.SilentRun = flag;
            }
            if (!TryNumber(value, out var n)) continue;
            switch (key)
            {
                case "PanelOpacity": s.PanelOpacity = (int)Math.Clamp(n, 35, 100); break;
                case "PanelTextSize": s.PanelTextSize = (int)Math.Clamp(n, 80, 120); break;
                case "PanelScale": s.PanelScale = (int)Math.Clamp(n, 70, 130); break;
                case "FoodShelfLifeMultiplier": s.FoodShelfLifeMultiplier = (int)Clamp(n, 5); break;
                case "VisualReactionSteps": s.VisualReactionSteps = (int)Math.Clamp(n, 0, 4); break;
                case "ItemHealingMode": s.ItemHealingMode = (int)Clamp(n, 4); break;
                case "SatietyMode": s.SatietyMode = (int)Clamp(n, 4); break;
                case "MoraleRecoveryMode": s.MoraleRecoveryMode = (int)Clamp(n, 4); break;
                case "NotificationVolume": s.NotificationVolume = (int)Math.Round(Math.Clamp(n, 0, 100)); break;
                case "ScavengingRangeMultiplier": s.ScavengingRangeMultiplier = n; break;
                case "LootMultiplier": s.LootMultiplier = (int)Clamp(n, MaxLoot); break;
                case "ExperienceMultiplier": s.ExperienceMultiplier = n; break;
                case "SurvivorSlots": s.SurvivorSlots = (int)Clamp(n, MaxSlots); break;
                case "StackMultiplier": s.StackMultiplier = n; break;
                case "MeleeDurability": s.MeleeDurability = n; break;
                case "FirearmDurability": s.FirearmDurability = n; break;
            }
        }
        s.Validate(); return s;
    }
    public void Save(string path)
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string N(double n) => n.ToString("0.######", CultureInfo.InvariantCulture);
        var text = "# Darkest Days Tweaks - F6 or L3+R3 opens/closes the settings menu\n" +
            "# Save this file to apply changes in game. Reload file applies them immediately.\n\n" +
            "# StackMultiplier: 1-1000. MeleeDurability and FirearmDurability: 1-1000000.\n" +
            $"StackMultiplier={N(StackMultiplier)}\nMeleeDurability={N(MeleeDurability)}\nFirearmDurability={N(FirearmDurability)}\n" +
            "# LootMultiplier: 1-5. FoodShelfLifeMultiplier: 1, 2, 3 or 5.\n" +
            $"LootMultiplier={LootMultiplier}\nFoodShelfLifeMultiplier={FoodShelfLifeMultiplier}\n" +
            "# VisualReactionSteps: 0-4, in half-second steps. 0 is vanilla.\n" +
            $"VisualReactionSteps={VisualReactionSteps}\n" +
            "# Recovery modes: 1 = vanilla, 2 = 2x, 3 = 3x, 4 = maximum.\n" +
            $"ItemHealingMode={ItemHealingMode}\nSatietyMode={SatietyMode}\nMoraleRecoveryMode={MoraleRecoveryMode}\n" +
            "# NotificationVolume: 0-100 in steps of 5. ScavengingRangeMultiplier: 1-3 in steps of 0.25.\n" +
            $"NotificationVolume={NotificationVolume}\nScavengingRangeMultiplier={N(ScavengingRangeMultiplier)}\n" +
            "# ExperienceMultiplier: 1-10. SurvivorSlots: 1 = vanilla, 2-32 = minimum total slots.\n" +
            $"ExperienceMultiplier={N(ExperienceMultiplier)}\n" +
            $"SurvivorSlots={SurvivorSlots}\n" +
            "# PanelOpacity: 35-100. PanelTextSize: 80-120. PanelScale: 70-130.\n" +
            $"PanelOpacity={PanelOpacity}\nPanelTextSize={PanelTextSize}\nPanelScale={PanelScale}\n" +
            "# ShowSettingsButton and silent footsteps: true or false. Silent footsteps affect detection.\n" +
            $"ShowSettingsButton={ShowSettingsButton.ToString().ToLowerInvariant()}\n" +
            $"SilentWalk={SilentWalk.ToString().ToLowerInvariant()}\nSilentRun={SilentRun.ToString().ToLowerInvariant()}\n";
        File.WriteAllText(path + ".tmp", text);
        File.Move(path + ".tmp", path, true);
    }
}

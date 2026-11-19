using System.Globalization;

namespace DarkestDaysTweaks;

public sealed class Settings
{
    public const double MaxStacks = 1000, MaxEase = 1000000;
    public double StackMultiplier = 1, MeleeDurability = 1, FirearmDurability = 1;
    public const int MaxSlots = 32;
    public int SurvivorSlots = 1;
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
        SurvivorSlots = Math.Clamp(SurvivorSlots, 1, MaxSlots);
        StackMultiplier = Clamp(StackMultiplier, MaxStacks);
        MeleeDurability = Clamp(MeleeDurability, MaxEase);
        FirearmDurability = Clamp(FirearmDurability, MaxEase);
    }
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
                if (key == "SilentWalk") s.SilentWalk = flag;
                if (key == "SilentRun") s.SilentRun = flag;
            }
            if (!TryNumber(value, out var n)) continue;
            switch (key)
            {
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
        var text = "# Darkest Days Tweaks - F6 opens/closes the settings menu\n" +
            "# 1.0 = vanilla. Higher values increase capacity or weapon lifespan.\n" +
            "# Stacks: 1..1000 (increased stack cap: 1000000). Durability: 1..1000000.\n" +
            "# Silent footsteps: detection only; audible footsteps are unchanged. true = ON.\n" +
            $"StackMultiplier={N(StackMultiplier)}\nMeleeDurability={N(MeleeDurability)}\nFirearmDurability={N(FirearmDurability)}\n" +
            $"SurvivorSlots={SurvivorSlots}\n" +
            $"SilentWalk={SilentWalk.ToString().ToLowerInvariant()}\nSilentRun={SilentRun.ToString().ToLowerInvariant()}\n";
        File.WriteAllText(path + ".tmp", text);
        File.Move(path + ".tmp", path, true);
    }
}

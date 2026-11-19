using DarkestDaysTweaks;

static void Check(bool ok, string name)
{
    if (!ok) throw new Exception(name);
    Console.WriteLine("PASS " + name);
}

Check(Settings.StackLimit(10, 1) == 10, "native stack unchanged at 1");
Check(Settings.StackLimit(10, 2.5) == 25, "stack multiplier");
Check(Settings.StackLimit(10, 1.01) == 11, "fractional rounding favors capacity");
Check(Settings.StackLimit(-1, 1000) == -1, "native sentinel preserved");
Check(Settings.StackLimit(int.MaxValue, 1000) == int.MaxValue, "overflow and native upper bound");
Check(Settings.StackLimit(100000, 1000) == 1000000, "bounded increased stacks");
Check(Settings.TryNumber("2,5", out var n) && n == 2.5, "comma decimal input");
Check(!Settings.TryNumber("NaN", out _) && !Settings.TryNumber("Infinity", out _), "nonfinite input rejected");

var directory = Path.Combine(Path.GetTempPath(), "DarkestDaysTweaks.SettingsChecks." + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
var path = Path.Combine(directory, "settings.txt");
try
{
    File.WriteAllText(path, "StackMultiplier=0\nMeleeDurability=-10\nFirearmDurability=1000000000\nWalkQuietness=2,5\nRunQuietness=NaN\nSilentWalk=true\nSilentRun=false\n");
    var settings = Settings.Load(path);
    Check(settings.StackMultiplier == 1 && settings.MeleeDurability == 1 && settings.FirearmDurability == Settings.MaxEase,
        "file inputs clamped to supported range");
    Check(settings.SurvivorSlots == 1, "old settings preserve native inventory slots");
    settings.SurvivorSlots = int.MaxValue;
    settings.Save(path);
    var loaded = Settings.Load(path);
    Check(loaded.SurvivorSlots == 32, "slot ceiling persists across reload");
    Check(loaded.SilentWalk && !loaded.SilentRun, "independent ON/OFF persistence");
    Check(!File.ReadAllText(path).Contains("Quietness"), "legacy numeric stealth settings removed on save");
}
finally
{
    if (File.Exists(path)) File.Delete(path);
    Directory.Delete(directory);
}
Console.WriteLine("All checks passed.");

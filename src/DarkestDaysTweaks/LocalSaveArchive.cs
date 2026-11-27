namespace DarkestDaysTweaks;

internal sealed class LocalSaveArchive
{
    private readonly string root;

    internal LocalSaveArchive(string root) => this.root = root;

    internal string CampaignFolder(string campaign) => Path.Combine(root, campaign);

    internal IEnumerable<string> Campaigns() => Directory.Exists(root)
        ? Directory.EnumerateDirectories(root).OrderByDescending(Directory.GetLastWriteTimeUtc)
        : Enumerable.Empty<string>();

    internal void Capture(string campaign, string phase, byte[] data, int version, DateTime written, bool manual)
    {
        string folder = CampaignFolder(campaign);
        string initial = Path.Combine(folder, "first-save.sav");
        if (!File.Exists(initial))
        {
            PhaseBackupFiles.WriteNew(initial, data);
            File.SetLastWriteTimeUtc(initial, written);
        }

        PhaseBackupFiles.Capture(folder, phase, data, version, written);
        if (manual) PhaseBackupFiles.WriteAtomic(Path.Combine(folder, "manual-save.sav"), data);
    }
}

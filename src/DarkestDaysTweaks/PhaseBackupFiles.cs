namespace DarkestDaysTweaks;

internal static class PhaseBackupFiles
{
    private const int Retention = 5;

    internal static void Capture(string folder, string phase, byte[] data, int version, DateTime? written = null)
    {
        string path = Path.Combine(folder, phase + ".sav");
        if (!SameSave(path, data) || !IsCompatible(path, data, version)) WriteVersioned(path, data, version);
        if (written.HasValue) File.SetLastWriteTimeUtc(path, written.Value);
        foreach (var old in OrderedPhases(folder).Skip(Retention))
        {
            File.Delete(old);
            File.Delete(old + ".version");
        }
    }

    internal static IEnumerable<string> LatestPhases(string folder) => OrderedPhases(folder).Take(Retention);

    private static IEnumerable<string> OrderedPhases(string folder) =>
        Directory.EnumerateFiles(folder, "*.sav").Where(IsPhaseFile)
            .OrderByDescending(File.GetLastWriteTimeUtc).ThenByDescending(Path.GetFileName, StringComparer.Ordinal);

    internal static string? FindMatchingCampaign(string slotFolder, byte[] current)
    {
        if (!Directory.Exists(slotFolder)) return null;
        string? matching = null;
        foreach (string campaign in Directory.EnumerateDirectories(slotFolder))
        {
            if (!LatestPhases(campaign).Any(path => SameSave(path, current)) &&
                !SameSave(Path.Combine(campaign, "before-restore.sav"), current)) continue;
            if (matching != null) return null;
            matching = campaign;
        }
        return matching;
    }

    internal static bool IsCompatible(string path, byte[] current, int version)
    {
        string stamp = path + ".version";
        return File.Exists(stamp)
            ? File.ReadAllBytes(stamp).AsSpan().SequenceEqual(BitConverter.GetBytes(version))
            : SameSave(path, current);
    }

    private static bool SameSave(string path, byte[] current) =>
        File.Exists(path) && new FileInfo(path).Length == current.Length &&
        File.ReadAllBytes(path).AsSpan().SequenceEqual(current);

    internal static void WriteVersioned(string path, byte[] data, int version)
    {
        string stamp = path + ".version";
        if (File.Exists(stamp)) File.Delete(stamp);
        WriteAtomic(path, data);
        WriteAtomic(stamp, BitConverter.GetBytes(version));
    }

    internal static void Restore(string recoveryPath, byte[] replacement, byte[] current,
        Action<string, byte[]> writeRecovery, Action<byte[]> writeSave, Func<byte[]> readSave, Action startLoad)
    {
        writeRecovery(recoveryPath, current);
        if (!File.ReadAllBytes(recoveryPath).AsSpan().SequenceEqual(current))
            throw new IOException("The current save could not be verified.");

        try
        {
            writeSave(replacement);
            if (!readSave().AsSpan().SequenceEqual(replacement))
                throw new IOException("Steam returned different data after writing the backup.");
            startLoad();
        }
        catch (Exception restoreError)
        {
            try
            {
                writeSave(current);
                if (!readSave().AsSpan().SequenceEqual(current))
                    throw new IOException("Steam returned different data after rolling back.");
            }
            catch (Exception rollbackError)
            {
                throw new AggregateException("Restore and rollback failed. The Before restore copy is still available.",
                    restoreError, rollbackError);
            }
            throw;
        }
    }

    internal static bool IsPhaseFile(string path)
    {
        var parts = Path.GetFileNameWithoutExtension(path).Split('-');
        return parts.Length == 3 && parts.All(part => part.Length == 5 && ushort.TryParse(part, out _));
    }

    internal static void WriteAtomic(string path, byte[] data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(data);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporary, path, overwrite: true);
    }

    internal static void WriteNew(string path, byte[] data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(data);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}

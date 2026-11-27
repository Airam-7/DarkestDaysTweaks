using System.Globalization;
using HarmonyLib;
using Il2CppDRS.Core.SaveSlots;
using Il2CppDRS.Model;
using Il2CppDRS.Presentation.UI.Screens;
using Il2CppDRS.Utils.Saves;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppPikPok.Core.KVS.IO;
using Il2CppPikPok.PlatformServices.Standalone;
using Il2CppPikPok.TreeFlowManager;
using Il2CppSteamworks;
using Microsoft.Win32;

namespace DarkestDaysTweaks;

internal sealed class SaveBackups
{
    internal sealed record Entry(string Path, string Phase, DateTime Written)
    {
        internal bool BeforeRestore => Phase.Length == 0;
    }

    internal sealed record Slot(SaveSlotData? Native, ushort Id, int Version, string Label, Entry[] Phases, Entry? Recovery,
        Entry? Initial = null, Entry? Manual = null);
    private sealed record SaveIdentity(string Campaign, string Phase, int Version, int Days, int Survivors);

    private const int MaxSaveBytes = 64 * 1024 * 1024;
    private const uint GameModelChunkId = 1u << 16;
    private const uint GameModelMetaChunkId = 2u << 16;
    private static SaveBackups? instance;
    private readonly string root;
    private readonly LocalSaveArchive archive;
    private readonly MelonLoader.MelonLogger.Instance log;
    private readonly Dictionary<string, (DateTime Written, long Length)> knownSaves = new(StringComparer.OrdinalIgnoreCase);
    private bool importedHistory;
    private float nextCheck;
    private string? remoteDirectory;
    private MainMenuScreen? mainMenu;
    private IPlayerSaveSlotHandler? slotHandler;
    private sealed record PendingLoad(ushort Slot, bool Cloud, Entry? Backup, float Deadline, byte[]? LocalCopy);
    private PendingLoad? pendingLoad;
    internal string LoadStatus { get; private set; } = "";

    internal SaveBackups(string directory, MelonLoader.MelonLogger.Instance logger)
    {
        root = Path.Combine(directory, "DarkestDaysTweaks.Backups");
        archive = new(Path.Combine(root, "Local"));
        log = logger;
    }

    internal bool MainMenuAvailable => mainMenu != null && mainMenu.isActiveAndEnabled;
    internal bool CanList => MainMenuAvailable || slotHandler != null;
    internal bool CanLoad => pendingLoad == null && CanList;

    internal void Install(HarmonyLib.Harmony harmony)
    {
        var focus = Required(typeof(DrsBaseWindow), "OnGainFocus");
        var show = Required(typeof(MainMenuScreen), "OnShow");
        var loseFocus = Required(typeof(MainMenuScreen), "OnLostFocus");
        var hide = Required(typeof(MainMenuScreen), "OnHide");
        harmony.Patch(focus, postfix: Hook(nameof(OnWindowFocused)));
        harmony.Patch(show, postfix: Hook(nameof(OnMenuShown)));
        harmony.Patch(loseFocus, prefix: Hook(nameof(OnMenuHidden)));
        harmony.Patch(hide, prefix: Hook(nameof(OnMenuHidden)));
        instance = this;
    }

    private static System.Reflection.MethodInfo Required(Type type, string name) =>
        AccessTools.Method(type, name) ?? throw new MissingMethodException(type.FullName, name);

    private static HarmonyMethod Hook(string name) => new(Required(typeof(SaveBackups), name));

    private static void OnWindowFocused(DrsBaseWindow __instance)
    {
        if (instance != null) instance.mainMenu = __instance.TryCast<MainMenuScreen>();
    }

    private static void OnMenuShown(MainMenuScreen __instance)
    {
        if (instance is not { } backup) return;
        backup.mainMenu = __instance;
        backup.slotHandler = __instance.m_playerSaveSlotHandler ?? backup.slotHandler;
    }

    private static void OnMenuHidden()
    {
        if (instance is not { } backup) return;
        backup.slotHandler = backup.mainMenu?.m_playerSaveSlotHandler ?? backup.slotHandler;
        backup.mainMenu = null;
    }

    internal void Update()
    {
        float now = UnityEngine.Time.realtimeSinceStartup;
        if (pendingLoad != null) FinishPendingLoad(now);
        if (now < nextCheck) return;
        nextCheck = now + 2f;

        if (slotHandler == null && MainMenuAvailable) slotHandler = mainMenu!.m_playerSaveSlotHandler;
        if (!CanList) return;

        try { ScanCommittedSaves(); }
        catch (Exception error) { log.Warning($"Could not scan the Steam saves: {error.Message}"); }
    }

    private void ScanCommittedSaves()
    {
        if (!importedHistory) ImportHistory();
        ScanDirectory(LocalSaveDirectory());
        remoteDirectory ??= TrySteamSaveDirectory();
        if (remoteDirectory != null && Directory.Exists(remoteDirectory)) ScanDirectory(remoteDirectory, AccountFolder());
    }

    private void ScanDirectory(string directory, string? accountFolder = null)
    {
        if (!Directory.Exists(directory)) return;
        foreach (string path in Directory.EnumerateFiles(directory, "gamesave_*.sav"))
        {
            if (!TryGetSlotId(path, out ushort slot)) continue;
            try
            {
                var file = new FileInfo(path);
                var stamp = (file.LastWriteTimeUtc, file.Length);
                if (knownSaves.TryGetValue(path, out var previous) && stamp == previous) continue;
                if (file.Length <= 0 || file.Length > MaxSaveBytes) continue;

                byte[] data = ReadBackup(path);
                file.Refresh();
                if (stamp != (file.LastWriteTimeUtc, file.Length) || data.Length != stamp.Length) continue;
                Capture(slot, data, stamp.LastWriteTimeUtc, accountFolder);
                knownSaves[path] = stamp;
            }
            catch (Exception error)
            {
                log.Warning($"Could not archive save {slot}: {error.Message}");
                var file = new FileInfo(path);
                if (file.Exists && error is not IOException) knownSaves[path] = (file.LastWriteTimeUtc, file.Length);
            }
        }
    }

    private static bool TryGetSlotId(string fileName, out ushort id)
    {
        const string prefix = "gamesave_";
        string name = Path.GetFileNameWithoutExtension(fileName);
        id = 0;
        return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            ushort.TryParse(name.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out id);
    }

    private void Capture(ushort slot, byte[] data, DateTime written, string? accountFolder)
    {
        var identity = ReadIdentity(data);
        if (identity == null) return;
        archive.Capture(identity.Campaign, identity.Phase, data, identity.Version, written, manual: false);
        if (accountFolder != null)
        {
            string folder = Path.Combine(accountFolder, slot.ToString(CultureInfo.InvariantCulture), identity.Campaign);
            PhaseBackupFiles.Capture(folder, identity.Phase, data, identity.Version, written);
        }
        log.Msg($"Phase backup saved for slot {slot} (phase {identity.Phase}).");
    }

    private static SaveIdentity? ReadIdentity(byte[] data)
    {
        var stream = new KeyValueSaveMemoryStream("DarkestDaysTweaks backup", new Il2CppStructArray<byte>(data));
        try
        {
            var reader = stream.OpenRead();
            Il2CppStructArray<byte>? modelBytes = null;
            GameModelMeta? meta = null;
            while (reader.ReadChunk(out _, out uint id, out var payload))
            {
                if (id == GameModelChunkId) modelBytes = payload;
                else if (id == GameModelMetaChunkId)
                {
                    var metaChunk = new GameModelMetaChunk();
                    metaChunk.Load(payload);
                    meta = metaChunk.Data;
                }
            }

            if (meta == null || !GameSaveMeta.GetVersionCompatible(meta) || modelBytes == null) return null;
            var modelChunk = new GameModelChunk();
            modelChunk.Load(modelBytes);
            var model = modelChunk.Data;
            var cycle = model?.GameCycle;
            if (model == null || cycle == null) return null;

            string campaign = model.RandomSeed.ToString("X8", CultureInfo.InvariantCulture);
            string phase = $"{cycle.DaysPassed:D5}-{cycle.CurrentPhaseIndex:D5}-{cycle.CyclesPassed:D5}";
            return new(campaign, phase, meta.ModelVersionNumber, meta.DaysPassed, meta.NumberOfSurvivors);
        }
        finally { stream.Dispose(); }
    }

    private void ImportHistory()
    {
        importedHistory = true;
        if (!Directory.Exists(root)) return;
        // Migrate the existing phase copies in chronological order, without reading Steam Cloud.
        var accounts = Directory.EnumerateDirectories(root)
            .Where(path => ulong.TryParse(Path.GetFileName(path), out _));
        var sources = accounts.SelectMany(account => Directory.EnumerateFiles(account, "*.sav", SearchOption.AllDirectories))
            .Where(PhaseBackupFiles.IsPhaseFile).OrderBy(File.GetLastWriteTimeUtc);
        var marker = Path.Combine(root, "Local", ".phase-history-imported");
        if (File.Exists(marker)) return;
        foreach (string path in sources)
        {
            try
            {
                byte[] data = ReadBackup(path);
                var info = ReadIdentity(data);
                if (info != null) archive.Capture(info.Campaign, info.Phase, data, info.Version,
                    File.GetLastWriteTimeUtc(path), manual: false);
            }
            catch (Exception error) { log.Warning($"Skipped an unreadable legacy backup: {error.Message}"); }
        }
        PhaseBackupFiles.WriteAtomic(marker, new byte[] { 1 });
    }

    internal Slot[] ListArchive()
    {
        if (!importedHistory) ImportHistory();
        var campaigns = new List<Slot>();
        foreach (string folder in archive.Campaigns())
        {
            try
            {
                var phases = PhaseBackupFiles.LatestPhases(folder)
                    .Select(path => new Entry(path, Path.GetFileNameWithoutExtension(path), File.GetLastWriteTimeUtc(path)))
                    .ToArray();
                var first = NamedEntry(folder, "first-save.sav");
                var manual = NamedEntry(folder, "manual-save.sav");
                var latest = phases.FirstOrDefault() ?? first ?? manual;
                if (latest == null) continue;
                var info = ReadIdentity(ReadBackup(latest.Path));
                if (info == null) continue;
                string label = $"Day {info.Days} | {info.Survivors} survivors | {latest.Written.ToLocalTime():g}";
                campaigns.Add(new(null, 0, info.Version, label, phases, null, first, manual));
            }
            catch (Exception error) { log.Warning($"Could not list a local campaign: {error.Message}"); }
        }
        return campaigns.ToArray();
    }

    private static Entry? NamedEntry(string folder, string name)
    {
        string path = Path.Combine(folder, name);
        if (!File.Exists(path)) return null;
        var identity = ReadIdentity(ReadBackup(path));
        return identity == null ? null : new Entry(path, identity.Phase, File.GetLastWriteTimeUtc(path));
    }

    internal void BackUpCurrent(Slot slot)
    {
        if (slot.Native == null) throw new InvalidOperationException("Choose a game save first.");
        string? directory = slot.Native.IsCloudSave ? SteamSaveDirectory() : LocalSaveDirectory();
        if (directory == null) throw new IOException("The locally stored game save is unavailable.");
        string path = Path.Combine(directory, SaveUtils.GetGameSaveNameOnlyWithExtension(slot.Id));
        var file = new FileInfo(path);
        var stamp = (file.LastWriteTimeUtc, file.Length);
        byte[] data = ReadBackup(path);
        file.Refresh();
        if (stamp != (file.LastWriteTimeUtc, file.Length) || data.Length != stamp.Length)
            throw new IOException("The game is still saving. Try again when it finishes.");
        var identity = ReadIdentity(data) ?? throw new IOException("The game save is incompatible.");
        archive.Capture(identity.Campaign, identity.Phase, data, identity.Version, stamp.LastWriteTimeUtc, manual: true);
    }

    internal Slot[] List()
    {
        var handler = mainMenu?.m_playerSaveSlotHandler ?? slotHandler;
        slotHandler = handler;
        var nativeSlots = handler?.ValidPlayerSaveSlots;
        if (nativeSlots == null) return Array.Empty<Slot>();

        int count = nativeSlots.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<SaveSlotData>>().Count;
        var metadata = handler!.TryCast<IPlayerSaveSlotMetaDataProvider>();
        var slots = new List<Slot>();
        for (int i = 0; i < count && slots.Count < 5; i++)
        {
            var native = nativeSlots[i];
            if (!TryGetSlotId(native.FileName, out ushort id)) continue;

            if (metadata == null || !metadata.TryGetSaveSlotMetaData(native, out var info) ||
                info == null || !GameSaveMeta.GetVersionCompatible(info)) continue;

            var date = native.LastModifiedDate;
            string label = $"Day {info.DaysPassed} | {info.NumberOfSurvivors} survivors | {date.Day:D2}/{date.Month:D2}/{date.Year:D4} {date.Hour:D2}:{date.Minute:D2}";

            Entry[] phases = Array.Empty<Entry>();
            Entry? recovery = null;
            if (native.IsCloudSave && FindSave(id) is { } file)
            {
                byte[] current = file.Read();
                string? campaign = FindCampaign(id, current);
                if (campaign != null)
                {
                    phases = PhaseBackupFiles.LatestPhases(campaign)
                        .Where(path => PhaseBackupFiles.IsCompatible(path, current, info.ModelVersionNumber))
                        .Select(path => new Entry(path, Path.GetFileNameWithoutExtension(path), File.GetLastWriteTimeUtc(path)))
                        .ToArray();
                    string recoveryPath = Path.Combine(campaign, "before-restore.sav");
                    if (File.Exists(recoveryPath) && PhaseBackupFiles.IsCompatible(recoveryPath, current, info.ModelVersionNumber))
                        recovery = new(recoveryPath, "", File.GetLastWriteTimeUtc(recoveryPath));
                }
            }
            slots.Add(new(native, id, info.ModelVersionNumber, label, phases, recovery));
        }
        log.Msg($"Saved games opened with {slots.Count} compatible saves and {slots.Sum(slot => slot.Phases.Length)} phase backups.");
        return slots.ToArray();
    }

    internal void LoadCurrent(Slot slot)
    {
        if (MainMenuAvailable) RequireMenu().TryLoadSaveSlot(slot.Native!);
        else QueueFromGameplay(slot, null);
    }

    internal void RestoreAndLoad(Slot slot, Entry entry)
    {
        if (slot.Native == null)
        {
            byte[] copy = ReadBackup(entry.Path);
            var identity = ReadIdentity(copy) ?? throw new IOException("The local backup is incompatible.");
            if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(entry.Path)), Path.GetFullPath(archive.CampaignFolder(identity.Campaign)),
                    StringComparison.OrdinalIgnoreCase))
                throw new IOException("This backup does not match its archived campaign.");
            if (MainMenuAvailable) LoadLocalCopy(copy);
            else QueueFromGameplay(slot, null, copy);
            return;
        }
        if (!MainMenuAvailable)
        {
            QueueFromGameplay(slot, entry);
            return;
        }
        var menu = RequireMenu();
        var file = FindSave(slot.Id) ?? throw new IOException("The current save is unavailable.");
        byte[] current = file.Read();
        string? campaign = FindCampaign(slot.Id, current);
        if (campaign == null || Path.GetDirectoryName(entry.Path) != campaign)
            throw new InvalidOperationException("The save has changed. Reopen Saved games to refresh its backups.");
        if (!PhaseBackupFiles.IsCompatible(entry.Path, current, slot.Version))
            throw new InvalidOperationException("This backup is from a different save version.");

        var cancelToken = Il2CppSystem.Threading.CancellationToken.None;
        byte[] replacement = ReadBackup(entry.Path);
        string recovery = Path.Combine(campaign, "before-restore.sav");
        PhaseBackupFiles.Restore(recovery, replacement, current,
            (path, bytes) => PhaseBackupFiles.WriteVersioned(path, bytes, slot.Version),
            file.Write, file.Read,
            () => menu.LoadExistingSaveSlot(slot.Id, slot.Native.IsCloudSave, cancelToken));
    }

    private void QueueFromGameplay(Slot slot, Entry? entry, byte[]? localCopy = null)
    {
        if (pendingLoad != null) throw new InvalidOperationException("A save is already loading.");
        var flow = FlowManager.Exists ? FlowManager.Instance : null;
        if (flow == null || flow.IsTransitioning)
            throw new InvalidOperationException("Wait for the current game transition to finish.");

        var state = flow.ActiveState;
        while (state != null && state.name != "GameLoop") state = state.Parent;
        if (state == null) throw new InvalidOperationException("Open Saved games from the main menu or an active run.");
        if (entry != null && !File.Exists(entry.Path))
            throw new IOException("This backup is no longer available.");

        pendingLoad = new(slot.Id, slot.Native?.IsCloudSave == true, entry, UnityEngine.Time.realtimeSinceStartup + 60f, localCopy);
        LoadStatus = "";
        try
        {
            flow.Restart();
            log.Msg(localCopy == null ? $"Queued load for slot {slot.Id} after the native flow restart."
                : "Queued a local backup after the native flow restart.");
        }
        catch
        {
            pendingLoad = null;
            throw;
        }
    }


    private void FinishPendingLoad(float now)
    {
        if (pendingLoad is not { } request) return;
        if (now >= request.Deadline)
        {
            pendingLoad = null;
            LoadStatus = "The game did not finish the load transition in time. Select the save again.";
            log.Warning(LoadStatus);
            return;
        }
        if (!MainMenuAvailable || !FlowManager.Exists || FlowManager.Instance.IsTransitioning) return;

        try
        {
            if (request.LocalCopy is { } copy)
            {
                pendingLoad = null;
                LoadLocalCopy(copy);
                return;
            }
            var handler = mainMenu!.m_playerSaveSlotHandler;
            if (handler == null) return;
            var slots = handler.ValidPlayerSaveSlots;
            if (slots == null) return;
            int count = slots.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<SaveSlotData>>().Count;
            for (int i = 0; i < count; i++)
            {
                var native = slots[i];
                if (native.IsCloudSave != request.Cloud || !TryGetSlotId(native.FileName, out ushort id) || id != request.Slot)
                    continue;
                pendingLoad = null;
                if (request.Backup is { } entry)
                {
                    var metadata = handler.TryCast<IPlayerSaveSlotMetaDataProvider>();
                    if (metadata == null || !metadata.TryGetSaveSlotMetaData(native, out var info) ||
                        info == null || !GameSaveMeta.GetVersionCompatible(info))
                        throw new InvalidOperationException("The target save is no longer compatible.");
                    ScanCommittedSaves();
                    RestoreAndLoad(new(native, id, info.ModelVersionNumber, "", Array.Empty<Entry>(), null), entry);
                }
                else RequireMenu().TryLoadSaveSlot(native);
                log.Msg($"Started loading slot {id} after leaving the active run.");
                return;
            }
        }
        catch (Exception error)
        {
            pendingLoad = null;
            LoadStatus = $"Could not load the selected save. {error.Message}";
            log.Error(error);
        }
    }

    private void LoadLocalCopy(byte[] copy)
    {
        var menu = RequireMenu();
        string local = LocalSaveDirectory();
        var occupied = new HashSet<ushort>();
        var native = menu.m_playerSaveSlotHandler?.ValidPlayerSaveSlots;
        if (native != null)
        {
            int count = native.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<SaveSlotData>>().Count;
            for (int i = 0; i < count; i++)
                if (TryGetSlotId(native[i].FileName, out ushort id)) occupied.Add(id);
        }
        foreach (string? directory in new[] { local, TrySteamSaveDirectory() })
        {
            if (directory == null || !Directory.Exists(directory)) continue;
            foreach (string path in Directory.EnumerateFiles(directory, "gamesave_*.sav*"))
            {
                string name = Path.GetFileName(path);
                if (name.EndsWith(".sav.bak", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
                if (TryGetSlotId(name, out ushort id)) occupied.Add(id);
            }
        }
        int maximum = PlayerSaveSlotHandler.MAX_SAVE_SLOTS;
        for (int number = 0; number < maximum; number++)
        {
            ushort id = (ushort)number;
            if (occupied.Contains(id)) continue;
            string path = Path.Combine(local, SaveUtils.GetGameSaveNameOnlyWithExtension(id));
            try { PhaseBackupFiles.WriteNew(path, copy); }
            catch (IOException) when (File.Exists(path)) { continue; }
            var file = new FileInfo(path);
            knownSaves[path] = (file.LastWriteTimeUtc, file.Length);
            log.Msg($"Local backup recovered to new local slot {id}.");
            menu.LoadExistingSaveSlot(id, false, Il2CppSystem.Threading.CancellationToken.None);
            return;
        }
        throw new IOException("There is no free local save slot. The archived copy is still available.");
    }

    private static string LocalSaveDirectory()
    {
        string path = StandaloneUserStorageService.s_localSavePath;
        return string.IsNullOrWhiteSpace(path) ? UnityEngine.Application.persistentDataPath : path;
    }

    private static string? TrySteamSaveDirectory()
    {
        try { return SteamSaveDirectory(); }
        catch (InvalidOperationException) { return null; }
    }

    private string? FindCampaign(ushort slot, byte[] current) => PhaseBackupFiles.FindMatchingCampaign(
        Path.Combine(AccountFolder(), slot.ToString(CultureInfo.InvariantCulture)), current);

    private MainMenuScreen RequireMenu()
    {
        var menu = mainMenu;
        if (menu == null || !MainMenuAvailable)
            throw new InvalidOperationException("Return to the main menu and wait for saving to finish.");
        return menu;
    }

    private string AccountFolder()
    {
        ulong account = SteamUser.GetSteamID().m_SteamID;
        if (account == 0) throw new InvalidOperationException("Steam account is unavailable.");
        return Path.Combine(root, account.ToString(CultureInfo.InvariantCulture));
    }

    private sealed record SaveAccess(Func<byte[]> Read, Action<byte[]> Write);

    private static SaveAccess? FindSave(ushort slot)
    {
        string? directory = SteamSaveDirectory();
        string? path = directory == null ? null : Path.Combine(directory, $"gamesave_{slot}.sav");
        string expected = SaveUtils.GetGameSaveNameOnlyWithExtension(slot);
        string? cloud = FindCloudSave(expected);
        if (path != null && File.Exists(path))
            return new(() => ReadBackup(path), cloud == null
                ? data => PhaseBackupFiles.WriteAtomic(path, data)
                : data => WriteCloud(cloud, data));
        return cloud == null ? null : new(() => ReadCloudFile(cloud), data => WriteCloud(cloud, data));
    }

    private static string? SteamSaveDirectory()
    {
        if (!OperatingSystem.IsWindows()) return null;
        uint account = unchecked((uint)SteamUser.GetSteamID().m_SteamID);
        if (account == 0) return null;
        using var steamKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        string steamRoot = steamKey?.GetValue("SteamPath") as string
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        return Path.Combine(steamRoot, "userdata", account.ToString(CultureInfo.InvariantCulture), "2239710", "remote");
    }

    private static string? FindCloudSave(string expected)
    {
        if (SteamRemoteStorage.FileExists(expected) && SteamRemoteStorage.GetFileSize(expected) > 0) return expected;
        int count = SteamRemoteStorage.GetFileCount();
        for (int index = 0; index < count; index++)
        {
            string name = SteamRemoteStorage.GetFileNameAndSize(index, out _);
            if (string.Equals(name, expected, StringComparison.OrdinalIgnoreCase) && SteamRemoteStorage.GetFileSize(name) > 0)
                return name;
        }
        return null;
    }

    private static byte[] ReadCloudFile(string name)
    {
        int size = SteamRemoteStorage.GetFileSize(name);
        if (size <= 0 || size > MaxSaveBytes)
            throw new IOException($"Steam reported an invalid size ({size}) for {name}.");
        var data = new Il2CppStructArray<byte>(size);
        if (SteamRemoteStorage.FileRead(name, data, size) != size)
            throw new IOException("Steam returned an incomplete save.");
        return data.ToArray();
    }

    private static void WriteCloud(string name, byte[] data)
    {
        if (!SteamRemoteStorage.FileWrite(name, new Il2CppStructArray<byte>(data), data.Length))
            throw new IOException("Steam rejected the save write. Check the recovery copy before continuing.");
    }

    private static byte[] ReadBackup(string path)
    {
        long size = new FileInfo(path).Length;
        if (size <= 0 || size > MaxSaveBytes) throw new IOException("The backup has an invalid size.");
        return File.ReadAllBytes(path);
    }

}

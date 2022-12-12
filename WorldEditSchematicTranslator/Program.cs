#region Using
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text;
#pragma warning disable IDE0004 // unneded cast, but i prefer explicit BinaryWriter usage
#endregion
namespace WorldEditSchematicTranslator;

internal partial class Program
{
    static Program()
    {
        InitializeVersionHistory();
        InitializeHeaderReaders();
        InitializeTileFrameImportant();
        InitializeTilesReaders();
        InitializeEntitiesReaders();
        InitializeLockFileNames();
    }

    public static async Task<int> Main(string[] Args)
    {
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
        ProgramFlags flags = new(Args);
        string dir = flags.FromDirectory ?? Environment.CurrentDirectory;
        string tempDir = GetTempDirectory(dir, flags.TempDirectory);

        if (flags.OutputDirectory is not null)
        {
            try { Path.GetFullPath(flags.OutputDirectory); }
            catch
            {
                Console.Error.WriteLine("[WorldEdit] Invalid output directory.");
                Environment.Exit(EXIT_CODE_INVALID_OUT_DIR);
            }
        }
        if (flags.Continuous && ((flags.OutputDirectory is null)
                                || Path.GetFullPath(dir) == Path.GetFullPath(flags.OutputDirectory)))
        {
            Console.Error.WriteLine("[WorldEdit] When setting the -continuous flag you must set -out to " +
                "a directory other than the current directory.");
            Environment.Exit(EXIT_CODE_CONTINUOUS_WITHOUT_OUT_DIR);
        }

        if (!GetFromVersion(flags.FromVersion, out Version? fromVersion))
        {
            if (flags.DirectFile is string _directFile)
            {
                using Stream inRawStream = File.OpenRead(_directFile);
                using BinaryReader versionReader = new(inRawStream, Encoding.UTF8);
                fromVersion = new(versionReader.ReadInt32(), versionReader.ReadInt32());
                CheckVersionIsKnown(fromVersion.ToString(), fromVersion);
            }
            else
            {
                Version[] history = History.Keys.OrderBy(v => v).ToArray();
                foreach (Version version in history)
                    if (!File.Exists(LockFilePaths[version]))
                    {
                        fromVersion = version;
                        break;
                    }
                if ((fromVersion ??= LastVersion) == LastVersion)
                    CheckLastVersionLock(flags.DoLog, flags.Exit);
            }
        }

        int failCounter;
        do
        {
            TranslateLoop(flags, fromVersion, dir, tempDir, out failCounter);
            if (flags.Continuous)
            {
                await Task.Delay(2000);
            }
        } while (flags.Continuous);

        return Exit(flags.Exit, flags.UseFailExitCode, failCounter);
    }

    #region TranslateLoop

    private static void TranslateLoop(ProgramFlags Flags, Version FromVersion,
        string DirectoryPath, string TempDirectoryPath, out int FailCounter)
    {
        List<FileInfo> files = new();
        if (Flags.DirectFile is not string directFile)
        {
            files.AddRange(
                GetDirectoryFiles(DirectoryPath, SCHEMATIC_PREFIX, Flags.OutputDirectory, Flags.SearchOption));
            if (!TryDeleteOldFiles(DirectoryPath, Flags.DeleteOldFiles))
            {
                files.AddRange(
                    GetDirectoryFiles(DirectoryPath, UNDO_PREFIX, Flags.OutputDirectory, Flags.SearchOption));
                files.AddRange(
                    GetDirectoryFiles(DirectoryPath, REDO_PREFIX, Flags.OutputDirectory, Flags.SearchOption));
                files.AddRange(
                    GetDirectoryFiles(DirectoryPath, CLIPBOARD_PREFIX, Flags.OutputDirectory, Flags.SearchOption));
            }
        }
        else
            files.Add(new(directFile));

        int totalCounter = files.Count, currCounter = 0, okCounter = 0, failCounter = 0;
        ulong totalSize = 0, currentSize = 0, lastSizeLog = 0;

        foreach (FileInfo file in files)
            totalSize += (ulong)file.Length;
        ulong logSize = ((totalSize < 100) ? 1 : (totalSize / 100));

        if (Flags.DirectFile is null)
            Console.Out.WriteLine($"[WorldEdit] Updating {files.Count} files from version {FromVersion} " +
                                  $"to {LastVersion}. Do not close this window!");
        Parallel.ForEach(files, (f => Translate(f, Flags.DoLog, totalCounter, ref currCounter,
                                                ref okCounter, ref failCounter, logSize, ref lastSizeLog,
                                                ref currentSize, TempDirectoryPath, FromVersion,
                                                DirectoryPath, Flags.OutputDirectory)));
        if (Flags.DoLog && (Flags.DirectFile is null))
            Console.Out.WriteLine($"[WorldEdit] Updated {okCounter} files from version {FromVersion} " +
                                  $"to {LastVersion}." + ((failCounter > 0)
                                                            ? $" Failed to update {failCounter} files."
                                                            : string.Empty));

        if (Flags.DirectFile is null)
            foreach (Version version in History.Keys)
                CreateLock((Flags.OutputDirectory ?? DirectoryPath), version);

        FailCounter = failCounter;
    }

    #endregion
    #region GetTempDirectory

    private static string GetTempDirectory(string CurrentDirectory, string? TempDirectory)
    {
        TempDirectory ??= CurrentDirectory;
        if (!Directory.Exists(TempDirectory))
            try { Directory.CreateDirectory(TempDirectory); }
            catch { TempDirectory = CurrentDirectory; }
        return TempDirectory;
    }

    #endregion
    #region CheckVersionIsKnown

    private static void CheckVersionIsKnown(string? FromVersionS, Version FromVersion)
    {
        if (History.ContainsKey(FromVersion))
            return;

        Console.Error.WriteLine($"[WorldEdit] Invalid version '{FromVersionS}'. " +
                                $"Existing versions: {string.Join(", ", History)}.");
        Environment.Exit(EXIT_CODE_INVALID_VERSION_HISTORY);
    }

    #endregion
    #region GetFromVersion

    private static bool GetFromVersion(string? FromVersionS, [MaybeNullWhen(false)]out Version FromVersion)
    {
        FromVersion = null;
        if (FromVersionS is null)
            return false;

        if (!Version.TryParse(FromVersionS, out FromVersion))
        {
            Console.Error.WriteLine($"[WorldEdit] Invalid version '{FromVersionS}'.");
            Environment.Exit(EXIT_CODE_INVALID_VERSION_ARGUMENT);
        }
        else
            CheckVersionIsKnown(FromVersionS, FromVersion);
        return true;
    }

    #endregion
    #region GetDirectoryFiles

    private static IEnumerable<FileInfo> GetDirectoryFiles(string DirectoryPath,
        string FilePrefix, string? OutDir, SearchOption SearchOption)
    {
        var files = Directory.EnumerateFiles(DirectoryPath, $"{FilePrefix}*.dat", SearchOption);
        if (OutDir is not null)
        {
            string dirPath = Path.GetFullPath(DirectoryPath);
            string outDir = Path.GetFullPath(OutDir);
            if (dirPath != outDir)
            {
                // Skip schematics that have already been translated unless the original version is newer
                files = files.Where(file =>
                {
                    string newFile = Path.GetFullPath(file).Replace(dirPath, outDir);
                    return (!File.Exists(newFile)
                         || (File.GetLastWriteTimeUtc(file) > File.GetLastWriteTimeUtc(newFile)));
                });
            }
        }
        return files.Select(f => new FileInfo(f));
    }

    #endregion
    #region TryDeleteOldFiles

    private static bool TryDeleteOldFiles(string DirectoryPath, bool DeleteOldFiles)
    {
        if (!DeleteOldFiles)
            return false;

        foreach (string file in Directory.EnumerateFiles(DirectoryPath, "undo-*.dat"))
            File.Delete(file);
        foreach (string file in Directory.EnumerateFiles(DirectoryPath, "redo-*.dat"))
            File.Delete(file);
        foreach (string file in Directory.EnumerateFiles(DirectoryPath, "clipboard-*.dat"))
            File.Delete(file);
        return true;
    }

    #endregion
    #region CheckLastVersionLock

    private static void CheckLastVersionLock(bool DoLog, bool Exit)
    {
        if ((LockFilePaths[LastVersion] is not string lockPath)
            || (LockFileContent[LastVersion] is not string content))
        {
            TranslationNotNeeded(DoLog, Exit);
            return;
        }

        using FileStream fs = File.OpenRead(lockPath);
        using BinaryReader br = new(fs);
        bool versioned;
        try { versioned = (br.ReadString() == content); }
        catch { versioned = false; }
        if (!versioned)
        {
            Console.Error.WriteLine("[WorldEdit] Unfortunately, you updated plugin too soon. " +
                                   $"Please revert back to pre-{History[LastVersion]} " +
                                   $"and delete {LockFileNames[LastVersion]} file.");
            Environment.Exit(EXIT_CODE_INVALID_LOCK);
        }
        else
            TranslationNotNeeded(DoLog, Exit);
    }
    private static void TranslationNotNeeded(bool DoLog, bool Exit)
    {
        if (DoLog)
            Console.Out.WriteLine($"[WorldEdit] Translation to v{LastVersion} is not needed.");
        Program.Exit(Exit, false, 0);
    }

    #endregion
    #region CreateLock

    private static void CreateLock(string DirectoryPath, Version Version)
    {
        string? lockPath = LockFilePaths[Version];
        if (lockPath is null)
            return;
        lockPath = Path.Combine(DirectoryPath, lockPath);
        if (File.Exists(lockPath))
            return;
        if (LockFileContent[Version] is not string content)
        {
            File.Create(lockPath).Close();
            return;
        }

        using FileStream fs = File.Create(lockPath);
        using BinaryWriter bw = new(fs);
        bw.Write((string)content);
    }

    #endregion
    #region Exit

    private static int Exit(bool Exit, bool UseFailExitCode, int FailCounter)
    {
        if (!Exit)
        {
            Console.Out.WriteLine("Press any key to exit...");
            Console.In.ReadLine();
        }
        int exCode = ((!UseFailExitCode || (FailCounter == 0)) ? EXIT_CODE_OK
                                                               : EXIT_CODE_ANY_TRANSLATION_FAILED);
        Environment.Exit(exCode);
        return exCode;
    }

    #endregion
    #region Translate

    private static void Translate(FileInfo FileInfo, bool DoLog, int TotalFilesCount,
        ref int CurrentCounter, ref int OKCounter, ref int FailCounter, ulong LogSize,
        ref ulong LastSizeLog, ref ulong CurrentFileSize, string TempDirectory, Version FromVersion,
        string FromDirectory, string? OutDirectory)
    {
        string tempPath;
        do tempPath = Path.Combine(TempDirectory, $"temp-{Random.Shared.NextInt64()}.dat");
        while (File.Exists(tempPath));

        bool translated = true;
        try
        {
            using Stream inRawStream = File.OpenRead(FileInfo.FullName);
            using Stream outRawStream = File.Open(tempPath, FileMode.Create);
            GZipStream inZipStream;
            BufferedStream inBufferedStream;

            Header header = null!;
            if (HeaderZipped[FromVersion])
            {
                inZipStream = new(inRawStream, CompressionMode.Decompress);
                inBufferedStream = new(inZipStream, BUFFER_SIZE);
                using BinaryReader headerReader = new(inBufferedStream, Encoding.UTF8, leaveOpen: true);
                header = ReadHeader[FromVersion](FromVersion, headerReader);
            }
            else
            {
                using BinaryReader headerReader = new(inRawStream, Encoding.UTF8, leaveOpen: true);
                header = ReadHeader[FromVersion](FromVersion, headerReader);
                inZipStream = new(inRawStream, CompressionMode.Decompress);
                inBufferedStream = new(inZipStream, BUFFER_SIZE);
            }
            using (BinaryWriter headerWriter = new(outRawStream, Encoding.UTF8, leaveOpen: true))
                header.Write(headerWriter);

            using (inZipStream)
            using (inBufferedStream)
            using (BinaryReader dataReader = new(inBufferedStream, Encoding.UTF8))
            using (GZipStream outZipStream = new(outRawStream, CompressionMode.Compress))
            using (BufferedStream outBufferedStream = new(outZipStream, BUFFER_SIZE))
            using (BinaryWriter dataWriter = new(outBufferedStream, Encoding.UTF8))
            {
                for (int i = 0; i < header.Width; i++)
                    for (int j = 0; j < header.Height; j++)
                    {
                        Tile tile = ReadTile[FromVersion](dataReader);

                        dataWriter.Write((ushort)tile.sTileHeader);
                        dataWriter.Write((byte)tile.bTileHeader);
                        dataWriter.Write((byte)tile.bTileHeader2);
                        dataWriter.Write((byte)tile.bTileHeader3);

                        if (tile.active())
                        {
                            dataWriter.Write((ushort)tile.type);
                            if (TileFrameImportant[LastVersion][tile.type])
                            {
                                dataWriter.Write((short)tile.frameX);
                                dataWriter.Write((short)tile.frameY);
                            }
                        }
                        dataWriter.Write((ushort)tile.wall);
                        dataWriter.Write((byte)tile.liquid);
                    }

                foreach (EntityReaderState state in ReadEntities[FromVersion](dataReader))
                    state.Write(dataWriter);
            }
        }
        catch (Exception e)
        {
            if (DoLog)
                Console.Error.WriteLine($"[WorldEdit] File '{FileInfo.FullName}' " +
                    $"could not be converted to Terraria v1.4.4:\n{e}");
            translated = false;
        }

        string newFilePath = FileInfo.FullName;
        if (translated && (OutDirectory is not null))
        {
            newFilePath = Path.GetFullPath(newFilePath)
                              .Replace(Path.GetFullPath(FromDirectory), Path.GetFullPath(OutDirectory));
            try { Directory.CreateDirectory(Path.GetDirectoryName(newFilePath)!); }
            catch (Exception ex)
            {
                if (DoLog)
                    Console.Error.WriteLine($"[WorldEdit] Directory '{newFilePath}' " +
                        $"could not be created:\n{ex}");
                translated = false;
            }
        }

        if (translated)
        {
            OKCounter++;
            File.Move(tempPath, newFilePath, true);
        }
        else
        {
            FailCounter++;
            File.Delete(tempPath);
        }
        if (DoLog)
        {
            Interlocked.Increment(ref CurrentCounter);
            if ((CurrentFileSize - LastSizeLog) >= LogSize)
            {
                Console.Out.WriteLine("[WorldEdit] Schematic translation progress: " +
                    $"{CurrentCounter}/{TotalFilesCount} (OK: {OKCounter}; Failed: {FailCounter})...");
                LastSizeLog = CurrentFileSize;
            }
        }
        Interlocked.Add(ref CurrentFileSize, (ulong)FileInfo.Length);
    }

    #endregion
}
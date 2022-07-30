using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace AssetStudio
{
    public static class CABManager
    {
        public static Dictionary<string, HashSet<long>> offsets = new Dictionary<string, HashSet<long>>();
        public static Dictionary<string, WMVEntry> WMVMap = new Dictionary<string, WMVEntry>();

        public static void BuildWMVMap(List<string> files)
        {
            Logger.Info(string.Format("Building WMVMap"));
            try
            {
                WMVMap.Clear();
                var unityDefaultResources = files.Last();
                if (unityDefaultResources.Contains("unity default resources"))
                {
                    WMVMap.Add("unity default resources", new WMVEntry(unityDefaultResources, 0, new List<string>()));
                    files.Remove(unityDefaultResources);
                }
                var unityBuiltinExtra = files.Last();
                if (unityBuiltinExtra.Contains("unity_builtin_extra"))
                {
                    WMVMap.Add("unity_builtin_extra", new WMVEntry(unityBuiltinExtra, 0, new List<string>()));
                    files.Remove(unityBuiltinExtra);
                }

                Progress.Reset();
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    using (var reader = new FileReader(file))
                    {
                        var wmvFile = new WMVFile(reader);
                        foreach (var bundle in wmvFile.Bundles)
                        {
                            foreach(var cab in bundle.Value.fileList)
                            {
                                using (var cabReader = new FileReader(cab.stream))
                                {
                                    if (cabReader.FileType == FileType.AssetsFile)
                                    {
                                        var assetsFile = new SerializedFile(cabReader, null);
                                        var dependancies = assetsFile.m_Externals.Select(x => x.fileName).ToList();
                                        WMVMap.Add(cab.path, new WMVEntry(file, bundle.Key, dependancies));
                                    }
                                }
                            }
                        }
                    }

                    Logger.Info($"[{i + 1}/{files.Count}] Processed {Path.GetFileName(file)}");
                    Progress.Report(i + 1, files.Count);
                }

                WMVMap = WMVMap.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
                var outputFile = new FileInfo(@"WMVMap.bin");

                using (var binaryFile = outputFile.Create())
                using (var writer = new BinaryWriter(binaryFile))
                {
                    writer.Write(WMVMap.Count);
                    foreach (var cab in WMVMap)
                    {
                        writer.Write(cab.Key);
                        writer.Write(cab.Value.Path);
                        writer.Write(cab.Value.Offset);
                        writer.Write(cab.Value.Dependancies.Count);
                        foreach(var dependancy in cab.Value.Dependancies)
                        {
                            writer.Write(dependancy);
                        }
                    }
                }
                Logger.Info($"WMVMap build successfully !!");
            }
            catch (Exception e)
            {
                Logger.Warning($"WMVMap was not build, {e.Message}");
            }
        }

        public static void LoadWMVMap()
        {
            Logger.Info(string.Format("Loading WMVMap"));
            try
            {
                WMVMap.Clear();
                using (var binaryFile = File.OpenRead("WMVMap.bin"))
                using (var reader = new BinaryReader(binaryFile))
                {
                    var count = reader.ReadInt32();
                    WMVMap = new Dictionary<string, WMVEntry>(count);
                    for (int i = 0; i < count; i++)
                    {
                        var cab = reader.ReadString();
                        var path = reader.ReadString();
                        var offset = reader.ReadInt64();
                        var depCount = reader.ReadInt32();
                        var dependancies = new List<string>();
                        for(int j = 0; j < depCount; j++)
                        {
                            var dependancy = reader.ReadString();
                            dependancies.Add(dependancy);
                        }
                        WMVMap.Add(cab, new WMVEntry(path, offset, dependancies));
                    }
                }
                Logger.Info(string.Format("Loaded WMVMap !!"));
            }
            catch (Exception e)
            {
                Logger.Warning($"WMVMap was not loaded, {e.Message}");
            }
        }
        public static void AddCabOffset(string cab)
        {
            if (WMVMap.TryGetValue(cab, out var wmvEntry))
            {
                if (!offsets.ContainsKey(wmvEntry.Path))
                {
                    offsets.Add(wmvEntry.Path, new HashSet<long>());
                }
                offsets[wmvEntry.Path].Add(wmvEntry.Offset);
                foreach (var dep in wmvEntry.Dependancies)
                {
                    AddCabOffset(dep);
                }
            }
        }

        public static bool FindCABFromWMV(string path, out List<string> cabs)
        {
            cabs = new List<string>();
            foreach (var pair in WMVMap)
            {
                if (pair.Value.Path.Contains(path))
                {
                    cabs.Add(pair.Key);
                }
            }
            return cabs.Count != 0;
        }

        public static void ProcessWMVFiles(ref string[] files)
        {
            var newFiles = files.ToList();
            foreach (var file in files)
            {
                if (!offsets.ContainsKey(file))
                {
                    offsets.Add(file, new HashSet<long>());
                }
                if (FindCABFromWMV(file, out var cabs))
                {
                    foreach (var cab in cabs)
                    {
                        AddCabOffset(cab);
                    }
                }
            }
            newFiles.AddRange(offsets.Keys.ToList());
            files = newFiles.ToArray();
        }

        public static void ProcessDependancies(ref string[] files)
        {
            Logger.Info("Resolving Dependancies...");
            var file = files.FirstOrDefault();
            if (Path.GetExtension(file) == ".wmv")
            {
                ProcessWMVFiles(ref files);
            }
        }
    }
    public class WMVEntry : IComparable<WMVEntry>
    {
        public string Path;
        public long Offset;
        public List<string> Dependancies;
        public WMVEntry(string path, long offset, List<string> dependancies)
        {
            Path = path;
            Offset = offset;
            Dependancies = dependancies;
        }
        public int CompareTo(WMVEntry other)
        {
            if (other == null) return 1;

            int result;
            if (other == null)
                throw new ArgumentException("Object is not a WMVEntry");

            result = Path.CompareTo(other.Path);

            if (result == 0)
                result = Offset.CompareTo(other.Offset);

            return result;
        }
    }
}

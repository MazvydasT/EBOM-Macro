using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EBOM_Macro
{
    public static class CSVManager
    {
        private const long PROGRESS_MAX = 200;
        private static readonly Encoding ENCODING = Encoding.GetEncoding("Windows-1252");

        public enum CPSCLevels
        {
            Level1,
            Level2,
            Level3
        };

        public static async Task<List<Item2>> ReadEBOMReport(string pathToEBOMReport, IProgress<ProgressUpdate> progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pathToEBOMReport) || !File.Exists(pathToEBOMReport)) return await Task.FromResult<List<Item2>>(null);

            using (var fileStream = new FileStream(pathToEBOMReport, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return await ReadEBOMReport(fileStream, progress, cancellationToken);
            }
        }

        private static async Task<List<Item2>> ReadEBOMReport(Stream ebomReportStream, IProgress<ProgressUpdate> progress = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Factory.StartNew(() =>
            {
                var items = new List<Item2>();

                long progressValue = 0;

                using (var streamReader = new StreamReader(ebomReportStream))
                using (var csvReader = new CsvReader(streamReader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    BadDataFound = null,
                    Delimiter = ",",
                    Encoding = ENCODING,
                    TrimOptions = TrimOptions.Trim,
                    WhiteSpaceChars = new[] { ' ', '\t', '\'' }
                }))
                {
                    var stream = streamReader.BaseStream;
                    var streamLength = stream.Length;

                    progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue });


                    /// <summary><c>vehicleLineTitle</c> should be obtained from source CSV</summary>
                    var vehicleLineTitle = "EBOM";

                    /// <summary><c>vehicleLineName</c> should be obtained from source CSV</summary>
                    var vehicleLineName = "PH-L663-0000";

                    var program = vehicleLineName.Split(new[] { '-' })[1];

                    var records = csvReader.GetRecords<EBOMReportRecord>();

                    var levelTracker = new Dictionary<int, Item2>();
                    var dsPhysicalIdTracker = new HashSet<string>();
                    var placeholderLookup = new Dictionary<string, Item2>();
                    Item2 root = null;

                    var cpscSplitChars = new[] { '-' };

                    var skipRecords = false;

                    try
                    {
                        foreach (var record in records)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var level = record.Level;

                            if (level == 0)
                            {
                                skipRecords = false;

                                if (dsPhysicalIdTracker.Contains(record.PhysicalId))
                                {
                                    skipRecords = true;
                                    continue;
                                }

                                else dsPhysicalIdTracker.Add(record.PhysicalId);
                            }

                            if (skipRecords) continue;

                            var item = new Item2
                            {
                                Number = record.PartNumber,
                                Version = record.Version,
                                Name = record.Name,

                                LocalTransformation = Utils.V6MatrixString2Matrix3D(record.Transformation),

                                Prefix = record.Prefix,
                                Base = record.Base,
                                Suffix = record.Suffix,

                                Owner = record.Owner,

                                PhysicalId = record.PhysicalId,

                                Parent = level == 0 ? null : (levelTracker.TryGetValue(level - 1, out Item2 parent) ? parent : null)
                            };

                            item.Parent?.Children.Add(item);

                            levelTracker[level] = item;

                            items.Add(item);

                            if (item.Parent == null)
                            {
                                var cpscLevel3Parts = record.CPSCLevel3?.Split(cpscSplitChars, StringSplitOptions.RemoveEmptyEntries);
                                var cpscLevel3Number = cpscLevel3Parts?.Length > 0 ? cpscLevel3Parts[0].Trim() : "";
                                var cpscLevel3Name = cpscLevel3Parts?.Length > 1 ? cpscLevel3Parts[1].Trim() : "";

                                placeholderLookup.TryGetValue(cpscLevel3Number, out Item2 level3Placeholder);

                                if (level3Placeholder == null)
                                {
                                    level3Placeholder = new Item2
                                    {
                                        Number = $"PH-{program}-{cpscLevel3Number}",
                                        Name = cpscLevel3Name
                                    };

                                    placeholderLookup.Add(cpscLevel3Number, level3Placeholder);

                                    var cpscLevel2Parts = record.CPSCLevel2?.Split(cpscSplitChars, StringSplitOptions.RemoveEmptyEntries);
                                    var cpscLevel2Number = cpscLevel2Parts?.Length > 0 ? cpscLevel2Parts[0].Trim() : "";
                                    var cpscLevel2Name = cpscLevel2Parts?.Length > 1 ? cpscLevel2Parts[1].Trim() : "";

                                    placeholderLookup.TryGetValue(cpscLevel2Number, out Item2 level2Placeholder);

                                    if (level2Placeholder == null)
                                    {
                                        level2Placeholder = new Item2
                                        {
                                            Number = $"PH-{program}-{cpscLevel2Number}",
                                            Name = cpscLevel2Name
                                        };

                                        placeholderLookup.Add(cpscLevel2Number, level2Placeholder);

                                        var cpscLevel1Parts = record.CPSCLevel1?.Split(cpscSplitChars, StringSplitOptions.RemoveEmptyEntries);
                                        var cpscLevel1Number = cpscLevel1Parts?.Length > 0 ? cpscLevel1Parts[0].Trim() : "";
                                        var cpscLevel1Name = cpscLevel1Parts?.Length > 1 ? cpscLevel1Parts[1].Trim() : "";

                                        placeholderLookup.TryGetValue(cpscLevel1Number, out Item2 level1Placeholder);

                                        if (level1Placeholder == null)
                                        {
                                            level1Placeholder = new Item2
                                            {
                                                Number = $"PH-{program}-{cpscLevel1Number}",
                                                Name = cpscLevel1Name
                                            };

                                            placeholderLookup.Add(cpscLevel1Number, level1Placeholder);

                                            if (root == null)
                                            {
                                                root = new Item2
                                                {
                                                    Number = vehicleLineTitle,
                                                    Name = vehicleLineName
                                                };

                                                items.Add(root);
                                            }

                                            level1Placeholder.Parent = root;
                                            root.Children.Add(level1Placeholder);

                                            items.Add(level1Placeholder);
                                        }

                                        level2Placeholder.Parent = level1Placeholder;
                                        level1Placeholder.Children.Add(level2Placeholder);

                                        items.Add(level2Placeholder);
                                    }

                                    level3Placeholder.Parent = level2Placeholder;
                                    level2Placeholder.Children.Add(level3Placeholder);

                                    items.Add(level3Placeholder);
                                }

                                item.Parent = level3Placeholder;
                                level3Placeholder.Children.Add(item);
                            }

                            if (progress != null)
                            {
                                var readProgress = stream.Position * PROGRESS_MAX / streamLength;

                                if (readProgress > progressValue)
                                {
                                    progressValue = readProgress;

                                    progress.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue, Message = $"Reading EBOM report: {((double)readProgress / PROGRESS_MAX):P0}" });
                                }
                            }
                        }
                    }

                    catch (HeaderValidationException headerValidationException)
                    {
                        throw new Exception($"Missing headers: {string.Join(", ", headerValidationException.InvalidHeaders.SelectMany(h => h.Names))}", headerValidationException);
                    }
                }

                progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = PROGRESS_MAX, Message = $"Done" });

                return items;
            });
        }

        public static async Task<List<Item>> GetItems(string pathToEBOMReport, IProgress<ProgressUpdate> progress = default, CancellationToken cancellationToken = default)
        {
            using (var fileStream = new FileStream(pathToEBOMReport, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return await GetItems(fileStream, progress, cancellationToken);
            }
        }

        private static async Task<List<Item>> GetItems(Stream ebomReportStream, IProgress<ProgressUpdate> progress = default, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Factory.StartNew(() =>
            {
                var items = new List<Item>();

                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    BadDataFound = null,
                    Delimiter = ",",
                    Encoding = ENCODING,
                    TrimOptions = TrimOptions.Trim,
                    WhiteSpaceChars = new[] { ' ', '\t', '\'' }
                };

                long progressValue = 0;

                using (var streamReader = new StreamReader(ebomReportStream))
                using (var csvReader = new CsvReader(streamReader, config))
                {
                    var stream = streamReader.BaseStream;
                    var streamLength = stream.Length;

                    progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue });

                    var records = csvReader.GetRecords<EBOMReportRecord>();

                    var levelTracker = new Dictionary<int, Item>();

                    try
                    {
                        foreach (var record in records)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                            }

                            var item = new Item
                            {
                                Number = record.PartNumber,
                                Version = record.Version,
                                Name = record.Name,

                                LocalTransformation = Utils.V6MatrixString2Matrix3D(record.Transformation),

                                Prefix = record.Prefix,
                                Base = record.Base,
                                Suffix = record.Suffix,

                                Owner = record.Owner,

                                Type = record.Level == 0 ? Item.ItemType.DS : Item.ItemType.PartAsy,

                                CPSC = record.CPSC,

                                Parent = record.Level == 0 ? null : (levelTracker.TryGetValue(record.Level - 1, out Item parent) ? parent : null),

                                PhysicalId = record.PhysicalId
                            };

                            item.Parent?.Children.Add(item);

                            levelTracker[record.Level] = item;

                            items.Add(item);

                            var readingProgress = stream.Position * PROGRESS_MAX / streamLength;
                            var newProgressValue = readingProgress / 3;

                            if (newProgressValue > progressValue)
                            {
                                progressValue = newProgressValue;

                                progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue, Message = $"Reading EBOM report: {((double)readingProgress / PROGRESS_MAX):P0}" });
                            }
                        }
                    }

                    catch (HeaderValidationException headerValidationException)
                    {
                        throw new Exception($"Missing headers: {string.Join(", ", headerValidationException.InvalidHeaders.SelectMany(h => h.Names))}", headerValidationException);
                    }
                }

                var itemsCount = items.Count;
                var progressSoFar = progressValue;

                var duplicateItems = items.Select((v, i) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    var deletionProgress = (i + 1) * PROGRESS_MAX / itemsCount;
                    var newProgressValue = progressSoFar + deletionProgress / 3;

                    if (newProgressValue > progressValue)
                    {
                        progressValue = newProgressValue;

                        progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue, Message = $"Deleting duplicate DSs: {((double)deletionProgress / PROGRESS_MAX):P0}" });
                    }

                    return v;
                }).Where(i => i.Type == Item.ItemType.DS).GroupBy(i => i.GetHash()).SelectMany(g => g.Skip(1).SelectMany(i =>
                {
                    i.Parent?.Children.Remove(i);
                    i.Parent = null;

                    return i.GetSelfAndDescendants();
                })).ToHashSet();

                items.RemoveAll(item => duplicateItems.Contains(item));

                //#########################
                // END Delete duplicate DSs
                //#########################

                //#################
                // Set External Ids
                //#################

                itemsCount = items.Count;
                progressSoFar = progressValue;

                using (var hasher = new SHA256Managed())
                {
                    for (var index = 0; index < itemsCount; ++index)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                        
                        var item = items[index];

                        var physicalIds = string.Join("_", item.GetDSToSelfPath().Select(i => i.PhysicalId));
                        var data = Encoding.UTF8.GetBytes(physicalIds);
                        var hash = hasher.ComputeHash(data);
                        var externalId = BitConverter.ToString(hash).Replace("-", "") + (item.Children.Count > 0 ? "_c" : "_i");

                        item.ExternalId = externalId;

                        var extIdProgress = (index + 1) * PROGRESS_MAX / itemsCount;
                        var newProgressValue = progressSoFar + extIdProgress / 3;

                        if (newProgressValue > progressValue)
                        {
                            progressValue = newProgressValue;

                            progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue, Message = $"Assigning external IDs: {((double)extIdProgress / PROGRESS_MAX):P0}" });
                        }
                    }
                }

                //#####################
                // END Set External Ids
                //#####################

                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                progress.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = PROGRESS_MAX, Message = "Done" });

                return items;
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public static List<Item> CreatePHsForCPSCLevel(IEnumerable<Item> childItems, /*string program,*/ CPSCLevels level) => childItems
            .GroupBy(i => i.CPSC.Substring(0, level == CPSCLevels.Level3 ? 6 : (level == CPSCLevels.Level2 ? 4 : 2)).PadRight(6, '0'))
            .Select(g =>
            {
                var ph = new Item
                {
                    CPSC = g.Key
                };

                ph.Children.AddRange(g.Select(c =>
                {
                    c.Parent = ph;

                    return c;
                }));

                ph.Type = Item.ItemType.PH;

                return ph;
            })
            .ToList();

        public static async Task ItemsToDSList(string csvPath, IEnumerable<Item> items, int itemCount, IProgress<ProgressUpdate> progress = default, CancellationToken cancellationToken = default)
        {
            using (var fileStream = new FileStream(csvPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
            using (var streamWriter = new StreamWriter(fileStream))
            using (var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture))
            {
                try
                {
                    await ItemsToDSList(csvWriter, items, itemCount, progress, cancellationToken);
                }

                finally
                {
                    fileStream?.SetLength(fileStream?.Position ?? 0); // Trunkates existing file
                }
            }
        }

        private static async Task ItemsToDSList(CsvWriter csvWriter, IEnumerable<Item> items, int itemCount, IProgress<ProgressUpdate> progress = default, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Factory.StartNew(() =>
            {
                csvWriter.WriteHeader<DSListRecord>();
                csvWriter.NextRecord();

                long progressValue = 0;
                var itemIndex = 0;

                foreach(var item in items)
                {
                    ++itemIndex;

                    cancellationToken.ThrowIfCancellationRequested();

                    if (item.Type == Item.ItemType.PartAsy) continue;

                    csvWriter.WriteRecord(new DSListRecord
                    {
                        PartNumber = item.Number,
                        Version = item.Version,
                        Name = item.Name,
                        CPSC = item.CPSC,
                        ExternalId = item.ExternalId,
                        Hash = item.GetHash(),
                        ParentExternalId = item.Parent?.ExternalId
                    });
                    csvWriter.NextRecord();

                    var newProgressValue = itemIndex * PROGRESS_MAX / itemCount;

                    if (newProgressValue > progressValue)
                    {
                        progressValue = newProgressValue;

                        progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue, Message = $"Writing DS list: {((double)newProgressValue / PROGRESS_MAX):P0}" });
                    }
                }

                progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = PROGRESS_MAX, Message = "Writing DS list is done" });

            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public static async Task<Dictionary<string, Item>> DSListToItems(string dsListPath, IProgress<ProgressUpdate> progress = default, CancellationToken cancellationToken = default)
        {
            using (var fileStream = new FileStream(dsListPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return await DSListToItems(fileStream, progress, cancellationToken);
            }
        }

        private static async Task<Dictionary<string, Item>> DSListToItems(Stream dsListStream, IProgress<ProgressUpdate> progress = default, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Factory.StartNew(() =>
            {
                var items = new Dictionary<string, Item>();

                var streamLength = dsListStream.Length;
                long progressValue = 0;

                using (var streamReader = new StreamReader(dsListStream))
                using (var csvReader = new CsvReader(streamReader, CultureInfo.InvariantCulture))
                {
                    var records = csvReader.GetRecords<DSListRecord>();

                    try
                    {
                        foreach (var record in records)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var item = new Item(record.Hash)
                            {
                                Number = record.PartNumber,
                                Version = record.Version,
                                Name = record.Name,
                                CPSC = record.CPSC,
                                ExternalId = record.ParentExternalId, // Set ExternalId to parent ExternalId for later lookup

                                State = Item.ItemState.Redundant
                            };

                            items[record.ExternalId] = item;

                            var readingProgress = dsListStream.Position * PROGRESS_MAX / streamLength;
                            var newProgressValue = readingProgress / 2;

                            if (newProgressValue > progressValue)
                            {
                                progressValue = newProgressValue;

                                progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue, Message = $"Reading DS list: {((double)readingProgress / PROGRESS_MAX):P0}" });
                            }
                        }
                    }

                    catch (HeaderValidationException headerValidationException)
                    {
                        throw new Exception($"Missing headers: {string.Join(", ", headerValidationException.InvalidHeaders.SelectMany(h => h.Names))}", headerValidationException);
                    }
                }

                var progressSoFar = progressValue;

                var itemCount = items.Count;
                var itemIndex = 0;

                foreach(var pair in items)
                {
                    ++itemIndex;

                    var externalId = pair.Key;
                    var item = pair.Value;

                    if(items.ContainsKey(item.ExternalId)) // Here ExternalId is parent ExternalId
                    {
                        var parent = items[item.ExternalId];

                        parent.Children.Add(item);
                        item.Parent = parent;
                    }

                    item.ExternalId = externalId; // Replace parent ExternalId with actual ExternalId

                    var matchingProgress = itemIndex * PROGRESS_MAX / itemCount;
                    var newProgressValue = progressSoFar + matchingProgress / 2;

                    if (newProgressValue > progressValue)
                    {
                        progressValue = newProgressValue;

                        progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue, Message = $"Matching parents with children: {((double)matchingProgress / PROGRESS_MAX):P0}" });
                    }
                }

                progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = PROGRESS_MAX, Message = "Done" });

                return items;
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }
}
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
        private const long PROGRESS_MAX = 300;
        private static readonly Encoding ENCODING = Encoding.GetEncoding("Windows-1252");

        public enum CPSCLevels
        {
            Level1,
            Level2,
            Level3
        };

        public static async Task<List<Item>> GetItems(string pathToEBOMReport, /*string program,*/ IProgress<ProgressUpdate> progress = default, CancellationToken cancellationToken = default)
        {
            using (var fileStream = new FileStream(pathToEBOMReport, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return await GetItems(fileStream, /*program,*/ progress, cancellationToken);
            }
        }

        private static async Task<List<Item>> GetItems(Stream ebomReportStream, /*string program,*/ IProgress<ProgressUpdate> progress = default, CancellationToken cancellationToken = default)
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

                                //#####################

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

                /*var duplicateDSs = items.Where(i => i.IsDS).GroupBy(i => i.GetHash()).Where(g => g.Count() > 1).OrderByDescending(g => g.Count()).ToArray();//.SelectMany(g => g.Skip(1)).ToArray();

                foreach(var dss in duplicateDSs)
                {
                    var ds = dss.First();

                    Debug.WriteLine($"{ds}\t{dss.Count()}");
                }*/

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

                        /*var ds = item.GetDS();
                        var dsNumberAndRev = $"{ds.Number}_{ds.Version}";

                        item.ExternalId = $"{item.PhysicalId}" + (item.IsDS ? "" : $"_{item.Parent?.PhysicalId}") + $"_{dsNumberAndRev}_" + (item.Children.Count > 0 ? "c" : "i");*/

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

                /*var level3PHs = CreatePHsForCPSCLevel(items.Where(i => i.Parent == null), program, CPSCLevels.Level3);
                var level2PHs = CreatePHsForCPSCLevel(level3PHs, program, CPSCLevels.Level2);
                var level1PHs = CreatePHsForCPSCLevel(level2PHs, program, CPSCLevels.Level1);

                var root = new Item
                {
                    Number = $"PRG-{program}",
                    Name = $"PROGRAM NODE FOR {program}"
                };
                root.Children.AddRange(level1PHs.OrderBy(i => i.Number).Select(i =>
                {
                    i.Parent = root;

                    return i;
                }));

                var placeHolders = level1PHs.Concat(level2PHs).Concat(level3PHs).Prepend(root)
                    .Select(i =>
                    {
                        i.ExternalId = $"{i.Number}_c";

                        return i;
                    });

                items.InsertRange(0, placeHolders);*/

                /*var asd = items.Where(i => i.Children.Count > 0).GroupBy(i => i.ExternalId).Where(g => g.Count() > 1).ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(i => i.GetAbsoluteTransformation()).Where(gr => gr.Count() > 1).ToDictionary(gr => gr.Key, gr => gr.ToArray())
                ).Where(p => p.Value.Count > 1).ToDictionary(p => p.Key, p => p.Value);

                foreach(var extIdP in asd)
                {
                    foreach(var xformMatP in extIdP.Value)
                    {
                        Debug.WriteLine($"{extIdP.Key}\t{string.Join("/", xformMatP.Value.First().DSToSelfPath().Select(i => i.PhysicalId))}\t{xformMatP.Key}");
                    }
                }*/

                /*var instances = items.Where(i => i.Children.Count == 0)
                    .Where(i => i.GetDS() != null)
                    .GroupBy(i => $"{i.Number} ({i.GetDS().Number}/{i.GetDS().Version})")
                    .Where(g => g.Count() > 1)
                    .Select(g => new { Number = g.Key, Values = g.GroupBy(i => $"{i.Prefix}-{i.Base}-{i.Suffix}/{i.Version} - {i.Name} ({i.Owner})").ToList() })
                    .Where(p => p.Values.Count > 1)
                    .ToDictionary(p => p.Number, p => p.Values.Select(g => g.Key).ToList());*/

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
                var i = 0;

                foreach(var item in items)
                {
                    ++i;

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

                    var newProgressValue = i * PROGRESS_MAX / itemCount;

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

                            //item.MatchingItem = item;

                            items[record.ExternalId] = item;

                            var streamPosition = dsListStream.Position;

                            progress?.Report(new ProgressUpdate { Max = streamLength, Value = streamPosition, Message = $"Reading DS list: {((double)streamPosition / streamLength):P0}" });
                        }
                    }

                    catch (HeaderValidationException headerValidationException)
                    {
                        throw new Exception($"Missing headers: {string.Join(", ", headerValidationException.InvalidHeaders.SelectMany(h => h.Names))}", headerValidationException);
                    }
                }

                foreach(var pair in items)
                {
                    var externalId = pair.Key;
                    var item = pair.Value;

                    if(items.ContainsKey(item.ExternalId)) // Here ExternalId is parent ExternalId
                    {
                        var parent = items[item.ExternalId];

                        parent.Children.Add(item);
                        item.Parent = parent;
                    }

                    item.ExternalId = externalId; // Replace parent ExternalId with actual ExternalId
                }

                progress?.Report(new ProgressUpdate { Max = streamLength, Value = streamLength, Message = "Done" });

                return items;
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }
}
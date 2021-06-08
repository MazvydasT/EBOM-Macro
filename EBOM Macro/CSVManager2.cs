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
    public static class CSVManager2
    {
        private const long PROGRESS_MAX = 200;
        private static readonly Encoding ENCODING = Encoding.GetEncoding("Windows-1252");

        public static async Task<(Item2 Root, IReadOnlyCollection<Item2> PHs, IReadOnlyCollection<Item2> Items)> ReadEBOMReport(string pathToEBOMReport, IProgress<ProgressUpdate> progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pathToEBOMReport) || !File.Exists(pathToEBOMReport)) return await Task.FromResult<(Item2 Root, IReadOnlyCollection<Item2> PHs, IReadOnlyCollection<Item2> Items)>(default);

            using (var fileStream = new FileStream(pathToEBOMReport, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return await ReadEBOMReport(fileStream, progress, cancellationToken);
            }
        }

        static async Task<(Item2 Root, IReadOnlyCollection<Item2> PHs, IReadOnlyCollection<Item2> Items)> ReadEBOMReport(Stream ebomReportStream, IProgress<ProgressUpdate> progress = null, CancellationToken cancellationToken = default)
        {
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

                                                //items.Add(root);
                                            }

                                            level1Placeholder.Parent = root;
                                            root.Children.Add(level1Placeholder);

                                            //items.Add(level1Placeholder);
                                        }

                                        level2Placeholder.Parent = level1Placeholder;
                                        level1Placeholder.Children.Add(level2Placeholder);

                                        //items.Add(level2Placeholder);
                                    }

                                    level3Placeholder.Parent = level2Placeholder;
                                    level2Placeholder.Children.Add(level3Placeholder);

                                    //items.Add(level3Placeholder);
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

                    progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = PROGRESS_MAX, Message = $"Done" });

                    return (root, placeholderLookup.Values, items.AsReadOnly());
                }
            });
        }

        public static async Task<Dictionary<string, Item2>> ReadDSList(string dsListPath, IProgress<ProgressUpdate> progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(dsListPath) || !File.Exists(dsListPath)) return await Task.FromResult<Dictionary<string, Item2>>(null);

            using (var fileStream = new FileStream(dsListPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return await ReadDSList(fileStream, progress, cancellationToken);
            }
        }

        static async Task<Dictionary<string, Item2>> ReadDSList(Stream dsListStream, IProgress<ProgressUpdate> progress = default, CancellationToken cancellationToken = default)
        {
            return await Task.Factory.StartNew(() =>
            {
                var items = new Dictionary<string, Item2>();

                var streamLength = dsListStream.Length;
                long progressValue = 0;

                using (var streamReader = new StreamReader(dsListStream))
                using (var csvReader = new CsvReader(streamReader, CultureInfo.InvariantCulture))
                {
                    var records = csvReader.GetRecords<DSListRecord2>();

                    var levelTracker = new Dictionary<int, Item2>();

                    try
                    {
                        foreach (var record in records)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var level = record.Level;

                            var item = new Item2
                            {
                                Number = record.Number,
                                Version = record.Version,
                                Name = record.Name,

                                Parent = level == 0 ? null : (levelTracker.TryGetValue(level - 1, out Item2 parent) ? parent : null)
                            };

                            item.Parent?.Children.Add(item);

                            levelTracker[level] = item;

                            items.Add(record.ExternalId, item);

                            if (progress != null)
                            {
                                var readingProgress = dsListStream.Position * PROGRESS_MAX / streamLength;

                                if (readingProgress > progressValue)
                                {
                                    progressValue = readingProgress;

                                    progress.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue, Message = $"Reading DS list: {((double)readingProgress / PROGRESS_MAX):P0}" });
                                }
                            }
                        }
                    }

                    catch (HeaderValidationException headerValidationException)
                    {
                        throw new Exception($"Missing headers: {string.Join(", ", headerValidationException.InvalidHeaders.SelectMany(h => h.Names))}", headerValidationException);
                    }
                }

                progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = PROGRESS_MAX, Message = "Done" });

                return items;
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public static async Task WriteDSList(string dsListPath, (Item2 Root, IReadOnlyCollection<Item2> PHs) items, IProgress<ProgressUpdate> progress = null, CancellationToken cancellationToken = default)
        {
            using (var fileStream = new FileStream(dsListPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
            {
                try
                {
                    await WriteDSList(fileStream, items, progress, cancellationToken);
                }

                finally
                {
                    fileStream.SetLength(fileStream.Position); // Trunkates existing file
                }
            }
        }

        static async Task WriteDSList(Stream stream, (Item2 Root, IReadOnlyCollection<Item2> PHs) items, IProgress<ProgressUpdate> progress = null, CancellationToken cancellationToken = default)
        {
            await Task.Factory.StartNew(() =>
            {
                var stack = new Stack<(Item2, int)>((items.Root, 0).Yield());

                var itemCount = items.PHs.Count + 1;

                var lookup = new HashSet<Item2>(itemCount);
                lookup.UnionWith(items.PHs.Prepend(items.Root));

                var index = 0;
                long progressValue = 0;

                using (var streamWriter = new StreamWriter(stream))
                using (var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture))
                {
                    csvWriter.WriteHeader<DSListRecord2>();

                    while(stack.Count > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        (var item, var level) = stack.Pop();

                        csvWriter.WriteRecord(new DSListRecord2
                        {
                            ExternalId = item.ExternalId,
                            Hash = item.Hash,
                            Level = level,
                            Name = item.Name,
                            Number = item.Number,
                            Version = item.Version
                        });

                        if (lookup.Contains(item))
                        {
                            var childLevel = level + 1;
                            var children = item.Children.Select(i => (i, childLevel)).Reverse();

                            foreach (var child in children)
                            {
                                stack.Push(child);
                            }

                            if (progress != null)
                            {
                                var readProgress = ++index * PROGRESS_MAX / itemCount;

                                if (readProgress > progressValue)
                                {
                                    progressValue = readProgress;

                                    progress.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue, Message = $"Writing DS list: {((double)readProgress / PROGRESS_MAX):P0}" });
                                }
                            }
                        }
                    }
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }
}
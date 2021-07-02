using CsvHelper;
using CsvHelper.Configuration;
using EBOM_Macro.Extensions;
using EBOM_Macro.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EBOM_Macro.Managers
{
    public static class CSVManager
    {
        const long PROGRESS_MAX = 200;
        static readonly Encoding EBOM_REPORT_ENCODING = Encoding.GetEncoding("Windows-1252");
        static readonly Encoding DS_LIST_ENCODING = new UTF8Encoding(false);

        public static async Task<ItemsContainer> ReadEBOMReport(string pathToEBOMReport, IProgress<ProgressUpdate> progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pathToEBOMReport)) return default;

            using (var fileStream = new FileStream(pathToEBOMReport, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return await ReadEBOMReport(fileStream, progress, cancellationToken);
            }
        }

        static async Task<ItemsContainer> ReadEBOMReport(Stream ebomReportStream, IProgress<ProgressUpdate> progress = null, CancellationToken cancellationToken = default)
        {
            return await Task.Factory.StartNew(() =>
            {
                var items = new List<Item>();

                long progressValue = 0;

                using (var streamReader = new StreamReader(ebomReportStream))
                using (var csvReader = new CsvReader(streamReader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    BadDataFound = null,
                    Delimiter = ",",
                    Encoding = EBOM_REPORT_ENCODING,
                    TrimOptions = TrimOptions.Trim,
                    WhiteSpaceChars = new[] { ' ', '\t', '\'' }
                }))
                {
                    var stream = streamReader.BaseStream;
                    var streamLength = stream.Length;

                    progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue });

                    string vehicleLineTitle = null, vehicleLineName = null, program = null; 

                    var records = csvReader.GetRecords<EBOMReportRecord>();

                    var levelTracker = new Dictionary<int, Item>();
                    var dsPhysicalIdTracker = new HashSet<string>();
                    var placeholderLookup = new Dictionary<string, Item>();
                    Item root = null;

                    var cpscSplitChars = new[] { '-' };

                    var skipRecords = false;

                    try
                    {
                        foreach (var record in records)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var level = record.Level;

                            if(vehicleLineTitle == null)
                            {
                                vehicleLineTitle = (record.Title ?? "").Trim();
                                vehicleLineName = (record.VehicleLineName ?? "").Trim();

                                var vehicleLineNameParts = vehicleLineName.Split(new[] { '-' });

                                if (vehicleLineNameParts.Length < 2)
                                {
                                    var columnName = csvReader.Context.Maps[record.GetType()].MemberMaps
                                        .Where(m => m.Data.Member.Name == nameof(EBOMReportRecord.VehicleLineName))
                                        .FirstOrDefault()?.Data.Names.FirstOrDefault();

                                    throw new InvalidDataException($"Unexpected {columnName} value: '{record.VehicleLineName}'. Unable to extract program name.");
                                }

                                program = vehicleLineNameParts[1].Trim();
                            }

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

                                PhysicalId = record.PhysicalId,

                                Maturity = record.Maturity,

                                Parent = level == 0 ? null : (levelTracker.TryGetValue(level - 1, out Item parent) ? parent : null),

                                Type = level == 0 ? Item.ItemType.DS : Item.ItemType.PartAsy
                            };

                            item.Parent?.Children.Add(item);

                            levelTracker[level] = item;

                            items.Add(item);

                            if (item.Parent == null)
                            {
                                var cpscLevel3Parts = record.CPSCLevel3?.Split(cpscSplitChars, StringSplitOptions.RemoveEmptyEntries);
                                var cpscLevel3Number = cpscLevel3Parts?.Length > 0 ? cpscLevel3Parts[0].Trim() : "";
                                var cpscLevel3Name = cpscLevel3Parts?.Length > 1 ? cpscLevel3Parts[1].Trim() : "";

                                placeholderLookup.TryGetValue(cpscLevel3Number, out Item level3Placeholder);

                                if (level3Placeholder == null)
                                {
                                    var level3PHNumber = $"PH-{program}-{cpscLevel3Number}";

                                    level3Placeholder = new Item
                                    {
                                        Number = level3PHNumber,
                                        Name = cpscLevel3Name,
                                        Type = Item.ItemType.PH,
                                        BaseExternalId = $"{level3PHNumber}_c",
                                        Maturity = EBOMReportRecord.MaturityState.FROZEN
                                    };

                                    placeholderLookup.Add(cpscLevel3Number, level3Placeholder);

                                    var cpscLevel2Parts = record.CPSCLevel2?.Split(cpscSplitChars, StringSplitOptions.RemoveEmptyEntries);
                                    var cpscLevel2Number = cpscLevel2Parts?.Length > 0 ? cpscLevel2Parts[0].Trim() : "";
                                    var cpscLevel2Name = cpscLevel2Parts?.Length > 1 ? cpscLevel2Parts[1].Trim() : "";

                                    placeholderLookup.TryGetValue(cpscLevel2Number, out Item level2Placeholder);

                                    if (level2Placeholder == null)
                                    {
                                        var level2PHNumber = $"PH-{program}-{cpscLevel2Number}";

                                        level2Placeholder = new Item
                                        {
                                            Number = level2PHNumber,
                                            Name = cpscLevel2Name,
                                            Type = Item.ItemType.PH,
                                            BaseExternalId = $"{level2PHNumber}_c",
                                            Maturity = EBOMReportRecord.MaturityState.FROZEN
                                        };

                                        placeholderLookup.Add(cpscLevel2Number, level2Placeholder);

                                        var cpscLevel1Parts = record.CPSCLevel1?.Split(cpscSplitChars, StringSplitOptions.RemoveEmptyEntries);
                                        var cpscLevel1Number = cpscLevel1Parts?.Length > 0 ? cpscLevel1Parts[0].Trim() : "";
                                        var cpscLevel1Name = cpscLevel1Parts?.Length > 1 ? cpscLevel1Parts[1].Trim() : "";

                                        placeholderLookup.TryGetValue(cpscLevel1Number, out Item level1Placeholder);

                                        if (level1Placeholder == null)
                                        {
                                            var level1PHNumber = $"PH-{program}-{cpscLevel1Number}";

                                            level1Placeholder = new Item
                                            {
                                                Number = level1PHNumber,
                                                Name = cpscLevel1Name,
                                                Type = Item.ItemType.PH,
                                                BaseExternalId = $"{level1PHNumber}_c",
                                                Maturity = EBOMReportRecord.MaturityState.FROZEN
                                            };

                                            placeholderLookup.Add(cpscLevel1Number, level1Placeholder);

                                            if (root == null)
                                            {
                                                root = new Item
                                                {
                                                    Number = vehicleLineName,
                                                    Name = vehicleLineTitle,
                                                    Type = Item.ItemType.PH,
                                                    BaseExternalId = $"{vehicleLineTitle}_c",
                                                    Maturity = EBOMReportRecord.MaturityState.FROZEN
                                                };
                                            }

                                            level1Placeholder.Parent = root;
                                            root.Children.Add(level1Placeholder);
                                        }

                                        level2Placeholder.Parent = level1Placeholder;
                                        level1Placeholder.Children.Add(level2Placeholder);
                                    }

                                    level3Placeholder.Parent = level2Placeholder;
                                    level2Placeholder.Children.Add(level3Placeholder);
                                }

                                item.Parent = level3Placeholder;
                                level3Placeholder.Children.Add(item);
                            }

                            if (items.Count > 1) ItemManager.SetBaseExternalId(items[items.Count - 2]);

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

                        if (items.Count > 0) ItemManager.SetBaseExternalId(items[items.Count - 1]);
                    }

                    catch (HeaderValidationException headerValidationException)
                    {
                        throw new Exception($"Missing headers: {string.Join(", ", headerValidationException.InvalidHeaders.SelectMany(h => h.Names))}", headerValidationException);
                    }

                    progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = PROGRESS_MAX, Message = $"Done" });

                    return new ItemsContainer { Root = root, PHs = placeholderLookup.Values, Items = items.AsReadOnly() };
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public static async Task<ExistingDataContainer> ReadDSList(string dsListPath, IProgress<ProgressUpdate> progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(dsListPath)) return default;

            using (var fileStream = new FileStream(dsListPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return await ReadDSList(fileStream, progress, cancellationToken);
            }
        }

        private static async Task<ExistingDataContainer> ReadDSList(Stream dsListStream, IProgress<ProgressUpdate> progress = default, CancellationToken cancellationToken = default)
        {
            return await Task.Factory.StartNew(() =>
            {
                var items = new Dictionary<string, Item>();
                var externalIdPrefix = "";

                var streamLength = dsListStream.Length;
                long progressValue = 0;

                using (var streamReader = new StreamReader(dsListStream, DS_LIST_ENCODING))
                using (var csvReader = new CsvReader(streamReader, CultureInfo.InvariantCulture))
                {
                    var levelTracker = new Dictionary<int, Item>();

                    try
                    {
                        csvReader.Read();

                        externalIdPrefix = csvReader.GetField(1);
                        csvReader.Read();
                        csvReader.ReadHeader();

                        var records = csvReader.GetRecords<DSListRecord>();

                        foreach (var record in records)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var level = record.Level;

                            var item = new Item(record.Hash.Length == 0 ? null : record.Hash)
                            {
                                Number = record.Number,
                                Version = record.Version,
                                Name = record.Name,

                                BaseExternalId = record.ExternalId,

                                Parent = level == 0 ? null : (levelTracker.TryGetValue(level - 1, out var parent) ? parent : null),

                                State = Item.ItemState.Redundant
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

                    catch (CsvHelper.MissingFieldException missingFieldException)
                    {
                        throw new FileFormatException("Unexpected file format", missingFieldException);
                    }
                }

                progress?.Report(new ProgressUpdate
                {
                    Max = PROGRESS_MAX,
                    Value = PROGRESS_MAX,
                    Message = "Done" + (string.IsNullOrWhiteSpace(externalIdPrefix) ? "" : $". ExternalId prefix: {externalIdPrefix}")
                });

                return new ExistingDataContainer { Items = items, ExternalIdPrefix = externalIdPrefix };
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public static async Task WriteDSList(string dsListPath, (Item Root, IReadOnlyCollection<Item> PHs) items, string externalIdPrefix, IProgress<ProgressUpdate> progress = null, CancellationToken cancellationToken = default)
        {
            using (var fileStream = new FileStream(dsListPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
            {
                try
                {
                    await WriteDSList(fileStream, items, externalIdPrefix, progress, cancellationToken);
                }

                finally
                {
                    fileStream.SetLength(fileStream.Position); // Trunkates existing file
                }
            }
        }

        private static async Task WriteDSList(Stream stream, (Item Root, IReadOnlyCollection<Item> PHs) items, string externalIdPrefix, IProgress<ProgressUpdate> progress = null, CancellationToken cancellationToken = default)
        {
            await Task.Factory.StartNew(() =>
            {
                var stack = new Stack<(Item, int)>((items.Root, 0).Yield());

                var itemCount = items.PHs.Count + 1;

                var lookup = new HashSet<Item>(itemCount);
                lookup.UnionWith(items.PHs.Prepend(items.Root));

                var index = 0;
                long progressValue = 0;

                using (var streamWriter = new StreamWriter(stream, DS_LIST_ENCODING, 1024, true))
                using (var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture, true))
                {
                    csvWriter.WriteField("ExternalId prefix:");
                    csvWriter.WriteField(externalIdPrefix);

                    csvWriter.NextRecord();
                    csvWriter.NextRecord();

                    csvWriter.WriteHeader<DSListRecord>();
                    csvWriter.NextRecord();

                    while (stack.Count > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        (var item, var level) = stack.Pop();

                        csvWriter.WriteRecord(new DSListRecord
                        {
                            ExternalId = item.BaseExternalId,
                            Hash = item.Type == Item.ItemType.DS ? item.GetHash() : null,
                            Level = level,
                            Name = item.Name,
                            Number = item.Number,
                            Version = item.Version
                        });
                        csvWriter.NextRecord();

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
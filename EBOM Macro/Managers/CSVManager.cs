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

                            var transformationMatrix = Utils.V6MatrixString2Matrix3D(record.Transformation);

                            var item = new Item
                            {
                                Attributes = new ItemAttributes
                                {
                                    Number = record.PartNumber,
                                    Version = record.Version,
                                    Name = record.Name,

                                    Rotation = transformationMatrix.GetEulerZYX(),
                                    Translation = transformationMatrix.GetTranslation() * 1000.0,

                                    Prefix = record.Prefix,
                                    Base = record.Base,
                                    Suffix = record.Suffix,

                                    Owner = record.Owner
                                },

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
                                var cpscLevel3Name = cpscLevel3Parts?.Length > 1 ? string.Join("-", cpscLevel3Parts.Skip(1)).Trim() : "";

                                placeholderLookup.TryGetValue(cpscLevel3Number, out Item level3Placeholder);

                                if (level3Placeholder == null)
                                {
                                    var level3PHNumber = $"PH-{program}-{cpscLevel3Number}";

                                    level3Placeholder = new Item
                                    {
                                        Attributes = new ItemAttributes
                                        {
                                            Number = level3PHNumber,
                                            Name = cpscLevel3Name
                                        },
                                        Type = Item.ItemType.PH,
                                        BaseExternalId = $"{level3PHNumber}_c",
                                        Maturity = EBOMReportRecord.MaturityState.FROZEN
                                    };

                                    placeholderLookup.Add(cpscLevel3Number, level3Placeholder);

                                    var cpscLevel2Parts = record.CPSCLevel2?.Split(cpscSplitChars, StringSplitOptions.RemoveEmptyEntries);
                                    var cpscLevel2Number = cpscLevel2Parts?.Length > 0 ? cpscLevel2Parts[0].Trim() : "";
                                    var cpscLevel2Name = cpscLevel2Parts?.Length > 1 ? string.Join("-", cpscLevel2Parts.Skip(1)).Trim() : "";

                                    placeholderLookup.TryGetValue(cpscLevel2Number, out Item level2Placeholder);

                                    if (level2Placeholder == null)
                                    {
                                        var level2PHNumber = $"PH-{program}-{cpscLevel2Number}";

                                        level2Placeholder = new Item
                                        {
                                            Attributes = new ItemAttributes
                                            {
                                                Number = level2PHNumber,
                                                Name = cpscLevel2Name
                                            },
                                            Type = Item.ItemType.PH,
                                            BaseExternalId = $"{level2PHNumber}_c",
                                            Maturity = EBOMReportRecord.MaturityState.FROZEN
                                        };

                                        placeholderLookup.Add(cpscLevel2Number, level2Placeholder);

                                        var cpscLevel1Parts = record.CPSCLevel1?.Split(cpscSplitChars, StringSplitOptions.RemoveEmptyEntries);
                                        var cpscLevel1Number = cpscLevel1Parts?.Length > 0 ? cpscLevel1Parts[0].Trim() : "";
                                        var cpscLevel1Name = cpscLevel1Parts?.Length > 1 ? string.Join("-", cpscLevel1Parts.Skip(1)).Trim() : "";

                                        placeholderLookup.TryGetValue(cpscLevel1Number, out Item level1Placeholder);

                                        if (level1Placeholder == null)
                                        {
                                            var level1PHNumber = $"PH-{program}-{cpscLevel1Number}";

                                            level1Placeholder = new Item
                                            {
                                                Attributes = new ItemAttributes
                                                {
                                                    Number = level1PHNumber,
                                                    Name = cpscLevel1Name
                                                },
                                                Type = Item.ItemType.PH,
                                                BaseExternalId = $"{level1PHNumber}_c",
                                                Maturity = EBOMReportRecord.MaturityState.FROZEN
                                            };

                                            placeholderLookup.Add(cpscLevel1Number, level1Placeholder);

                                            if (root == null)
                                            {
                                                root = new Item
                                                {
                                                    Attributes = new ItemAttributes
                                                    {
                                                        Number = vehicleLineName,
                                                        Name = vehicleLineTitle
                                                    },
                                                    Type = Item.ItemType.PH,
                                                    BaseExternalId = $"{vehicleLineName}_c",
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

                    return new ItemsContainer { Root = root, PHs = placeholderLookup.Values, Items = items.AsReadOnly(), Program = program };
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }
}
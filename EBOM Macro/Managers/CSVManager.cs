using CsvHelper;
using CsvHelper.Configuration;
using EBOM_Macro.Extensions;
using EBOM_Macro.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EBOM_Macro.Managers
{
    /// <summary>
    /// Class responsible for all CSV reading within application 
    /// </summary>
    public static class CSVManager
    {
        /// <summary>
        /// Constant that adjusts how often progress update is sent
        /// </summary>
        const long PROGRESS_MAX = 200;

        /// <summary>
        /// eMS EBOM report encoding
        /// </summary>
        static readonly Encoding EBOM_REPORT_ENCODING = new UTF8Encoding(false);

        /// <summary>
        /// Lists string type properties of a record object
        /// </summary>
        static readonly IReadOnlyList<PropertyInfo> RECORD_STRING_PROPERTIES = typeof(EBOMReportRecord).GetProperties()
            .Where(p => p.PropertyType == typeof(string)).ToList();

        /// <summary>
        /// Asynchronously returns read eMS EBOM report data to the caller.
        /// </summary>
        /// <param name="pathToEBOMReport">Path to eMS EBOM report</param>
        /// <param name="progress">Optional IProgress construct for reporting progress back to caller</param>
        /// <param name="cancellationToken">Optional cancellation token to allow caller to cancel the task</param>
        /// <returns></returns>
        public static async Task<ItemsContainer> ReadEBOMReport(string pathToEBOMReport, IProgress<ProgressUpdate> progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pathToEBOMReport)) return default;

            using (var fileStream = new FileStream(pathToEBOMReport, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return await ReadEBOMReport(fileStream, progress, cancellationToken);
            }
        }

        /// <summary>
        /// Asynchronously returns read eMS EBOM report data to the caller.
        /// </summary>
        /// <param name="ebomReportStream">Stream containing eMS EBOM report data</param>
        /// <param name="progress">Optional IProgress construct for reporting progress back to caller</param>
        /// <param name="cancellationToken">Optional cancellation token to allow caller to cancel the task</param>
        /// <returns></returns>
        static async Task<ItemsContainer> ReadEBOMReport(Stream ebomReportStream, IProgress<ProgressUpdate> progress = null, CancellationToken cancellationToken = default)
        {
            return await Task.Factory.StartNew(() =>
            {
                var items = new List<Item>();

                long progressValue = 0;

                var cacheKey = new object();

                using (var streamReader = new StreamReader(ebomReportStream, EBOM_REPORT_ENCODING))
                using (var csvReader = new CsvReader(streamReader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    BadDataFound = null, // Ignores bad data
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

                    var materialRoundBracketsRegex = new Regex(@"\(.*\)", RegexOptions.Compiled);
                    var materialSquareBracketRegex = new Regex(@"\[.*\].*?$", RegexOptions.Compiled);

                    const char plusChar = '+';

                    var roundBrackets = new[] { '(', ')' };

                    try
                    {
                        foreach (var record in records) // Iterates over all eMS EBOM reports rows
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            // Iterates over string type properties of a record and converts encoding.
                            // This is to safeguard against input CSV file where declared encoding is different to actual encoding,
                            // which causes issues when comparing new data to existing data.
                            foreach (var property in RECORD_STRING_PROPERTIES)
                            {
                                var value = (string)property.GetValue(record);

                                if (value == null) continue;

                                var csvEncodingBytes = EBOM_REPORT_ENCODING.GetBytes(value);
                                var xmlEncodingBytes = Encoding.Convert(EBOM_REPORT_ENCODING, XMLManager.XML_ENCODING, csvEncodingBytes);

                                value = XMLManager.XML_ENCODING.GetString(xmlEncodingBytes);

                                property.SetValue(record, value);
                            }

                            var level = record.Level;

                            if (vehicleLineTitle == null)
                            {
                                vehicleLineTitle = (record.Title ?? "").Trim();
                                vehicleLineName = (record.VehicleLineName ?? "").Trim();

                                var programRegex = new Regex(@"[A-Z]+\d+.*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

                                program = new Regex("([^A-Z0-9])", RegexOptions.Compiled | RegexOptions.IgnoreCase)
                                    .Split($"{vehicleLineName}-{vehicleLineTitle}")
                                    .Where(v => programRegex.IsMatch(v))
                                    .FirstOrDefault();

                                if (program == null)
                                {
                                    var columnNames = string.Join(", ", csvReader.Context.Maps[record.GetType()].MemberMaps
                                        .Where(m => m.Data.Member.Name == nameof(EBOMReportRecord.VehicleLineName) || m.Data.Member.Name == nameof(EBOMReportRecord.Title))
                                        .Select(m => m.Data.Names.FirstOrDefault()));

                                    throw new InvalidDataException($"Unexpected {columnNames} values: '{record.VehicleLineName}', '{record.Title}'. Unable to extract program name.");
                                }
                            }

                            if (level == 0)
                            {
                                skipRecords = false;

                                var uniqueIdentifier = $"{record.PhysicalId}#{record.Transformation}";

                                if (dsPhysicalIdTracker.Contains(uniqueIdentifier))
                                {
                                    skipRecords = true;
                                    continue;
                                }

                                else dsPhysicalIdTracker.Add(uniqueIdentifier);
                            }

                            if (skipRecords) continue;

                            var transformationMatrix = Utils.V6MatrixString2Matrix3D(record.Transformation);

                            var material = record.Material;

                            if (material != null)
                            {
                                // Remove material duplication
                                material = string.Join($"{plusChar}", material.Split(plusChar).Select(v => v.Trim()).Distinct());

                                if (material.Contains("(") || material.Contains("["))
                                {
                                    material = string.Join($"{plusChar}", material.Split(plusChar).Select(v =>
                                    {
                                        var value = v.Trim();

                                        string newValue;

                                        if (materialSquareBracketRegex.Match(value) is var squareMatch && squareMatch.Success)
                                        {
                                            newValue = squareMatch.Value.Replace("[", "").Replace("]", "");
                                        }

                                        else if (materialRoundBracketsRegex.Match(value) is var roundMatch && roundMatch.Success)
                                        {
                                            newValue = roundMatch.Value.Trim(roundBrackets);
                                        }

                                        else newValue = null;

                                        // Check if extracted value still contains brackets, if so, revert to original
                                        if (newValue?.Where(c => "(){}".Contains(c)).Any() ?? false) newValue = value;

                                        return newValue;
                                    }).Where(v => v != null).Distinct());
                                }

                                const string ellipsis = "[...]";
                                const int lengthLimit = 256; // Material field character limit in eMS
                                if (material.Length > lengthLimit) material = material.Substring(0, lengthLimit - ellipsis.Length) + ellipsis;
                            }

                            var item = new Item
                            {
                                Attributes = new ItemAttributes
                                {
                                    Number = record.PartNumber,
                                    Version = record.Version,
                                    Name = record.Name,

                                    Rotation = transformationMatrix.GetEulerZYX(),

                                    // * 1000 is to convert Catia distances in meters to Process Designer distances in milimeters
                                    Translation = transformationMatrix.GetTranslation() * 1000.0,

                                    Prefix = record.Prefix,
                                    Base = record.Base,
                                    Suffix = record.Suffix,

                                    Owner = record.Owner,

                                    Material = material,

                                    CCR = record.CCR
                                },

                                PhysicalId = record.PhysicalId,

                                Parent = level == 0 ? null : (levelTracker.TryGetValue(level - 1, out Item parent) ? parent : null),

                                Type = level == 0 ? Item.ItemType.DS : Item.ItemType.PartAsy,

                                IsInstance = record.Has3DShape,

                                Filename = record.Filename
                            };

                            if (item.Parent != null)
                            {
                                item.Parent.Children.Add(item);

                                item.Parent.IsInstance = false;
                            }

                            levelTracker[level] = item;

                            items.Add(item);

                            if (item.Parent == null)
                            {
                                var cpscLevel3Parts = record.CPSCLevel3?.Split(cpscSplitChars, StringSplitOptions.RemoveEmptyEntries);
                                var cpscLevel3Number = cpscLevel3Parts?.Length > 0 ? cpscLevel3Parts[0].Trim() : "NO_LEVEL_3_CPSC";
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

                                        IsInstance = false
                                    };

                                    placeholderLookup.Add(cpscLevel3Number, level3Placeholder);

                                    var cpscRegex = new Regex(@"^\d{6}.*$", RegexOptions.Compiled | RegexOptions.Multiline);

                                    var cpscLevel2Parts = record.CPSCLevel2?.Split(cpscSplitChars, StringSplitOptions.RemoveEmptyEntries);
                                    var cpscLevel2Number = cpscLevel2Parts?.Length > 0 ? cpscLevel2Parts[0].Trim() : (cpscRegex.IsMatch(cpscLevel3Number) ? cpscLevel3Number.Substring(0, 4).PadRight(6, '0') : "NO_LEVEL_2_CPSC");
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

                                            IsInstance = false
                                        };

                                        placeholderLookup.Add(cpscLevel2Number, level2Placeholder);

                                        var cpscLevel1Parts = record.CPSCLevel1?.Split(cpscSplitChars, StringSplitOptions.RemoveEmptyEntries);
                                        var cpscLevel1Number = cpscLevel1Parts?.Length > 0 ? cpscLevel1Parts[0].Trim() : (cpscRegex.IsMatch(cpscLevel2Number) ? cpscLevel2Number.Substring(0, 2).PadRight(6, '0') : "NO_LEVEL_1_CPSC");
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

                                                IsInstance = false
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

                                                    IsExpanded = true,

                                                    IsInstance = false
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

                            if (items.Count > 1) ItemManager.SetBaseExternalId(items[items.Count - 2], cacheKey);

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

                        if (items.Count > 0) ItemManager.SetBaseExternalId(items[items.Count - 1], cacheKey);
                    }

                    catch (HeaderValidationException headerValidationException)
                    {
                        throw new Exception($"Missing headers: {string.Join(", ", headerValidationException.InvalidHeaders.SelectMany(h => h.Names))}", headerValidationException);
                    }

                    progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = PROGRESS_MAX, Message = $"Done" });

                    return new ItemsContainer(root, placeholderLookup.Values, items, program, cacheKey);
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }
}
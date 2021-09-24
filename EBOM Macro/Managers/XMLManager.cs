using EBOM_Macro.Extensions;
using EBOM_Macro.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using System.Xml;
using System.Xml.Linq;

namespace EBOM_Macro.Managers
{
    public static class XMLManager
    {
        private const long PROGRESS_MAX = 300;
        private static readonly Encoding WINDOWS_1252_ENCODING = Encoding.GetEncoding("Windows-1252");

        const string PM_COMPOUND_PART = "PmCompoundPart";
        const string PM_PART_INSTANCE = "PmPartInstance";
        const string PM_LAYOUT = "PmLayout";
        const string PM_PART_PROTOTYPE = "PmPartPrototype";
        const string PM_3D_REP = "Pm3DRep";
        const string PM_REFERENCE_FILE = "PmReferenceFile";

        public static async Task ItemsToXML(string xmlPath, IReadOnlyList<XMLExportData> xmlExportData, string metaData, IProgress<ProgressUpdate> progress = default, CancellationToken cancellationToken = default)
        {
            using (var fileStream = new FileStream(xmlPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
            using (var streamWriter = new StreamWriter(fileStream, WINDOWS_1252_ENCODING))
            using (var xmlWriter = XmlWriter.Create(streamWriter, new XmlWriterSettings()
            {
                Indent = true,
                NewLineChars = "\n",
                IndentChars = "\t",

                Encoding = WINDOWS_1252_ENCODING
            }))
            {
                try
                {
                    await ItemsToXML(xmlWriter, xmlExportData, metaData, progress, cancellationToken);
                }

                finally
                {
                    fileStream?.SetLength(fileStream?.Position ?? 0); // Trunkates existing file
                }
            }
        }

        private static async Task ItemsToXML(XmlWriter xmlWriter, IReadOnlyList<XMLExportData> xmlExportData, string metaData, IProgress<ProgressUpdate> progress = default, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Factory.StartNew(() =>
            {
                var filePathTracker = new Dictionary<string, string>();

                xmlWriter.WriteStartDocument(true);

                if (!string.IsNullOrWhiteSpace(metaData))
                {
                    xmlWriter.WriteWhitespace("\n\n");
                    xmlWriter.WriteComment("\n" + metaData.TrimEnd() + "\n");
                    xmlWriter.WriteWhitespace("\n\n");
                }

                xmlWriter.WriteStartElement("Data");
                xmlWriter.WriteStartElement("Objects");

                var externalIdTracker = new HashSet<string>();
                var redundantItemTracker = new Dictionary<string, Item>();

                var dsCacheKey = new object();
                var selfAndDescendantsCacheKey = new object();

                long progressValue = 0;

                for (int dataIndex = 0, dataCount = xmlExportData?.Count ?? 0; dataIndex < dataCount; ++dataIndex)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var data = xmlExportData[dataIndex];

                    var items = data.Items;
                    var externalIdPrefix = data.ExternalIdPrefix;
                    var ldiFolderPath = data.LDIFolderPath;

                    var allItems = items.PHs.Concat(items.Items).Prepend(items.Root);
                    var itemCount = items.Items.Count + items.PHs.Count + 1;

                    progressValue = 0;
                    var itemIndex = 0;

                    foreach (var item in allItems)
                    {
                        ++itemIndex;

                        cancellationToken.ThrowIfCancellationRequested();

                        if (item.IsChecked == false || (item.IsChecked == null && item.State == Item.ItemState.HasModifiedDescendants)) continue;

                        var isCompound = !item.IsInstance;

                        var externalId = item.ReusedExternalId ?? $"{externalIdPrefix}{item.BaseExternalId}";

                        if (externalIdTracker.Contains(externalId)) continue;

                        externalIdTracker.Add(externalId);
                        redundantItemTracker.Remove(externalId);

                        var externalIdBase = externalId[externalId.Length - 2] == '_' ? externalId.Substring(0, externalId.Length - 1) : $"{externalId}_";
                        var layoutExternalId = externalId + "l";
                        var prototypeExternalId = externalIdBase + "p";

                        xmlWriter.WriteStartElement(isCompound ? PM_COMPOUND_PART : PM_PART_INSTANCE);
                        xmlWriter.WriteAttributeString("ExternalId", externalId);

                        xmlWriter.WriteStartElement("ActiveInCurrentVersion");
                        xmlWriter.WriteEndElement();

                        xmlWriter.WriteStartElement("name");
                        xmlWriter.WriteString(item.Attributes.Name?.Trim() ?? "");
                        xmlWriter.WriteEndElement();

                        xmlWriter.WriteStartElement("layout");
                        xmlWriter.WriteString(layoutExternalId);
                        xmlWriter.WriteEndElement();

                        if (item == items.Root)
                        {
                            xmlWriter.WriteStartElement("OriginalPartExtId");
                            xmlWriter.WriteString(externalIdPrefix);
                            xmlWriter.WriteEndElement();
                        }

                        if (isCompound)
                        {
                            xmlWriter.WriteStartElement("number");
                            xmlWriter.WriteString(item.Attributes.Number?.Trim() ?? "");
                            xmlWriter.WriteEndElement();


                            xmlWriter.WriteStartElement("TCe_Revision");
                            if (item.Attributes.Version > 0) xmlWriter.WriteString(item.Attributes.Version.ToString());
                            xmlWriter.WriteEndElement();


                            xmlWriter.WriteStartElement("Prefix");
                            xmlWriter.WriteString(item.Attributes.Prefix?.Trim() ?? "");
                            xmlWriter.WriteEndElement();


                            xmlWriter.WriteStartElement("Base");
                            xmlWriter.WriteString(item.Attributes.Base?.Trim() ?? "");
                            xmlWriter.WriteEndElement();


                            xmlWriter.WriteStartElement("Suffix");
                            xmlWriter.WriteString(item.Attributes.Suffix?.Trim() ?? "");
                            xmlWriter.WriteEndElement();


                            xmlWriter.WriteStartElement("Data_Source");
                            xmlWriter.WriteString(item.Attributes.Owner?.Trim() ?? "");
                            xmlWriter.WriteEndElement();


                            xmlWriter.WriteStartElement("children");

                            var children = item.Children.OrderBy(c => c.Attributes.Number).ThenBy(c => c.Attributes.Version);

                            foreach (var childItem in children)
                            {
                                if (childItem.IsChecked == false && childItem.State == Item.ItemState.New) continue;

                                xmlWriter.WriteStartElement("item");
                                xmlWriter.WriteString(childItem.ReusedExternalId ?? $"{externalIdPrefix}{childItem.BaseExternalId}");
                                xmlWriter.WriteEndElement();
                            }

                            xmlWriter.WriteEndElement();
                        }

                        else
                        {
                            xmlWriter.WriteStartElement("prototype");
                            xmlWriter.WriteString(prototypeExternalId);
                            xmlWriter.WriteEndElement();
                        }

                        xmlWriter.WriteEndElement();


                        if (item.RedundantChildren != null)
                        {
                            var redundantItems = item.RedundantChildren.SelectMany(rc => rc.GetSelfAndDescendants(selfAndDescendantsCacheKey))
                                .Where(i => !externalIdTracker.Contains(i.BaseExternalId));

                            foreach (var redundantItem in redundantItems)
                            {
                                redundantItemTracker[redundantItem.BaseExternalId] = redundantItem;
                            }
                        }


                        xmlWriter.WriteStartElement(PM_LAYOUT);
                        xmlWriter.WriteAttributeString("ExternalId", layoutExternalId);

                        xmlWriter.WriteStartElement("location");

                        foreach (var component in item.Attributes.Translation.Items())
                        {
                            xmlWriter.WriteStartElement("item");
                            xmlWriter.WriteString(component.ToString());
                            xmlWriter.WriteEndElement();
                        }

                        xmlWriter.WriteEndElement();


                        xmlWriter.WriteStartElement("rotation");

                        foreach (var component in item.Attributes.Rotation.Items())
                        {
                            xmlWriter.WriteStartElement("item");
                            xmlWriter.WriteString(component.ToString());
                            xmlWriter.WriteEndElement();
                        }

                        xmlWriter.WriteEndElement();


                        xmlWriter.WriteEndElement();


                        if (!isCompound)
                        {
                            var jtPath = item.Attributes.FilePath;

                            string jtPathHash;
                            bool newJTPathHash;

                            if (filePathTracker.ContainsKey(jtPath))
                            {
                                jtPathHash = filePathTracker[jtPath];
                                newJTPathHash = false;
                            }

                            else
                            {
                                jtPathHash = BitConverter.ToString(StaticResources.SHA256.ComputeHash(Encoding.UTF8.GetBytes(jtPath))).Replace("-", "");
                                filePathTracker[jtPath] = jtPathHash;
                                newJTPathHash = true;
                            }

                            var threeDRepExternalId = externalIdBase + "r";
                            var fileReferenceExternalId = jtPathHash + "_f";

                            xmlWriter.WriteStartElement(PM_PART_PROTOTYPE);
                            xmlWriter.WriteAttributeString("ExternalId", prototypeExternalId);

                            xmlWriter.WriteStartElement("catalogNumber");
                            xmlWriter.WriteString(item.Attributes.Number?.Trim() ?? "");
                            xmlWriter.WriteEndElement();

                            xmlWriter.WriteStartElement("name");
                            xmlWriter.WriteString(item.Attributes.Name?.Trim() ?? "");
                            xmlWriter.WriteEndElement();


                            xmlWriter.WriteStartElement("TCe_Revision");
                            if (item.Attributes.Version > 0) xmlWriter.WriteString(item.Attributes.Version.ToString());
                            xmlWriter.WriteEndElement();


                            xmlWriter.WriteStartElement("Prefix");
                            xmlWriter.WriteString(item.Attributes.Prefix?.Trim() ?? "");
                            xmlWriter.WriteEndElement();


                            xmlWriter.WriteStartElement("Base");
                            xmlWriter.WriteString(item.Attributes.Base?.Trim() ?? "");
                            xmlWriter.WriteEndElement();

                            xmlWriter.WriteStartElement("Suffix");
                            xmlWriter.WriteString(item.Attributes.Suffix?.Trim() ?? "");
                            xmlWriter.WriteEndElement();

                            xmlWriter.WriteStartElement("Data_Source");
                            xmlWriter.WriteString(item.Attributes.Owner?.Trim() ?? "");
                            xmlWriter.WriteEndElement();

                            xmlWriter.WriteStartElement("material");
                            xmlWriter.WriteString(item.Attributes.Material?.Trim() ?? "");
                            xmlWriter.WriteEndElement();

                            xmlWriter.WriteStartElement("threeDRep");
                            xmlWriter.WriteString(threeDRepExternalId);
                            xmlWriter.WriteEndElement();

                            xmlWriter.WriteEndElement();



                            xmlWriter.WriteStartElement("Pm3DRep");
                            xmlWriter.WriteAttributeString("ExternalId", threeDRepExternalId);

                            xmlWriter.WriteStartElement("file");
                            xmlWriter.WriteString(fileReferenceExternalId);
                            xmlWriter.WriteEndElement();

                            xmlWriter.WriteEndElement();


                            if (newJTPathHash)
                            {
                                xmlWriter.WriteStartElement("PmReferenceFile");
                                xmlWriter.WriteAttributeString("ExternalId", fileReferenceExternalId);

                                xmlWriter.WriteStartElement("fileName");
                                xmlWriter.WriteString(jtPath);
                                xmlWriter.WriteEndElement();

                                xmlWriter.WriteEndElement();
                            }
                        }

                        var writingProgress = dataIndex * (PROGRESS_MAX / dataCount) + itemIndex * (PROGRESS_MAX / dataCount) / itemCount;
                        var newProgressValue = writingProgress / 2;

                        if (newProgressValue > progressValue)
                        {
                            progressValue = newProgressValue;

                            progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue, Message = $"Writing tree data to XML: {((double)writingProgress / PROGRESS_MAX):P0}" });
                        }
                    }
                }

                var progressSoFar = PROGRESS_MAX / 2;



                /**
                 * Writes redundant items to XML while marking them OLD_LEVEL
                 */

                var redundantItemCount = redundantItemTracker.Count;
                var redundantItemNumber = 0;

                progressValue = 0;

                foreach (var redundantItem in redundantItemTracker.Values)
                {
                    xmlWriter.WriteStartElement(redundantItem.IsInstance ? PM_PART_INSTANCE : PM_COMPOUND_PART);
                    xmlWriter.WriteAttributeString("ExternalId", redundantItem.BaseExternalId);

                    xmlWriter.WriteStartElement("ActiveInCurrentVersion");
                    xmlWriter.WriteString("OLD_LEVEL");
                    xmlWriter.WriteEndElement();

                    xmlWriter.WriteEndElement();

                    var newProgressValue = progressSoFar + ++redundantItemNumber * PROGRESS_MAX / redundantItemCount / 2;

                    if (newProgressValue > progressValue)
                    {
                        progressValue = newProgressValue;

                        progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue, Message = $"Writing OLD_LEVEL data to XML: {((double)redundantItemNumber / redundantItemCount):P0}" });
                    }
                }

                xmlWriter.WriteEndDocument();

                progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = PROGRESS_MAX, Message = $"Writing XML is done" });

            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public static async Task<Dictionary<string, Item>> ReadExistingData(string existingDataXMLPath, IProgress<ProgressUpdate> progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(existingDataXMLPath)) return default;

            using (var fileStream = new FileStream(existingDataXMLPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return await ReadExistingData(fileStream, progress, cancellationToken);
            }
        }

        private static async Task<Dictionary<string, Item>> ReadExistingData(Stream existingDataXMLStream, IProgress<ProgressUpdate> progress = default, CancellationToken cancellationToken = default)
        {
            return await Task.Factory.StartNew(() =>
            {
                var itemDictionary = new Dictionary<string, Item>();
                var externalIdPrefix = "";

                var streamLength = existingDataXMLStream.Length;
                long progressValue = 0;

                var childIdTracker = new Dictionary<Item, List<string>>();
                var layoutIdTracker = new Dictionary<Item, string>();
                var prototypeIdTracker = new Dictionary<Item, string>();
                var threeDRepIdTracker = new Dictionary<Item, string>();
                var fileIdTracker = new Dictionary<string, string>();

                var translationTracker = new Dictionary<string, Vector3D>();
                var rotationTracker = new Dictionary<string, Vector3D>();
                var prototypeDataTracker = new Dictionary<string, Item>();
                var fileDataTracker = new Dictionary<string, string>();

                using (var xmlReader = XmlReader.Create(existingDataXMLStream))
                {
                    while (xmlReader.Read())
                    {
                        if (cancellationToken.IsCancellationRequested) return default;

                        if (xmlReader.NodeType != XmlNodeType.Element) continue;

                        var elementName = xmlReader.Name;

                        if (elementName == PM_COMPOUND_PART || elementName == PM_PART_INSTANCE || elementName == PM_LAYOUT ||
                            elementName == PM_PART_PROTOTYPE || elementName == PM_3D_REP || elementName == PM_REFERENCE_FILE)
                        {
                            var element = XElement.Parse(xmlReader.ReadOuterXml());
                            var externalId = element?.Attribute("ExternalId")?.Value?.Trim() ?? "";

                            if (externalId.Length == 0) continue;

                            if (elementName == PM_COMPOUND_PART || elementName == PM_PART_INSTANCE)
                            {
                                var item = new Item
                                {
                                    BaseExternalId = externalId,
                                    Attributes = new ItemAttributes { Name = element.Element("name")?.Value },
                                    State = Item.ItemState.Redundant,
                                    IsInstance = elementName == PM_PART_INSTANCE
                                };

                                itemDictionary[externalId] = item;

                                var layoutId = element.Element("layout")?.Value?.Trim() ?? "";

                                if (layoutId.Length > 0) layoutIdTracker[item] = layoutId;

                                if (string.IsNullOrWhiteSpace(externalIdPrefix)) externalIdPrefix = element.Element("OriginalPartExtId")?.Value;

                                if (elementName == PM_COMPOUND_PART)
                                {
                                    var childIds = element.Element("children")?.Elements("item").Select(e => e.Value?.Trim() ?? "").Where(v => v.Length > 0).ToList();

                                    if ((childIds?.Count ?? 0) > 0) childIdTracker[item] = childIds;

                                    item.Attributes.Name = element.Element("name")?.Value;
                                    item.Attributes.Number = element.Element("number")?.Value;
                                    item.Attributes.Version = double.TryParse(element.Element("TCe_Revision")?.Value, out var version) ? version : 0;
                                    item.Attributes.Owner = element.Element("Data_Source")?.Value;
                                    item.Attributes.Prefix = element.Element("Prefix")?.Value;
                                    item.Attributes.Base = element.Element("Base")?.Value;
                                    item.Attributes.Suffix = element.Element("Suffix")?.Value;
                                }

                                else
                                {
                                    var prototypeId = element.Element("prototype")?.Value?.Trim() ?? "";
                                    if (prototypeId.Length > 0) prototypeIdTracker[item] = prototypeId;
                                }
                            }

                            else if (elementName == PM_LAYOUT)
                            {
                                var translation = Vector3D.Parse(string.Join(",", element.Element("location")?.Elements("item").Select(e => e.Value) ?? Enumerable.Empty<string>()));
                                var rotation = Vector3D.Parse(string.Join(",", element.Element("rotation")?.Elements("item").Select(e => e.Value) ?? Enumerable.Empty<string>()));

                                if (translation != default) translationTracker[externalId] = translation;
                                if (rotation != default) rotationTracker[externalId] = rotation;
                            }

                            else if (elementName == PM_PART_PROTOTYPE)
                            {
                                var prototypeItem = new Item
                                {
                                    Attributes = new ItemAttributes
                                    {
                                        Name = element.Element("name")?.Value,
                                        Number = element.Element("catalogNumber")?.Value,
                                        Version = double.TryParse(element.Element("TCe_Revision")?.Value, out var version) ? version : 0,
                                        Owner = element.Element("Data_Source")?.Value,
                                        Prefix = element.Element("Prefix")?.Value,
                                        Base = element.Element("Base")?.Value,
                                        Suffix = element.Element("Suffix")?.Value,
                                        Material = element.Element("material")?.Value
                                    }
                                };

                                prototypeDataTracker[externalId] = prototypeItem;

                                var threeDRepId = element.Element("threeDRep")?.Value?.Trim() ?? "";
                                if (threeDRepId.Length > 0) threeDRepIdTracker[prototypeItem] = threeDRepId;
                            }

                            else if (elementName == PM_3D_REP)
                            {
                                var fileId = element.Element("file")?.Value?.Trim() ?? "";
                                if (fileId.Length > 0) fileIdTracker[externalId] = fileId;
                            }

                            else if (elementName == PM_REFERENCE_FILE)
                            {
                                var fileName = element.Element("fileName")?.Value?.Trim() ?? "";
                                if (fileName.Length > 0) fileDataTracker[externalId] = fileName;
                            }
                        }

                        if (progress != null)
                        {
                            var overallProgress = existingDataXMLStream.Position * PROGRESS_MAX / streamLength / 2;

                            if (overallProgress > progressValue)
                            {
                                progressValue = overallProgress;

                                progress.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue, Message = $"Reading existing data: {((double)existingDataXMLStream.Position / streamLength):P0}" });
                            }
                        }
                    }
                }

                var progressSoFar = progressValue;

                var counter = 0;
                var itemCount = itemDictionary.Count;

                foreach (var item in itemDictionary.Values)
                {
                    if (cancellationToken.IsCancellationRequested) return default;

                    var children = childIdTracker.TryGetValue(item, out var childIds) ?
                        childIds.Select(id => itemDictionary.TryGetValue(id, out var childItem) ? childItem : null).Where(i => i != null).Select(i =>
                        {
                            i.Parent = item;

                            return i;
                        }) :
                        null;

                    if (children != null) item.Children.AddRange(children);

                    if (layoutIdTracker.TryGetValue(item, out var layoutId))
                    {
                        if (translationTracker.TryGetValue(layoutId, out var translation)) item.Attributes.Translation = translation;
                        if (rotationTracker.TryGetValue(layoutId, out var rotation)) item.Attributes.Rotation = rotation;
                    }

                    if (prototypeIdTracker.TryGetValue(item, out var prototypeId) && prototypeDataTracker.TryGetValue(prototypeId, out var prototypeData))
                    {
                        item.Attributes.Name = string.IsNullOrWhiteSpace(item.Attributes.Name) ? prototypeData.Attributes.Name : item.Attributes.Name;
                        item.Attributes.Number = prototypeData.Attributes.Number;
                        item.Attributes.Version = prototypeData.Attributes.Version;
                        item.Attributes.Owner = prototypeData.Attributes.Owner;
                        item.Attributes.Prefix = prototypeData.Attributes.Prefix;
                        item.Attributes.Base = prototypeData.Attributes.Base;
                        item.Attributes.Suffix = prototypeData.Attributes.Suffix;

                        if (threeDRepIdTracker.TryGetValue(prototypeData, out var threeDRepId) &&
                            fileIdTracker.TryGetValue(threeDRepId, out var fileId) &&
                            fileDataTracker.TryGetValue(fileId, out var filePath))
                        {
                            item.Attributes.FilePath = filePath;
                        }
                    }

                    if (progress != null)
                    {
                        var overallProgress = progressSoFar + ++counter * PROGRESS_MAX / itemCount / 2;

                        if (overallProgress > progressValue)
                        {
                            progressValue = overallProgress;

                            progress.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue, Message = $"Assembling structure: {((double)counter / itemCount):P0}" });
                        }
                    }
                }

                progress?.Report(new ProgressUpdate
                {
                    Max = PROGRESS_MAX,
                    Value = PROGRESS_MAX,
                    Message = "Done" + (string.IsNullOrWhiteSpace(externalIdPrefix) ? "" : $". ExternalId prefix: {externalIdPrefix}")
                });

                return itemDictionary;
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }
}
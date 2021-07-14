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

        public static async Task ItemsToXML(string xmlPath, IReadOnlyList<XMLExportData> xmlExportData, IProgress<ProgressUpdate> progress = default, CancellationToken cancellationToken = default)
        {
            using (var fileStream = new FileStream(xmlPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
            using (var streamWriter = new StreamWriter(fileStream))
            using (var xmlWriter = XmlWriter.Create(streamWriter, new XmlWriterSettings()
            {
                Indent = true,
                NewLineChars = "\n",
                IndentChars = "\t"
            }))
            {
                try
                {
                    await ItemsToXML(xmlWriter, xmlExportData, progress, cancellationToken);
                }

                finally
                {
                    fileStream?.SetLength(fileStream?.Position ?? 0); // Trunkates existing file
                }
            }
        }

        private static async Task ItemsToXML(XmlWriter xmlWriter, IReadOnlyList<XMLExportData> xmlExportData, IProgress<ProgressUpdate> progress = default, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Factory.StartNew(() =>
            {
                var filePathTracker = new Dictionary<string, string>();
                
                xmlWriter.WriteStartDocument(true);
                xmlWriter.WriteStartElement("Data");
                xmlWriter.WriteStartElement("Objects");

                var externalIdTracker = new HashSet<string>();

                for (int dataIndex = 0, dataCount = xmlExportData?.Count ?? 0; dataIndex < dataCount; ++dataIndex)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var data = xmlExportData[dataIndex];

                    var items = data.Items;
                    var externalIdPrefix = data.ExternalIdPrefix;
                    var ldiFolderPath = data.LDIFolderPath;

                    var allItems = items.PHs.Concat(items.Items).Prepend(items.Root);
                    var itemCount = items.Items.Count + items.PHs.Count + 1;

                    long progressValue = 0;
                    var itemIndex = 0;

                    foreach (var item in allItems)
                    {
                        ++itemIndex;

                        cancellationToken.ThrowIfCancellationRequested();

                        if (item.IsChecked == false || (item.IsChecked == null && item.State == Item.ItemState.HasModifiedDescendants)) continue;

                        var childCount = item.Children.Count;

                        var isCompound = childCount > 0;

                        var externalId = $"{externalIdPrefix}{item.BaseExternalId}";

                        if (externalIdTracker.Contains(externalId)) continue;

                        externalIdTracker.Add(externalId);

                        var externlaIdBase = externalId.Substring(0, externalId.Length - 1);
                        var layoutExternalId = externalId + "l";
                        var prototypeExternalId = externlaIdBase + "p";

                        xmlWriter.WriteStartElement(isCompound ? "PmCompoundPart" : "PmPartInstance");
                        xmlWriter.WriteAttributeString("ExternalId", externalId);

                        xmlWriter.WriteStartElement("ActiveInCurrentVersion");
                        xmlWriter.WriteEndElement();

                        xmlWriter.WriteStartElement("name");
                        xmlWriter.WriteString(item.Name?.Trim() ?? "");
                        xmlWriter.WriteEndElement();

                        xmlWriter.WriteStartElement("layout");
                        xmlWriter.WriteString(layoutExternalId);
                        xmlWriter.WriteEndElement();

                        if (isCompound)
                        {
                            xmlWriter.WriteStartElement("number");
                            xmlWriter.WriteString(item.Number?.Trim() ?? "");
                            xmlWriter.WriteEndElement();

                            if (item.Version > 0)
                            {
                                xmlWriter.WriteStartElement("TCe_Revision");
                                xmlWriter.WriteString(item.Version.ToString());
                                xmlWriter.WriteEndElement();
                            }

                            if (!string.IsNullOrWhiteSpace(item.Prefix))
                            {
                                xmlWriter.WriteStartElement("Prefix");
                                xmlWriter.WriteString(item.Prefix);
                                xmlWriter.WriteEndElement();
                            }

                            if (!string.IsNullOrWhiteSpace(item.Base))
                            {
                                xmlWriter.WriteStartElement("Base");
                                xmlWriter.WriteString(item.Base);
                                xmlWriter.WriteEndElement();
                            }

                            if (!string.IsNullOrWhiteSpace(item.Suffix))
                            {
                                xmlWriter.WriteStartElement("Suffix");
                                xmlWriter.WriteString(item.Suffix);
                                xmlWriter.WriteEndElement();
                            }

                            if (!string.IsNullOrWhiteSpace(item.Owner))
                            {
                                xmlWriter.WriteStartElement("Data_Source");
                                xmlWriter.WriteString(item.Owner);
                                xmlWriter.WriteEndElement();
                            }


                            xmlWriter.WriteStartElement("children");

                            foreach (var childItem in item.Children.OrderBy(c => c.Number).ThenBy(c => c.Version))
                            {
                                if (childItem.IsChecked == false && childItem.State == Item.ItemState.New) continue;

                                xmlWriter.WriteStartElement("item");
                                xmlWriter.WriteString($"{externalIdPrefix}{childItem.BaseExternalId}");
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
                            var redundantItems = item.RedundantChildren.SelectMany(rc => rc.GetSelfAndDescendants());

                            foreach (var redundantItem in redundantItems)
                            {
                                xmlWriter.WriteStartElement(redundantItem.Children.Count > 0 ? "PmCompoundPart" : "PmPartInstance");
                                xmlWriter.WriteAttributeString("ExternalId", redundantItem.BaseExternalId);

                                xmlWriter.WriteStartElement("ActiveInCurrentVersion");
                                xmlWriter.WriteString("OLD_LEVEL");
                                xmlWriter.WriteEndElement();

                                xmlWriter.WriteEndElement();
                            }
                        }


                        xmlWriter.WriteStartElement("PmLayout");
                        xmlWriter.WriteAttributeString("ExternalId", layoutExternalId);

                        xmlWriter.WriteStartElement("location");

                        foreach (var component in item.Translation.Items())
                        {
                            xmlWriter.WriteStartElement("item");
                            xmlWriter.WriteString(component.ToString());
                            xmlWriter.WriteEndElement();
                        }

                        xmlWriter.WriteEndElement();


                        xmlWriter.WriteStartElement("rotation");

                        foreach (var component in item.Rotation.Items())
                        {
                            xmlWriter.WriteStartElement("item");
                            xmlWriter.WriteString(component.ToString());
                            xmlWriter.WriteEndElement();
                        }

                        xmlWriter.WriteEndElement();


                        xmlWriter.WriteEndElement();


                        if (!isCompound)
                        {
                            var ds = item.GetDS();

                            var jtPath = Path.Combine(ldiFolderPath, $"{ds.Number}_{ds.Version}__".GetSafeFileName(), $"{item.Number}.jt".GetSafeFileName());
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

                            var threeDRepExternalId = externlaIdBase + "r";
                            var fileReferenceExternalId = jtPathHash + "_f";

                            xmlWriter.WriteStartElement("PmPartPrototype");
                            xmlWriter.WriteAttributeString("ExternalId", prototypeExternalId);

                            xmlWriter.WriteStartElement("catalogNumber");
                            xmlWriter.WriteString(item.Number?.Trim() ?? "");
                            xmlWriter.WriteEndElement();

                            xmlWriter.WriteStartElement("name");
                            xmlWriter.WriteString(item.Name?.Trim() ?? "");
                            xmlWriter.WriteEndElement();

                            if (item.Version > 0)
                            {
                                xmlWriter.WriteStartElement("TCe_Revision");
                                xmlWriter.WriteString(item.Version.ToString());
                                xmlWriter.WriteEndElement();
                            }

                            if (!string.IsNullOrWhiteSpace(item.Prefix))
                            {
                                xmlWriter.WriteStartElement("Prefix");
                                xmlWriter.WriteString(item.Prefix);
                                xmlWriter.WriteEndElement();
                            }

                            if (!string.IsNullOrWhiteSpace(item.Base))
                            {
                                xmlWriter.WriteStartElement("Base");
                                xmlWriter.WriteString(item.Base);
                                xmlWriter.WriteEndElement();
                            }

                            if (!string.IsNullOrWhiteSpace(item.Suffix))
                            {
                                xmlWriter.WriteStartElement("Suffix");
                                xmlWriter.WriteString(item.Suffix);
                                xmlWriter.WriteEndElement();
                            }

                            if (!string.IsNullOrWhiteSpace(item.Owner))
                            {
                                xmlWriter.WriteStartElement("Data_Source");
                                xmlWriter.WriteString(item.Owner);
                                xmlWriter.WriteEndElement();
                            }

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

                        var newProgressValue = dataIndex * (PROGRESS_MAX / dataCount) +  itemIndex * (PROGRESS_MAX / dataCount) / itemCount;

                        if (newProgressValue > progressValue)
                        {
                            progressValue = newProgressValue;

                            progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue, Message = $"Writing XML: {((double)newProgressValue / PROGRESS_MAX):P0}" });
                        }
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
                var items = new Dictionary<string, Item>();
                var externalIdPrefix = "";

                var streamLength = existingDataXMLStream.Length;
                long progressValue = 0;

                var childIdTracker = new Dictionary<Item, List<string>>();
                var layoutIdTracker = new Dictionary<Item, string>();
                var prototypeIdTracker = new Dictionary<Item, string>();

                var translationTracker = new Dictionary<string, Vector3D>();
                var rotationTracker = new Dictionary<string, Vector3D>();
                var prototypeDataTracker = new Dictionary<string, Item>();

                using (var xmlReader = XmlReader.Create(existingDataXMLStream))
                {
                    while (xmlReader.Read())
                    {
                        if (cancellationToken.IsCancellationRequested) return default;

                        if (xmlReader.NodeType != XmlNodeType.Element) continue;

                        var elementName = xmlReader.Name;

                        if (elementName == "PmCompoundPart" || elementName == "PmPartInstance" || elementName == "PmLayout" || elementName == "PmPartPrototype")
                        {
                            var element = XElement.Parse(xmlReader.ReadOuterXml());
                            var externalId = element?.Attribute("ExternalId")?.Value?.Trim() ?? "";

                            if (externalId.Length == 0) continue;

                            if (elementName == "PmCompoundPart" || elementName == "PmPartInstance")
                            {
                                var item = new Item
                                {
                                    BaseExternalId = externalId,
                                    Name = element.Element("name")?.Value,
                                    State = Item.ItemState.Redundant
                                };

                                items[externalId] = item;

                                var layoutId = element.Element("layout")?.Value?.Trim() ?? "";

                                if (layoutId.Length > 0) layoutIdTracker[item] = layoutId;

                                if (elementName == "PmCompoundPart")
                                {
                                    var childIds = element.Element("children")?.Elements("item").Select(e => e.Value?.Trim() ?? "").Where(v => v.Length > 0).ToList();

                                    if ((childIds?.Count ?? 0) > 0) childIdTracker[item] = childIds;

                                    item.Name = element.Element("name")?.Value;
                                    item.Number = element.Element("number")?.Value;
                                    item.Version = double.TryParse(element.Element("TCe_Revision")?.Value, out var version) ? version : 0;
                                    item.Owner = element.Element("Data_Source")?.Value;
                                    item.Prefix = element.Element("Prefix")?.Value;
                                    item.Base = element.Element("Base")?.Value;
                                    item.Suffix = element.Element("Suffix")?.Value;
                                }

                                else
                                {
                                    var prototypeId = element.Element("prototype")?.Value?.Trim() ?? "";
                                    if (prototypeId.Length > 0) prototypeIdTracker[item] = prototypeId;
                                }
                            }

                            else if (elementName == "PmLayout")
                            {
                                var translation = Vector3D.Parse(string.Join(",", element.Element("location")?.Elements("item").Select(e => e.Value) ?? Enumerable.Empty<string>()));
                                var rotation = Vector3D.Parse(string.Join(",", element.Element("rotation")?.Elements("item").Select(e => e.Value) ?? Enumerable.Empty<string>()));

                                if (translation != default) translationTracker[externalId] = translation;
                                if (rotation != default) rotationTracker[externalId] = rotation;
                            }

                            else
                            {
                                prototypeDataTracker[externalId] = new Item
                                {
                                    Name = element.Element("name")?.Value,
                                    Number = element.Element("catalogNumber")?.Value,
                                    Version = double.TryParse(element.Element("TCe_Revision")?.Value, out var version) ? version : 0,
                                    Owner = element.Element("Data_Source")?.Value,
                                    Prefix = element.Element("Prefix")?.Value,
                                    Base = element.Element("Base")?.Value,
                                    Suffix = element.Element("Suffix")?.Value
                                };
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
                var itemCount = items.Count;

                foreach (var item in items.Values)
                {
                    if (cancellationToken.IsCancellationRequested) return default;

                    var children = childIdTracker.TryGetValue(item, out var childIds) ?
                        childIds.Select(id => items.TryGetValue(id, out var childItem) ? childItem : null).Where(i => i != null).Select(i =>
                        {
                            i.Parent = item;

                            return i;
                        }) :
                        null;

                    if (children != null) item.Children.AddRange(children);

                    if (layoutIdTracker.TryGetValue(item, out var layoutId))
                    {
                        if (translationTracker.TryGetValue(layoutId, out var translation)) item.Translation = translation;
                        if (rotationTracker.TryGetValue(layoutId, out var rotation)) item.Rotation = rotation;
                    }

                    if (prototypeIdTracker.TryGetValue(item, out var prototypeId) && prototypeDataTracker.TryGetValue(prototypeId, out var prototypeData))
                    {
                        if (string.IsNullOrWhiteSpace(item.Name)) item.Name = prototypeData.Name;
                        item.Number = prototypeData.Number;
                        item.Version = prototypeData.Version;
                        item.Owner = prototypeData.Owner;
                        item.Prefix = prototypeData.Prefix;
                        item.Base = prototypeData.Base;
                        item.Suffix = prototypeData.Suffix;

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

                return items;
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }
}
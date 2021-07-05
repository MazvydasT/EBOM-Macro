using EBOM_Macro.Extensions;
using EBOM_Macro.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace EBOM_Macro.Managers
{
    public static class XMLManager
    {
        private const long PROGRESS_MAX = 300;

        public static async Task ItemsToXML(string xmlPath, ItemsContainer items, string externalIdPrefix, string existingDataExternalIdPrefix, string ldiFolderPath, IProgress<ProgressUpdate> progress = default, CancellationToken cancellationToken = default)
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
                    await ItemsToXML(xmlWriter, items, externalIdPrefix, existingDataExternalIdPrefix, ldiFolderPath, progress, cancellationToken);
                }

                finally
                {
                    fileStream?.SetLength(fileStream?.Position ?? 0); // Trunkates existing file
                }
            }
        }

        private static async Task ItemsToXML(XmlWriter xmlWriter, ItemsContainer items, string externalIdPrefix, string existingDataExternalIdPrefix, string ldiFolderPath, IProgress<ProgressUpdate> progress = default, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Factory.StartNew(() =>
            {
                var filePathTracker = new Dictionary<string, string>();

                long progressValue = 0;
                var itemIndex = 0;

                xmlWriter.WriteStartDocument(true);
                xmlWriter.WriteStartElement("Data");
                xmlWriter.WriteStartElement("Objects");

                var allItems = items.PHs.Concat(items.Items).Prepend(items.Root);
                var itemCount = items.Items.Count + items.PHs.Count + 1;

                foreach (var item in allItems)
                {
                    ++itemIndex;

                    cancellationToken.ThrowIfCancellationRequested();

                    //if (item.State == Item.ItemState.Unchanged /*|| item.State == Item.ItemState.UnchangedWithHierarchy*/) continue;
                    if (item.IsChecked == false || (item.IsChecked == null && item.State == Item.ItemState.HasModifiedDescendants)) continue;

                    var childCount = item.Children.Count;

                    var isCompound = childCount > 0;

                    var externalId = $"{externalIdPrefix}{item.BaseExternalId}";
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

                        for (var childIndex = 0; childIndex < childCount; ++childIndex)
                        {
                            xmlWriter.WriteStartElement("item");
                            xmlWriter.WriteString($"{externalIdPrefix}{item.Children[childIndex].BaseExternalId}");
                            xmlWriter.WriteEndElement();
                        }

                        xmlWriter.WriteEndElement();

                        if(item.RedundantChildren != null)
                        {
                            foreach(var redundantItem in item.RedundantChildren)
                            {
                                xmlWriter.WriteStartElement(redundantItem.Children.Count > 0 ? "PmCompoundPart" : "PmPartInstance");
                                xmlWriter.WriteAttributeString("ExternalId", $"{existingDataExternalIdPrefix}{redundantItem.BaseExternalId}");

                                xmlWriter.WriteStartElement("ActiveInCurrentVersion");
                                xmlWriter.WriteString("OLD_LEVEL");
                                xmlWriter.WriteEndElement();

                                xmlWriter.WriteEndElement();
                            }
                        }
                    }

                    else
                    {
                        xmlWriter.WriteStartElement("prototype");
                        xmlWriter.WriteString(prototypeExternalId);
                        xmlWriter.WriteEndElement();
                    }

                    xmlWriter.WriteEndElement();


                    xmlWriter.WriteStartElement("PmLayout");
                    xmlWriter.WriteAttributeString("ExternalId", layoutExternalId);

                    var transformation = item.LocalTransformation;

                    xmlWriter.WriteStartElement("location");

                    foreach (var component in transformation.GetTranslation().Items())
                    {
                        xmlWriter.WriteStartElement("item");
                        xmlWriter.WriteString((component * 1000.0).ToString());
                        xmlWriter.WriteEndElement();
                    }

                    xmlWriter.WriteEndElement();


                    xmlWriter.WriteStartElement("rotation");

                    foreach (var component in transformation.GetEulerZYX().Items())
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

                        var jtPath = Path.Combine(ldiFolderPath, $"{ds.Number}_{ds.Version}__", $"{item.Number}.jt");
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

                    var newProgressValue = itemIndex * PROGRESS_MAX / itemCount;

                    if (newProgressValue > progressValue)
                    {
                        progressValue = newProgressValue;

                        progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue, Message = $"Writing XML: {((double)newProgressValue / PROGRESS_MAX):P0}" });
                    }
                }

                xmlWriter.WriteEndDocument();

                progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = PROGRESS_MAX, Message = $"Writing XML is done" });

            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }
}
using EBOM_Macro.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EBOM_Macro.Managers
{
    public static class ItemManager
    {
        const long PROGRESS_MAX = 300;

        public static void ResetItemSelection(Item item)
        {
            if (item == null) return;

            item.IsChecked = false;

            var items = item.GetSelfAndDescendants();

            foreach (var i in items)
            {
                if (i.Maturity == EBOMReportRecord.MaturityState.IN_WORK || i.State == Item.ItemState.New || i.State == Item.ItemState.Modified)
                    i.SelectWithoutDescendants.Execute(null);
            }
        }

        public static async Task<ItemsContainer> SetStatus(ItemsContainer items, Dictionary<string, Item> existingData, string externalIdPrefix, bool reuseExternalIds, IProgress<ProgressUpdate> progress = null, CancellationToken cancellationToken = default)
        {
            progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = 0 });

            if (items.Root == null)
            {
                progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = PROGRESS_MAX });
                return default;
            }

            if (cancellationToken.IsCancellationRequested) return items;

            return await Task.Factory.StartNew(() =>
            {
                var allItems = items.PHs.Concat(items.Items).Prepend(items.Root);

                var allItemCount = items.PHs.Count + items.Items.Count + 1;
                var itemCounter = 0;

                long progressValue = 0, progressSoFar = 0;

                var numberLookup = existingData?.Values.ToLookup(i => i.Attributes.Number);

                var ancestorCacheKey = new object();
                var selfAndDescendantsCacheKey = new object();
                var dsCacheKey = new object();

                var previouslyMatchedExternalIds = new HashSet<string>();

                /**
                 * Checks if new item ExternalIDs exists within existing data set,
                 * if it does, it get recorded so it does not get assigned in ExternalID matching stage.
                 */

                if (reuseExternalIds && existingData != null)
                {
                    foreach (var item in allItems)
                    {
                        if (cancellationToken.IsCancellationRequested) return items;

                        var itemExternalId = $"{externalIdPrefix}{item.BaseExternalId}";

                        if (existingData.ContainsKey(itemExternalId)) previouslyMatchedExternalIds.Add(itemExternalId);

                        if (progress != null)
                        {
                            var comparisonProgress = ++itemCounter * PROGRESS_MAX / allItemCount / 4;

                            if (comparisonProgress > progressValue)
                            {
                                progressValue = comparisonProgress;

                                progress.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue });
                            }
                        }
                    }
                }

                progressSoFar = PROGRESS_MAX / 4;
                progressValue = 0;
                itemCounter = 0;


                /**
                 * Matches new items to existing ExternalIDs by part number, location and rotation.
                 */

                foreach (var item in allItems)
                {
                    if (cancellationToken.IsCancellationRequested) return items;

                    if (progress != null)
                    {
                        var comparisonProgress = progressSoFar + ++itemCounter * PROGRESS_MAX / allItemCount / 4;

                        if (comparisonProgress > progressValue)
                        {
                            progressValue = comparisonProgress;

                            progress.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue });
                        }
                    }

                    item.ReusedExternalId = null;

                    if (item.Type == Item.ItemType.PH || !reuseExternalIds || existingData == null) continue;

                    var itemExternalId = $"{externalIdPrefix}{item.BaseExternalId}";

                    if (!existingData.ContainsKey(itemExternalId))
                    {
                        var matchedNumbers = numberLookup[item.Attributes.Number]
                            .Where(i => !previouslyMatchedExternalIds.Contains(i.BaseExternalId) && item.IsInstance == i.IsInstance)
                            .ToList();

                        if (item.Type == Item.ItemType.DS)
                        {
                            if (matchedNumbers.Count == 1)
                            {
                                item.ReusedExternalId = matchedNumbers[0].BaseExternalId;
                                previouslyMatchedExternalIds.Add(item.ReusedExternalId);
                            }
                        }

                        else // Item is instance or sub DS assembly
                        {
                            var repeatExpanded = false;

                            var matchedInstances = matchedNumbers.Where(i => i.Parent.BaseExternalId == (item.Parent.ReusedExternalId ?? $"{externalIdPrefix}{item.Parent.BaseExternalId}")).ToList();
                            var siblings = item.Parent.Children.Where(i => !previouslyMatchedExternalIds.Contains(i.ReusedExternalId ?? $"{externalIdPrefix}{i.BaseExternalId}") && i.Attributes.Number == item.Attributes.Number).ToList();

                            if (matchedInstances.Count == 1 && siblings.Count == 1)
                            {
                                item.ReusedExternalId = matchedInstances[0].BaseExternalId;
                                previouslyMatchedExternalIds.Add(item.ReusedExternalId);
                            }

                            else if (matchedInstances.Count > 1 || siblings.Count > 1)
                            {
                                var matchedTransformationInstances = matchedInstances.Where(i => i.Attributes.Translation == item.Attributes.Translation && i.Attributes.Rotation == item.Attributes.Rotation).ToList();

                                var transformationSiblings = siblings.Where(i => i.Attributes.Translation == item.Attributes.Translation && i.Attributes.Rotation == item.Attributes.Rotation).ToList();

                                if (matchedTransformationInstances.Count == 1 && transformationSiblings.Count == 1)
                                {
                                    item.ReusedExternalId = matchedTransformationInstances[0].BaseExternalId;
                                    previouslyMatchedExternalIds.Add(item.ReusedExternalId);
                                }

                                else if (!repeatExpanded && (matchedTransformationInstances.Count > 1 || transformationSiblings.Count > 1))
                                {
                                    var siblingIndex = transformationSiblings.IndexOf(item);

                                    if (siblingIndex < matchedTransformationInstances.Count)
                                    {
                                        item.ReusedExternalId = matchedTransformationInstances[siblingIndex].BaseExternalId;
                                        previouslyMatchedExternalIds.Add(item.ReusedExternalId);
                                    }
                                }
                            }
                        }
                    }
                }

                progressSoFar = progressValue;
                progressValue = 0;
                itemCounter = 0;

                foreach (var item in allItems)
                {
                    if (cancellationToken.IsCancellationRequested) return items;

                    if (progress != null)
                    {
                        var comparisonProgress = progressSoFar + ++itemCounter * PROGRESS_MAX / allItemCount / 4;

                        if (comparisonProgress > progressValue)
                        {
                            progressValue = comparisonProgress;

                            progress.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue });
                        }
                    }

                    item.IsChecked = false;

                    item.State = Item.ItemState.New;
                    item.RedundantChildren = null;
                    item.ChangedAttributes = null;

                    if (existingData != null && existingData.TryGetValue(item.ReusedExternalId ?? $"{externalIdPrefix}{item.BaseExternalId}", out var matchingItem))
                    {
                        item.State = Item.ItemState.Unchanged;

                        var childrenHashSet = item.Children.Select(i => i.ReusedExternalId ?? $"{externalIdPrefix}{i.BaseExternalId}").ToHashSet();
                        var redundantItems = matchingItem.Children.Where(i => !childrenHashSet.Contains(i.BaseExternalId)).ToList();

                        if (redundantItems.Count > 0)
                        {
                            item.State = Item.ItemState.Modified;
                            item.RedundantChildren = redundantItems;
                        }

                        else
                        {
                            var matchingItemChildrenHashSet = matchingItem.Children.Select(i => i.BaseExternalId).ToHashSet();
                            var newItemsCount = item.Children.Where(i => !matchingItemChildrenHashSet.Contains(i.ReusedExternalId ?? $"{externalIdPrefix}{i.BaseExternalId}")).Count();

                            if (newItemsCount > 0)
                            {
                                item.State = Item.ItemState.Modified;
                            }
                        }


                        var differentAttributes = matchingItem.GetDifferentAttributes(item);
                        if (differentAttributes.Count > 0)
                        {
                            item.State = Item.ItemState.Modified;
                            item.ChangedAttributes = differentAttributes;
                        }
                    }
                }

                progressSoFar = progressValue;
                progressValue = 0;
                itemCounter = 0;

                foreach (var item in allItems)
                {
                    if (cancellationToken.IsCancellationRequested) return items;

                    if (progress != null)
                    {
                        var comparisonProgress = progressSoFar + ++itemCounter * PROGRESS_MAX / allItemCount / 4;

                        if (comparisonProgress > progressValue)
                        {
                            progressValue = comparisonProgress;

                            progress.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue });
                        }
                    }

                    if (item.Maturity == EBOMReportRecord.MaturityState.IN_WORK)
                    {
                        item.SelectWithoutDescendants.Execute(null);
                    }

                    if (item.State == Item.ItemState.New || item.State == Item.ItemState.Modified)
                    {
                        item.SelectWithoutDescendants.Execute(null);

                        if (item.Parent?.State == Item.ItemState.Unchanged)
                        {
                            var unchangedAncestors = item.GetAncestors(ancestorCacheKey).Where(i => i.State == Item.ItemState.Unchanged);

                            foreach (var ancestor in unchangedAncestors)
                            {
                                ancestor.State = Item.ItemState.HasModifiedDescendants;
                            }
                        }
                    }
                }

                progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = PROGRESS_MAX });

                return items;

            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public static void SetBaseExternalId(Item item)
        {
            var physicalIds = string.Join("_", item.GetDSToSelfPath().Select(i => i.PhysicalId));
            var data = Encoding.UTF8.GetBytes(physicalIds);
            var hash = StaticResources.SHA256.ComputeHash(data);
            var baseExternalId = BitConverter.ToString(hash).Replace("-", "") + (item.IsInstance ? "_i" : "_c");

            item.BaseExternalId = baseExternalId;
        }
    }
}

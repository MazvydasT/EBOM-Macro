using EBOM_Macro.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

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

                foreach (var item in allItems)
                {
                    if (cancellationToken.IsCancellationRequested) return items;

                    if (progress != null)
                    {
                        var comparisonProgress = ++itemCounter * PROGRESS_MAX / allItemCount / 3;

                        if (comparisonProgress > progressValue)
                        {
                            progressValue = comparisonProgress;

                            progress.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue });
                        }
                    }

                    item.ReusedExternalId = null;

                    if (item.Type == Item.ItemType.PH || !reuseExternalIds || existingData == null) continue;

                    if (!existingData.ContainsKey($"{externalIdPrefix}{item.BaseExternalId}"))
                    {
                        var matchedNumbers = numberLookup[item.Attributes.Number]
                            .Where(i => item.Children.Count > 0 ? i.Children.Count > 0 : i.Children.Count == 0)
                            .ToList();

                        if (item.Type == Item.ItemType.DS)
                        {
                            if (matchedNumbers.Count == 1) item.ReusedExternalId = matchedNumbers[0].BaseExternalId;
                        }

                        else if (item.Type != Item.ItemType.DS) // Item is instance or sub DS assembly
                        {
                            var dsNumber = item.GetDS().Attributes.Number;

                            var matchedInstances = matchedNumbers.Where(i => i.GetAncestors(ancestorCacheKey).Any(a => a.Attributes.Number == dsNumber)).ToList();

                            if (matchedInstances.Count == 1)
                            {
                                item.ReusedExternalId = matchedInstances[0].BaseExternalId;
                            }

                            else if (matchedInstances.Count > 1)
                            {                                
                                matchedInstances = matchedInstances.Where(i => i.Attributes.Translation == item.Attributes.Translation && i.Attributes.Rotation == item.Attributes.Rotation)/*.Where(i =>
                                    Math.Abs(i.Attributes.Translation.X - item.Attributes.Translation.X) < 0.001 &&
                                    Math.Abs(i.Attributes.Translation.Y - item.Attributes.Translation.Y) < 0.001 &&
                                    Math.Abs(i.Attributes.Translation.Z - item.Attributes.Translation.Z) < 0.001 &&
                                    Math.Abs(i.Attributes.Rotation.X - item.Attributes.Rotation.X) < 0.001 &&
                                    Math.Abs(i.Attributes.Rotation.Y - item.Attributes.Rotation.Y) < 0.001 &&
                                    Math.Abs(i.Attributes.Rotation.Z - item.Attributes.Rotation.Z) < 0.001)*/.ToList();

                                if (matchedInstances.Count == 1) item.ReusedExternalId = matchedInstances[0].BaseExternalId;
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
                        var comparisonProgress = progressSoFar + ++itemCounter * PROGRESS_MAX / allItemCount / 3;

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

                            if (newItemsCount > 0) item.State = Item.ItemState.Modified;
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
                        var comparisonProgress = progressSoFar + ++itemCounter * PROGRESS_MAX / allItemCount / 3;

                        if (comparisonProgress > progressValue)
                        {
                            progressValue = comparisonProgress;

                            progress.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue });
                        }
                    }

                    if(item.Maturity == EBOMReportRecord.MaturityState.IN_WORK)
                    {
                        item.SelectWithoutDescendants.Execute(null);
                    }

                    if (item.State == Item.ItemState.New || item.State == Item.ItemState.Modified)
                    {
                        item.SelectWithoutDescendants.Execute(null);

                        if (item.Parent?.State == Item.ItemState.Unchanged)
                        {
                            var unchangedAncestors = item.GetAncestors().Where(i => i.State == Item.ItemState.Unchanged);

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
            var baseExternalId = BitConverter.ToString(hash).Replace("-", "") + (item.Children.Count > 0 ? "_c" : "_i");

            item.BaseExternalId = baseExternalId;
        }
    }
}

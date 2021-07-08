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
        const long PROGRESS_MAX = 200;

        public static async Task<ItemsContainer> SetStatus(ItemsContainer items, Dictionary<string, Item> existingData, string externalIdPrefix, IProgress<ProgressUpdate> progress = null, CancellationToken cancellationToken = default)
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

                var processedWithHierarchy = new HashSet<Item>(allItemCount);

                long progressValue = 0;

                foreach (var item in allItems)
                {
                    if (cancellationToken.IsCancellationRequested) return items;

                    if (progress != null)
                    {
                        var comparisonProgress = ++itemCounter * PROGRESS_MAX / allItemCount / 2;

                        if (comparisonProgress > progressValue)
                        {
                            progressValue = comparisonProgress;

                            progress.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue });
                        }
                    }

                    item.IsChecked = false;

                    if (processedWithHierarchy.Contains(item)) continue;

                    item.State = Item.ItemState.New;
                    item.RedundantChildren = null;

                    if (existingData != null && existingData.TryGetValue($"{externalIdPrefix}{item.BaseExternalId}", out var matchingItem))
                    {
                        if (item.GetAttributes() != matchingItem.GetAttributes())
                        {
                            item.State = Item.ItemState.Modified;
                        }

                        else
                        {
                            var childrenHashSet = item.Children.Select(i => i.BaseExternalId).ToHashSet();
                            var redundantItems = matchingItem.Children.Where(i => !childrenHashSet.Contains(i.BaseExternalId)).ToList();

                            if (redundantItems.Count > 0)
                            {
                                item.State = Item.ItemState.Modified;
                                item.RedundantChildren = redundantItems;
                            }

                            else
                            {
                                var matchingItemChildrenHashSet = matchingItem.Children.Select(i => i.BaseExternalId).ToHashSet();
                                var newItemsCount = item.Children.Where(i => !matchingItemChildrenHashSet.Contains(i.BaseExternalId)).Count();

                                item.State = newItemsCount > 0 ? Item.ItemState.Modified : Item.ItemState.Unchanged;
                            }
                        }
                    }
                }

                var progressSoFar = progressValue;
                itemCounter = 0;

                foreach (var item in allItems)
                {
                    if (cancellationToken.IsCancellationRequested) return items;

                    if (progress != null)
                    {
                        var comparisonProgress = progressSoFar + ++itemCounter * PROGRESS_MAX / allItemCount / 2;

                        if (comparisonProgress > progressValue)
                        {
                            progressValue = comparisonProgress;

                            progress.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue });
                        }
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

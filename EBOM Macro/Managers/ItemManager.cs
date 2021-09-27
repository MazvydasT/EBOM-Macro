using EBOM_Macro.Extensions;
using EBOM_Macro.Models;
using EBOM_Macro.States;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EBOM_Macro.Managers
{
    /// <summary>
    /// Class responsible for handling Item manipulations
    /// </summary>
    public static class ItemManager
    {
        /// <summary>
        /// Constant that adjusts how often progress update is sent to caller
        /// </summary>
        const long PROGRESS_MAX = 300;

        /// <summary>
        /// Resets selection of an Item and a whole branch that belongs to it
        /// </summary>
        /// <param name="item">Item that should get its selection reset</param>
        public static void ResetItemSelection(Item item)
        {
            if (item == null) return;

            item.IsChecked = false;

            var items = item.GetSelfAndDescendants();

            foreach (var i in items)
            {
                if (i.State == Item.ItemState.New || i.State == Item.ItemState.Modified)
                    i.SelectWithoutDescendants.Execute(null);
            }
        }

        /// <summary>
        /// Asynchronously compares Item objects sourced from eMS EBOM report against items sourced from eMS XML file,
        /// performs ExternalId matching and reuse as well as autoselects Item objects that should be updated in eMS.
        /// </summary>
        /// <param name="items">Items read from eMS EBOM report</param>
        /// <param name="existingData">Items read from eMS XML file</param>
        /// <param name="externalIdPrefix">User selected ExternalId prefix</param>
        /// <param name="reuseExternalIds">Flag that determines if ExternalIds should be reused</param>
        /// <param name="ldiFolderPath">Path to LDI folder</param>
        /// <param name="progress">Optional IProgress construct for reporting progress back to the caller</param>
        /// <param name="cancellationToken">Optional cancellation token to allow caller to cancel the task</param>
        /// <returns></returns>
        public static async Task<ItemsContainer> SetStatus(ItemsContainer items, Dictionary<string, Item> existingData, string externalIdPrefix, bool reuseExternalIds, string ldiFolderPath, IProgress<ProgressUpdate> progress = null, CancellationToken cancellationToken = default)
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

                    if (item.IsInstance)
                    {
                        var ds = item.GetDS(dsCacheKey);

                        var jtPath = Path.Combine(ldiFolderPath, $"{ds.Attributes.Number}_{ds.Attributes.Version}__".GetSafeFileName(), $"{item.Attributes.Number}.jt".GetSafeFileName());

                        item.Attributes.FilePath = jtPath;
                    }

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

                foreach (var item in allItems) // Sets Item State property based on camprison results
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

                    // Clear previous comparison results
                    item.State = Item.ItemState.New; // Reset State to New
                    item.RedundantChildren = null; // Clear all redundant children (Items to be deleted)
                    item.ChangedAttributes = null; // Clear all Changed attributes

                    if (existingData != null && existingData.TryGetValue(item.ReusedExternalId ?? $"{externalIdPrefix}{item.BaseExternalId}", out var matchingItem))
                    {
                        item.State = Item.ItemState.Unchanged;

                        var childrenHashSet = item.Children.Select(i => i.ReusedExternalId ?? $"{externalIdPrefix}{i.BaseExternalId}").ToHashSet();

                        // Get Items that no longer exists in EBOM and should be removed
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

                foreach (var item in allItems) // Selects Items for export as well as sets flag to show if Item has modified children
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

        /// <summary>
        /// Updates stats.
        /// </summary>
        /// <param name="items">Struct containing Items read from eMS EBOM report</param>
        /// <param name="statsState">Reference to StatsState instance</param>
        public static async void UpdateStats(ItemsContainer items, StatsState statsState)
        {
            statsState.UnchangedAssemblies = 0;
            statsState.ModifiedAssemblies = 0;
            statsState.NewAssemblies = 0;
            statsState.DeletedAssemblies = 0;

            statsState.UnchangedParts = 0;
            statsState.ModifiedParts = 0;
            statsState.NewParts = 0;
            statsState.DeletedParts = 0;

            statsState.SelectedUnchangedAssemblies = 0;
            statsState.SelectedModifiedAssemblies = 0;
            statsState.SelectedNewAssemblies = 0;
            statsState.SelectedDeletedAssemblies = 0;

            statsState.SelectedUnchangedParts = 0;
            statsState.SelectedModifiedParts = 0;
            statsState.SelectedNewParts = 0;
            statsState.SelectedDeletedParts = 0;

            var itemObjects = (items.Items ?? Enumerable.Empty<Item>()).SelectMany(i => (i.RedundantChildren ?? Enumerable.Empty<Item>()).Prepend(i));
            var assemblies = (items.PHs ?? Enumerable.Empty<Item>()).Concat(items.Root?.Yield() ?? Enumerable.Empty<Item>())
                .SelectMany(i => (i.RedundantChildren ?? Enumerable.Empty<Item>()).Prepend(i))
                .Concat(itemObjects.Where(i => !i.IsInstance));
            var parts = itemObjects.Where(i => i.IsInstance);

            await Task.WhenAll(
                Task.Run(() =>
                {
                    foreach (var item in assemblies)
                    {
                        var countTowardsChecked = !(item.IsChecked == false || (item.IsChecked == null && item.State == Item.ItemState.HasModifiedDescendants));

                        switch (item.State)
                        {
                            case Item.ItemState.HasModifiedDescendants:
                            case Item.ItemState.Unchanged:
                                ++statsState.UnchangedAssemblies;
                                if (countTowardsChecked) ++statsState.SelectedUnchangedAssemblies;
                                break;

                            case Item.ItemState.Modified:
                                ++statsState.ModifiedAssemblies;
                                if (countTowardsChecked) ++statsState.SelectedModifiedAssemblies;
                                break;

                            case Item.ItemState.New:
                                ++statsState.NewAssemblies;
                                if (countTowardsChecked) ++statsState.SelectedNewAssemblies;
                                break;

                            case Item.ItemState.Redundant:
                                ++statsState.DeletedAssemblies;
                                if (countTowardsChecked) ++statsState.SelectedDeletedAssemblies;
                                break;
                        }
                    }
                }),

                Task.Run(() =>
                {
                    foreach (var item in parts)
                    {
                        var countTowardsChecked = !(item.IsChecked == false || (item.IsChecked == null && item.State == Item.ItemState.HasModifiedDescendants));

                        switch (item.State)
                        {
                            case Item.ItemState.Unchanged:
                                ++statsState.UnchangedParts;
                                if (countTowardsChecked) ++statsState.SelectedUnchangedParts;
                                break;

                            case Item.ItemState.Modified:
                                ++statsState.ModifiedParts;
                                if (countTowardsChecked) ++statsState.SelectedModifiedParts;
                                break;

                            case Item.ItemState.New:
                                ++statsState.NewParts;
                                if (countTowardsChecked) ++statsState.SelectedNewParts;
                                break;

                            case Item.ItemState.Redundant:
                                ++statsState.DeletedParts;
                                if (countTowardsChecked) ++statsState.SelectedDeletedParts;
                                break;
                        }
                    }
                })
            );
        }

        /// <summary>
        /// Sets Item's BaseExternalId property
        /// </summary>
        /// <param name="item">Item object</param>
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

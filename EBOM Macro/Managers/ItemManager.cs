using EBOM_Macro.Extensions;
using EBOM_Macro.Models;
using EBOM_Macro.States;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
        /// Used to tracks how many times each ExternalId appears within same eMS EBOM report
        /// </summary>
        private static ConditionalWeakTable<object, Dictionary<string, int>> baseExternalIdTrackerCache = new ConditionalWeakTable<object, Dictionary<string, int>>();

        /// <summary>
        /// Resets selection of an Item and a whole branch that belongs to it
        /// </summary>
        /// <param name="item">Item that should get its selection reset</param>
        public static void ResetItemSelection(Item item, object cacheKey = null)
        {
            if (item == null) return;

            item.IsChecked = false;

            var items = item.GetSelfAndDescendants(cacheKey, true);

            foreach (var i in items)
            {
                if (i.State == Item.ItemState.New || i.State == Item.ItemState.Modified || i.State == Item.ItemState.Redundant)
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
        /// <param name="comFoxTranslationSystemIsUsed">Flag that determines if path to JT should be constructed to match COM/FOX translator output</param>
        /// <param name="ldiFolderPath">Path to LDI folder</param>
        /// <param name="progress">Optional IProgress construct for reporting progress back to the caller</param>
        /// <param name="cancellationToken">Optional cancellation token to allow caller to cancel the task</param>
        /// <returns></returns>
        public static async Task<ItemsContainer> SetStatus(ItemsContainer items, Dictionary<string, Item> existingData, string externalIdPrefix, bool reuseExternalIds, bool comFoxTranslationSystemIsUsed, string ldiFolderPath, IProgress<ProgressUpdate> progress = null, CancellationToken cancellationToken = default)
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

                                progress.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue, Message = $"Checking ExternalIds within existing data: {((double)itemCounter / allItemCount):P0}" });
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

                            progress.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue, Message = $"Matching new items to existing ExternalIds: {((double)itemCounter / allItemCount):P0}" });
                        }
                    }

                    item.ReusedExternalId = null;

                    if (item.IsInstance)
                    {
                        string jtPath;

                        if (comFoxTranslationSystemIsUsed)
                        {
                            jtPath = Path.Combine(ldiFolderPath, item.Filename?.GetSafeFileName() ?? "");
                        }

                        else
                        {
                            var ds = item.GetDS(items.CacheKey);

                            jtPath = Path.Combine(ldiFolderPath, $"{ds.Attributes.Number}_{ds.Attributes.Version}__".GetSafeFileName(), $"{item.Attributes.Number}.jt".GetSafeFileName());
                        }


                        item.Attributes.FilePath = jtPath;
                    }

                    if (item.Type == Item.ItemType.PH || !reuseExternalIds || existingData == null) continue;

                    var itemExternalId = $"{externalIdPrefix}{item.BaseExternalId}";

                    if (!existingData.ContainsKey(itemExternalId))
                    {
                        var matchedInstances = numberLookup[item.Attributes.Number].Where(i => item.IsInstance == i.IsInstance &&
                            (item.Type == Item.ItemType.DS || i.Parent.BaseExternalId == (item.Parent.ReusedExternalId ?? $"{externalIdPrefix}{item.Parent.BaseExternalId}")) &&
                            !previouslyMatchedExternalIds.Contains(i.BaseExternalId));

                        var siblings = item.Parent.Children.Where(i => i.Attributes.Number == item.Attributes.Number && !previouslyMatchedExternalIds.Contains(i.ReusedExternalId ?? $"{externalIdPrefix}{i.BaseExternalId}"));

                        // if (matchedInstances.Count == 1 && siblings.Count == 1)
                        if (matchedInstances.Any() && !matchedInstances.Skip(1).Any() && siblings.Any() && !siblings.Skip(1).Any())
                        {
                            item.ReusedExternalId = matchedInstances.First().BaseExternalId;
                            previouslyMatchedExternalIds.Add(item.ReusedExternalId);
                        }

                        // else if (matchedInstances.Count > 1 || siblings.Count > 1)
                        else if (matchedInstances.Skip(1).Any() || siblings.Skip(1).Any())
                        {
                            var matchedTransformationInstances = matchedInstances.Where(i => i.Attributes.Translation.ToString() == item.Attributes.Translation.ToString() && i.Attributes.Rotation.ToString() == item.Attributes.Rotation.ToString());

                            var transformationSiblings = siblings.Where(i => i.Attributes.Translation.ToString() == item.Attributes.Translation.ToString() && i.Attributes.Rotation.ToString() == item.Attributes.Rotation.ToString());

                            // if (matchedTransformationInstances.Count == 1 && transformationSiblings.Count == 1)
                            if (matchedTransformationInstances.Any() && !matchedTransformationInstances.Skip(1).Any() && transformationSiblings.Any() && !transformationSiblings.Skip(1).Any())
                            {
                                item.ReusedExternalId = matchedTransformationInstances.First().BaseExternalId;
                                previouslyMatchedExternalIds.Add(item.ReusedExternalId);
                            }

                            //else if (matchedTransformationInstances.Count > 1 || transformationSiblings.Count > 1)
                            else if (matchedTransformationInstances.Skip(1).Any() || transformationSiblings.Skip(1).Any())
                            {
                                var siblingIndex = transformationSiblings.Select((transformationSibling, index) => (index, transformationSibling))
                                    .Where(pair => pair.transformationSibling == item)
                                    .Select(pair => pair.index + 1)
                                    .FirstOrDefault() - 1;

                                if (siblingIndex < 0) continue;

                                var matchedTransformationInstance = matchedTransformationInstances.ElementAtOrDefault(siblingIndex);

                                if (matchedTransformationInstance != null)
                                {
                                    item.ReusedExternalId = matchedTransformationInstance.BaseExternalId;
                                    previouslyMatchedExternalIds.Add(item.ReusedExternalId);
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

                            progress.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue, Message = $"Updating item states: {((double)itemCounter / allItemCount):P0}" });
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

                            progress.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue, Message = $"Selecting items for export: {((double)itemCounter / allItemCount):P0}" });
                        }
                    }

                    if (item.State == Item.ItemState.New || item.State == Item.ItemState.Modified)
                    {
                        item.SelectWithoutDescendants.Execute(null);

                        if (item.Parent?.State == Item.ItemState.Unchanged)
                        {
                            var unchangedAncestors = item.GetAncestors(items.CacheKey).Where(i => i.State == Item.ItemState.Unchanged);

                            foreach (var ancestor in unchangedAncestors)
                            {
                                ancestor.State = Item.ItemState.HasModifiedDescendants;
                            }
                        }
                    }
                }

                progress?.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = PROGRESS_MAX });

                items.RefreshCacheKey();

                return items;

            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }



        private static ConditionalWeakTable<object, Tuple<CancellationTokenSource, Task>> updateStatsCancellationTokens = new ConditionalWeakTable<object, Tuple<CancellationTokenSource, Task>>();

        /// <summary>
        /// Updates stats.
        /// </summary>
        /// <param name="items">Struct containing Items read from eMS EBOM report</param>
        /// <param name="statsState">Reference to StatsState instance</param>
        public static void UpdateStats(ItemsContainer items, StatsState statsState)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            var updateStatsTask = new Task(async () =>
            {
                var statsStateUnchangedAssemblies = 0;
                var statsStateModifiedAssemblies = 0;
                var statsStateNewAssemblies = 0;
                var statsStateDeletedAssemblies = 0;

                var statsStateUnchangedParts = 0;
                var statsStateModifiedParts = 0;
                var statsStateNewParts = 0;
                var statsStateDeletedParts = 0;

                var statsStateSelectedUnchangedAssemblies = 0;
                var statsStateSelectedModifiedAssemblies = 0;
                var statsStateSelectedNewAssemblies = 0;
                var statsStateSelectedDeletedAssemblies = 0;

                var statsStateSelectedUnchangedParts = 0;
                var statsStateSelectedModifiedParts = 0;
                var statsStateSelectedNewParts = 0;
                var statsStateSelectedDeletedParts = 0;

                if (cancellationToken.IsCancellationRequested) return;

                var itemObjects = (items.Items ?? Enumerable.Empty<Item>()).SelectMany(i => (i.RedundantChildren ?? Enumerable.Empty<Item>()).Prepend(i));
                var assemblies = (items.PHs ?? Enumerable.Empty<Item>()).Concat(items.Root?.Yield() ?? Enumerable.Empty<Item>())
                    .SelectMany(i => (i.RedundantChildren ?? Enumerable.Empty<Item>()).Prepend(i))
                    .Concat(itemObjects.Where(i => !i.IsInstance));
                var parts = itemObjects.Where(i => i.IsInstance);

                if (cancellationToken.IsCancellationRequested) return;

                await Task.WhenAll(
                    Task.Run(() =>
                    {
                        foreach (var item in assemblies)
                        {
                            if (cancellationToken.IsCancellationRequested) return;

                            var countTowardsChecked = !(item.IsChecked == false || (item.IsChecked == null && item.State == Item.ItemState.HasModifiedDescendants));

                            switch (item.State)
                            {
                                case Item.ItemState.HasModifiedDescendants:
                                case Item.ItemState.Unchanged:
                                    ++statsStateUnchangedAssemblies;
                                    if (countTowardsChecked) ++statsStateSelectedUnchangedAssemblies;
                                    break;

                                case Item.ItemState.Modified:
                                    ++statsStateModifiedAssemblies;
                                    if (countTowardsChecked) ++statsStateSelectedModifiedAssemblies;
                                    break;

                                case Item.ItemState.New:
                                    ++statsStateNewAssemblies;
                                    if (countTowardsChecked) ++statsStateSelectedNewAssemblies;
                                    break;

                                case Item.ItemState.Redundant:
                                    ++statsStateDeletedAssemblies;
                                    if (countTowardsChecked) ++statsStateSelectedDeletedAssemblies;
                                    break;
                            }
                        }
                    }),

                    Task.Run(() =>
                    {
                        foreach (var item in parts)
                        {
                            if (cancellationToken.IsCancellationRequested) return;

                            var countTowardsChecked = !(item.IsChecked == false || (item.IsChecked == null && item.State == Item.ItemState.HasModifiedDescendants));

                            switch (item.State)
                            {
                                case Item.ItemState.Unchanged:
                                    ++statsStateUnchangedParts;
                                    if (countTowardsChecked) ++statsStateSelectedUnchangedParts;
                                    break;

                                case Item.ItemState.Modified:
                                    ++statsStateModifiedParts;
                                    if (countTowardsChecked) ++statsStateSelectedModifiedParts;
                                    break;

                                case Item.ItemState.New:
                                    ++statsStateNewParts;
                                    if (countTowardsChecked) ++statsStateSelectedNewParts;
                                    break;

                                case Item.ItemState.Redundant:
                                    ++statsStateDeletedParts;
                                    if (countTowardsChecked) ++statsStateSelectedDeletedParts;
                                    break;
                            }
                        }
                    })
                );

                if (cancellationToken.IsCancellationRequested) return;

                statsState.UnchangedAssemblies = statsStateUnchangedAssemblies;
                statsState.ModifiedAssemblies = statsStateModifiedAssemblies;
                statsState.NewAssemblies = statsStateNewAssemblies;
                statsState.DeletedAssemblies = 0;

                statsState.UnchangedParts = statsStateUnchangedParts;
                statsState.ModifiedParts = statsStateModifiedParts;
                statsState.NewParts = statsStateNewParts;
                statsState.DeletedParts = statsStateDeletedParts;

                statsState.SelectedUnchangedAssemblies = statsStateSelectedUnchangedAssemblies;
                statsState.SelectedModifiedAssemblies = statsStateSelectedModifiedAssemblies;
                statsState.SelectedNewAssemblies = statsStateSelectedNewAssemblies;
                statsState.SelectedDeletedAssemblies = statsStateSelectedDeletedAssemblies;

                statsState.SelectedUnchangedParts = statsStateSelectedUnchangedParts;
                statsState.SelectedModifiedParts = statsStateSelectedModifiedParts;
                statsState.SelectedNewParts = statsStateSelectedNewParts;
                statsState.SelectedDeletedParts = statsStateSelectedDeletedParts;
            }, cancellationToken);

            lock (statsState)
            {
                if (updateStatsCancellationTokens.TryGetValue(statsState, out var pair))
                {
                    pair.Item1.Cancel();

                    pair.Item2.Wait();

                    updateStatsCancellationTokens.Remove(statsState);
                }

                updateStatsCancellationTokens.Add(statsState, new Tuple<CancellationTokenSource, Task>(cancellationTokenSource, updateStatsTask));

                updateStatsTask.Start();
            }
        }

        /// <summary>
        /// Sets Item's BaseExternalId property
        /// </summary>
        /// <param name="item">Item object</param>
        public static void SetBaseExternalId(Item item, object cacheKey = null)
        {
            var joinedPhysicalIds = string.Join("_", item.GetDSToSelfPath(cacheKey).Select(i => i.PhysicalId));

            var physicalIds = joinedPhysicalIds;

        RecalculateBaseExternalId:

            var data = Encoding.UTF8.GetBytes(physicalIds);
            var hash = StaticResources.SHA256.ComputeHash(data);
            var baseExternalId = BitConverter.ToString(hash).Replace("-", "") + (item.IsInstance ? "_i" : "_c");

            if (cacheKey != null)
            {
                if (!baseExternalIdTrackerCache.TryGetValue(cacheKey, out var baseExternalIdTracker))
                    baseExternalIdTrackerCache.Add(cacheKey, baseExternalIdTracker = new Dictionary<string, int>());

                if (baseExternalIdTracker.TryGetValue(baseExternalId, out var baseExternalIdCounter))
                {
                    baseExternalIdTracker[baseExternalId] = ++baseExternalIdCounter;
                    physicalIds = $"{joinedPhysicalIds}_{baseExternalIdCounter}";
                    goto RecalculateBaseExternalId;
                }

                else baseExternalIdTracker[baseExternalId] = 0;
            }

            item.BaseExternalId = baseExternalId;
        }
    }
}

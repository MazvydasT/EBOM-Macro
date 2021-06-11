using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EBOM_Macro
{
    public static class Item2Manager
    {
        const long PROGRESS_MAX = 1;

        public static async Task<Items2Container> SetStatus(Items2Container items, IReadOnlyDictionary<string, Item2> existingData, string prefix, IProgress<ProgressUpdate> progress = null, CancellationToken cancellationToken = default)
        {
            progress.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = 0 });

            if (items.Root == null) return default;

            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Factory.StartNew(() =>
            {
                var allItems = items.PHs.Concat(items.Items).Prepend(items.Root);

                var allItemCount = items.PHs.Count + items.Items.Count + 1;
                var itemCounter = 0;

                long progressValue = 0;

                foreach (var item in allItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    item.State = Item2.ItemState.New;
                    item.IsChecked = true;
                    
                    if (progress != null)
                    {
                        var comparisonProgress = ++itemCounter * PROGRESS_MAX / allItemCount;

                        if (comparisonProgress > progressValue)
                        {
                            progressValue = comparisonProgress;

                            progress.Report(new ProgressUpdate { Max = PROGRESS_MAX, Value = progressValue });
                        }
                    }

                    if (existingData == null) continue;
                }

                return items;

            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public static void SetBaseExternalId(Item2 item)
        {
            var physicalIds = string.Join("_", item.GetDSToSelfPath().Select(i => i.PhysicalId));
            var data = Encoding.UTF8.GetBytes(physicalIds);
            var hash = StaticResources.SHA256.ComputeHash(data);
            var baseExternalId = BitConverter.ToString(hash).Replace("-", "") + (item.Children.Count > 0 ? "_c" : "_i");

            item.BaseExternalId = baseExternalId;
        }
    }
}

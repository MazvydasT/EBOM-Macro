using System.Threading;
using System.Threading.Tasks;

namespace EBOM_Macro
{
    public static class Item2Manager
    {
        public static async Task SetExternalIds(Items2Container items, string prefix, CancellationToken cancellationToken = default)
        {
            if (items.Root == null) return;

            await Task.Factory.StartNew(() =>
            {

            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }
}

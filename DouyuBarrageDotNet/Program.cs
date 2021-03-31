using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DouyuBarrageDotNet
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await DouyuBarrage.ChatMessageFromUrl("https://www.douyu.com/74751")
                .ForEachAwaitWithCancellationAsync((x, _) =>
                {
                    Console.WriteLine($"{x.UserName}：{x.Message}   ({x.ColorName}:{x.Color:X})");
                    return Task.CompletedTask;
                }, CancellationToken.None);
        }
    }
}

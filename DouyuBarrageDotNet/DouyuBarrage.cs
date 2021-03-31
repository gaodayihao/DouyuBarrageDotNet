using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace DouyuBarrageDotNet
{
    public class DouyuBarrage
    {
        private static readonly HttpClient Http = new ();

        public static async IAsyncEnumerable<string> RawFromUrl(string url, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var html = await Http.GetAsync(url, cancellationToken);
            var roomId = Regex.Match(await html.Content.ReadAsStringAsync(cancellationToken), @"\$ROOM.room_id[ ]?=[ ]?(\d+);").Groups[1].Value;
            using var ws = new ClientWebSocket();
            ws.Options.AddSubProtocol("-");
            await ws.ConnectAsync(new Uri("wss://danmuproxy.douyu.com:8506/"), cancellationToken);
            await ws.LoginAsync(roomId, cancellationToken);
            await ws.JoinGroupAsync(roomId, cancellationToken: cancellationToken);

            var task = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await ws.SendTickAsync(cancellationToken);
                    await Task.Delay(45000, cancellationToken);
                }
            }, cancellationToken);

            while (ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                yield return await ws.ReceiveStringAsync(cancellationToken);
            }

            GC.KeepAlive(task);
            await ws.LogoutAsync(cancellationToken);
        }

        public static IAsyncEnumerable<JToken> JObjectFromUrl(string url) => RawFromUrl(url).Select(DecodeStringToJObject);

        public static IAsyncEnumerable<Barrage> ChatMessageFromUrl(string url) => JObjectFromUrl(url).Where(x => x["type"].Value<string>() == "chatmsg").Select(Barrage.FromJToken);

        private static JToken DecodeStringToJObject(string str)
        {
            if (str.Contains("//"))
            {
                var result = new JArray();
                foreach (var field in str.Split(new[] { "//" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    result.Add(DecodeStringToJObject(field));
                }
                return result;
            }

            if (str.Contains("@="))
            {
                var result = new JObject();
                foreach (var field in str.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var tokens = field.Split(new[] { "@=" }, StringSplitOptions.None);
                    var k = tokens[0];
                    var v = UnscapeSlashAt(tokens[1]);
                    result[k] = DecodeStringToJObject(v);
                }
                return result;
            }
            if (str.Contains("@A="))
            {
                return DecodeStringToJObject(UnscapeSlashAt(str));
            }
            return UnscapeSlashAt(str);

            static string UnscapeSlashAt(string str)
            {
                return str
                    .Replace("@S", "/")
                    .Replace("@A", "@");
            }
        }
    }
}

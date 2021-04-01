using System;
using System.Buffers;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DouyuBarrageDotNet
{
    public static class ClientWebSocketExtensions
    {
        const short ClientSendToServer = 689;
        const short ServerSendToClient = 690;
        const byte Encrypted = 0;
        const byte Reserved = 0;
        const byte ByteZero = 0;

        public static Task LoginAsync(this ClientWebSocket stream, string roomId, CancellationToken cancellationToken)
        {
            return SendAsync(stream, $"type@=loginreq/roomid@={roomId}/", cancellationToken);
        }

        public static Task JoinGroupAsync(this ClientWebSocket stream, string roomId, int groupId = -9999, CancellationToken cancellationToken = default)
        {
            return SendAsync(stream, $"type@=joingroup/rid@={roomId}/gid@={groupId}/", cancellationToken);
        }

        public static Task SendTickAsync(this ClientWebSocket stream, CancellationToken cancellationToken)
        {
            return SendAsync(stream, $"type@=keeplive/tick@={Environment.TickCount}/", cancellationToken);
        }

        public static Task LogoutAsync(this ClientWebSocket stream, CancellationToken cancellationToken)
        {
            return SendAsync(stream, $"type@=logout/", cancellationToken);
        }

        private static Task SendAsync(ClientWebSocket stream, string msg, CancellationToken cancellationToken)
        {
            return SendAsync(stream, Encoding.UTF8.GetBytes(msg), cancellationToken);
        }

        private static async Task SendAsync(ClientWebSocket stream, byte[] body, CancellationToken cancellationToken)
        {
            var buffer = new byte[4];
            await stream.SendAsync(GetBytesI32(4 + 4 + body.Length + 1), WebSocketMessageType.Binary, false, cancellationToken);
            await stream.SendAsync(GetBytesI32(4 + 4 + body.Length + 1), WebSocketMessageType.Binary, false, cancellationToken);

            await stream.SendAsync(GetBytesI16(ClientSendToServer), WebSocketMessageType.Binary, false, cancellationToken);
            await stream.SendAsync(new[] { Encrypted }, WebSocketMessageType.Binary, false, cancellationToken);
            await stream.SendAsync(new[] { Reserved }, WebSocketMessageType.Binary, false, cancellationToken);

            await stream.SendAsync(body, WebSocketMessageType.Binary, false, cancellationToken);
            await stream.SendAsync(new[] { ByteZero }, WebSocketMessageType.Binary, false, cancellationToken);

            Memory<byte> GetBytesI32(int v)
            {
                buffer[0] = (byte)v;
                buffer[1] = (byte)(v >> 8);
                buffer[2] = (byte)(v >> 16);
                buffer[3] = (byte)(v >> 24);
                return new Memory<byte>(buffer, 0, 4);
            }

            Memory<byte> GetBytesI16(short v)
            {
                buffer[0] = (byte)v;
                buffer[1] = (byte)(v >> 8); ;
                return new Memory<byte>(buffer, 0, 2);
            }
        }

        public static async Task<string> ReceiveStringAsync(this ClientWebSocket stream, CancellationToken cancellationToken)
        {
            var intBuffer = new byte[4];
            var int32Buffer = new Memory<byte>(intBuffer, 0, 4);
            var int16Buffer = int32Buffer.Slice(0, 2);
            var int8Buffer = int32Buffer.Slice(0, 1);

            var fullMsgLength = await ReadInt32Async();
            var fullMsgLength2 = await ReadInt32Async();
            Debug.Assert(fullMsgLength == fullMsgLength2);

            int length = fullMsgLength - 1 - 4 - 4;
            var buffer = ArrayPool<byte>.Shared.Rent(length);
            var packType = await ReadInt16Async();
            Debug.Assert(packType == ServerSendToClient);
            short encrypted = await ReadByteAsync();
            Debug.Assert(encrypted == Encrypted);
            short reserved = await ReadByteAsync();
            Debug.Assert(reserved == Reserved);
            Memory<byte> bytes = await ReadBytesAsync(length).ConfigureAwait(false);
            byte zero = await ReadByteAsync().ConfigureAwait(false);
            Debug.Assert(zero == ByteZero);
            ArrayPool<byte>.Shared.Return(buffer);

            return Encoding.UTF8.GetString(bytes.Span);

            async ValueTask<int> ReadInt32Async()
            {
                var memory = int32Buffer;
                int read = 0;
                while (read < 4)
                {
                    var result = await stream.ReceiveAsync(memory.Slice(read), cancellationToken);
                    read += result.Count;
                }
                Debug.Assert(read == memory.Length);

                return intBuffer[0] | (intBuffer[1] << 8) | (intBuffer[2] << 16) | (intBuffer[3] << 24);
            }

            async ValueTask<short> ReadInt16Async()
            {
                var result = await stream.ReceiveAsync(int16Buffer, cancellationToken);
                Debug.Assert(result.Count == int16Buffer.Length);
                return (short)(intBuffer[0] | (intBuffer[1] << 8));
            }

            async ValueTask<byte> ReadByteAsync()
            {
                var result = await stream.ReceiveAsync(int8Buffer, cancellationToken);
                Debug.Assert(result.Count == int8Buffer.Length);
                return int8Buffer.Span[0];
            }

            async ValueTask<Memory<byte>> ReadBytesAsync(int readLength)
            {
                var memory = new Memory<byte>(buffer, 0, readLength);
                int read = 0;
                while (read < readLength)
                {
                    var result = await stream.ReceiveAsync(memory.Slice(read), cancellationToken);
                    read += result.Count;
                }
                Debug.Assert(read == memory.Length);
                return memory;
            }
        }
    }
}

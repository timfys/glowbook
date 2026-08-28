using System.Collections.Concurrent;
using System.Threading.Channels;

namespace GlowBook.Web.Services;

public sealed class ChatRealtimeNotifier
{
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<Guid, Channel<ClientMessageDto>>> _threads = new();

    public (ChannelReader<ClientMessageDto> Reader, Action Unsubscribe) Subscribe(int threadClientId)
    {
        var subId = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<ClientMessageDto>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        var subs = _threads.GetOrAdd(threadClientId, _ => new ConcurrentDictionary<Guid, Channel<ClientMessageDto>>());
        subs[subId] = channel;

        void Unsubscribe()
        {
            subs.TryRemove(subId, out _);
            if (subs.IsEmpty)
                _threads.TryRemove(threadClientId, out _);
        }

        return (channel.Reader, Unsubscribe);
    }

    public void Publish(int threadClientId, ClientMessageDto message)
    {
        if (!_threads.TryGetValue(threadClientId, out var subs))
            return;

        foreach (var channel in subs.Values)
            channel.Writer.TryWrite(message);
    }
}

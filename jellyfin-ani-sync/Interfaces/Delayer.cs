using System;
using System.Threading.Tasks;

namespace jellyfin_ani_sync.Interfaces;

public interface IAsyncDelayer
{
    Task Delay(TimeSpan timeSpan);
}
public class Delayer : IAsyncDelayer
{
    public async Task Delay(TimeSpan timeSpan)
    {
        await Task.Delay(timeSpan);
    }
}
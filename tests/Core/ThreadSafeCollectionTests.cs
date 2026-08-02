using System.Linq;
using System.Threading.Tasks;
using YLproxy.Core.Concurrency;
using Xunit;

namespace YLproxy.Tests.Core;

public sealed class ThreadSafeCollectionTests
{
    [Fact]
    public void AddRemoveClear_ShouldWork()
    {
        var collection = new ThreadSafeCollection<int>();

        collection.Add(1);
        collection.Add(2);
        Assert.Equal(2, collection.Count);

        Assert.True(collection.Remove(1));
        Assert.Single(collection.GetAll());

        collection.Clear();
        Assert.Empty(collection.GetAll());
    }

    [Fact]
    public void GetAll_ShouldReturnSnapshot()
    {
        var collection = new ThreadSafeCollection<int>();
        collection.Add(1);

        var snapshot = collection.GetAll();
        snapshot.Add(2);

        Assert.Single(collection.GetAll());
    }

    [Fact]
    public async Task ParallelAdd_ShouldBeThreadSafe()
    {
        var collection = new ThreadSafeCollection<int>();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Task.Run(() =>
        {
            for (var j = 0; j < 50; j++)
            {
                collection.Add(i * 100 + j);
            }
        })));

        Assert.Equal(1000, collection.Count);
    }

    [Fact]
    public void Dispose_CanBeCalled()
    {
        var collection = new ThreadSafeCollection<int>();
        collection.Dispose();
        Assert.True(true);
    }
}

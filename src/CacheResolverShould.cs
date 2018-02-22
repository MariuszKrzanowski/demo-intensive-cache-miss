using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using System.Threading;
using System.Collections.Generic;
using MrMatrix.Net.IntensiveCacheMiss;

namespace Tests
{
    public class CacheResolverShould
    {
        private const int DifferentKeysCounter = 10000;
        private int[] _callCounters = new int[DifferentKeysCounter];
        private SemaphoreSlim _synchronizationContext = new SemaphoreSlim(0);
        private CacheResolver<int, string> _sut = new CacheResolver<int, string>();
        private List<Task<string>> _results = new List<Task<string>>();
        private HashSet<string> _receivedData = new HashSet<string>();

        private async Task<string> CacheDataSupplierMock(int k)
        {
            await _synchronizationContext.WaitAsync();
            _callCounters[k]++;
            _synchronizationContext.Release();
            return await Task.FromResult($"{k}-{_callCounters[k]}");
        }

        [Fact]
        public async Task CallDataSupplierServiceOnceForEachKey()
        {
            // arrange
            for (int i = 0; i < _callCounters.Length; i++)
            {
                _results.Add(_sut.TakeResourceAsync(i, CacheDataSupplierMock));
            }

            // act
            _synchronizationContext.Release();
            await Task.WhenAll(_results).ConfigureAwait(false);

            // assert 
            foreach (var result in _results)
            {
                _receivedData.Add(result.Result);
            }

            for (int i = 0; i < _callCounters.Length; i++)
            {
                _callCounters[i].Should().Be(1);
            }
        }

        [Fact]
        public async Task CallDataSupplierServiceOnceWhenSameKeyIsRequestedByTwoConcurrentRequests()
        {
            // arrange
            for (int i = 0; i < _callCounters.Length; i++)
            {
                _results.Add(_sut.TakeResourceAsync(i, CacheDataSupplierMock));
                _results.Add(_sut.TakeResourceAsync(i, CacheDataSupplierMock));
            }

            // act
            _synchronizationContext.Release();
            await Task.WhenAll(_results).ConfigureAwait(false);

            // assert 
            foreach (var result in _results)
            {
                _receivedData.Add(result.Result);
            }

            _receivedData.Count.Should().Be(DifferentKeysCounter);
            for (int i = 0; i < _callCounters.Length; i++)
            {
                _callCounters[i].Should().Be(1);
            }
        }

        [Fact]
        public async Task ToCallTwiceCacheWhenSameKeyIsRequestedByTwoSequentialRequests()
        {
            // arrange
            for (int i = 0; i < _callCounters.Length; i++)
            {
                _results.Add(_sut.TakeResourceAsync(i, CacheDataSupplierMock));
            }

            // act
            _synchronizationContext.Release();
            await Task.WhenAll(_results).ConfigureAwait(false);
            foreach (var result in _results)
            {
                _receivedData.Add(result.Result);
            }
            await _synchronizationContext.WaitAsync();
            _results = new List<Task<string>>();
            for (int i = 0; i < _callCounters.Length; i++)
            {
                _results.Add(_sut.TakeResourceAsync(i, CacheDataSupplierMock));
            }
            _synchronizationContext.Release();
            await Task.WhenAll(_results).ConfigureAwait(false);

            // assert 
            foreach (var result in _results)
            {
                _receivedData.Add(result.Result);
            }
            _receivedData.Count.Should().Be(2 * DifferentKeysCounter);
            for (int i = 0; i < _callCounters.Length; i++)
            {
                _callCounters[i].Should().Be(2);
            }
        }
    }
}

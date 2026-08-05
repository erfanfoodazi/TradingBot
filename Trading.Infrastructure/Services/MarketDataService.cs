using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trading.Core.Interfaces;
using Trading.Shared.Requests;
using Trading.Shared.Responses;

namespace Trading.Infrastructure.Services
{
    public class MarketDataService : IMarketDataService
    {
        private readonly IPythonApiClient _python;
        public MarketDataService(IPythonApiClient python)
        {
            _python = python;
        }

        public async Task<List<CandleResponseDto>> GetCandlesAsync(string symbol, string timeframe, int count)
        {
            var response = await _python.GetCandlesAsync(
            new CandleHistoryRequestDto
            {
                Symbol = symbol,
                Timeframe = timeframe,
                Count = count
            });

            return response.Data ?? [];
        }

        public async Task<List<SymbolResponseDto>> GetSymbolsAsync()
        {
            var response = await _python.GetSymbolsAsync();
            return response.Data ?? [];
        }

        public async Task<HealthResponseDto> HealthAsync()
        {
            var response = await _python.HealthAsync();
            return response.Data!;
        }
    }
}

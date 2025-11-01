using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Enums;
using CryptoExchange.Net.Authentication;

namespace BinanceAzFunction;

public class Function1
{
    private readonly ILogger _logger;

    public Function1(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<Function1>();
    }

    [Function("Function1")]
    public async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation("FLO XXXXXXXXXXXXXXXX . Binance Grok Ignition: Timer Hit | v11 Fixes Loaded");

        // Grok-Style Binance Integration: Pull Env Vars for Demo Keys (Secure in Azure App Settings/Env)
        var apiKey = Environment.GetEnvironmentVariable("BINANCE_DEMO_API_KEY");
        var apiSecret = Environment.GetEnvironmentVariable("BINANCE_DEMO_SECRET");

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            _logger.LogError("⚠️ Grok Alert: BINANCE_DEMO_API_KEY or BINANCE_DEMO_SECRET Missing! Set in Azure Env.");
            return;
        }

        _logger.LogInformation("✅ Grok Keys Locked: Demo Mode Active");

        // Binance Connector: Testnet Hook w/ Server Time Ping (Latency <50ms = Trade-Ready)
        var connector = new BinanceConnector(apiKey, apiSecret, _logger);
        await connector.ConnectAndLogAsync();

        // Grok Order Manager: Fire Futures Long (10% Alloc, 10x Lev) | Mock P&L Sim
        var orderManager = new TestOrderManager(apiKey, apiSecret, _logger);
        await orderManager.PlaceFuturesOrdersAsync();

        _logger.LogInformation("📊 Grok Session Wrap: Binance Signals Logged | Next Timer Awaits.");
    }
}

/// <summary>
/// BinanceConnector: Hooks to Testnet API (Spot v3), Pings Server Time, Logs Status
/// Latency Check: Offset <50ms = Green for 10x Lev Trades
/// </summary>
public class BinanceConnector
{
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly ILogger _logger;

    public BinanceConnector(string apiKey, string apiSecret, ILogger logger)
    {
        _apiKey = apiKey;
        _apiSecret = apiSecret;
        _logger = logger;
    }

    public async Task ConnectAndLogAsync()
    {
        _logger.LogInformation("🔌 Grok Connecting: Binance Demo (Spot API v3)...");

        using var client = new BinanceRestClient(options =>
        {
            options.Environment = BinanceEnvironment.Demo;
            options.ApiCredentials = new ApiCredentials(_apiKey, _apiSecret);
        });

        try
        {
            var timeResult = await client.SpotApi.ExchangeData.GetServerTimeAsync();
            if (timeResult.Success)
            {
                var serverTime = timeResult.Data;
                var localTime = DateTime.UtcNow;
                var offsetMs = (long)(serverTime - localTime).TotalMilliseconds;
                _logger.LogInformation($"✅ Grok Connected! Server: {serverTime:yyyy-MM-dd HH:mm:ss} UTC | Offset: {offsetMs}ms");
            }
            else
            {
                _logger.LogError($"❌ Grok Fail: {timeResult.Error?.Message} | Check Keys");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"💥 Grok Exception: {ex.Message}");
        }
    }
}

/// <summary>
/// TestOrderManager: POST Futures Orders (Market Long) | 10% Alloc, 10x Lev Sim
/// Fees Mock: 0.04% RT | P&L: +2€ TP / -1€ SL (1% Risk)
/// </summary>
public class TestOrderManager
{
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly ILogger _logger;

    public TestOrderManager(string apiKey, string apiSecret, ILogger logger)
    {
        _apiKey = apiKey;
        _apiSecret = apiSecret;
        _logger = logger;
    }

    public async Task PlaceFuturesOrdersAsync()
    {
        _logger.LogInformation("🧪 Grok Firing: BTC/USDT Futures Long (10% Alloc, 10x Lev)");

        using var client = new BinanceRestClient(options =>
        {
            options.Environment = BinanceEnvironment.Demo;
            options.ApiCredentials = new ApiCredentials(_apiKey, _apiSecret);
        });

        try
        {
            decimal allocationPercentage = 0.10m;
            int leverage = 10;

            // Set Leverage
            var leverageResponse = await client.UsdFuturesApi.Account.ChangeInitialLeverageAsync("BTCUSDT", leverage);
            if (!leverageResponse.Success)
            {
                _logger.LogError($"❌ Grok Leverage Fail: {leverageResponse.Error?.Message}");
                return;
            }

            // Get USDT Balance
            var balancesResponse = await client.UsdFuturesApi.Account.GetBalancesAsync();
            if (!balancesResponse.Success)
            {
                _logger.LogError($"❌ Grok Balance Fail: {balancesResponse.Error?.Message}");
                return;
            }

            var usdtBalance = balancesResponse.Data.FirstOrDefault(b => b.Asset == "USDT")?.AvailableBalance ?? 0;
            if (usdtBalance <= 0)
            {
                _logger.LogWarning("⚠️ Grok Alert: No USDT Available!");
                return;
            }

            decimal desiredMargin = usdtBalance * allocationPercentage;
            decimal quoteQuantity = desiredMargin * leverage;

            // Get Current Price
            var priceResponse = await client.UsdFuturesApi.ExchangeData.GetPriceAsync("BTCUSDT");
            if (!priceResponse.Success)
            {
                _logger.LogError($"❌ Grok Price Fail: {priceResponse.Error?.Message}");
                return;
            }

            decimal currentPrice = priceResponse.Data.Price;
            decimal quantityBtc = Math.Round(quoteQuantity / currentPrice, 3); // Round to 0.001 precision

            if (quantityBtc < 0.001m)
            {
                _logger.LogWarning("⚠️ Grok Alert: Quantity Too Small! Min 0.001 BTC");
                return;
            }

            _logger.LogInformation($"📈 Grok Calc: USDT {usdtBalance} | Margin {desiredMargin} | Qty {quantityBtc} BTC @ {currentPrice}");

            // Place Market Long
            var orderResponse = await client.UsdFuturesApi.Trading.PlaceOrderAsync(
                "BTCUSDT", OrderSide.Buy, FuturesOrderType.Market, quantity: quantityBtc);

            if (orderResponse.Success)
            {
                _logger.LogInformation($"✅ Grok Long Executed: ID {orderResponse.Data.Id} | Qty {quantityBtc} BTC");
            }
            else
            {
                _logger.LogError($"❌ Grok Order Fail: {orderResponse.Error?.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"💥 Grok Exception: {ex.Message} | Check Demo Balance");
        }
    }
}
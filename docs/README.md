# Market Data C#/.NET SDK

This directory contains detailed documentation for the official Market Data C#/.NET
SDK, maintained by [Market Data](https://www.marketdata.app/). Originally created by
Omid Rad (Exceptal) and donated to Market Data.

The SDK targets .NET 8 and .NET 10 (both LTS) with the latest stable C# language
version. Public endpoint methods are asynchronous, accept `CancellationToken`, and
return typed response records or CSV responses.

## Getting started

1. [Install the NuGet package](installation.md).
2. [Configure credentials](authentication.md) with .NET configuration and user-secrets.
3. [Create and register the client](client.md), including `HttpClient` ownership and DI.
4. [Configure requests](settings.md) with `MarketDataClientOptions` and
   `MarketDataRequestOptions`.
5. Explore the endpoint APIs:
   - [Stocks](stocks/README.md)
   - [Options](options/README.md)
   - [Funds](funds/README.md)
   - [Markets](markets/README.md)
   - [Utilities](utilities/README.md)

The repository root [README](../README.md) contains the complete feature overview,
retry and exception guidance, diagnostics, test instructions, and known limitations.

## API contract

The live [OpenAPI schema](https://api.marketdata.app/schema/) is the primary source
for versioned endpoint paths and wire parameters. Funds and Utilities are implemented
SDK surfaces but are currently absent from that schema. Bulk stock candles remain
deferred because the schema's path definition is inconsistent.


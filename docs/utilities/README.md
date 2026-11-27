# Utilities (C# SDK)

The C#/.NET SDK from Market Data provides methods for the Utilities endpoints: API service status, request-header inspection, authenticated-user information, and token deletion. These endpoints take no request parameters beyond a `CancellationToken` and have no CSV variants.

Reach the resource through `client.Utilities`.

## Utilities Endpoints

- [API Status (C# SDK)](./status.md)
- [Headers (C# SDK)](./headers.md) — Echo back the HTTP headers the Market Data API received, using the C# SDK, to debug what your application or a proxy actually sends.
- [User (C# SDK)](./user.md)
- [Delete Token (C# SDK)](./delete-token.md) — Delete the authenticated account's current API token.

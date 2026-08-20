# Utilities

The C#/.NET SDK from Market Data provides methods for the Utilities endpoints: API service status, request-header inspection, and authenticated-user information. These are diagnostic endpoints, so they take no request parameters beyond a `CancellationToken` and have no CSV variants.

Reach the resource through `client.Utilities`.

## Utilities Endpoints

- [Status](./status.md)
- [Headers](./headers.md)
- [User](./user.md)

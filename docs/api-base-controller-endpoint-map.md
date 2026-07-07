# API Base Controller Endpoint Map

This map covers the public API endpoints exposed by the base controllers in the `Umbrella.AspNetCore.WebUtilities.Mvc` namespace:

- `UmbrellaGenericRepositoryApiController`
- `UmbrellaGenericRepositoryDataServiceApiController`

The map describes the reusable base controller contract, including supported virtual hooks/callbacks and custom data-service implementations. Derived controllers and services that introduce response codes outside this contract should add their own endpoint-specific attributes.

## Controller-Level Responses

| Controller | Class-Level Responses |
| --- | --- |
| `UmbrellaGenericRepositoryApiController` | `401`, `403`, `405`, `422`, `500` |
| `UmbrellaGenericRepositoryDataServiceApiController` | `401`, `403`, `405`, `500` |

## Consolidated Endpoint Map

| Endpoint | Controllers | Inputs | Method-Level Responses | Effective Responses |
| --- | --- | --- | --- | --- |
| `GET SearchSlim` | Both | `pageNumber`, `pageSize`, `sorters`, `filters`, `filterCombinator`, `CancellationToken` | Repository: `200`<br>Data service: `200`, `422` | Repository: `200`, `401`, `403`, `405`, `422`, `500`<br>Data service: `200`, `401`, `403`, `405`, `422`, `500` |
| `GET` | Both | `id`, `CancellationToken` | Repository: `200`, `404`<br>Data service: `200`, `404`, `422` | Repository: `200`, `401`, `403`, `404`, `405`, `422`, `500`<br>Data service: `200`, `401`, `403`, `404`, `405`, `422`, `500` |
| `POST` | Both | Body model, `CancellationToken` | Repository: `201`, `400`, `409`<br>Data service: `201`, `400`, `409`, `422` | Repository: `201`, `400`, `401`, `403`, `405`, `409`, `422`, `500`<br>Data service: `201`, `400`, `401`, `403`, `405`, `409`, `422`, `500` |
| `PUT` | Both | Body model, `CancellationToken` | Repository: `200`, `400`, `404`, `409`<br>Data service: `200`, `400`, `404`, `409`, `422` | Repository: `200`, `400`, `401`, `403`, `404`, `405`, `409`, `422`, `500`<br>Data service: `200`, `400`, `401`, `403`, `404`, `405`, `409`, `422`, `500` |
| `DELETE` | Both | `id`, `CancellationToken` | Repository: `204`, `404`, `409`<br>Data service: `204`, `404`, `409`, `422` | Repository: `204`, `401`, `403`, `404`, `405`, `409`, `422`, `500`<br>Data service: `204`, `401`, `403`, `404`, `405`, `409`, `422`, `500` |
| `GET ExistsById` | `UmbrellaGenericRepositoryDataServiceApiController` only | `id`, `CancellationToken` | `200`, `422` | `200`, `401`, `403`, `405`, `422`, `500` |
| `GET TotalCount` | `UmbrellaGenericRepositoryDataServiceApiController` only | `CancellationToken` | `200` | `200`, `401`, `403`, `405`, `500` |

`409` on `POST` and `DELETE` is extension-point driven. It can be returned by supported virtual hooks/callbacks or custom data-service implementations, but is not produced by the default built-in create/delete path when those hooks return `null`.

## Input Type Differences

`SearchSlim` differs slightly between the two controllers:

| Controller | Sorters Input | Filters Input |
| --- | --- | --- |
| `UmbrellaGenericRepositoryApiController` | `[FromQuery] SortExpression<TEntity>[]? sorters` | `[FromQuery] FilterExpression<TEntity>[]? filters` |
| `UmbrellaGenericRepositoryDataServiceApiController` | `[FromQuery] IEnumerable<SortExpressionDescriptor>? sorters` | `[FromQuery] IEnumerable<FilterExpressionDescriptor>? filters` |

All other shared endpoints are conceptually aligned, with model and key type names differing by generic controller design.

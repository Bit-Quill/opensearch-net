`OpenSearch.Client` is a high level OpenSearch .NET client that still maps very closely to the original OpenSearch API. All requests and responses are exposed through types, making it ideal for getting up and running quickly.

Under the covers, `OpenSearch.Client` uses the `OpenSearch.Net` low level client to dispatch requests and responses, using and extending many of the types within `OpenSearch.Net`. The low level client itself is still exposed on the high level client through the `.LowLevel` property.

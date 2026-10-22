# Payment Gateway

An ASP.NET Core (.NET 8) API that lets a merchant process a card payment through an acquiring bank and retrieve it later. The acquiring bank is the Mountebank simulator that comes with the challenge.

## Running it

You need Docker. To run the API outside Docker or to run the tests, you also need the .NET 8 SDK (a newer SDK works too).

### Everything in Docker

```bash
docker compose up --build
```

The API listens on http://localhost:5067 and the bank simulator on http://localhost:8080.

### API on your machine, simulator in Docker

```bash
docker compose up -d bank_simulator
dotnet run --project src/PaymentGateway.Api
```

The API uses the same port, so stop the `payment_gateway` container first if it is running. This runs in the Development environment, where Swagger UI is available at http://localhost:5067/swagger.

### Tests

```bash
docker compose up -d bank_simulator
dotnet test
```

The two tests in `BankSimulatorTests` call the simulator and fail if it is not running. All other tests run in memory.

## Try it

```bash
curl -i -X POST http://localhost:5067/api/Payments \
  -H "Content-Type: application/json" \
  -d '{"cardNumber":"2222405343248877","expiryMonth":4,"expiryYear":2030,"currency":"GBP","amount":100,"cvv":"123"}'
```

```
HTTP/1.1 201 Created
Location: http://localhost:5067/api/Payments/8f3bcb69-38f2-47c3-b6f5-9dc7f871c94a

{"id":"8f3bcb69-38f2-47c3-b6f5-9dc7f871c94a","status":"Authorized","cardNumberLastFour":"8877","expiryMonth":4,"expiryYear":2030,"currency":"GBP","amount":100}
```

The simulator decides by the last digit of the card number. Changing the card number in the request above gives:

| Card number | Response |
|---|---|
| `2222405343248877` (odd) | `201 Created`, `Authorized` |
| `2222405343248878` (even) | `201 Created`, `Declined` |
| `2222405343248870` (zero) | `502 Bad Gateway`, the bank returned 503 |
| `1234` | `422 Unprocessable Entity`, rejected without calling the bank |

To retrieve a payment, follow the `Location` header:

```bash
curl -i http://localhost:5067/api/Payments/8f3bcb69-38f2-47c3-b6f5-9dc7f871c94a
```

## API

### `POST /api/Payments`

| Field | Rules |
|---|---|
| `cardNumber` | Required. 14 to 19 characters, digits only. |
| `expiryMonth` | Required. 1 to 12. |
| `expiryYear` | Required. Together with the month, must not be in the past. |
| `currency` | Required. One of `GBP`, `USD`, `EUR`. |
| `amount` | Required. Whole number greater than zero, in the minor currency unit (1050 is 10.50). |
| `cvv` | Required. 3 or 4 characters, digits only. |

| Status | Meaning |
|---|---|
| `201 Created` | The bank authorized or declined the payment. The body is the payment, and `Location` points to it. |
| `422 Unprocessable Entity` | Rejected. The request broke one or more rules, the bank was not called and no payment was created. The body lists the problems per field. |
| `400 Bad Request` | The body could not be read at all, for example malformed JSON or text where a number is expected. |
| `502 Bad Gateway` | The bank gave no decision (unreachable, timed out or returned an error). No payment was created. |

### `GET /api/Payments/{id}`

Returns `200 OK` with the payment, or `404 Not Found`.

A payment looks like this. Only the last four digits of the card are ever returned.

```json
{
  "id": "8f3bcb69-38f2-47c3-b6f5-9dc7f871c94a",
  "status": "Authorized",
  "cardNumberLastFour": "8877",
  "expiryMonth": 4,
  "expiryYear": 2030,
  "currency": "GBP",
  "amount": 100
}
```

### Errors

Errors use the standard problem details format (RFC 9457) and include a `traceId`. A merchant can quote it to support, and the same id is on every log line for that request.

```json
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "Payment rejected",
  "status": 422,
  "detail": "The payment was not sent to the acquiring bank because the request is invalid.",
  "errors": {
    "cardNumber": ["Card number must be between 14 and 19 characters long."],
    "expiryMonth": ["Expiry month must be between 1 and 12."]
  },
  "traceId": "00-1eec83de0532d6202c5ee66d9b97f152-d20d999e9adbc065-00"
}
```

## Design decisions

**Rejected requests are not stored.** The spec says no payment is created when a request is rejected, and a stored payment can only be `Authorized` or `Declined`. That is why `Rejected` is a `422` response and not a payment status. Storing the raw request would also mean storing card data, including the CVV, which PCI DSS does not allow. Rejections are still visible: each one is logged with the failing field names and counted in a metric, which is enough to spot a merchant with a broken integration or a burst that looks like card testing. 422 is used because the request can be read but is invalid, which is also what Checkout's own API returns for invalid payment data.

**When the bank gives no decision, the gateway returns 502 and stores nothing.** That covers an unreachable bank, a timeout, an error status and a response that can't be read. There is no automatic retry, because an authorization is not idempotent and sending it twice could charge the shopper twice. A timeout is the awkward case: the bank may have authorized the payment and only the response was lost. This is covered under idempotency in "What could be done next".

**Card data.** Only the last four digits are stored and returned. The CVV is never stored. Logs contain payment ids, outcomes and field names, never card data, and validation messages don't echo the submitted values. The request models that carry the card number are classes rather than records, so their generated `ToString` can't print it into a log. The bank's authorization code is stored (it would be needed for capture, refunds and reconciliation) but not returned.

**Structure.** The controller deals with HTTP only: it validates the request, calls `PaymentService` and maps the outcome to a status code. `PaymentService` calls the bank and stores the result. Each boundary has its own models. `PostPaymentRequest` is what the merchant sends and `ValidatedPaymentRequest` is what is left after validation, so unvalidated data can't reach the bank. `Payment` is what is stored and `PaymentResponse` is what the merchant sees. The bank's JSON format stays inside the `AcquiringBank` folder.

**Validation.** A small hand-written validator reports every failing field at once. It reads the current time through `TimeProvider`, so the expiry rules can be tested against fixed dates. The request model has nullable fields on purpose: a missing field reaches the validator and gets a clear message, instead of failing during deserialization with a message about .NET types.

**Configuration and hosting.** The bank's URL and timeout come from configuration and are validated at startup, so a bad setting stops the app from starting instead of failing on the first payment. In Docker the URL is set through an environment variable. The image is built in two stages and runs as a non-root user. The app does not redirect HTTP to HTTPS: TLS is expected to be terminated in front of the service, and for an API a redirect doesn't protect anything, since the card data has already been sent by the time the redirect comes back.

**Observability.** On top of the logs and the `traceId` described above, logs are written as JSON in Docker, with the trace id on every line. A `payments` counter (meter `PaymentGateway`) counts requests by outcome: `authorized`, `declined`, `rejected` and `bank_unavailable`. .NET's built-in metrics already cover request rates and durations by status code, and calls to the bank. With the API running locally:

```bash
dotnet-counters monitor -n PaymentGateway.Api --counters PaymentGateway,Microsoft.AspNetCore.Hosting,System.Net.Http
```

**Testing.** There are three layers.
- Unit tests for the validator and the bank client. The bank client tests use a stub `HttpMessageHandler`, so they cover timeouts, error statuses and unreadable responses that the simulator can't produce.
- API tests that run the whole app in memory with a fake bank and a fixed clock, so they don't depend on Docker or on today's date.
- Two end-to-end tests against the simulator, with nothing replaced.

The validator tests check which field was rejected rather than the wording of the message. Each invalid input breaks exactly one rule, so a rejection proves that rule works. The exact messages are checked once, in an API test, because they are what merchants see.

## Assumptions

- There is no merchant authentication in this phase. In production the merchant would be identified from their API key, and logs and metrics would carry the merchant id.
- Payments are kept in memory and are lost on restart, as the spec allows.
- Supported currencies are `GBP`, `USD` and `EUR`, and must be upper case.
- A card is valid until the end of its expiry month, checked in UTC. The issuer has the final say anyway, so the gateway avoids being stricter than it needs to be.
- The amount must be greater than zero. A zero amount authorization is a different flow (card verification).
- Card numbers must be digits only, so a number with spaces is rejected rather than cleaned up.
- A body that can't be read gets ASP.NET's standard `400` response. Its messages mention .NET type names; that is cosmetic, and customizing it wasn't worth the extra code.

## What could be done next

- **Idempotency.** A payment can reach the gateway twice: a merchant retries after a timeout, or a shopper double-clicks and the merchant sends both requests. Today both would be charged. The next step would be an `Idempotency-Key` header, where the merchant sends the same key for the same purchase attempt (for example derived from the order id) rather than a new one per request. Before calling the bank, the gateway would claim the key by inserting it into a table with a unique constraint on merchant and key, so when two requests arrive together the database lets only one of them through. A later request with the same key gets the stored result, a key reused with a different body is rejected, and a request that arrives while the first one is still in flight gets a `409`. Keys must be scoped to the merchant, so this depends on merchant authentication. If the bank was never reached, the key can be released so the merchant can retry. After a timeout it can't, because the bank may have authorized the payment; that case has to be settled with the bank first, which needs the bank to accept an idempotency key or a reference it can be queried by.
- **Tokenization.** In production, card data would be tokenized before it reaches the gateway, for example in the browser through a hosted payment field, so only a small isolated vault handles the card number. The gateway would detokenize right before calling the bank.
- **Error codes.** Machine-readable codes next to the messages (for example `cvv_invalid`), so merchants can handle errors without parsing text.
- **Observability.** Export metrics and traces with OpenTelemetry, and alert on the bank failure rate, sudden rises in rejections and latency.
- **End-to-end tests in CI**, starting the simulator with docker compose.
- **Persistent storage** behind `IPaymentsRepository`.
- **Move to .NET 10 (LTS).** .NET 8 reaches end of support on 10 November 2026 and stops receiving security patches. The upgrade would be its own change: check the breaking changes, run the full test suite, compare performance, roll out gradually and keep the previous image ready for rollback.
- **Refresh dependencies.** Some template packages are several versions behind (`Microsoft.AspNetCore.Mvc.Testing` 6.0, `xunit` 2.4, `Swashbuckle.AspNetCore` 6.2) and pull in packages with known advisories (`dotnet list package --vulnerable --include-transitive`). Most are test only, but Swagger UI ships with the API. .NET's own `Microsoft.AspNetCore.OpenApi` could replace Swashbuckle.

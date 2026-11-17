# Payment gateway

An API that lets a merchant take card payments through an acquiring bank and look them up later. It validates the request, rejects bad input without calling the bank, asks the bank (a Mountebank simulator) to authorize, and stores the outcome in memory. Only the last four card digits are kept. Merchant auth, real storage and refunds are out of scope.

## Run

```bash
docker compose up --build
```

- API: http://localhost:5050
- Swagger: http://localhost:5050/swagger
- health: http://localhost:5050/health
- Aspire dashboard: http://127.0.0.1:18888

## Try it

```bash
curl -i -X POST http://localhost:5050/api/payments \
  -H 'Content-Type: application/json' \
  -d '{"cardNumber":"2222405343248877","expiryMonth":4,"expiryYear":2030,"currency":"GBP","amount":100,"cvv":"123"}'
```

Then GET the `Location` from the response. Card ending odd: Authorized, even: Declined, 0: bank down (502). Invalid input: 400 Rejected.

Send an `Idempotency-Key` header to make retries safe: the same key and body return the original payment, a different body gets 422. If the bank times out the outcome is unknown. The bank simulator is static and has no way to look up a past payment, so retries with that key are refused instead of risking a second charge. A bank with a payment lookup would let the gateway check and then retry safely.

## Test

```bash
dotnet test
```

No docker needed, the bank is faked.

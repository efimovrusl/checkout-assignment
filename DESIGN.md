# Design

The acquiring bank is a static Mountebank simulator (`imposters/bank_simulator.ejs`). It only accepts `POST /payments`, answers by the card's last digit and keeps no state. Most decisions below follow from that.

- No lookup of past payments, so a timeout or unreadable answer leaves the outcome unknown. The idempotency key is then locked and retries get 502 instead of risking a second charge.
- No idempotency on the bank side, so the gateway never retries the bank call itself.
- A 503 (card ending in 0) means the bank did not process the payment, so the key is released and a retry is allowed.
- The simulator returns 400 for missing fields. The gateway validates everything first, so a bank 4xx is a gateway bug and becomes 502, not the merchant's fault.
- The bank contract (snake_case fields, `MM/yyyy` expiry) is fixed, so the bank client maps to it and the simulator file is left untouched.
- The authorization code has no use without capture or refund endpoints, so it is not stored.
- Answers depend only on the last digit, so tests fake the bank and the simulator is for running the app.

## Other choices

- Rejected: the gateway refused bad input (400, bank never called). Declined: the bank said no (201, stored).
- Only the last four card digits are stored and returned. Card data is never logged or traced.
- Payments and idempotency keys live in memory. There is no merchant auth, so keys are global.

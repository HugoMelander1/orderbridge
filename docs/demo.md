# Demo guide

Start with `docker compose up --build`, then open http://localhost:5173.

## Main scenario

1. In the clearly labeled local demo panel, select **TemporaryError** and click **Apply mode**.
2. Create an order containing one keyboard.
3. Open its details. The timeline shows HTTP 503 and a scheduled retry; Pending displays the next attempt time.
4. Select **Normal** and apply it before all four attempts are exhausted.
5. Wait for Reserved. Details show **Successful reservations: 1**. Follow the same correlation ID in `docker compose logs worker inventory`.

## Exhaustion and replay

Leave TemporaryError enabled for over 50 seconds after creating an order. After four failed attempts, status becomes Failed. The timeline says the message was routed to orders.dead. In RabbitMQ management at http://localhost:15672, inspect the durable orders.dead queue. Defaults: username orderbridge, password orderbridge-local (or your .env override).

Restore Normal, then click **Replay order** in Failed order details. It reaches Reserved with one successful reservation. The existing DLQ message remains diagnostic history.

## Business rejection and ambiguous timeout

Select Insufficient and a product, apply, and order that product. Status becomes Rejected immediately; no technical retry occurs.

Select Delayed and create an order. Calls take 12 seconds, exceeding the worker's 8-second timeout. The first warehouse transaction may succeed even though the worker schedules a retry. Restore Normal and observe one successful reservation.

## Restart durability

Stop only the worker: `docker compose stop worker`. Create an order: it remains Pending with an unpublished outbox row. Start the worker: `docker compose start worker`. The persisted message is published and the order is processed. Repeat while a retry is scheduled to demonstrate durable due times.

Data survives normal `docker compose down`. To intentionally discard **all local demo data**, use `docker compose down -v`; the next start reapplies migrations and seeds the three products. This is destructive to local demo volumes.

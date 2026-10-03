import {
  SimulationStore,
  type DemoOrder,
  type DemoSettings,
  type OrderLine,
} from "./SimulationStore";
import { SimulationClock } from "./SimulationClock";

export class SimulationService {
  private eventId = 0;
  constructor(
    readonly store: SimulationStore,
    readonly clock: SimulationClock,
  ) {}

  private event(order: DemoOrder, detail: string) {
    order.events.push({
      id: ++this.eventId,
      at: new Date(this.clock.at).toISOString(),
      detail,
    });
  }
  private reserve(order: DemoOrder, unavailable: string | null) {
    if (this.store.decisions.has(order.id))
      return this.store.decisions.get(order.id)!;
    const accepted = order.lines.every(
      (line) =>
        line.sku !== unavailable &&
        this.store.products.find((p) => p.sku === line.sku)!.stock >=
          line.quantity,
    );
    if (accepted)
      for (const line of order.lines)
        this.store.products.find((p) => p.sku === line.sku)!.stock -=
          line.quantity;
    this.store.decisions.set(order.id, accepted);
    return accepted;
  }
  private outcome(order: DemoOrder, reserved: boolean) {
    order.status = reserved ? "Reserved" : "Rejected";
    order.error = reserved ? null : "Insufficient inventory.";
    order.nextAttemptAt = null;
    this.event(
      order,
      reserved
        ? "Simulated warehouse reservation confirmed."
        : "Insufficient stock. No retry scheduled.",
    );
  }
  private failure(order: DemoOrder, message: string) {
    order.error = message;
    const delay = [5000, 15000, 30000][order.attempts - 1];
    if (delay === undefined) {
      order.status = "Failed";
      order.nextAttemptAt = null;
      this.event(
        order,
        message + " Attempts exhausted; simulated dead letter: orders.dead.",
      );
      return;
    }
    order.status = "Pending";
    order.nextAttemptAt = new Date(this.clock.at + delay).toISOString();
    this.event(order, message + ` Retry scheduled in ${delay / 1000} seconds.`);
    const generation = order.generation;
    this.clock.schedule(delay, () => {
      if (order.generation === generation) this.attempt(order);
    });
  }
  private attempt(order: DemoOrder) {
    order.status = "Processing";
    order.nextAttemptAt = null;
    order.attempts++;
    this.event(
      order,
      `Simulated delivery ${order.attempts}: contacting warehouse.`,
    );
    if (this.store.settings.mode === "TemporaryError") {
      this.failure(order, "Simulated HTTP 503.");
    } else if (this.store.settings.mode === "Delayed") {
      // The warehouse can commit after the caller times out. The key survives replay.
      this.clock.schedule(12000, () => this.reserve(order, null));
      const generation = order.generation;
      this.clock.schedule(8000, () => {
        if (order.generation === generation)
          this.failure(order, "Simulated warehouse timeout.");
      });
    } else {
      this.outcome(
        order,
        this.reserve(
          order,
          this.store.settings.mode === "Insufficient"
            ? this.store.settings.sku
            : null,
        ),
      );
    }
  }

  create(inputOrder: { customer: string; items: OrderLine[] }) {
    const input = inputOrder;
    if (!input.customer?.trim() || input.customer.length > 100)
      throw new Error("Customer must contain 1–100 characters.");
    if (
      !Array.isArray(input.items) ||
      input.items.length < 1 ||
      input.items.length > 10 ||
      new Set(input.items.map((l) => l.sku)).size !== input.items.length ||
      input.items.some(
        (l) =>
          !Number.isInteger(l.quantity) ||
          l.quantity < 1 ||
          l.quantity > 100 ||
          !this.store.products.some((p) => p.sku === l.sku),
      )
    )
      throw new Error(
        "Select distinct products with quantities between 1 and 100.",
      );
    const order: DemoOrder = {
      id: crypto.randomUUID(),
      correlationId: crypto.randomUUID(),
      customer: input.customer.trim(),
      status: "Pending",
      itemsJson: JSON.stringify(
        input.items.map((l) => ({ Sku: l.sku, Quantity: l.quantity })),
      ),
      lines: input.items.map((l) => ({ ...l })),
      createdAt: new Date(this.clock.at).toISOString(),
      attempts: 0,
      generation: 0,
      error: null,
      nextAttemptAt: null,
      events: [],
    };
    this.store.orders.unshift(order);
    this.event(order, "Order created; simulated outbox message queued.");
    this.clock.schedule(500, () => this.attempt(order));
    return order;
  }

  replay(id: string) {
    const order = this.store.findOrder(id);
    if (order.status !== "Failed")
      throw new Error("Only failed orders can be replayed.");
    order.generation++;
    order.status = "Pending";
    order.attempts = 0;
    order.error = null;
    order.nextAttemptAt = null;
    this.event(order, "Replay queued with the original reservation key.");
    this.clock.schedule(500, () => this.attempt(order));
    return order;
  }

  changeMode(settings: DemoSettings) {
    if (
      !["Normal", "TemporaryError", "Delayed", "Insufficient"].includes(
        settings.mode,
      ) ||
      (settings.mode === "Insufficient" &&
        !this.store.products.some((p) => p.sku === settings.sku))
    )
      throw new Error("Select a supported mode and product.");
    this.store.settings = { ...settings };
    return this.store.settings;
  }
}

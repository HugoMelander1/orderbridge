import { describe, expect, it } from "vitest";
import { Simulation } from "./simulation";
import type { Order, Product } from "./api";

function demo() {
  let now = Date.now();
  const api = new Simulation(() => now);
  return {
    api,
    advance: (ms: number) => {
      now += ms;
    },
  };
}
const input = {
  customer: "Demo visitor",
  items: [{ sku: "KB-01", quantity: 2 }],
};

describe("browser simulation", () => {
  it("reserves stock and isolates each visitor", async () => {
    const { api, advance } = demo();
    const order = await api.request<Order>("/orders", input);
    advance(1000);
    expect((await api.request<Order>("/orders/" + order.id)).status).toBe(
      "Reserved",
    );
    expect((await api.request<Product[]>("/products"))[0].stock).toBe(98);
    expect(
      (await new Simulation().request<Product[]>("/products"))[0].stock,
    ).toBe(100);
  });
  it("exhausts four attempts and replays after recovery", async () => {
    const { api, advance } = demo();
    await api.request("/demo", { mode: "TemporaryError", sku: null });
    const order = await api.request<Order>("/orders", input);
    advance(51000);
    const failed = await api.request<Order>("/orders/" + order.id);
    expect(failed.status).toBe("Failed");
    expect(failed.attempts).toBe(4);
    expect(failed.events.at(-1)?.detail).toContain("orders.dead");
    await api.request("/demo", { mode: "Normal", sku: null });
    await api.request("/orders/" + order.id + "/replay", {});
    advance(1000);
    expect((await api.request<Order>("/orders/" + order.id)).status).toBe(
      "Reserved",
    );
    expect((await api.request<Product[]>("/products"))[0].stock).toBe(98);
  });
  it("keeps one reservation after lost responses, exhaustion and replay", async () => {
    const { api, advance } = demo();
    await api.request("/demo", { mode: "Delayed", sku: null });
    const order = await api.request<Order>("/orders", input);
    advance(100000);
    expect((await api.request<Order>("/orders/" + order.id)).status).toBe(
      "Failed",
    );
    await api.request("/demo", { mode: "Normal", sku: null });
    await api.request("/orders/" + order.id + "/replay", {});
    advance(1000);
    expect((await api.request<Order>("/orders/" + order.id)).status).toBe(
      "Reserved",
    );
    expect(await api.request("/reservations/" + order.id)).toEqual({
      count: 1,
    });
    expect((await api.request<Product[]>("/products"))[0].stock).toBe(98);
  });
  it("rejects insufficient stock without retry or partial stock changes", async () => {
    const { api, advance } = demo();
    await api.request("/demo", { mode: "Insufficient", sku: "KB-01" });
    const order = await api.request<Order>("/orders", {
      ...input,
      items: [...input.items, { sku: "MS-02", quantity: 1 }],
    });
    advance(1000);
    const rejected = await api.request<Order>("/orders/" + order.id);
    expect(rejected.status).toBe("Rejected");
    expect(rejected.attempts).toBe(1);
    expect(
      (await api.request<Product[]>("/products")).every((p) => p.stock === 100),
    ).toBe(true);
  });
});

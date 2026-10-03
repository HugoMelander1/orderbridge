import type { Order, Product } from "../api";

export type OrderLine = { sku: string; quantity: number };
export type DemoSettings = { mode: string; sku: string | null };
export type DemoOrder = Order & { generation: number; lines: OrderLine[] };

/** Data owned by one visitor's simulation. No shared server state. */
export class SimulationStore {
  readonly products: Product[] = [
    { sku: "KB-01", name: "Mechanical keyboard", stock: 100 },
    { sku: "MS-02", name: "Wireless mouse", stock: 100 },
    { sku: "DK-03", name: "USB-C dock", stock: 100 },
  ];
  readonly orders: DemoOrder[] = [];
  readonly decisions = new Map<string, boolean>();
  settings: DemoSettings = { mode: "Normal", sku: "KB-01" };

  findOrder(id: string): DemoOrder {
    const order = this.orders.find((order) => order.id === id);
    if (!order) throw new Error("Order not found.");
    return order;
  }

  listOrders(search: string, status: string | null, page: number) {
    const query = search.toLowerCase();
    const orders = this.orders.filter(
      (order) =>
        (!status || order.status === status) &&
        (order.customer.toLowerCase().includes(query) ||
          order.id.includes(query)),
    );
    return {
      total: orders.length,
      items: orders.slice((page - 1) * 10, page * 10),
    };
  }

  counts() {
    return ["Pending", "Processing", "Reserved", "Rejected", "Failed"].map(
      (status) => ({
        status,
        count: this.orders.filter((order) => order.status === status).length,
      }),
    );
  }
}

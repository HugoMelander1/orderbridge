import {
  SimulationStore,
  type DemoSettings,
  type OrderLine,
} from "./demo/SimulationStore";
import { SimulationClock } from "./demo/SimulationClock";
import { SimulationService } from "./demo/SimulationService";

/** API adapter used by the existing UI. Processing lives in SimulationService. */
export class Simulation {
  private readonly service: SimulationService;
  constructor(now = () => Date.now()) {
    this.service = new SimulationService(
      new SimulationStore(),
      new SimulationClock(now),
    );
  }

  async request<T>(path: string, body?: unknown): Promise<T> {
    const { store, clock } = this.service;
    clock.advance();
    const url = new URL(path, "https://demo.invalid");
    const route = url.pathname;
    let result: unknown;
    if (route === "/products" && body === undefined) result = store.products;
    else if (route === "/dependencies")
      result = {
        postgres: false,
        rabbit: false,
        inventory: false,
        demoEnabled: true,
      };
    else if (route === "/dashboard") result = store.counts();
    else if (route === "/demo")
      result =
        body === undefined
          ? store.settings
          : this.service.changeMode(body as DemoSettings);
    else if (route === "/orders" && body !== undefined)
      result = this.service.create(
        body as { customer: string; items: OrderLine[] },
      );
    else if (route === "/orders")
      result = store.listOrders(
        url.searchParams.get("search") ?? "",
        url.searchParams.get("status"),
        Math.max(1, Number(url.searchParams.get("page")) || 1),
      );
    else if (route.startsWith("/reservations/"))
      result = {
        count: store.decisions.get(route.split("/")[2]) === true ? 1 : 0,
      };
    else if (route.startsWith("/orders/"))
      result =
        route.endsWith("/replay") && body !== undefined
          ? this.service.replay(route.split("/")[2])
          : store.findOrder(route.split("/")[2]);
    else throw new Error("Unknown demo route.");
    return structuredClone(result) as T;
  }
}

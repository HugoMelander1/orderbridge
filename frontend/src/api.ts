export type Product = { sku: string; name: string; stock: number };
export type Order = {
  id: string;
  customer: string;
  status: string;
  itemsJson: string;
  createdAt: string;
  attempts: number;
  correlationId: string;
  error: string | null;
  nextAttemptAt: string | null;
  events: { id: number; at: string; detail: string }[];
};
export class ApiClient {
  async request<T>(path: string, body?: unknown): Promise<T> {
    const response = await fetch("/api" + path, {
      method: body === undefined ? "GET" : "POST",
      headers: { "Content-Type": "application/json" },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!response.ok) {
      const error = await response.json().catch(() => ({}));
      throw new Error(error.detail || "Request failed. Please try again.");
    }
    return response.json();
  }
}
export const isSimulation = import.meta.env.VITE_DEMO_MODE === "true";
export const api = isSimulation
  ? new (await import("./simulation")).Simulation()
  : new ApiClient();

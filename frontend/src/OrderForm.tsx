import { useState, type FormEvent } from "react";
import type { Product } from "./api";
export function OrderForm({
  products,
  onSubmit,
  busy,
}: {
  products: Product[];
  onSubmit: (body: {
    customer: string;
    items: { sku: string; quantity: number }[];
  }) => Promise<void>;
  busy: boolean;
}) {
  const [customer, setCustomer] = useState("");
  const [quantities, setQuantities] = useState<Record<string, number>>({});
  const [error, setError] = useState("");
  async function submit(e: FormEvent) {
    e.preventDefault();
    const items = products
      .filter((p) => (quantities[p.sku] || 0) > 0)
      .map((p) => ({ sku: p.sku, quantity: quantities[p.sku] }));
    if (!items.length) {
      setError("Select at least one product.");
      return;
    }
    setError("");
    try {
      await onSubmit({ customer, items });
    } catch (e) {
      setError((e as Error).message);
    }
  }
  return (
    <form onSubmit={submit}>
      <label>
        Customer name
        <input
          required
          maxLength={100}
          value={customer}
          onChange={(e) => setCustomer(e.target.value)}
          placeholder="e.g. Acme Studio"
        />
      </label>
      <p className="muted">Choose products and quantities to reserve.</p>
      {products.map((p) => (
        <label className="product" key={p.sku}>
          <span>
            <strong>{p.name}</strong>
            <small>
              {p.sku} · {p.stock} available
            </small>
          </span>
          <input
            aria-label={p.name + " quantity"}
            type="number"
            min="0"
            max="100"
            value={quantities[p.sku] || 0}
            onChange={(e) =>
              setQuantities({ ...quantities, [p.sku]: Number(e.target.value) })
            }
          />
        </label>
      ))}
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
      <button className="primary wide" disabled={busy || !products.length}>
        {busy ? "Creating order…" : "Create order →"}
      </button>
    </form>
  );
}

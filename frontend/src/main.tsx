import React, { useEffect, useRef, useState } from "react";
import { createRoot } from "react-dom/client";
import { api, type Order, type Product } from "./api";
import { OrderForm } from "./OrderForm";
import "./style.css";
const statuses = ["Pending", "Processing", "Reserved", "Rejected", "Failed"];
function App() {
  const drawer = useRef<HTMLElement>(null);
  const [products, setProducts] = useState<Product[]>([]),
    [orders, setOrders] = useState<Order[]>([]),
    [counts, setCounts] = useState<Record<string, number>>({}),
    [total, setTotal] = useState<number | null>(null),
    [search, setSearch] = useState(""),
    [status, setStatus] = useState(""),
    [page, setPage] = useState(1),
    [selected, setSelected] = useState<Order | null>(null),
    [creating, setCreating] = useState(false),
    [busy, setBusy] = useState(false),
    [error, setError] = useState(""),
    [loading, setLoading] = useState(true);
  const [deps, setDeps] = useState<{
      postgres: boolean;
      rabbit: boolean;
      inventory: boolean;
      demoEnabled: boolean;
    } | null>(null),
    [mode, setMode] = useState("Normal"),
    [sku, setSku] = useState("KB-01"),
    [reservationCount, setReservationCount] = useState<number | null>(null);
  async function refresh() {
    try {
      const [list, stats, p, d] = await Promise.all([
        api.request<{ items: Order[]; total: number }>(
          "/orders?" +
            new URLSearchParams({ search, status, page: String(page) }),
        ),
        api.request<{ status: string; count: number }[]>("/dashboard"),
        api.request<Product[]>("/products"),
        api.request<NonNullable<typeof deps>>("/dependencies"),
      ]);
      setOrders(list.items);
      setTotal(list.total);
      setCounts(
        Object.fromEntries(
          statuses.map((s) => [
            s,
            stats.find((x) => x.status === s)?.count ?? 0,
          ]),
        ),
      );
      setProducts(p);
      setDeps(d);
      setError("");
      if (selected) {
        setSelected(await api.request<Order>("/orders/" + selected.id));
        if (d.demoEnabled)
          setReservationCount(
            (
              await api.request<{ count: number }>(
                "/reservations/" + selected.id,
              )
            ).count,
          );
      }
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }
  useEffect(() => {
    void refresh();
    const timer = setInterval(() => void refresh(), 2500);
    return () => clearInterval(timer);
  }, [search, status, page, selected?.id]);
  useEffect(() => {
    if (deps?.demoEnabled)
      api
        .request<{ mode: string; sku: string | null }>("/demo")
        .then((x) => {
          setMode(x.mode);
          if (x.sku) setSku(x.sku);
        })
        .catch((e) => setError(e.message));
  }, [deps?.demoEnabled]);
  useEffect(() => {
    if (!creating && !selected) return;
    const previous = document.activeElement as HTMLElement | null;
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const focusable = () =>
      Array.from(
        drawer.current?.querySelectorAll<HTMLElement>(
          "button:not(:disabled),input:not(:disabled),select:not(:disabled),a[href]",
        ) ?? [],
      );
    focusable()[0]?.focus();
    function keyboard(e: KeyboardEvent) {
      if (e.key === "Escape") {
        setCreating(false);
        setSelected(null);
      }
      if (e.key === "Tab") {
        const elements = focusable(),
          first = elements[0],
          last = elements.at(-1);
        if (e.shiftKey && document.activeElement === first) {
          e.preventDefault();
          last?.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
          e.preventDefault();
          first?.focus();
        }
      }
    }
    document.addEventListener("keydown", keyboard);
    return () => {
      document.removeEventListener("keydown", keyboard);
      document.body.style.overflow = previousOverflow;
      previous?.focus();
    };
  }, [creating, selected?.id]);
  async function action(fn: () => Promise<void>) {
    setBusy(true);
    try {
      await fn();
      await refresh();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="shell">
      <aside>
        <a className="brand" href="/">
          ▰ <span>OrderBridge</span>
        </a>
        <span className="eyebrow">INTEGRATION WORKSPACE</span>
        <nav>
          <a className="active" href="#orders">
            ▦ &nbsp; Order overview
          </a>
          <a href="#demo">⚙ &nbsp; Demo controls</a>
        </nav>
        <div className="aside-bottom">
          <span className="dot" /> Local development
          <small>
            At-least-once delivery
            <br />
            Idempotent reservations
          </small>
        </div>
      </aside>
      <main>
        <header>
          <span className="muted">Workspace / Orders</span>
          <span className="environment">DEVELOPMENT</span>
        </header>
        <section className="heading">
          <div>
            <span className="eyebrow">OPERATIONS</span>
            <h1>Every order. Connected.</h1>
            <p className="muted">
              Follow orders from request to warehouse reservation.
            </p>
          </div>
          <button className="primary" onClick={() => setCreating(true)}>
            ＋ New order
          </button>
        </section>
        <div className="dependencies" aria-label="Dependency status">
          {["postgres", "rabbit", "inventory"].map((key) => (
            <span key={key}>
              <i
                className={
                  "dot " +
                  (deps ? (deps[key as "postgres"] ? "up" : "down") : "unknown")
                }
              />
              {
                {
                  postgres: "PostgreSQL",
                  rabbit: "RabbitMQ",
                  inventory: "Warehouse API",
                }[key]
              }{" "}
              <small>
                {deps
                  ? deps[key as "postgres"]
                    ? "Connected"
                    : "Unavailable"
                  : loading
                    ? "Checking…"
                    : "Not checked"}
              </small>
            </span>
          ))}
          <small className="poll">↻ Updates every 2.5s</small>
        </div>
        {error && (
          <div role="alert" className="error banner">
            {error} <button onClick={() => void refresh()}>Retry</button>
          </div>
        )}
        <section className="stats">
          {statuses.map((s) => (
            <button
              key={s}
              onClick={() => {
                setStatus(status === s ? "" : s);
                setPage(1);
              }}
              className={status === s ? "chosen" : ""}
            >
              <span>
                <i className={"dot " + s} />
                {s}
              </span>
              <strong>{counts[s] ?? "—"}</strong>
              <small>
                {
                  {
                    Pending: "Awaiting dispatch or retry",
                    Processing: "Contacting warehouse",
                    Reserved: "Inventory secured",
                    Rejected: "Insufficient inventory",
                    Failed: "Needs your attention",
                  }[s]
                }
              </small>
            </button>
          ))}
        </section>
        <section className="panel" id="orders">
          <div className="panel-title">
            <h2>
              Orders <span className="count">{total ?? "—"}</span>
            </h2>
            <span className="muted">Request → Queue → Warehouse</span>
          </div>
          <div className="filters">
            <input
              aria-label="Search orders"
              placeholder="Search customer or order ID…"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
            />
            <select
              aria-label="Filter status"
              value={status}
              onChange={(e) => {
                setStatus(e.target.value);
                setPage(1);
              }}
            >
              <option value="">All statuses</option>
              {statuses.map((s) => (
                <option key={s}>{s}</option>
              ))}
            </select>
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Order / Customer</th>
                  <th>Created</th>
                  <th>Status</th>
                  <th>Retries</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {orders.map((o) => (
                  <tr key={o.id}>
                    <td>
                      <strong>{o.customer}</strong>
                      <small className="mono">
                        {o.id.slice(0, 8).toUpperCase()}
                      </small>
                    </td>
                    <td>{new Date(o.createdAt).toLocaleString()}</td>
                    <td>
                      <span className={"badge " + o.status}>{o.status}</span>
                      {o.nextAttemptAt && (
                        <small>
                          Retry at{" "}
                          {new Date(o.nextAttemptAt).toLocaleTimeString()}
                        </small>
                      )}
                    </td>
                    <td>{o.attempts}</td>
                    <td>
                      <button
                        aria-label={"View " + o.customer}
                        onClick={() => {
                          setSelected(o);
                          setReservationCount(null);
                        }}
                      >
                        View →
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {!orders.length && (
            <div className="empty">
              <span>◇</span>
              <h3>
                {loading
                  ? "Loading orders…"
                  : error
                    ? "The order API is unavailable"
                    : "Your integration starts here"}
              </h3>
              <p>
                {loading
                  ? "Connecting to the order API."
                  : error
                    ? "Start the backend, then retry the connection."
                    : "Create an order to watch the delivery pipeline in action."}
              </p>
            </div>
          )}
          <footer>
            <span>
              {total === null ? "Data unavailable" : total + " orders"} · Page{" "}
              {page} of {Math.max(1, Math.ceil((total ?? 0) / 10))}
            </span>
            <div>
              <button disabled={page === 1} onClick={() => setPage(page - 1)}>
                ← Previous
              </button>
              <button
                disabled={page * 10 >= (total ?? 0)}
                onClick={() => setPage(page + 1)}
              >
                Next →
              </button>
            </div>
          </footer>
        </section>
        {deps?.demoEnabled && (
          <section id="demo" className="panel demo">
            <div>
              <span className="eyebrow">LOCAL DEMO ONLY</span>
              <h2>Test the unexpected.</h2>
              <p className="muted">
                Simulate warehouse conditions and inspect real retry behavior.
              </p>
            </div>
            <div className="demo-controls">
              <label>
                Warehouse mode
                <select value={mode} onChange={(e) => setMode(e.target.value)}>
                  {["Normal", "Delayed", "TemporaryError", "Insufficient"].map(
                    (m) => (
                      <option key={m}>{m}</option>
                    ),
                  )}
                </select>
              </label>
              {mode === "Insufficient" && (
                <label>
                  Unavailable product
                  <select value={sku} onChange={(e) => setSku(e.target.value)}>
                    {products.map((p) => (
                      <option key={p.sku} value={p.sku}>
                        {p.name}
                      </option>
                    ))}
                  </select>
                </label>
              )}
              <button
                disabled={busy}
                onClick={() =>
                  void action(async () => {
                    await api.request("/demo", { mode, sku });
                  })
                }
              >
                Apply mode
              </button>
            </div>
          </section>
        )}
        <p className="footnote">
          OrderBridge · Reliable integrations, observable by design.
        </p>
      </main>
      {(creating || selected) && (
        <div
          className="overlay"
          onClick={() => {
            setCreating(false);
            setSelected(null);
          }}
        >
          <section
            className="drawer"
            ref={drawer}
            role="dialog"
            aria-modal="true"
            aria-label={creating ? "Create order" : "Order details"}
            onClick={(e) => e.stopPropagation()}
          >
            <button
              className="close"
              aria-label="Close dialog"
              onClick={() => {
                setCreating(false);
                setSelected(null);
              }}
            >
              ✕
            </button>
            {creating ? (
              <>
                <span className="eyebrow">NEW REQUEST</span>
                <h2>Create an order</h2>
                <OrderForm
                  products={products}
                  busy={busy}
                  onSubmit={async (body) => {
                    setBusy(true);
                    try {
                      const o = await api.request<Order>("/orders", body);
                      setCreating(false);
                      setSelected(o);
                      await refresh();
                    } finally {
                      setBusy(false);
                    }
                  }}
                />
              </>
            ) : (
              selected && (
                <>
                  <span className="eyebrow">ORDER DETAILS</span>
                  <h2>{selected.customer}</h2>
                  <span className={"badge " + selected.status}>
                    {selected.status}
                  </span>
                  <p className="mono break">{selected.id}</p>
                  <h3>Products</h3>
                  {(
                    JSON.parse(selected.itemsJson) as {
                      Sku: string;
                      Quantity: number;
                    }[]
                  ).map((line) => (
                    <div className="detail-line" key={line.Sku}>
                      <span>
                        {products.find((p) => p.sku === line.Sku)?.name ??
                          line.Sku}
                      </span>
                      <strong>× {line.Quantity}</strong>
                    </div>
                  ))}
                  <h3>Traceability</h3>
                  <small className="mono break">
                    Correlation: {selected.correlationId}
                  </small>
                  {reservationCount !== null && (
                    <p>
                      Successful reservations:{" "}
                      <strong>{reservationCount}</strong>
                    </p>
                  )}
                  {selected.error && <p className="error">{selected.error}</p>}
                  {selected.status === "Failed" && deps?.demoEnabled && (
                    <button
                      className="primary"
                      disabled={busy}
                      onClick={() =>
                        void action(async () => {
                          await api.request(
                            "/orders/" + selected.id + "/replay",
                            {},
                          );
                        })
                      }
                    >
                      Replay order
                    </button>
                  )}
                  <h3>Event timeline</h3>
                  <ol className="timeline">
                    {selected.events
                      ?.slice()
                      .sort((a, b) => a.id - b.id)
                      .map((e) => (
                        <li key={e.id}>
                          <small>{new Date(e.at).toLocaleString()}</small>
                          <p>{e.detail}</p>
                        </li>
                      ))}
                  </ol>
                </>
              )
            )}
          </section>
        </div>
      )}
    </div>
  );
}
createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);

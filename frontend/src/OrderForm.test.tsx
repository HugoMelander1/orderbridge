import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi } from "vitest";
import { OrderForm } from "./OrderForm";
describe("Order form", () => {
  it("requires a product and submits customer with selected quantity", async () => {
    const submit = vi.fn().mockResolvedValue(undefined),
      user = userEvent.setup();
    render(
      <OrderForm
        products={[{ sku: "KB-01", name: "Keyboard", stock: 100 }]}
        onSubmit={submit}
        busy={false}
      />,
    );
    await user.type(screen.getByLabelText("Customer name"), "Acme");
    await user.click(screen.getByRole("button", { name: /Create order/ }));
    expect(screen.getByRole("alert")).toHaveTextContent(
      "Select at least one product",
    );
    expect(submit).not.toHaveBeenCalled();
    await user.clear(screen.getByLabelText("Keyboard quantity"));
    await user.type(screen.getByLabelText("Keyboard quantity"), "2");
    await user.click(screen.getByRole("button", { name: /Create order/ }));
    expect(submit).toHaveBeenCalledWith({
      customer: "Acme",
      items: [{ sku: "KB-01", quantity: 2 }],
    });
  });
});

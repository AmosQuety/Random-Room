import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { RecoveryCodeCard } from "./RecoveryCodeCard";

const writeText = vi.fn();

describe("RecoveryCodeCard", () => {
  beforeEach(() => {
    writeText.mockReset().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", { value: { writeText }, configurable: true });
  });

  it("shows the code, what it is for, and that it is shown only once", () => {
    render(<RecoveryCodeCard code="7QX4-K9M2-VB3H-T8RD" />);

    expect(screen.getByText("7QX4-K9M2-VB3H-T8RD")).toBeInTheDocument();
    expect(screen.getByText(/forget your PIN/i)).toBeInTheDocument();
    expect(screen.getByText(/shown only once/i)).toBeInTheDocument();
    expect(screen.getByText(/do not share it/i)).toBeInTheDocument();
  });

  it("copies the code", async () => {
    render(<RecoveryCodeCard code="7QX4-K9M2-VB3H-T8RD" />);

    fireEvent.click(screen.getByRole("button", { name: /copy code/i }));

    await waitFor(() => expect(writeText).toHaveBeenCalledWith("7QX4-K9M2-VB3H-T8RD"));
    expect(await screen.findByRole("button", { name: /copied/i })).toBeInTheDocument();
  });

  it("takes its heading from the caller, so a replaced code is not called the original", () => {
    render(<RecoveryCodeCard code="AAAA-BBBB-CCCC-DDDD" heading="Your new recovery code" />);

    expect(screen.getByRole("heading", { name: "Your new recovery code" })).toBeInTheDocument();
  });
});

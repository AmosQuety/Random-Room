import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ApiError } from "../lib/api";
import type { PlayerView } from "../lib/types";
import { SeatControls } from "./SeatControls";

const players: PlayerView[] = [
  { name: "Amos", online: true, claimed: true },
  { name: "Lydia", online: false, claimed: true },
  { name: "James", online: false, claimed: false },
];

function renderControls(overrides: Partial<Parameters<typeof SeatControls>[0]> = {}) {
  const onReset = vi.fn().mockResolvedValue({ player: "Lydia", inviteToken: "fresh-token" });
  const view = render(<SeatControls slug="abc" roomTitle="Friday" hostPlayer="Amos" players={players} status="Completed" onReset={onReset} {...overrides} />);
  return { onReset, ...view };
}

const open = () => fireEvent.click(screen.getByText(/someone locked out/i));

describe("SeatControls", () => {
  it("lists every seat except the host's and says the host's own cannot be reset", () => {
    renderControls();
    open();

    expect(screen.getByText("Lydia")).toBeInTheDocument();
    expect(screen.getByText("James")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /reset pin for amos/i })).not.toBeInTheDocument();
    expect(screen.getByText(/your own seat cannot be reset/i)).toBeInTheDocument();
  });

  it("asks before resetting, and resets only on the second step", async () => {
    const { onReset } = renderControls();
    open();

    fireEvent.click(screen.getByRole("button", { name: /reset pin for lydia/i }));
    expect(onReset).not.toHaveBeenCalled();
    expect(screen.getByText(/their current device is signed out/i)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /yes, reset/i }));
    await waitFor(() => expect(onReset).toHaveBeenCalledWith("Lydia"));
  });

  it("shows the new one-time link once the seat is reset and the room shows it empty", async () => {
    const { rerender, onReset } = renderControls();
    open();
    fireEvent.click(screen.getByRole("button", { name: /reset pin for lydia/i }));
    fireEvent.click(screen.getByRole("button", { name: /yes, reset/i }));
    await waitFor(() => expect(onReset).toHaveBeenCalled());

    // The server pushes the emptied seat to everyone after a reset.
    const emptied = players.map((p) => (p.name === "Lydia" ? { ...p, claimed: false } : p));
    rerender(<SeatControls slug="abc" roomTitle="Friday" hostPlayer="Amos" players={emptied} status="Completed" onReset={onReset} />);

    expect(await screen.findByText(/\/room\/abc\/claim\/fresh-token/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /copy link for lydia/i })).toBeInTheDocument();
  });

  it("offers a new link for a seat that never joined, not a PIN reset", () => {
    renderControls();
    open();

    expect(screen.getByRole("button", { name: /get a new link for james/i })).toBeInTheDocument();
  });

  it("cannot reset anyone while a game is running, and says why", () => {
    renderControls({ status: "Active" });
    open();

    expect(screen.getByRole("button", { name: /reset pin for lydia/i })).toBeDisabled();
    expect(screen.getByText(/end the game first/i)).toBeInTheDocument();
  });

  it("shows the server's reason when a reset is refused", async () => {
    const onReset = vi.fn().mockRejectedValue(new ApiError("End the current game before resetting a seat.", 409));
    renderControls({ onReset });
    open();
    fireEvent.click(screen.getByRole("button", { name: /reset pin for lydia/i }));
    fireEvent.click(screen.getByRole("button", { name: /yes, reset/i }));

    expect(await screen.findByRole("alert")).toHaveTextContent("End the current game before resetting a seat.");
  });

  it("drops a link once its owner has used it", async () => {
    const { rerender, onReset } = renderControls({ players: players.map((p) => (p.name === "Lydia" ? { ...p, claimed: false } : p)) });
    open();
    fireEvent.click(screen.getByRole("button", { name: /get a new link for lydia/i }));
    fireEvent.click(screen.getByRole("button", { name: /yes, reset/i }));
    await screen.findByText(/claim\/fresh-token/);

    rerender(<SeatControls slug="abc" roomTitle="Friday" hostPlayer="Amos" players={players} status="Completed" onReset={onReset} />);

    await waitFor(() => expect(screen.queryByText(/claim\/fresh-token/)).not.toBeInTheDocument());
  });
});

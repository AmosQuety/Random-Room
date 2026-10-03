import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../lib/api";
import { RecoverHostScreen } from "./RecoverHostScreen";

const api = vi.hoisted(() => ({ recoverHost: vi.fn() }));
vi.mock("../lib/api", async (importOriginal) => ({ ...(await importOriginal<object>()), ...api }));

const recovery = { player: "Amos", inviteToken: "invite-tok", recoveryCode: "NEW1-CODE-ABCD-EFGH" };

function submitCode(code: string) {
  fireEvent.change(screen.getByLabelText("Recovery code"), { target: { value: code } });
  fireEvent.click(screen.getByRole("button", { name: /recover my seat/i }));
}

describe("RecoverHostScreen", () => {
  beforeEach(() => {
    api.recoverHost.mockReset().mockResolvedValue(recovery);
    window.history.replaceState({}, "", "/room/abc/recover");
  });

  it("cannot be submitted until a code is typed", () => {
    render(<RecoverHostScreen slug="abc" />);

    expect(screen.getByRole("button", { name: /recover my seat/i })).toBeDisabled();
  });

  it("sends the code as typed, for the server to tidy up", async () => {
    render(<RecoverHostScreen slug="abc" />);

    submitCode("7qx4 k9m2 vb3h t8rd");

    await waitFor(() => expect(api.recoverHost).toHaveBeenCalledWith("abc", "7qx4 k9m2 vb3h t8rd"));
  });

  it("shows the new code and will not continue until the host says they saved it", async () => {
    render(<RecoverHostScreen slug="abc" />);
    submitCode("7QX4-K9M2-VB3H-T8RD");

    expect(await screen.findByText("NEW1-CODE-ABCD-EFGH")).toBeInTheDocument();
    expect(screen.getByText(/welcome back, amos/i)).toBeInTheDocument();
    const next = screen.getByRole("button", { name: /choose a new pin/i });
    expect(next).toBeDisabled();

    fireEvent.click(screen.getByRole("checkbox", { name: /i have saved my new recovery code/i }));
    expect(next).toBeEnabled();
    fireEvent.click(next);

    expect(window.location.pathname).toBe("/room/abc/claim/invite-tok");
  });

  it("says when the code is wrong, and lets the host try again", async () => {
    api.recoverHost.mockRejectedValue(new ApiError("That recovery code is not right.", 403));
    render(<RecoverHostScreen slug="abc" />);

    submitCode("AAAA-AAAA-AAAA-AAAA");

    expect(await screen.findByRole("alert")).toHaveTextContent("That recovery code is not right.");
    expect(screen.getByRole("button", { name: /recover my seat/i })).toBeEnabled();
  });

  it("says to wait when attempts are throttled", async () => {
    api.recoverHost.mockRejectedValue(new ApiError("Too many attempts.", 429));
    render(<RecoverHostScreen slug="abc" />);

    submitCode("AAAA-AAAA-AAAA-AAAA");

    expect(await screen.findByRole("alert")).toHaveTextContent(/wait a minute/i);
  });

  it("says so when the server cannot be reached", async () => {
    api.recoverHost.mockRejectedValue(new TypeError("fetch failed"));
    render(<RecoverHostScreen slug="abc" />);

    submitCode("AAAA-AAAA-AAAA-AAAA");

    expect(await screen.findByRole("alert")).toHaveTextContent(/could not reach the server/i);
  });

  it("points a player who is not the host to the host instead", () => {
    render(<RecoverHostScreen slug="abc" />);

    expect(screen.getByText(/not the host\? ask the host to reset your seat/i)).toBeInTheDocument();
  });
});

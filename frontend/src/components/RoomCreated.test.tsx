import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { resetRetentionDaysCache } from "../lib/useRetentionDays";
import { CreateRoomScreen } from "./CreateRoomScreen";

const api = vi.hoisted(() => ({ createRoom: vi.fn(), getConfig: vi.fn() }));
vi.mock("../lib/api", async (importOriginal) => ({ ...(await importOriginal<object>()), ...api }));

describe("the room-created card", () => {
  beforeEach(() => {
    resetRetentionDaysCache();
    api.getConfig.mockReset().mockResolvedValue({ retentionDays: 30 });
    api.createRoom.mockReset().mockResolvedValue({
      slug: "abc123",
      invites: [
        { player: "Amos", inviteToken: "t1" },
        { player: "Lydia", inviteToken: "t2" },
      ],
      recoveryCode: "7QX4-K9M2-VB3H-T8RD",
    });
  });

  async function createRoom() {
    render(<CreateRoomScreen />);
    fireEvent.click(screen.getByRole("radio", { name: /random picker/i }));
    fireEvent.click(screen.getByRole("button", { name: /continue with random picker/i }));
    fireEvent.change(await screen.findByLabelText("Choice 1"), { target: { value: "Pizza" } });
    fireEvent.change(screen.getByLabelText("Choice 2"), { target: { value: "Tacos" } });
    fireEvent.click(screen.getByRole("button", { name: /next/i }));
    fireEvent.change(screen.getByLabelText("Your name (host)"), { target: { value: "Amos" } });
    fireEvent.change(screen.getByLabelText("Player 2"), { target: { value: "Lydia" } });
    // The host referees, so two players need a third name for the host.
    fireEvent.click(screen.getByRole("button", { name: /add a player/i }));
    fireEvent.change(screen.getByLabelText("Player 3"), { target: { value: "Ben" } });
    fireEvent.click(screen.getByRole("button", { name: /create the room/i }));
  }

  it("tells the host their recovery code, once, next to the invite links", async () => {
    await createRoom();

    expect(await screen.findByText("7QX4-K9M2-VB3H-T8RD")).toBeInTheDocument();
    expect(screen.getByText(/forget your PIN/i)).toBeInTheDocument();
  });

  it("says how long the room is kept and that the host can delete it sooner", async () => {
    await createRoom();

    // The same sentence is on the Players step, so wait for the created card before looking.
    await screen.findByText("7QX4-K9M2-VB3H-T8RD");
    expect(screen.getByText(/Rooms are deleted after 30 days without play\. The host can delete a room sooner\./)).toBeInTheDocument();
  });

  // jsdom has no layout engine, so this guards the cause (a flex-1, basis 0, name block never wraps below the buttons and
  // is squeezed to a sliver on a phone); the effect was checked in a real browser at 411px.
  it("lets the name and link wrap below the buttons instead of being squeezed beside them", async () => {
    await createRoom();
    await screen.findByText("7QX4-K9M2-VB3H-T8RD");

    const nameBlock = screen.getByText("Amos", { selector: "p" }).closest("div");

    expect(nameBlock).toHaveClass("basis-36");
    expect(nameBlock).not.toHaveClass("flex-1");
  });

  it("names the host's own field as the host's, not as Player 1", async () => {
    render(<CreateRoomScreen />);
    fireEvent.click(screen.getByRole("radio", { name: /random picker/i }));
    fireEvent.click(screen.getByRole("button", { name: /continue with random picker/i }));
    fireEvent.change(await screen.findByLabelText("Choice 1"), { target: { value: "A" } });
    fireEvent.change(screen.getByLabelText("Choice 2"), { target: { value: "B" } });
    fireEvent.click(screen.getByRole("button", { name: /next/i }));

    expect(screen.getByLabelText("Your name (host)")).toBeInTheDocument();
    expect(screen.getByLabelText("Player 2")).toBeInTheDocument();
    expect(screen.queryByLabelText("Player 1")).not.toBeInTheDocument();
  });

  it("says it on the Players step too, before anything is created", async () => {
    render(<CreateRoomScreen />);
    fireEvent.click(screen.getByRole("radio", { name: /random picker/i }));
    fireEvent.click(screen.getByRole("button", { name: /continue with random picker/i }));
    fireEvent.change(await screen.findByLabelText("Choice 1"), { target: { value: "A" } });
    fireEvent.change(screen.getByLabelText("Choice 2"), { target: { value: "B" } });
    fireEvent.click(screen.getByRole("button", { name: /next/i }));

    expect(await screen.findByText(/Rooms are deleted after 30 days without play\./)).toBeInTheDocument();
  });
});

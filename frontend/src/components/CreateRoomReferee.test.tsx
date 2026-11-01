import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { resetRetentionDaysCache } from "../lib/useRetentionDays";
import { CreateRoomScreen } from "./CreateRoomScreen";

const api = vi.hoisted(() => ({ createRoom: vi.fn(), getConfig: vi.fn() }));
vi.mock("../lib/api", async (importOriginal) => ({ ...(await importOriginal<object>()), ...api }));

async function openPlayersStep() {
  render(<CreateRoomScreen />);
  fireEvent.click(screen.getByRole("radio", { name: /random picker/i }));
  fireEvent.click(screen.getByRole("button", { name: /continue with random picker/i }));
  fireEvent.change(await screen.findByLabelText("Choice 1"), { target: { value: "Pizza" } });
  fireEvent.change(screen.getByLabelText("Choice 2"), { target: { value: "Tacos" } });
  fireEvent.click(screen.getByRole("button", { name: /next/i }));
}

const enter = (label: string, name: string) => fireEvent.change(screen.getByLabelText(label), { target: { value: name } });

describe("creating a room where the host referees", () => {
  beforeEach(() => {
    resetRetentionDaysCache();
    api.getConfig.mockReset().mockResolvedValue({ retentionDays: 30 });
    api.createRoom.mockReset().mockResolvedValue({ slug: "abc", invites: [], recoveryCode: "AAAA-BBBB" });
  });

  it("tells the host they referee and counts only the people who play", async () => {
    await openPlayersStep();
    enter("Player 1", "Amos");
    enter("Player 2", "Lydia");

    expect(screen.getByText(/you are the host and referee\. add the people who will play/i)).toBeInTheDocument();
    expect(screen.getByText(/1 player so far \(not counting the host\)\. Random Picker takes 2-12 players\./)).toBeInTheDocument();
  });

  it("needs the minimum number of players besides the host", async () => {
    await openPlayersStep();
    enter("Player 1", "Amos");
    enter("Player 2", "Lydia");

    expect(screen.getByRole("button", { name: /create the room/i })).toBeDisabled();
    expect(screen.getByText(/needs at least 2 players\. Add 1 more\./i)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /add a player/i }));
    enter("Player 3", "Ben");

    expect(screen.getByRole("button", { name: /create the room/i })).toBeEnabled();
  });

  it("asks the server for a room where the host does not play", async () => {
    await openPlayersStep();
    enter("Player 1", "Amos");
    enter("Player 2", "Lydia");
    fireEvent.click(screen.getByRole("button", { name: /add a player/i }));
    enter("Player 3", "Ben");
    fireEvent.click(screen.getByRole("button", { name: /create the room/i }));

    await screen.findByText("AAAA-BBBB");
    expect(api.createRoom).toHaveBeenCalledWith(expect.any(String), "random-picker", expect.anything(), ["Amos", "Lydia", "Ben"], "Amos", false);
  });

  describe("when the host wants to play too", () => {
    it("is off by default", async () => {
      await openPlayersStep();

      expect(screen.getByRole("checkbox", { name: "I want to play too" })).not.toBeChecked();
    });

    it("counts the host as a player", async () => {
      await openPlayersStep();
      enter("Player 1", "Amos");
      enter("Player 2", "Lydia");
      fireEvent.click(screen.getByRole("checkbox", { name: "I want to play too" }));

      expect(screen.getByText(/^2 players so far\. Random Picker takes 2-12 players\./)).toBeInTheDocument();
      expect(screen.getByRole("button", { name: /create the room/i })).toBeEnabled();
    });

    it("asks the server for a room where the host plays", async () => {
      await openPlayersStep();
      enter("Player 1", "Amos");
      enter("Player 2", "Lydia");
      fireEvent.click(screen.getByRole("checkbox", { name: "I want to play too" }));
      fireEvent.click(screen.getByRole("button", { name: /create the room/i }));

      await screen.findByText("AAAA-BBBB");
      expect(api.createRoom).toHaveBeenCalledWith(expect.any(String), "random-picker", expect.anything(), ["Amos", "Lydia"], "Amos", true);
    });
  });
});

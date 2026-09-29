import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { CreateRoomScreen } from "./CreateRoomScreen";

describe("CreateRoomScreen", () => {
  it("cannot continue until a game is picked", () => {
    render(<CreateRoomScreen />);
    expect(screen.getByRole("button", { name: /next/i })).toBeDisabled();
    expect(screen.getByText(/pick a game to continue/i)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("radio", { name: /random picker/i }));
    expect(screen.getByRole("button", { name: /continue with random picker/i })).toBeEnabled();
  });

  it("walks through setup and players, blocking on an unfinished setup", async () => {
    render(<CreateRoomScreen />);
    fireEvent.click(screen.getByRole("radio", { name: /random picker/i }));
    fireEvent.click(screen.getByRole("button", { name: /continue with random picker/i }));

    expect(await screen.findByRole("heading", { name: /set up random picker/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /next/i })).toBeDisabled();

    fireEvent.change(await screen.findByLabelText("Choice 1"), { target: { value: "Pizza" } });
    fireEvent.change(screen.getByLabelText("Choice 2"), { target: { value: "Tacos" } });
    fireEvent.click(screen.getByRole("button", { name: /next/i }));

    expect(screen.getByRole("heading", { name: /who's playing/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /create the room/i })).toBeDisabled();
    expect(screen.getByText(/needs at least 2 players/i)).toBeInTheDocument();
  });

  it("flags duplicate player names", async () => {
    render(<CreateRoomScreen />);
    fireEvent.click(screen.getByRole("radio", { name: /random picker/i }));
    fireEvent.click(screen.getByRole("button", { name: /continue with random picker/i }));
    fireEvent.change(await screen.findByLabelText("Choice 1"), { target: { value: "A" } });
    fireEvent.change(screen.getByLabelText("Choice 2"), { target: { value: "B" } });
    fireEvent.click(screen.getByRole("button", { name: /next/i }));

    fireEvent.change(screen.getByLabelText("Player 1"), { target: { value: "Amos" } });
    fireEvent.change(screen.getByLabelText("Player 2"), { target: { value: "amos" } });
    expect(screen.getByText(/each player needs a different name/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /create the room/i })).toBeDisabled();
  });
});

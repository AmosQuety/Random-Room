import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { CATEGORIES } from "../games/categories";
import type { AnyGameModule } from "../games/types";
import { GamePicker } from "./GamePicker";

function fakeGame(index: number): AnyGameModule {
  const category = CATEGORIES[index % CATEGORIES.length].key;
  return {
    key: `game-${index}`,
    name: `Game ${index}`,
    description: `Description ${index}`,
    hook: index === 7 ? "A very unique hook" : `Hook ${index}`,
    category,
    accent: "sky",
    Glyph: () => null,
    minPlayers: 2,
    maxPlayers: 12,
    defaultSetup: null,
    isSetupValid: () => true,
    toApiSetup: () => ({}),
    SetupForm: () => null,
    GameScreen: () => null,
  };
}

const twenty = Array.from({ length: 20 }, (_, i) => fakeGame(i));

describe("GamePicker", () => {
  it("groups games under their category headings", () => {
    render(<GamePicker games={twenty} selectedKey={null} onSelect={() => {}} />);
    for (const { label } of CATEGORIES) expect(screen.getByRole("heading", { name: new RegExp(label) })).toBeInTheDocument();
    expect(screen.getAllByRole("radio")).toHaveLength(20);
  });

  it("only shows categories that have games", () => {
    render(<GamePicker games={[fakeGame(1)]} selectedKey={null} onSelect={() => {}} />);
    expect(screen.getAllByRole("heading", { level: 4 })).toHaveLength(1);
  });

  it("reports the chosen game and marks it selected", () => {
    const onSelect = vi.fn();
    const { rerender } = render(<GamePicker games={twenty} selectedKey={null} onSelect={onSelect} />);
    fireEvent.click(screen.getByRole("radio", { name: /Game 3/ }));
    expect(onSelect).toHaveBeenCalledWith("game-3");
    rerender(<GamePicker games={twenty} selectedKey="game-3" onSelect={onSelect} />);
    expect(screen.getByRole("radio", { name: /Game 3/ })).toBeChecked();
  });

  it("offers search only when there are many games, and filters by hook text", () => {
    const { unmount } = render(<GamePicker games={twenty.slice(0, 4)} selectedKey={null} onSelect={() => {}} />);
    expect(screen.queryByRole("searchbox")).toBeNull();
    unmount();

    render(<GamePicker games={twenty} selectedKey={null} onSelect={() => {}} />);
    fireEvent.change(screen.getByRole("searchbox"), { target: { value: "unique hook" } });
    expect(screen.getAllByRole("radio")).toHaveLength(1);
    expect(screen.getByRole("radio", { name: /Game 7/ })).toBeInTheDocument();
  });

  it("shows an empty state when nothing matches", () => {
    render(<GamePicker games={twenty} selectedKey={null} onSelect={() => {}} />);
    fireEvent.change(screen.getByRole("searchbox"), { target: { value: "zzzz" } });
    expect(screen.getByText(/no games match that/i)).toBeInTheDocument();
  });
});

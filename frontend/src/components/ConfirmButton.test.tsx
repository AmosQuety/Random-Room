import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ConfirmButton } from "./ConfirmButton";

function renderButton(onConfirm = vi.fn()) {
  render(
    <ConfirmButton question="End the game for everyone?" confirmLabel="Yes, end it" cancelLabel="Keep playing" onConfirm={onConfirm}>
      End game
    </ConfirmButton>,
  );
  return onConfirm;
}

describe("ConfirmButton", () => {
  it("does nothing on the first click except ask", () => {
    const onConfirm = renderButton();

    fireEvent.click(screen.getByRole("button", { name: "End game" }));

    expect(onConfirm).not.toHaveBeenCalled();
    expect(screen.getByRole("group", { name: "End the game for everyone?" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "End game" })).not.toBeInTheDocument();
  });

  it("acts only when the player confirms, then goes back to the plain button", () => {
    const onConfirm = renderButton();
    fireEvent.click(screen.getByRole("button", { name: "End game" }));

    fireEvent.click(screen.getByRole("button", { name: "Yes, end it" }));

    expect(onConfirm).toHaveBeenCalledTimes(1);
    expect(screen.getByRole("button", { name: "End game" })).toBeInTheDocument();
  });

  it("puts focus on the safe choice so a stray Enter does not end the game", () => {
    renderButton();

    fireEvent.click(screen.getByRole("button", { name: "End game" }));

    expect(screen.getByRole("button", { name: "Keep playing" })).toHaveFocus();
  });

  it("cancels on Escape or Keep playing and returns focus to the original button", () => {
    const onConfirm = renderButton();
    fireEvent.click(screen.getByRole("button", { name: "End game" }));

    fireEvent.keyDown(screen.getByRole("group"), { key: "Escape" });

    expect(onConfirm).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: "End game" })).toHaveFocus();

    fireEvent.click(screen.getByRole("button", { name: "End game" }));
    fireEvent.click(screen.getByRole("button", { name: "Keep playing" }));
    expect(onConfirm).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: "End game" })).toBeInTheDocument();
  });

  it("cannot be opened while disabled", () => {
    render(
      <ConfirmButton disabled question="Sure?" confirmLabel="Yes" cancelLabel="No" onConfirm={vi.fn()}>
        End game
      </ConfirmButton>,
    );
    expect(screen.getByRole("button", { name: "End game" })).toBeDisabled();
  });
});

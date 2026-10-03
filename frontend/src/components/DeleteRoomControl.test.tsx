import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { ApiError } from "../lib/api";
import { DeleteRoomControl } from "./DeleteRoomControl";

describe("DeleteRoomControl", () => {
  it("says what deleting does, and asks before doing it", () => {
    const onDelete = vi.fn().mockResolvedValue(undefined);
    render(<DeleteRoomControl onDelete={onDelete} />);

    expect(screen.getByText(/cannot be undone/i)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Delete room" }));

    expect(onDelete).not.toHaveBeenCalled();
    expect(screen.getByText("Delete this room for everyone?")).toBeInTheDocument();
  });

  it("deletes only on the second step, and keeps the room when the host backs out", async () => {
    const onDelete = vi.fn().mockResolvedValue(undefined);
    render(<DeleteRoomControl onDelete={onDelete} />);

    fireEvent.click(screen.getByRole("button", { name: "Delete room" }));
    fireEvent.click(screen.getByRole("button", { name: "Keep it" }));
    expect(onDelete).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "Delete room" }));
    fireEvent.click(screen.getByRole("button", { name: "Yes, delete the room" }));
    await waitFor(() => expect(onDelete).toHaveBeenCalledTimes(1));
  });

  it("shows the server's reason when deleting is refused", async () => {
    render(<DeleteRoomControl onDelete={vi.fn().mockRejectedValue(new ApiError("Only the host can delete the room.", 403))} />);

    fireEvent.click(screen.getByRole("button", { name: "Delete room" }));
    fireEvent.click(screen.getByRole("button", { name: "Yes, delete the room" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Only the host can delete the room.");
  });
});

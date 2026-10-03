import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { InviteActions } from "./InviteActions";

const writeText = vi.fn();

function renderActions() {
  return render(<InviteActions slug="abc" roomTitle="Friday night" player="Lydia" token="tok-1" />);
}

function setShare(share: ((data: ShareData) => Promise<void>) | undefined) {
  Object.defineProperty(navigator, "share", { value: share, configurable: true });
}

describe("InviteActions", () => {
  beforeEach(() => {
    writeText.mockReset().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", { value: { writeText }, configurable: true });
    setShare(undefined);
  });
  afterEach(() => setShare(undefined));

  it("offers no Share button on a browser that cannot share, but still copies", async () => {
    renderActions();

    expect(screen.queryByRole("button", { name: /share/i })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /copy link for lydia/i }));

    await waitFor(() => expect(writeText).toHaveBeenCalledWith(`${window.location.origin}/room/abc/claim/tok-1`));
  });

  it("opens the share sheet with the player's private link and a plain explanation", async () => {
    const share = vi.fn().mockResolvedValue(undefined);
    setShare(share);
    renderActions();

    fireEvent.click(screen.getByRole("button", { name: /share link with lydia/i }));

    await waitFor(() => expect(share).toHaveBeenCalledTimes(1));
    const data = share.mock.calls[0][0] as ShareData;
    expect(data.url).toBe(`${window.location.origin}/room/abc/claim/tok-1`);
    expect(data.text).toContain("Lydia");
    expect(data.text).toContain("Friday night");
    expect(writeText).not.toHaveBeenCalled();
  });

  it("does nothing more when the player closes the share sheet", async () => {
    const share = vi.fn().mockRejectedValue(new DOMException("closed", "AbortError"));
    setShare(share);
    renderActions();

    fireEvent.click(screen.getByRole("button", { name: /share link with lydia/i }));

    await waitFor(() => expect(share).toHaveBeenCalled());
    expect(writeText).not.toHaveBeenCalled();
  });

  it("falls back to copying the link when sharing fails for any other reason", async () => {
    setShare(vi.fn().mockRejectedValue(new DOMException("no targets", "NotAllowedError")));
    renderActions();

    fireEvent.click(screen.getByRole("button", { name: /share link with lydia/i }));

    await waitFor(() => expect(writeText).toHaveBeenCalledWith(`${window.location.origin}/room/abc/claim/tok-1`));
  });

  it("shows and hides a scannable code for the link, with a reminder that it is private", async () => {
    renderActions();
    const toggle = screen.getByRole("button", { name: /show code for lydia/i });
    expect(toggle).toHaveAttribute("aria-expanded", "false");

    fireEvent.click(toggle);

    const code = await screen.findByRole("img", { name: /qr code for lydia's private link/i });
    expect(code.querySelector("path")?.getAttribute("d")?.length).toBeGreaterThan(100);
    expect(screen.getByText(/show it only to them/i)).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /hide code for lydia/i }));
    expect(screen.queryByRole("img", { name: /qr code/i })).not.toBeInTheDocument();
  });
});

import { describe, expect, it } from "vitest";
import { navigate, redirect } from "./router";

describe("router", () => {
  it("navigate adds a history entry, so Back returns to the previous page", () => {
    const before = window.history.length;

    navigate("/room/abc/claim/token");

    expect(window.location.pathname).toBe("/room/abc/claim/token");
    expect(window.history.length).toBe(before + 1);
  });

  it("redirect replaces the current entry, so Back skips the page that redirected", () => {
    navigate("/room/abc/claim/token");
    const before = window.history.length;

    redirect("/room/abc");

    expect(window.location.pathname).toBe("/room/abc");
    expect(window.history.length).toBe(before);
  });

  it("redirect still tells the app the path changed", () => {
    const seen: string[] = [];
    const listener = () => seen.push(window.location.pathname);
    window.addEventListener("choicemaker:navigate", listener);

    redirect("/somewhere");

    window.removeEventListener("choicemaker:navigate", listener);
    expect(seen).toEqual(["/somewhere"]);
  });
});

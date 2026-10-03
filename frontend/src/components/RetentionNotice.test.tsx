import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { RetentionNotice } from "./RetentionNotice";

describe("RetentionNotice", () => {
  it("says how long a room is kept without play", () => {
    render(<RetentionNotice days={30} />);

    expect(screen.getByText("Rooms are deleted after 30 days without play.")).toBeInTheDocument();
  });

  it("says day, not days, for one day", () => {
    render(<RetentionNotice days={1} />);

    expect(screen.getByText(/after 1 day without play/)).toBeInTheDocument();
  });

  it("adds that the host can delete a room sooner when asked", () => {
    render(<RetentionNotice days={30} hostCanDelete />);

    expect(screen.getByText(/The host can delete a room sooner\./)).toBeInTheDocument();
  });

  it.each([0, null, undefined])("shows nothing when the period is %s, rather than something wrong", (days) => {
    const { container } = render(<RetentionNotice days={days} hostCanDelete />);

    expect(container).toBeEmptyDOMElement();
  });
});

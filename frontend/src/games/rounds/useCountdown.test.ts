import { act, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { TimerView } from "./types";
import { useCountdown } from "./useCountdown";

const NOW = new Date("2026-09-29T12:00:00.000Z");

function timer(secondsAhead: number, serverSkewMs = 0): TimerView {
  return {
    deadlineAt: new Date(NOW.getTime() + serverSkewMs + secondsAhead * 1000).toISOString(),
    serverNow: new Date(NOW.getTime() + serverSkewMs).toISOString(),
  };
}

describe("useCountdown", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });
  afterEach(() => vi.useRealTimers());

  it("counts down to the server deadline", () => {
    const { result } = renderHook(() => useCountdown(timer(10), true, () => {}));
    expect(result.current).toBe(10);

    act(() => vi.advanceTimersByTime(4000));
    expect(result.current).toBe(6);
  });

  it("uses the server's clock, not the device's, when the two disagree", () => {
    // The device is an hour behind the server: the round still lasts 10 seconds.
    const { result } = renderHook(() => useCountdown(timer(10, 3_600_000), true, () => {}));
    expect(result.current).toBe(10);
  });

  it("asks the server to check once the deadline passes, then retries a limited number of times", () => {
    const onExpire = vi.fn();
    renderHook(() => useCountdown(timer(2), true, onExpire));

    act(() => vi.advanceTimersByTime(2300));
    expect(onExpire).toHaveBeenCalledTimes(1);

    act(() => vi.advanceTimersByTime(20_000));
    expect(onExpire).toHaveBeenCalledTimes(3);
  });

  it("does nothing when there is no deadline or the round is not being collected", () => {
    const onExpire = vi.fn();
    const noDeadline = renderHook(() => useCountdown({ deadlineAt: null, serverNow: NOW.toISOString() }, true, onExpire));
    const inactive = renderHook(() => useCountdown(timer(1), false, onExpire));

    act(() => vi.advanceTimersByTime(10_000));

    expect(noDeadline.result.current).toBeNull();
    expect(inactive.result.current).toBeNull();
    expect(onExpire).not.toHaveBeenCalled();
  });

  it("never goes below zero", () => {
    const { result } = renderHook(() => useCountdown(timer(1), true, () => {}));
    act(() => vi.advanceTimersByTime(5000));
    expect(result.current).toBe(0);
  });
});

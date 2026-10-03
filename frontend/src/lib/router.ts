import { useEffect, useState } from "react";

const NAVIGATE_EVENT = "choicemaker:navigate";

/** Re-renders whenever the URL path changes, from a link click, back/forward, or navigate(). */
export function usePath(): string {
  const [path, setPath] = useState(() => window.location.pathname);

  useEffect(() => {
    const onChange = () => setPath(window.location.pathname);
    window.addEventListener("popstate", onChange);
    window.addEventListener(NAVIGATE_EVENT, onChange);
    return () => {
      window.removeEventListener("popstate", onChange);
      window.removeEventListener(NAVIGATE_EVENT, onChange);
    };
  }, []);

  return path;
}

export function navigate(path: string) {
  window.history.pushState({}, "", path);
  window.dispatchEvent(new Event(NAVIGATE_EVENT));
}

/** Like navigate, but replaces the current history entry, so Back skips a page the user was only passing through. */
export function redirect(path: string) {
  window.history.replaceState({}, "", path);
  window.dispatchEvent(new Event(NAVIGATE_EVENT));
}

import { useState } from "react";
import { ApiError } from "../lib/api";
import { ConfirmButton } from "./ConfirmButton";
import { Alert } from "./ui";

interface Props {
  onDelete: () => Promise<void>;
}

/** Lets the host remove the room and everything in it for everyone, now rather than at the retention date. */
export function DeleteRoomControl({ onDelete }: Props) {
  const [error, setError] = useState<string | null>(null);

  async function remove() {
    setError(null);
    try {
      await onDelete();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Could not reach the server. Check your connection and try again.");
    }
  }

  return (
    <section aria-label="Delete the room" className="flex flex-col gap-3 rounded-xl border-2 border-dashed border-ink px-4 py-3">
      <p className="text-sm text-muted">
        Finished with this room? Deleting it removes the players, scores and everything typed in it, for everyone. This cannot be undone.
      </p>
      {error && <Alert>{error}</Alert>}
      <div>
        <ConfirmButton
          variant="dark"
          question="Delete this room for everyone?"
          confirmLabel="Yes, delete the room"
          cancelLabel="Keep it"
          onConfirm={() => void remove()}
        >
          Delete room
        </ConfirmButton>
      </div>
    </section>
  );
}

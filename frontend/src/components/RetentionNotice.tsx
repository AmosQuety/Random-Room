interface Props {
  /** Days without play before a room is deleted; nothing is shown when it is 0, unknown or missing. */
  days: number | null | undefined;
  /** Add that the host can delete the room sooner. */
  hostCanDelete?: boolean;
  className?: string;
}

/** Tells people how long what they type is kept, in plain words. */
export function RetentionNotice({ days, hostCanDelete = false, className = "" }: Props) {
  if (!days || days < 1) return null;
  return (
    <p className={`text-sm text-muted ${className}`}>
      Rooms are deleted after {days} {days === 1 ? "day" : "days"} without play.
      {hostCanDelete && " The host can delete a room sooner."}
    </p>
  );
}

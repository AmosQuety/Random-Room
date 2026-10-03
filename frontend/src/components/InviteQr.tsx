import qrcode from "qrcode-generator";

interface Props {
  /** The text to encode: a player's private invite link. Never sent anywhere; the code is drawn in this browser. */
  value: string;
  label: string;
}

// The standard asks for a quiet zone of 4 modules around the code; scanners struggle without it.
const QUIET_ZONE = 4;

/** A QR code drawn as inline SVG. Always black on white, whatever the theme, because that is what scanners read best. */
export default function InviteQr({ value, label }: Props) {
  // Level M survives a smudge or a cracked screen without making the code much denser than L.
  const code = qrcode(0, "M");
  code.addData(value);
  code.make();

  const count = code.getModuleCount();
  let path = "";
  for (let row = 0; row < count; row++) {
    for (let col = 0; col < count; col++) {
      if (code.isDark(row, col)) path += `M${col + QUIET_ZONE} ${row + QUIET_ZONE}h1v1h-1z`;
    }
  }
  const size = count + QUIET_ZONE * 2;

  return (
    <svg role="img" aria-label={label} viewBox={`0 0 ${size} ${size}`} shapeRendering="crispEdges" className="size-56 max-w-full rounded-lg border-2 border-ink bg-white">
      <path d={path} fill="#000" />
    </svg>
  );
}

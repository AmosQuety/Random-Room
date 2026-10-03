/** Rooms hold 2 to 12 players; each game can narrow that range further. */
export const ROOM_MIN_PLAYERS = 2;
export const ROOM_MAX_PLAYERS = 12;

export interface PlayerRange {
  minPlayers: number;
  maxPlayers: number;
}

/** A friendly explanation when the player list doesn't fit the game, or null when it fits. */
export function playerCountIssue(count: number, game: PlayerRange & { name: string }): string | null {
  const min = Math.max(game.minPlayers, ROOM_MIN_PLAYERS);
  const max = Math.min(game.maxPlayers, ROOM_MAX_PLAYERS);
  if (count < min) return `${game.name} needs at least ${min} players. Add ${min - count} more.`;
  if (count > max) return `${game.name} works best with up to ${max} players. Remove ${count - max}.`;
  return null;
}

export function describePlayerRange(game: PlayerRange): string {
  const min = Math.max(game.minPlayers, ROOM_MIN_PLAYERS);
  const max = Math.min(game.maxPlayers, ROOM_MAX_PLAYERS);
  return min === max ? `${min} players` : `${min}-${max} players`;
}

/** Names of players who have not opened their invite link yet; a round waiting on one of them will not finish by itself. */
export function unjoinedNames(players: readonly { name: string; claimed: boolean }[]): string[] {
  return players.filter((p) => !p.claimed).map((p) => p.name);
}

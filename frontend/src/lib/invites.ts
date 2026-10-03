/** Where a player opens their one-time invite to set a PIN. */
export const claimPath = (slug: string, token: string) => `/room/${slug}/claim/${token}`;

export const claimUrl = (slug: string, token: string) => `${window.location.origin}${claimPath(slug, token)}`;

// Deterministic abstract avatars from placeimg.dev. Only colours and styles go in the URL, never the name.
// Params used are the documented ones (placeimg.dev/docs): gradient, gradientStyle (mesh|waves), pattern.

const FALLBACK = "internship-coach-user";

// Hand-picked pairs so every result looks good: orange+indigo, cyan+purple, pink+coral,
// emerald+blue, yellow+red, violet+teal, blue+magenta.
const palettes: [string, string][] = [
    ["F97316", "4F46E5"],
    ["06B6D4", "9333EA"],
    ["EC4899", "FF7F50"],
    ["10B981", "2563EB"],
    ["FACC15", "DC2626"],
    ["7C3AED", "0D9488"],
    ["3B82F6", "D946EF"],
];
const styles = [null, "mesh", "waves"] as const; // null = the default diagonal gradient
const patterns = [null, "dots", "stripes", "grid", "noise"] as const;

export const normalizeName = (name: string | null | undefined) => (name ?? "").normalize("NFKC").trim().toLowerCase() || FALLBACK;

// FNV-1a over UTF-8 bytes plus a murmur3 finalizer so all bits are mixed.
export function hashName(name: string | null | undefined): number {
    let h = 0x811c9dc5;
    for (const byte of new TextEncoder().encode(normalizeName(name))) h = Math.imul(h ^ byte, 0x01000193);
    h = Math.imul(h ^ (h >>> 16), 0x85ebca6b);
    h = Math.imul(h ^ (h >>> 13), 0xc2b2ae35);
    return (h ^ (h >>> 16)) >>> 0;
}

// Same pair in 4 orders (a-b, b-a, a-b-a, b-a-b): more variety, still only two harmonious hues.
export function avatarPalette(name: string | null | undefined): string[] {
    const h = hashName(name);
    const [a, b] = palettes[h % palettes.length];
    return [[a, b], [b, a], [a, b, a], [b, a, b]][(h >>> 8) % 4];
}

// shortcut: 7 palettes x 4 orders x 3 styles x 5 patterns = 420 looks, so large user bases repeat; switch to a local seeded SVG if that matters.
export function generateAvatarUrl(name: string | null | undefined, size = 400): string {
    const h = hashName(name);
    const px = Math.min(2000, Math.max(16, Math.round(size)));
    // text=" " hides the default "WxH" label placeimg draws in the middle.
    const params = new URLSearchParams({ gradient: avatarPalette(name).join(","), text: " " });
    const style = styles[(h >>> 12) % styles.length];
    const pattern = patterns[(h >>> 20) % patterns.length];
    if (style) params.set("gradientStyle", style);
    if (pattern) params.set("pattern", pattern);
    return `https://placeimg.dev/${px}?${params.toString().replace(/%2C/g, ",")}`;
}

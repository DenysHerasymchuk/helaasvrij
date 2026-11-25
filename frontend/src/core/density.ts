import { useState } from "react";

export type Density = "compact" | "default" | "roomy" | "large";

// Every size in the app is in rem, so the root font size is the one lever for how big everything is.
export const densitySizes: Record<Density, number> = { compact: 14, default: 15, roomy: 16, large: 17 };

const KEY = "density";

export function readDensity(): Density {
    try {
        const saved = localStorage.getItem(KEY);
        if (saved && saved in densitySizes) return saved as Density;
    } catch {
        // Storage blocked: use the default.
    }
    return "default";
}

export function applyDensity(density: Density) {
    document.documentElement.style.fontSize = `${densitySizes[density]}px`;
}

/** Per-device display size, saved in this browser only. */
export function useDensity() {
    const [density, setDensity] = useState<Density>(readDensity);

    const choose = (next: Density) => {
        applyDensity(next);
        try {
            localStorage.setItem(KEY, next);
        } catch {
            // The choice lasts for this page only.
        }
        setDensity(next);
    };

    return { density, choose };
}

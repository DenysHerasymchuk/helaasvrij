import { useState } from "react";

export type Theme = "light" | "dark";

const KEY = "theme";

// index.html applies the saved or system theme before React loads, so the class is the source of truth.
const current = (): Theme => (document.documentElement.classList.contains("dark-mode") ? "dark" : "light");

export function useTheme() {
    const [theme, setTheme] = useState<Theme>(current);

    const toggle = () => {
        const next: Theme = theme === "dark" ? "light" : "dark";
        const root = document.documentElement;
        root.classList.add("theme-fade");
        root.classList.toggle("dark-mode", next === "dark");
        setTimeout(() => root.classList.remove("theme-fade"), 250);
        try {
            localStorage.setItem(KEY, next);
        } catch {
            // Storage blocked (private mode): the choice lasts for this page only.
        }
        setTheme(next);
    };

    return { theme, toggle };
}

// Read once: charts and rings skip their entrance when the user asks for less motion.
export const reduceMotion = typeof window !== "undefined" && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

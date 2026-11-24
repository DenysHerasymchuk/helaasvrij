// The match score with a circle that fills to it. Green from 75%, orange from 50%.
const ringColor = (pct: number) =>
    pct >= 75 ? "var(--color-fg-success-primary)" : pct >= 50 ? "var(--color-fg-warning-primary)" : "var(--color-fg-error-primary)";

export function ScoreRing({ value, size = 56 }: { value: number; size?: number }) {
    const stroke = 5;
    const r = (size - stroke) / 2;
    const c = 2 * Math.PI * r;
    return (
        <div className="relative shrink-0" style={{ width: size, height: size }} role="img" aria-label={`${value}% match`}>
            <svg width={size} height={size} className="-rotate-90" aria-hidden="true">
                <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke="var(--color-bg-quaternary)" strokeWidth={stroke} />
                <circle
                    cx={size / 2}
                    cy={size / 2}
                    r={r}
                    fill="none"
                    stroke={ringColor(value)}
                    className="dash-ring"
                    strokeWidth={stroke}
                    strokeLinecap="round"
                    strokeDasharray={`${(value / 100) * c} ${c}`}
                />
            </svg>
            <span className="absolute inset-0 flex items-center justify-center text-sm font-semibold text-primary tabular-nums">{value}%</span>
        </div>
    );
}

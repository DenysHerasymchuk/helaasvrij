export type DiffLine = { kind: "same" | "added" | "removed"; text: string };

// Headings and bullets are formatting, not content: compare the words.
const clean = (line: string) => line.replace(/^#{1,3}\s+/, "").replace(/^[-*•]\s+/, "").trim();
const lines = (text: string) => text.split("\n").map(clean).filter(Boolean);

/** Line diff of two documents (longest common subsequence). Order follows the new document, removed lines sit where they were. */
export function diffLines(before: string, after: string): DiffLine[] {
    const a = lines(before).slice(0, 1500);
    const b = lines(after).slice(0, 1500);
    const key = (s: string) => s.toLowerCase();
    // lcs[i][j] = length of the common subsequence of a[i..] and b[j..]
    const lcs = Array.from({ length: a.length + 1 }, () => new Array<number>(b.length + 1).fill(0));
    for (let i = a.length - 1; i >= 0; i--) {
        for (let j = b.length - 1; j >= 0; j--) {
            lcs[i][j] = key(a[i]) === key(b[j]) ? lcs[i + 1][j + 1] + 1 : Math.max(lcs[i + 1][j], lcs[i][j + 1]);
        }
    }

    const out: DiffLine[] = [];
    let i = 0;
    let j = 0;
    while (i < a.length && j < b.length) {
        if (key(a[i]) === key(b[j])) {
            out.push({ kind: "same", text: b[j] });
            i++;
            j++;
        } else if (lcs[i + 1][j] >= lcs[i][j + 1]) {
            out.push({ kind: "removed", text: a[i++] });
        } else {
            out.push({ kind: "added", text: b[j++] });
        }
    }
    while (i < a.length) out.push({ kind: "removed", text: a[i++] });
    while (j < b.length) out.push({ kind: "added", text: b[j++] });
    return out;
}

import type { ReactNode } from "react";

/**
 * Tiny markdown renderer for assistant replies: headings, bold, italic, inline code, code blocks, lists and links.
 * Safe by construction: it only builds React elements from text (React escapes it), never HTML strings.
 */

type Block =
    | { kind: "code"; text: string }
    | { kind: "heading"; text: string }
    | { kind: "ul" | "ol"; items: string[] }
    | { kind: "p"; lines: string[] };

export function parseBlocks(source: string): Block[] {
    const blocks: Block[] = [];
    const lines = source.replace(/\r\n/g, "\n").split("\n");
    for (let i = 0; i < lines.length; i++) {
        const line = lines[i];
        if (/^\s*```/.test(line)) {
            const code: string[] = [];
            for (i++; i < lines.length && !/^\s*```/.test(lines[i]); i++) code.push(lines[i]);
            blocks.push({ kind: "code", text: code.join("\n") });
            continue;
        }
        const heading = /^\s{0,3}#{1,6}\s+(.*)$/.exec(line);
        const bullet = /^\s*[-*+]\s+(.*)$/.exec(line);
        const numbered = /^\s*\d+[.)]\s+(.*)$/.exec(line);
        const last = blocks[blocks.length - 1];
        if (heading) blocks.push({ kind: "heading", text: heading[1].replace(/\s#+\s*$/, "") });
        else if (bullet || numbered) {
            const kind = bullet ? "ul" : "ol";
            const text = (bullet ?? numbered)![1];
            if (last?.kind === kind) last.items.push(text);
            else blocks.push({ kind, items: [text] });
        } else if (!line.trim()) continue;
        else if (last?.kind === "p" && lines[i - 1]?.trim()) last.lines.push(line.trim());
        else blocks.push({ kind: "p", lines: [line.trim()] });
    }
    return blocks;
}

// Order matters: code first so its content is never formatted, then links, bold, italic.
const INLINE = /(`[^`\n]+`)|\[([^\]\n]+)\]\(([^)\s]+)\)|\*\*([^*\n]+)\*\*|__([^_\n]+)__|\*([^*\n]+)\*|(?<![\w])_([^_\n]+)_(?![\w])/;

/** Internal app path ("/postings"), an http(s)/mailto URL, or null when the link is not allowed. */
export function linkTarget(href: string): { internal: boolean; href: string } | null {
    if (/^\/(?!\/)/.test(href)) return { internal: true, href };
    if (/^(https?:\/\/|mailto:)/i.test(href)) return { internal: false, href };
    return null;
}

function renderInline(text: string, onNavigate: (path: string) => void, key = "i"): ReactNode[] {
    const out: ReactNode[] = [];
    let rest = text;
    let n = 0;
    while (rest) {
        const m = INLINE.exec(rest);
        if (!m) {
            out.push(rest);
            break;
        }
        if (m.index > 0) out.push(rest.slice(0, m.index));
        const k = `${key}-${n++}`;
        const [, code, linkText, href, bold1, bold2, italic1, italic2] = m;
        if (code) out.push(<code key={k} className="rounded bg-tertiary px-1 py-0.5 font-mono text-[0.85em] text-primary">{code.slice(1, -1)}</code>);
        else if (linkText) {
            const target = linkTarget(href);
            const label = renderInline(linkText, onNavigate, k);
            if (!target) out.push(<span key={k}>{label}</span>);
            else if (target.internal)
                out.push(
                    <a
                        key={k}
                        href={target.href}
                        onClick={(event) => {
                            event.preventDefault();
                            onNavigate(target.href);
                        }}
                        className="font-medium text-brand-secondary underline underline-offset-2"
                    >
                        {label}
                    </a>,
                );
            else
                out.push(
                    <a key={k} href={target.href} target="_blank" rel="noopener noreferrer" className="font-medium text-brand-secondary underline underline-offset-2">
                        {label}
                    </a>,
                );
        } else if (bold1 || bold2) out.push(<strong key={k} className="font-semibold">{renderInline(bold1 ?? bold2, onNavigate, k)}</strong>);
        else out.push(<em key={k}>{renderInline(italic1 ?? italic2, onNavigate, k)}</em>);
        rest = rest.slice(m.index + m[0].length);
    }
    return out;
}

export function ChatMarkdown({ source, onNavigate }: { source: string; onNavigate: (path: string) => void }) {
    return (
        <div className="flex flex-col gap-2">
            {parseBlocks(source).map((block, i) => {
                switch (block.kind) {
                    case "code":
                        return (
                            <pre key={i} className="overflow-x-auto rounded-lg bg-primary p-2 font-mono text-xs text-primary ring-1 ring-secondary">
                                <code>{block.text}</code>
                            </pre>
                        );
                    case "heading":
                        return <p key={i} className="font-semibold text-primary">{renderInline(block.text, onNavigate, `h${i}`)}</p>;
                    case "ul":
                    case "ol": {
                        const List = block.kind;
                        return (
                            <List key={i} className={`${block.kind === "ul" ? "list-disc" : "list-decimal"} ml-5 flex flex-col gap-1`}>
                                {block.items.map((item, j) => (
                                    <li key={j}>{renderInline(item, onNavigate, `l${i}-${j}`)}</li>
                                ))}
                            </List>
                        );
                    }
                    default:
                        return (
                            <p key={i}>
                                {block.lines.map((line, j) => (
                                    <span key={j}>
                                        {j > 0 && <br />}
                                        {renderInline(line, onNavigate, `p${i}-${j}`)}
                                    </span>
                                ))}
                            </p>
                        );
                }
            })}
        </div>
    );
}

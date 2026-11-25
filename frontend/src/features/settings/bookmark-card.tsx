import { BOOKMARKLET_READY, BOOKMARKLET_TYPE } from "@/features/postings/add-position-page";
import { metaClass } from "@/core/ui";
import { SettingsCard, SettingsRow } from "./settings-row";

/**
 * Runs on the job page, in the user's own browser: waits until our page is ready, then sends the address, title and visible text.
 * shortcut: sends the whole page text (capped at 20000 characters), the importer ignores the menu noise; upgrade to a site-specific selector if it gets noisy.
 */
function bookmarklet(origin: string) {
    const code = `(function(){var o=${JSON.stringify(origin)},d={type:${JSON.stringify(BOOKMARKLET_TYPE)},title:document.title,url:location.href,text:document.body.innerText.slice(0,20000)},w=window.open(o+'/add-position');addEventListener('message',function(e){if(e.origin===o&&e.data===${JSON.stringify(BOOKMARKLET_READY)}){w.postMessage(d,o)}})})()`;
    return `javascript:${encodeURIComponent(code)}`;
}

export function BookmarkCard() {
    return (
        <SettingsCard title="Save from a job page" description="Add a LinkedIn, Indeed or any other job from the page you are looking at. Nothing is read by our servers.">
            <SettingsRow title="Bookmark" hint="Drag this button to your bookmarks bar. On a job page, click it and the position opens here for you to confirm.">
                {/* React blocks javascript: hrefs, so the attribute is set through a ref. */}
                <a
                    ref={(el) => el?.setAttribute("href", bookmarklet(window.location.origin))}
                    onClick={(e) => e.preventDefault()}
                    draggable
                    className="cursor-grab rounded-lg px-3 py-2 text-sm font-semibold text-secondary shadow-xs ring-1 ring-primary ring-inset hover:bg-primary_hover"
                >
                    Save to helaasvrij
                </a>
            </SettingsRow>
            <p className={`${metaClass} px-4 pb-3`}>Your bookmarks bar is hidden? Press Ctrl+Shift+B (Cmd+Shift+B on Mac) to show it.</p>
        </SettingsCard>
    );
}

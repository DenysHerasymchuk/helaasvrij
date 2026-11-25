import { File06, FileCheck02, MessageChatCircle, Plus, SearchLg, Target04 } from "@untitledui/icons";
import { Button } from "@/components/base/buttons/button";

/** Everything you can start from Home, as one row of buttons above the sections. */
export function ShortcutButtons() {
    return (
        <nav aria-label="Shortcuts" className="flex flex-wrap gap-3">
            <Button size="md" href="/backlog" iconLeading={Plus}>
                Add position
            </Button>
            <Button size="md" color="secondary" href="/find" iconLeading={SearchLg}>
                Find positions
            </Button>
            <Button size="md" color="secondary" href="/gaps" iconLeading={Target04}>
                Close gaps
            </Button>
            <Button size="md" color="secondary" href="/documents" iconLeading={FileCheck02}>
                Tailor CV
            </Button>
            <Button size="md" color="secondary" href="/documents" iconLeading={File06}>
                Cover letter
            </Button>
            <Button size="md" color="secondary" href="/interview" iconLeading={MessageChatCircle}>
                Practice interview
            </Button>
        </nav>
    );
}

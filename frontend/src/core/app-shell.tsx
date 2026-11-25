import { Calendar, Columns03, File06, HomeLine, MessageChatCircle, SearchLg, Target04 } from "@untitledui/icons";
import { RouterProvider } from "react-aria-components";
import { Navigate, Outlet, useHref, useLocation, useNavigate } from "react-router";
import { MobileNavigationHeader } from "@/components/application/app-navigation/base-components/mobile-header";
import { NavList } from "@/components/application/app-navigation/base-components/nav-list";
import type { NavItemType } from "@/components/application/app-navigation/config";
import { AccountMenu } from "@/core/account-menu";
import { type CurrentUser, useAuth } from "@/core/auth";
import { Logo } from "@/core/logo";
import { ChatWidget } from "@/features/chat/chat-widget";

// Five places for the journey; the per-posting tools live on the posting page, with "All tools" as a shortcut.
const mainItems: NavItemType[] = [
    { label: "Home", href: "/dashboard", icon: HomeLine },
    { label: "Board", href: "/board", icon: Columns03 },
    { label: "Find positions", href: "/find", icon: SearchLg },
    { label: "Calendar", href: "/calendar", icon: Calendar },
];

const toolItems: NavItemType[] = [
    { label: "Gap plan", href: "/gaps", icon: Target04 },
    { label: "Documents", href: "/documents", icon: File06 },
    { label: "Interview prep", href: "/interview", icon: MessageChatCircle },
];

// Every tab of the board, and the position pages, highlight "Board"; the search pages highlight "Find positions".
const boardPaths = ["/summary", "/list", "/board", "/backlog", "/sprints", "/positions"];
const navMatch = (path: string) => (path.startsWith("/find") ? "/find" : boardPaths.some((p) => path.startsWith(p)) ? "/board" : path);

function Sidebar({ activeUrl, user, onSignOut }: { activeUrl: string; user: CurrentUser; onSignOut: () => void }) {
    return (
        <nav aria-label="Main" className="flex h-full w-full flex-col justify-between overflow-auto bg-primary pt-6 lg:pt-8">
            <div>
                <Logo className="mx-6 block h-7 w-auto" />
                <NavList activeUrl={navMatch(activeUrl)} items={mainItems} />
                <NavList activeUrl={activeUrl} items={toolItems} className="mt-3 border-t border-secondary pt-3" />
            </div>
            <div className="border-t border-secondary pt-4 pb-6">
                <AccountMenu user={user} onSignOut={onSignOut} />
            </div>
        </nav>
    );
}

/** Guards every route inside the shell: wait for the session check, then send anonymous users to /login. */
export function AppShell() {
    const { user, status } = useAuth();
    const location = useLocation();

    if (status === "loading") {
        return (
            <div className="flex min-h-dvh items-center justify-center bg-secondary">
                <p role="status" className="text-md text-tertiary">
                    Loading...
                </p>
            </div>
        );
    }

    if (!user) {
        // Remember where the user was going; the login page sends them back after sign-in.
        return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
    }

    return <Shell user={user} />;
}

function Shell({ user }: { user: CurrentUser }) {
    const navigate = useNavigate();
    const { pathname } = useLocation();
    const { logout } = useAuth();
    const onSignOut = () => void logout().then(() => navigate("/login"));

    return (
        // Lets Untitled UI (react-aria) links navigate client-side through React Router.
        <RouterProvider navigate={navigate} useHref={useHref}>
            <div className="flex min-h-dvh flex-col bg-secondary lg:flex-row">
                <MobileNavigationHeader>
                    <Sidebar activeUrl={pathname} user={user} onSignOut={onSignOut} />
                </MobileNavigationHeader>
                <aside className="hidden w-72 shrink-0 border-r border-secondary lg:sticky lg:top-0 lg:block lg:h-dvh">
                    <Sidebar activeUrl={pathname} user={user} onSignOut={onSignOut} />
                </aside>
                <main className="min-w-0 flex-1">
                    <Outlet />
                </main>
                <ChatWidget />
            </div>
        </RouterProvider>
    );
}

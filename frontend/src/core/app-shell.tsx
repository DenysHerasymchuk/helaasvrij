import { Briefcase01, CheckDone01, Columns03, File06, HomeLine, LogOut01, MessageChatCircle, Moon01, SearchLg, Settings01, Sun, Terminal, User01 } from "@untitledui/icons";
import { RouterProvider } from "react-aria-components";
import { Navigate, Outlet, useHref, useLocation, useNavigate } from "react-router";
import { MobileNavigationHeader } from "@/components/application/app-navigation/base-components/mobile-header";
import { NavList } from "@/components/application/app-navigation/base-components/nav-list";
import type { NavItemType } from "@/components/application/app-navigation/config";
import { AvatarLabelGroup } from "@/components/base/avatar/avatar-label-group";
import { Button } from "@/components/base/buttons/button";
import { type CurrentUser, initialsOf, useAuth } from "@/core/auth";
import { generateAvatarUrl } from "@/core/avatar";
import { Logo } from "@/core/logo";
import { useTheme } from "@/core/theme";
import { ChatWidget } from "@/features/chat/chat-widget";

// Sidebar follows the journey: build profile, find a posting, close gaps, apply and track.
const sections: { title?: string; items: NavItemType[] }[] = [
    { items: [{ label: "Dashboard", href: "/dashboard", icon: HomeLine }] },
    { title: "1. Your profile", items: [{ label: "Profile", href: "/profile", icon: User01 }] },
    {
        title: "2. Find a posting",
        items: [
            { label: "Find internships", href: "/postings", icon: SearchLg },
            { label: "My postings", href: "/postings/saved", icon: Briefcase01 },
        ],
    },
    { title: "3. Close your gaps", items: [{ label: "Gap plan", href: "/gaps", icon: CheckDone01 }] },
    {
        title: "4. Apply and track",
        items: [
            { label: "Documents", href: "/documents", icon: File06 },
            { label: "Interview prep", href: "/interview", icon: MessageChatCircle },
            { label: "Applications", href: "/applications", icon: Columns03 },
        ],
    },
];

const footerItems: NavItemType[] = [
    { label: "Settings", href: "/settings", icon: Settings01 },
    { label: "Connect Claude", href: "/connect-claude", icon: Terminal },
];

function ThemeToggle() {
    const { theme, toggle } = useTheme();
    const dark = theme === "dark";
    return (
        <Button size="sm" color="tertiary" iconLeading={dark ? Sun : Moon01} onPress={toggle} aria-label={dark ? "Switch to light mode" : "Switch to dark mode"}>
            {dark ? "Light mode" : "Dark mode"}
        </Button>
    );
}

// Detail pages highlight their parent item: /postings/job/x -> Find internships, /postings/<id> -> My postings.
const navMatch = (path: string) =>
    path.startsWith("/postings/job/") ? "/postings" : /^\/postings\/(?!saved$)[^/]+$/.test(path) ? "/postings/saved" : path;

function Sidebar({ activeUrl, user, onSignOut }: { activeUrl: string; user: CurrentUser; onSignOut: () => void }) {
    return (
        <nav aria-label="Main" className="flex h-full w-full flex-col justify-between overflow-auto bg-primary pt-6 lg:pt-8">
            <div>
                <Logo className="mx-6 block h-7 w-auto" />
                {sections.map((section, i) => (
                    <div key={section.title ?? i}>
                        {section.title && <p className="px-7 pt-5 text-xs font-semibold text-quaternary">{section.title}</p>}
                        <NavList activeUrl={navMatch(activeUrl)} items={section.items} className={section.title ? "pt-1" : undefined} />
                    </div>
                ))}
            </div>
            <div className="flex flex-col gap-4 pb-6">
                <NavList activeUrl={activeUrl} items={footerItems} className="pt-0" />
                <div className="px-4">
                    <ThemeToggle />
                </div>
                <div className="mx-4 flex items-center gap-3 border-t border-secondary px-2 pt-5">
                    <AvatarLabelGroup
                        size="md"
                        src={generateAvatarUrl(user.displayName, 80)}
                        initials={initialsOf(user.displayName)}
                        title={user.displayName}
                        subtitle={user.gitHubLogin ? `@${user.gitHubLogin}` : user.email}
                    />
                    <Button size="sm" color="tertiary" aria-label="Sign out" iconLeading={LogOut01} onPress={onSignOut} />
                </div>
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

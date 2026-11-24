import { Briefcase01, CheckDone01, Columns03, File06, HomeLine, LogOut01, MessageChatCircle, Moon01, Settings01, Sun, User01 } from "@untitledui/icons";
import { RouterProvider } from "react-aria-components";
import { Navigate, Outlet, useHref, useLocation, useNavigate } from "react-router";
import { MobileNavigationHeader } from "@/components/application/app-navigation/base-components/mobile-header";
import { NavList } from "@/components/application/app-navigation/base-components/nav-list";
import type { NavItemType } from "@/components/application/app-navigation/config";
import { AvatarLabelGroup } from "@/components/base/avatar/avatar-label-group";
import { Button } from "@/components/base/buttons/button";
import { type CurrentUser, initialsOf, useAuth } from "@/core/auth";
import { generateAvatarUrl } from "@/core/avatar";
import { useTheme } from "@/core/theme";
import { ChatWidget } from "@/features/chat/chat-widget";

const mainItems: NavItemType[] = [
    { label: "Dashboard", href: "/dashboard", icon: HomeLine },
    { label: "Postings", href: "/postings", icon: Briefcase01 },
    { label: "Gap plan", href: "/gaps", icon: CheckDone01 },
    { label: "Documents", href: "/documents", icon: File06 },
    { label: "Interview prep", href: "/interview", icon: MessageChatCircle },
    { label: "Applications", href: "/applications", icon: Columns03 },
    { label: "Profile", href: "/profile", icon: User01 },
];

const footerItems: NavItemType[] = [{ label: "Settings", href: "/settings", icon: Settings01 }];

function ThemeToggle() {
    const { theme, toggle } = useTheme();
    const dark = theme === "dark";
    return (
        <Button size="sm" color="tertiary" iconLeading={dark ? Sun : Moon01} onPress={toggle} aria-label={dark ? "Switch to light mode" : "Switch to dark mode"}>
            {dark ? "Light mode" : "Dark mode"}
        </Button>
    );
}

function Sidebar({ activeUrl, user, onSignOut }: { activeUrl: string; user: CurrentUser; onSignOut: () => void }) {
    return (
        <nav aria-label="Main" className="flex h-full w-full flex-col justify-between overflow-auto bg-primary pt-6 lg:pt-8">
            <div>
                <p className="px-6 text-lg font-semibold text-brand-secondary">Internship Coach</p>
                <NavList activeUrl={activeUrl} items={mainItems} />
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

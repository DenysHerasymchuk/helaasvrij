import { Briefcase01, CheckDone01, Columns03, File06, HomeLine, LogOut01, MessageChatCircle, Settings01, User01 } from "@untitledui/icons";
import { RouterProvider } from "react-aria-components";
import { Outlet, useHref, useLocation, useNavigate } from "react-router";
import { MobileNavigationHeader } from "@/components/application/app-navigation/base-components/mobile-header";
import { NavList } from "@/components/application/app-navigation/base-components/nav-list";
import type { NavItemType } from "@/components/application/app-navigation/config";
import { AvatarLabelGroup } from "@/components/base/avatar/avatar-label-group";
import { Button } from "@/components/base/buttons/button";
import { demoUser } from "@/features/demo/demo-user";

const mainItems: NavItemType[] = [
    { label: "Dashboard", href: "/dashboard", icon: HomeLine },
    { label: "Postings", href: "/postings", icon: Briefcase01, badge: 4 },
    { label: "Gap plan", href: "/gaps", icon: CheckDone01, badge: 7 },
    { label: "Documents", href: "/documents", icon: File06 },
    { label: "Interview prep", href: "/interview", icon: MessageChatCircle },
    { label: "Applications", href: "/applications", icon: Columns03 },
    { label: "Profile", href: "/profile", icon: User01 },
];

const footerItems: NavItemType[] = [{ label: "Settings", href: "/settings", icon: Settings01 }];

function Sidebar({ activeUrl }: { activeUrl: string }) {
    return (
        <nav aria-label="Main" className="flex h-full w-full flex-col justify-between overflow-auto bg-primary pt-6 lg:pt-8">
            <div>
                <p className="px-6 text-lg font-semibold text-brand-secondary">Internship Coach</p>
                <NavList activeUrl={activeUrl} items={mainItems} />
            </div>
            <div className="flex flex-col gap-4 pb-6">
                <NavList activeUrl={activeUrl} items={footerItems} className="pt-0" />
                <div className="mx-4 flex items-center gap-3 border-t border-secondary px-2 pt-5">
                    <AvatarLabelGroup size="md" initials={demoUser.initials} title={demoUser.name} subtitle={`@${demoUser.github}`} />
                    {/* shortcut: no session yet, sign out just returns to the login page */}
                    <Button size="sm" color="tertiary" aria-label="Sign out" iconLeading={LogOut01} href="/login" />
                </div>
            </div>
        </nav>
    );
}

export function AppShell() {
    const navigate = useNavigate();
    const { pathname } = useLocation();

    return (
        // Lets Untitled UI (react-aria) links navigate client-side through React Router.
        <RouterProvider navigate={navigate} useHref={useHref}>
            <div className="flex min-h-dvh flex-col bg-secondary lg:flex-row">
                <MobileNavigationHeader>
                    <Sidebar activeUrl={pathname} />
                </MobileNavigationHeader>
                <aside className="hidden w-72 shrink-0 border-r border-secondary lg:sticky lg:top-0 lg:block lg:h-dvh">
                    <Sidebar activeUrl={pathname} />
                </aside>
                <main className="min-w-0 flex-1">
                    <Outlet />
                </main>
            </div>
        </RouterProvider>
    );
}

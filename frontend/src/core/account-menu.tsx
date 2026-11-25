import type { FC } from "react";
import { ChevronSelectorVertical, LogOut01, Moon01, Settings01, Sun, Terminal } from "@untitledui/icons";
import { Button, Menu, MenuItem, MenuTrigger, Popover, Separator } from "react-aria-components";
import { AvatarLabelGroup } from "@/components/base/avatar/avatar-label-group";
import { useBreakpoint } from "@/hooks/use-breakpoint";
import { cx } from "@/utils/cx";
import { type CurrentUser, initialsOf } from "@/core/auth";
import { generateAvatarUrl } from "@/core/avatar";
import { useTheme } from "@/core/theme";

const itemClass =
    "flex cursor-pointer items-center gap-2 rounded-md px-2 py-2 text-sm font-semibold text-secondary outline-hidden data-[focused]:bg-primary_hover data-[focus-visible]:outline-2 data-[focus-visible]:-outline-offset-2 data-[focus-visible]:outline-focus-ring";

function Item({ icon: Icon, label, ...props }: { icon: FC<{ className?: string }>; label: string; href?: string; onAction?: () => void }) {
    return (
        <MenuItem {...props} textValue={label} className={itemClass}>
            <Icon className="size-5 text-fg-quaternary" />
            {label}
        </MenuItem>
    );
}

/** Name + avatar row that opens the account menu: settings, Connect Claude, theme, sign out. */
export function AccountMenu({ user, onSignOut }: { user: CurrentUser; onSignOut: () => void }) {
    const { theme, toggle } = useTheme();
    const isDesktop = useBreakpoint("lg");
    const dark = theme === "dark";

    return (
        <MenuTrigger>
            <Button
                aria-label="Account menu"
                className="group mx-4 flex w-[calc(100%-2rem)] items-center justify-between gap-3 rounded-xl px-2 py-2 text-left outline-focus-ring hover:bg-primary_hover focus-visible:outline-2 focus-visible:outline-offset-2 pressed:bg-primary_hover"
            >
                <AvatarLabelGroup
                    size="md"
                    src={generateAvatarUrl(user.displayName, 80)}
                    initials={initialsOf(user.displayName)}
                    title={user.displayName}
                    subtitle={user.gitHubLogin ? `@${user.gitHubLogin}` : user.email}
                />
                <ChevronSelectorVertical className="size-4 shrink-0 stroke-[2.25px] text-fg-quaternary group-hover:text-fg-quaternary_hover" />
            </Button>
            <Popover
                placement={isDesktop ? "right bottom" : "top right"}
                offset={8}
                className={cx("w-60 rounded-xl bg-primary p-1.5 shadow-lg ring-1 ring-secondary", "entering:animate-in entering:fade-in exiting:animate-out exiting:fade-out")}
            >
                <Menu aria-label="Account" className="outline-hidden">
                    <Item icon={Settings01} label="Settings" href="/settings" />
                    <Item icon={Terminal} label="Connect Claude" href="/connect-claude" />
                    <Item icon={dark ? Sun : Moon01} label={dark ? "Light mode" : "Dark mode"} onAction={toggle} />
                    <Separator className="my-1.5 h-px bg-border-secondary" />
                    <Item icon={LogOut01} label="Sign out" onAction={onSignOut} />
                </Menu>
            </Popover>
        </MenuTrigger>
    );
}

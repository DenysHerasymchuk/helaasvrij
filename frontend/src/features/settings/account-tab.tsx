import { Link } from "react-router";
import { AvatarLabelGroup } from "@/components/base/avatar/avatar-label-group";
import { initialsOf, useAuth } from "@/core/auth";
import { generateAvatarUrl } from "@/core/avatar";
import { SettingsCard, SettingsRow } from "./settings-row";

export function AccountTab() {
    const { user } = useAuth();
    if (!user) return null;

    return (
        <SettingsCard>
            <SettingsRow title="Signed in as">
                <AvatarLabelGroup
                    size="md"
                    src={generateAvatarUrl(user.displayName, 80)}
                    initials={initialsOf(user.displayName)}
                    title={user.displayName}
                    subtitle={user.gitHubLogin ? `@${user.gitHubLogin}` : user.email}
                />
            </SettingsRow>
            <SettingsRow title="Email" hint="Where match alerts go once they launch.">
                <span className="text-sm text-secondary">{user.email}</span>
            </SettingsRow>
            <SettingsRow title="Sources" hint="GitHub, CV and website are managed on your profile.">
                <Link to="/profile" className="text-sm font-semibold text-brand-secondary outline-focus-ring hover:underline focus-visible:outline-2 focus-visible:outline-offset-2">
                    Open profile
                </Link>
            </SettingsRow>
        </SettingsCard>
    );
}

import { FaGithub } from "react-icons/fa6";
import { Button } from "@/components/base/buttons/button";
import { Checkbox } from "@/components/base/checkbox/checkbox";
import { Form } from "@/components/base/form/form";
import { Input } from "@/components/base/input/input";
import { AuthCard } from "./auth-card";

// Adapted from the Untitled UI "login-card-combined" template.
export function LoginPage() {
    return (
        <AuthCard
            title="Welcome back"
            subtitle="Sign in to see your internship matches."
            footer={
                <>
                    <span className="text-sm text-tertiary">Don't have an account?</span>
                    <Button color="link-color" size="md" href="/register">
                        Create account
                    </Button>
                </>
            }
        >
            <Form
                // shortcut: no auth API yet, form does nothing; wire to POST /api/auth/login when it exists.
                onSubmit={(e) => e.preventDefault()}
                className="flex flex-col gap-6"
            >
                <div className="flex flex-col gap-5">
                    <Input isRequired type="email" name="email" label="Email" placeholder="you@student.be" size="lg" />
                    <Input isRequired type="password" name="password" label="Password" placeholder="••••••••" size="lg" />
                </div>

                <Checkbox label="Remember me for 30 days" name="remember" />

                <div className="flex flex-col gap-4">
                    <Button type="submit" size="lg">
                        Sign in
                    </Button>
                    {/* shortcut: GitHub OAuth endpoint is not built yet; this link 404s until then. */}
                    <Button color="secondary" size="lg" href="/api/auth/github" iconLeading={FaGithub}>
                        Sign in with GitHub
                    </Button>
                </div>
            </Form>
        </AuthCard>
    );
}

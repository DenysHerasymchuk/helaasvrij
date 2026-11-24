import { type FormEvent, useState } from "react";
import { FaGithub } from "react-icons/fa6";
import { Navigate, useLocation, useSearchParams } from "react-router";
import { Button } from "@/components/base/buttons/button";
import { Checkbox } from "@/components/base/checkbox/checkbox";
import { Form } from "@/components/base/form/form";
import { Input } from "@/components/base/input/input";
import { ApiError, safePath, useAuth } from "@/core/auth";
import { AuthCard } from "./auth-card";

// Adapted from the Untitled UI "login-card-combined" template.
export function LoginPage() {
    const { status, login } = useAuth();
    const location = useLocation();
    const [params] = useSearchParams();
    const [error, setError] = useState<string | null>(params.get("error") === "github" ? "GitHub sign-in did not work. Try again or use your email." : null);
    const [submitting, setSubmitting] = useState(false);
    const from = safePath((location.state as { from?: string } | null)?.from);

    // Already signed in (or just did): go where the user was heading.
    if (status === "authenticated") return <Navigate to={from} replace />;

    async function onSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        const data = new FormData(event.currentTarget);
        setError(null);
        setSubmitting(true);
        try {
            await login(String(data.get("email")), String(data.get("password")), data.get("remember") !== null);
        } catch (e) {
            setError(e instanceof ApiError ? e.message : "Something went wrong. Try again.");
            setSubmitting(false);
        }
    }

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
            <Form onSubmit={onSubmit} className="flex flex-col gap-6">
                <div className="flex flex-col gap-5">
                    <Input isRequired type="email" name="email" label="Email" placeholder="you@student.be" size="lg" isInvalid={!!error} />
                    <Input isRequired type="password" name="password" label="Password" placeholder="••••••••" size="lg" isInvalid={!!error} />
                </div>

                <Checkbox label="Remember me for 30 days" name="remember" />

                {error && (
                    <p role="alert" className="text-sm text-error-primary">
                        {error}
                    </p>
                )}

                <div className="flex flex-col gap-4">
                    <Button type="submit" size="lg" isLoading={submitting} isDisabled={submitting}>
                        Sign in
                    </Button>
                    {/* Plain link: GitHub sign-in must be a full-page navigation to the API, not a client-side route. */}
                    <Button color="secondary" size="lg" href="/api/auth/github?returnUrl=/dashboard" iconLeading={FaGithub}>
                        Sign in with GitHub
                    </Button>
                </div>
            </Form>
        </AuthCard>
    );
}

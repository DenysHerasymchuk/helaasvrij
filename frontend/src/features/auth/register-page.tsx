import { type FormEvent, useState } from "react";
import { CheckCircle } from "@untitledui/icons";
import { FaGithub } from "react-icons/fa6";
import { Navigate } from "react-router";
import { Button } from "@/components/base/buttons/button";
import { Form } from "@/components/base/form/form";
import { HintText } from "@/components/base/input/hint-text";
import { Input, InputBase, TextField } from "@/components/base/input/input";
import { Label } from "@/components/base/input/label";
import { ApiError, useAuth } from "@/core/auth";
import { cx } from "@/utils/cx";
import { AuthCard } from "./auth-card";

// Adapted from the Untitled UI "signup-card-combined" template.
export function RegisterPage() {
    const { status, register } = useAuth();
    const [password, setPassword] = useState("");
    const [errors, setErrors] = useState<Record<string, string>>({});
    const [formError, setFormError] = useState<string | null>(null);
    const [submitting, setSubmitting] = useState(false);

    if (status === "authenticated") return <Navigate to="/dashboard" replace />;

    async function onSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        const data = new FormData(event.currentTarget);
        setErrors({});
        setFormError(null);
        setSubmitting(true);
        try {
            await register(String(data.get("name")), String(data.get("email")), password);
        } catch (e) {
            if (e instanceof ApiError) {
                setErrors(e.fieldErrors);
                // 409 (email taken) has no field map: show it under the email field.
                if (e.status === 409) setErrors({ email: e.message });
                else if (Object.keys(e.fieldErrors).length === 0) setFormError(e.message);
            } else {
                setFormError("Something went wrong. Try again.");
            }
            setSubmitting(false);
        }
    }

    return (
        <AuthCard
            title="Create your account"
            subtitle="See what each position expects from you."
            footer={
                <>
                    <span className="text-sm text-tertiary">Already have an account?</span>
                    <Button color="link-color" size="md" href="/login">
                        Sign in
                    </Button>
                </>
            }
        >
            <Form onSubmit={onSubmit} className="flex flex-col gap-6">
                <div className="flex flex-col gap-5">
                    <Input isRequired name="name" label="Name" placeholder="Your full name" size="lg" isInvalid={!!errors.name} hint={errors.name} />
                    <Input isRequired type="email" name="email" label="Email" placeholder="you@example.com" size="lg" isInvalid={!!errors.email} hint={errors.email} />
                    <TextField isRequired size="lg" name="password" value={password} onChange={setPassword} minLength={8} isInvalid={!!errors.password}>
                        <Label>Password</Label>
                        <InputBase type="password" placeholder="Create a password" />
                        <HintText className="flex items-center gap-1">
                            <CheckCircle
                                className={cx(
                                    "size-4 stroke-[2.25px] text-fg-quaternary group-invalid:text-fg-error-secondary",
                                    password.length >= 8 && "text-fg-success-primary",
                                )}
                            />
                            At least 8 characters.
                        </HintText>
                        {errors.password && <HintText isInvalid>{errors.password}</HintText>}
                    </TextField>
                </div>

                <div className="flex flex-col gap-4">
                    {formError && (
                        <p role="alert" className="text-sm text-error-primary">
                            {formError}
                        </p>
                    )}
                    <Button type="submit" size="lg" isLoading={submitting} isDisabled={submitting}>
                        Create account
                    </Button>
                    {/* Plain link: GitHub sign-in must be a full-page navigation to the API, not a client-side route. */}
                    <Button color="secondary" size="lg" href="/api/auth/github?returnUrl=/dashboard" iconLeading={FaGithub}>
                        Sign up with GitHub
                    </Button>
                </div>
            </Form>
        </AuthCard>
    );
}

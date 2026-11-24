import { useState } from "react";
import { CheckCircle } from "@untitledui/icons";
import { FaGithub } from "react-icons/fa6";
import { Button } from "@/components/base/buttons/button";
import { Form } from "@/components/base/form/form";
import { HintText } from "@/components/base/input/hint-text";
import { Input, InputBase, TextField } from "@/components/base/input/input";
import { Label } from "@/components/base/input/label";
import { cx } from "@/utils/cx";
import { AuthCard } from "./auth-card";

// Adapted from the Untitled UI "signup-card-combined" template.
export function RegisterPage() {
    const [password, setPassword] = useState("");

    return (
        <AuthCard
            title="Create your account"
            subtitle="See what Belgian internships expect from you."
            footer={
                <>
                    <span className="text-sm text-tertiary">Already have an account?</span>
                    <Button color="link-color" size="md" href="/login">
                        Sign in
                    </Button>
                </>
            }
        >
            <Form
                // shortcut: no auth API yet, form does nothing; wire to POST /api/auth/register when it exists.
                onSubmit={(e) => e.preventDefault()}
                className="flex flex-col gap-6"
            >
                <div className="flex flex-col gap-5">
                    <Input isRequired name="name" label="Name" placeholder="Your full name" size="lg" />
                    <Input isRequired type="email" name="email" label="Email" placeholder="you@student.be" size="lg" />
                    <TextField isRequired size="lg" name="password" value={password} onChange={setPassword} minLength={8}>
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
                    </TextField>
                </div>

                <div className="flex flex-col gap-4">
                    <Button type="submit" size="lg">
                        Create account
                    </Button>
                    {/* shortcut: GitHub OAuth endpoint is not built yet; this link 404s until then. */}
                    <Button color="secondary" size="lg" href="/api/auth/github" iconLeading={FaGithub}>
                        Sign up with GitHub
                    </Button>
                </div>
            </Form>
        </AuthCard>
    );
}

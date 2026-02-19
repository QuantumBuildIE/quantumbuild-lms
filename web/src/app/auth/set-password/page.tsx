"use client";

import { useState, useEffect, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { apiClient, clearStoredTokens } from "@/lib/api/client";
import { useAuth } from "@/lib/auth/use-auth";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { toast } from "sonner";

function SetPasswordForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { logout } = useAuth();

  const [email, setEmail] = useState("");
  const [token, setToken] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Clear any existing auth session on mount to prevent another user's
  // session from persisting through the set-password flow
  useEffect(() => {
    clearStoredTokens();
    logout();
  }, [logout]);

  useEffect(() => {
    const emailParam = searchParams.get("email");
    const tokenParam = searchParams.get("token");

    if (emailParam) {
      setEmail(decodeURIComponent(emailParam));
    }
    if (tokenParam) {
      setToken(decodeURIComponent(tokenParam));
    }

    if (!emailParam || !tokenParam) {
      setError("Invalid password setup link. Please contact your administrator.");
    }
  }, [searchParams]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!newPassword || !confirmPassword) {
      setError("Please fill in all fields");
      return;
    }

    if (newPassword.length < 8) {
      setError("Password must be at least 8 characters long");
      return;
    }

    if (newPassword !== confirmPassword) {
      setError("Passwords do not match");
      return;
    }

    setIsLoading(true);

    try {
      const response = await apiClient.post("/auth/set-password", {
        email,
        token,
        newPassword,
        confirmPassword,
      });

      if (response.data.message) {
        router.push("/login?passwordSet=true");
      }
    } catch (err) {
      const error = err as { response?: { data?: { errors?: string[] } } };
      const errorMessage = error.response?.data?.errors?.[0] ||
        "Failed to set password. The link may have expired.";
      setError(errorMessage);
      toast.error(errorMessage);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50 dark:bg-slate-950 px-4">
      <Card className="w-full max-w-md">
        <CardHeader className="space-y-1 text-center">
          <div className="flex flex-col items-center gap-3">
            <svg viewBox="0 0 46 46" fill="none" className="w-12 h-12">
              <circle cx="23" cy="23" r="21" fill="#4d8eff" fillOpacity="0.1" stroke="#4d8eff" strokeWidth="1.5" strokeOpacity="0.3"/>
              <path d="M23 10V23L30 30" stroke="#4d8eff" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"/>
              <circle cx="23" cy="23" r="3" fill="#4d8eff"/>
              <circle cx="23" cy="7" r="2" fill="#4d8eff" opacity="0.6"/>
              <circle cx="39" cy="23" r="2" fill="#4d8eff" opacity="0.6"/>
              <circle cx="23" cy="39" r="2" fill="#4d8eff" opacity="0.6"/>
              <circle cx="7" cy="23" r="2" fill="#4d8eff" opacity="0.6"/>
            </svg>
            <CardTitle className="text-2xl font-bold tracking-tight">
              Certified<span className="text-primary font-extrabold">IQ</span>
            </CardTitle>
            <p className="text-sm text-muted-foreground">Intelligent compliance, every time.</p>
          </div>
          <CardDescription>Set up your account password</CardDescription>
        </CardHeader>
        <CardContent>
          {error && !email && !token ? (
            <div className="text-center space-y-4">
              <p className="text-red-600 dark:text-red-400">{error}</p>
              <Link href="/login">
                <Button variant="outline">Go to Login</Button>
              </Link>
            </div>
          ) : (
            <form onSubmit={handleSubmit} className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="email">Email</Label>
                <Input
                  id="email"
                  type="email"
                  value={email}
                  disabled
                  className="bg-slate-100 dark:bg-slate-800"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="newPassword">New Password</Label>
                <Input
                  id="newPassword"
                  type="password"
                  placeholder="Enter your new password"
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  disabled={isLoading}
                  autoComplete="new-password"
                  minLength={8}
                />
                <p className="text-xs text-muted-foreground">
                  Password must be at least 8 characters
                </p>
              </div>
              <div className="space-y-2">
                <Label htmlFor="confirmPassword">Confirm Password</Label>
                <Input
                  id="confirmPassword"
                  type="password"
                  placeholder="Confirm your new password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  disabled={isLoading}
                  autoComplete="new-password"
                />
              </div>
              {error && (
                <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
              )}
              <Button type="submit" className="w-full" disabled={isLoading}>
                {isLoading ? "Setting Password..." : "Set Password"}
              </Button>
              <div className="text-center text-sm text-muted-foreground">
                Already have an account?{" "}
                <Link href="/login" className="text-primary hover:underline">
                  Sign in
                </Link>
              </div>
            </form>
          )}
          <div className="mt-6 text-center">
            <span className="text-xs text-muted-foreground">A CertifiedIQ Product</span>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

export default function SetPasswordPage() {
  return (
    <Suspense fallback={
      <div className="min-h-screen flex items-center justify-center bg-slate-50 dark:bg-slate-950 px-4">
        <Card className="w-full max-w-md">
          <CardHeader className="space-y-1 text-center">
            <CardTitle className="text-2xl font-bold tracking-tight">
              Certified<span className="text-primary font-extrabold">IQ</span>
            </CardTitle>
            <CardDescription>Loading...</CardDescription>
          </CardHeader>
        </Card>
      </div>
    }>
      <SetPasswordForm />
    </Suspense>
  );
}

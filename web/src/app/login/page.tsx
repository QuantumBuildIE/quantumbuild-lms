"use client";

import { Suspense, useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useAuth } from "@/lib/auth/use-auth";
import { getHomeRoute } from "@/lib/auth/get-home-route";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { toast } from "sonner";

function isValidReturnUrl(url: string | null): url is string {
  return !!url
    && url.startsWith("/")
    && !url.startsWith("/login")
    && !url.startsWith("/register")
    && !url.startsWith("/forgot-password");
}

function LoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { user, login, isLoading: authLoading } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [rememberMe, setRememberMe] = useState(true);
  const [isLoading, setIsLoading] = useState(false);

  // Show success toast when redirected from set-password flow
  useEffect(() => {
    if (searchParams.get("passwordSet") === "true") {
      toast.success("Password set successfully! Please sign in with your new password.");
    }
  }, [searchParams]);

  // Redirect already-logged-in users
  useEffect(() => {
    if (user && !authLoading) {
      const returnUrl = searchParams.get("returnUrl");
      router.push(isValidReturnUrl(returnUrl) ? returnUrl : getHomeRoute(user));
    }
  }, [user, authLoading, router, searchParams]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!email || !password) {
      toast.error("Please enter both email and password");
      return;
    }

    setIsLoading(true);

    try {
      const result = await login(email, password, rememberMe);

      if (result.success && result.user) {
        toast.success("Login successful");
        const returnUrl = searchParams.get("returnUrl");
        const destination = isValidReturnUrl(returnUrl) ? returnUrl : getHomeRoute(result.user);
        router.push(destination);
      } else {
        toast.error(result.error || "Login failed");
      }
    } catch {
      toast.error("An unexpected error occurred");
    } finally {
      setIsLoading(false);
    }
  };

  const loading = isLoading || authLoading;

  // Show nothing while redirecting a logged-in user
  if (user) {
    return null;
  }

  return (
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
            Certified<span className="text-[#4d8eff] font-extrabold">IQ</span>
          </CardTitle>
          <p className="text-sm text-muted-foreground">Intelligent compliance, every time.</p>
        </div>
        <CardDescription>Sign in to your account to continue</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="email">Email</Label>
            <Input
              id="email"
              type="email"
              placeholder="your@email.com"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              disabled={loading}
              autoComplete="email"
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="password">Password</Label>
            <Input
              id="password"
              type="password"
              placeholder="Enter your password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              disabled={loading}
              autoComplete="current-password"
            />
          </div>
          <div className="flex items-center space-x-2">
            <Checkbox
              id="rememberMe"
              checked={rememberMe}
              onCheckedChange={(checked) => setRememberMe(checked === true)}
              disabled={loading}
            />
            <Label
              htmlFor="rememberMe"
              className="text-sm font-normal cursor-pointer"
            >
              Keep me logged in
            </Label>
          </div>
          <Button type="submit" className="w-full" disabled={loading}>
            {loading ? "Signing in..." : "Sign In"}
          </Button>
        </form>
        <div className="mt-6 text-center">
          <span className="text-xs text-muted-foreground">A QuantumBuild Product</span>
        </div>
      </CardContent>
    </Card>
  );
}

export default function LoginPage() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50 dark:bg-slate-950 px-4">
      <Suspense fallback={
        <Card className="w-full max-w-md">
          <CardHeader className="space-y-1 text-center">
            <CardTitle className="text-2xl font-bold tracking-tight">
              Certified<span className="text-[#4d8eff] font-extrabold">IQ</span>
            </CardTitle>
            <CardDescription>Loading...</CardDescription>
          </CardHeader>
        </Card>
      }>
        <LoginForm />
      </Suspense>
    </div>
  );
}

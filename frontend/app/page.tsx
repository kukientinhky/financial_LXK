"use client";

import { FormEvent, useEffect, useState } from "react";
import { useRouter } from "next/navigation";

type AuthMode = "login" | "register";

type LoginResponse = {
  userId?: string;
  accessToken?: string;
  AccessToken?: string;
  token?: string;
};

const tokenStorageKey = "expensecraft_access_token";
const legacyUserIdStorageKey = "expensecraft_user_id";
const apiUrl = process.env.NEXT_PUBLIC_API_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";

export default function AuthPage() {
  const router = useRouter();
  const [mode, setMode] = useState<AuthMode>("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [message, setMessage] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (localStorage.getItem(tokenStorageKey)) {
      router.replace("/dashboard");
    }
  }, [router]);

  async function submitAuth(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (isSubmitting) {
      return;
    }

    setMessage("");

    if (mode === "register" && password !== confirmPassword) {
      setMessage("Mật khẩu xác nhận không khớp.");
      return;
    }

    setIsSubmitting(true);

    try {
      if (mode === "register") {
        const registerResponse = await fetch(`${apiUrl}/api/users/register`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ email, password }),
        });

        if (!registerResponse.ok) {
          throw new Error(await readError(registerResponse));
        }
      }

      const loginResponse = await fetch(`${apiUrl}/api/users/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password }),
      });

      if (!loginResponse.ok) {
        throw new Error(await readError(loginResponse));
      }

      const result = (await loginResponse.json()) as LoginResponse;
      const accessToken = result.accessToken ?? result.AccessToken ?? result.token;

      if (!accessToken) {
        throw new Error("Máy chủ không trả về mã đăng nhập hợp lệ. Vui lòng thử lại.");
      }

      localStorage.setItem(tokenStorageKey, accessToken);
      localStorage.removeItem(legacyUserIdStorageKey);
      router.replace("/dashboard");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Không thể xác thực tài khoản.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="min-h-screen overflow-hidden">
      <div className="page-shell">
        <section className="relative z-10 grid min-h-[calc(100vh-80px)] items-center gap-10 2xl:grid-cols-[1.12fr_0.88fr]">
          <div className="reveal" data-delay="1">
            <span className="badge">ExpenseCraft</span>
            <h1 className="mt-5 max-w-4xl text-5xl leading-[0.95] sm:text-6xl lg:text-7xl">
              Quản lý thu chi rõ ràng, hiện đại và chủ động.
            </h1>
            <p className="mt-6 max-w-xl text-lg leading-8 text-[color:var(--muted)]">
              Đăng nhập để theo dõi ngân sách, dòng tiền, xu hướng chi tiêu và các tín hiệu tài chính quan trọng trong một không gian trực quan.
            </p>
          </div>

          <div className="panel reveal" data-delay="2">
            <div className="panel-header">
              <div>
                <p className="text-sm uppercase tracking-[0.28em] text-[color:var(--accent-strong)]">
                  {mode === "login" ? "Chào mừng trở lại" : "Bắt đầu theo dõi"}
                </p>
                <h2 className="mt-2 text-4xl">{mode === "login" ? "Đăng nhập" : "Tạo tài khoản"}</h2>
              </div>
              <button
                className="ghost-button px-5"
                type="button"
                disabled={isSubmitting}
                onClick={() => {
                  setMode(mode === "login" ? "register" : "login");
                  setMessage("");
                }}
              >
                {mode === "login" ? "Đăng ký" : "Đăng nhập"}
              </button>
            </div>

            <form className="mt-8 grid gap-5" onSubmit={submitAuth}>
              <div className="field">
                <label htmlFor="email">Địa chỉ email</label>
                <input
                  id="email"
                  name="email"
                  type="email"
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  placeholder="ban@email.com"
                  autoComplete="email"
                  required
                />
              </div>

              <div className="field">
                <label htmlFor="password">Mật khẩu</label>
                <input
                  id="password"
                  name="password"
                  type="password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  placeholder="Tối thiểu 8 ký tự"
                  autoComplete={mode === "login" ? "current-password" : "new-password"}
                  required
                  minLength={8}
                />
              </div>

              {mode === "register" ? (
                <div className="field">
                  <label htmlFor="confirm-password">Xác nhận mật khẩu</label>
                  <input
                    id="confirm-password"
                    name="confirmPassword"
                    type="password"
                    value={confirmPassword}
                    onChange={(event) => setConfirmPassword(event.target.value)}
                    placeholder="Nhập lại mật khẩu"
                    autoComplete="new-password"
                    required
                    minLength={8}
                  />
                </div>
              ) : null}

              {message ? (
                <div className="rounded-lg border border-[color:var(--danger)] bg-[color:var(--panel)] px-4 py-3 text-sm text-[color:var(--danger)]">
                  {message}
                </div>
              ) : null}

              <button className="action-button" type="submit" disabled={isSubmitting}>
                {isSubmitting ? "Đang xử lý..." : mode === "login" ? "Đăng nhập vào trang chủ" : "Tạo tài khoản và vào trang chủ"}
              </button>
            </form>

          </div>
        </section>
      </div>
    </main>
  );
}

async function readError(response: Response) {
  const text = await response.text();

  if (!text) {
    return `Yêu cầu thất bại với mã ${response.status}.`;
  }

  try {
    const parsed = JSON.parse(text) as {
      title?: string;
      detail?: string;
      errors?: Record<string, string[] | string>;
    };
    const validationMessages = parsed.errors
      ? Object.values(parsed.errors)
          .flatMap((value) => (Array.isArray(value) ? value : [value]))
          .filter(Boolean)
      : [];

    return [parsed.detail, ...validationMessages, parsed.title].filter(Boolean).join(" ") || text;
  } catch {
    return text;
  }
}

export default function Home() {
  return (
    <div className="flex min-h-screen flex-col">
      <div className="page-shell">
        <header className="mb-10 flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
          <div className="reveal" data-delay="1">
            <span className="badge">ExpenseCraft</span>
            <h1 className="mt-4 text-4xl leading-tight sm:text-5xl">
              Master your spending with clarity and calm.
            </h1>
            <p className="mt-3 max-w-xl text-base text-[color:var(--muted)]">
              Sign in to review your dashboards or create a new account to start
              tracking budgets, bills, and savings in one elegant place.
            </p>
          </div>
          <div className="panel reveal" data-delay="2">
            <div className="panel-header">
              <div>
                <p className="text-xs uppercase tracking-[0.3em] text-[color:var(--muted)]">
                  Weekly Pulse
                </p>
                <h2 className="mt-2 text-2xl">$1,284.60</h2>
                <p className="text-sm text-[color:var(--muted)]">
                  Smart insights keep spending on track.
                </p>
              </div>
              <div className="text-right">
                <p className="text-xs uppercase tracking-[0.3em] text-[color:var(--muted)]">
                  Auto-saved
                </p>
                <p className="mt-2 text-lg font-semibold text-[color:var(--accent-strong)]">
                  18%
                </p>
              </div>
            </div>
            <div className="mt-6 grid gap-3 sm:grid-cols-3">
              <div className="stat-card">
                <p className="text-xs uppercase tracking-[0.2em] text-[color:var(--muted)]">
                  Bills
                </p>
                <p className="mt-3 text-xl font-semibold">$420</p>
              </div>
              <div className="stat-card">
                <p className="text-xs uppercase tracking-[0.2em] text-[color:var(--muted)]">
                  Food
                </p>
                <p className="mt-3 text-xl font-semibold">$312</p>
              </div>
              <div className="stat-card">
                <p className="text-xs uppercase tracking-[0.2em] text-[color:var(--muted)]">
                  Savings
                </p>
                <p className="mt-3 text-xl font-semibold">$552</p>
              </div>
            </div>
          </div>
        </header>

        <main className="page-grid lg:grid-cols-2">
          <section className="panel reveal" data-delay="2">
            <div className="panel-header">
              <div>
                <h2 className="text-3xl">Welcome back</h2>
                <p className="mt-2 text-sm text-[color:var(--muted)]">
                  Keep your cashflow in perfect balance.
                </p>
              </div>
              <span className="badge">Sign In</span>
            </div>

            <form className="mt-8 grid gap-5">
              <div className="field">
                <label htmlFor="signin-email">Email address</label>
                <input
                  id="signin-email"
                  name="email"
                  type="email"
                  placeholder="you@company.com"
                  autoComplete="email"
                />
              </div>
              <div className="field">
                <label htmlFor="signin-password">Password</label>
                <input
                  id="signin-password"
                  name="password"
                  type="password"
                  placeholder="Enter your password"
                  autoComplete="current-password"
                />
              </div>
              <div className="flex items-center justify-between text-sm text-[color:var(--muted)]">
                <label className="flex items-center gap-2">
                  <input type="checkbox" className="accent-[#f2c37c]" />
                  Remember me
                </label>
                <button type="button" className="text-[color:var(--accent-strong)]">
                  Forgot password?
                </button>
              </div>
              <button type="submit" className="action-button">
                Sign in to dashboard
              </button>
              <button type="button" className="ghost-button">
                Send a one-time login link
              </button>
            </form>
          </section>

          <section className="panel reveal" data-delay="3">
            <div className="panel-header">
              <div>
                <h2 className="text-3xl">Create your account</h2>
                <p className="mt-2 text-sm text-[color:var(--muted)]">
                  Build a smarter plan for every month.
                </p>
              </div>
              <span className="badge">Sign Up</span>
            </div>

            <form className="mt-8 grid gap-5">
              <div className="grid gap-5 sm:grid-cols-2">
                <div className="field">
                  <label htmlFor="signup-name">Full name</label>
                  <input
                    id="signup-name"
                    name="name"
                    type="text"
                    placeholder="Alex Morgan"
                    autoComplete="name"
                  />
                </div>
                <div className="field">
                  <label htmlFor="signup-phone">Phone</label>
                  <input
                    id="signup-phone"
                    name="phone"
                    type="tel"
                    placeholder="+1 555 390 2244"
                    autoComplete="tel"
                  />
                </div>
              </div>
              <div className="field">
                <label htmlFor="signup-email">Email address</label>
                <input
                  id="signup-email"
                  name="email"
                  type="email"
                  placeholder="you@company.com"
                  autoComplete="email"
                />
              </div>
              <div className="grid gap-5 sm:grid-cols-2">
                <div className="field">
                  <label htmlFor="signup-password">Password</label>
                  <input
                    id="signup-password"
                    name="password"
                    type="password"
                    placeholder="Create a password"
                    autoComplete="new-password"
                  />
                </div>
                <div className="field">
                  <label htmlFor="signup-confirm">Confirm password</label>
                  <input
                    id="signup-confirm"
                    name="confirmPassword"
                    type="password"
                    placeholder="Repeat password"
                    autoComplete="new-password"
                  />
                </div>
              </div>
              <div className="grid gap-5 sm:grid-cols-2">
                <div className="field">
                  <label htmlFor="signup-currency">Default currency</label>
                  <select id="signup-currency" name="currency" defaultValue="USD">
                    <option value="USD">USD - US Dollar</option>
                    <option value="EUR">EUR - Euro</option>
                    <option value="GBP">GBP - British Pound</option>
                    <option value="JPY">JPY - Japanese Yen</option>
                    <option value="VND">VND - Vietnamese Dong</option>
                  </select>
                </div>
                <div className="field">
                  <label htmlFor="signup-budget">Monthly budget</label>
                  <input
                    id="signup-budget"
                    name="budget"
                    type="number"
                    placeholder="1500"
                    min="0"
                  />
                </div>
              </div>
              <label className="flex items-start gap-3 text-sm text-[color:var(--muted)]">
                <input type="checkbox" className="mt-1 accent-[#f2c37c]" />
                I agree to the terms and confirm I am at least 18 years old.
              </label>
              <button type="submit" className="action-button">
                Create account
              </button>
            </form>
          </section>
        </main>
      </div>
    </div>
  );
}

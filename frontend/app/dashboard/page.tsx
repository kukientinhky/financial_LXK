"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";

type CurrentUser = {
  userId: string | null;
  email: string | null;
};

const apiUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

const monthlyCashflow = [
  { month: "T1", income: 4200, expenses: 2860 },
  { month: "T2", income: 4380, expenses: 3140 },
  { month: "T3", income: 4520, expenses: 3020 },
  { month: "T4", income: 4750, expenses: 3375 },
  { month: "T5", income: 4860, expenses: 3190 },
  { month: "T6", income: 5100, expenses: 3420 },
];

const categories = [
  { name: "Nhà ở", amount: 1180, color: "#f2c37c" },
  { name: "Ăn uống", amount: 520, color: "#7dd3fc" },
  { name: "Di chuyển", amount: 340, color: "#c4b5fd" },
  { name: "Mua sắm", amount: 410, color: "#fda4af" },
  { name: "Tiết kiệm", amount: 780, color: "#86efac" },
];

const transactions = [
  ["Lương tháng", "Thu nhập", "+127,5tr", "Hôm nay"],
  ["Tiền thuê nhà", "Nhà ở", "-29,5tr", "20/06"],
  ["Đi siêu thị", "Ăn uống", "-3,1tr", "18/06"],
  ["Quỹ đầu tư", "Tiết kiệm", "-11,2tr", "15/06"],
  ["Tiền điện", "Hóa đơn", "-2,2tr", "14/06"],
];

export default function DashboardPage() {
  const router = useRouter();
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [authChecked, setAuthChecked] = useState(false);

  useEffect(() => {
    const token = localStorage.getItem("expensecraft_access_token");

    if (!token) {
      router.replace("/");
      return;
    }

    async function loadUser() {
      try {
        const response = await fetch(`${apiUrl}/api/users/me`, {
          headers: { Authorization: `Bearer ${token}` },
        });

        if (!response.ok) {
          throw new Error("Phiên đăng nhập đã hết hạn.");
        }

        setUser((await response.json()) as CurrentUser);
      } catch {
        localStorage.removeItem("expensecraft_access_token");
        localStorage.removeItem("expensecraft_user_id");
        router.replace("/");
      } finally {
        setAuthChecked(true);
      }
    }

    loadUser();
  }, [router]);

  function logout() {
    localStorage.removeItem("expensecraft_access_token");
    localStorage.removeItem("expensecraft_user_id");
    router.replace("/");
  }

  if (!authChecked) {
    return (
      <main className="grid min-h-screen place-items-center text-[color:var(--muted)]">
        Đang chuẩn bị bảng điều khiển tài chính...
      </main>
    );
  }

  const income = 5100;
  const expenses = 3420;
  const savings = income - expenses;
  const maxCashflow = Math.max(...monthlyCashflow.flatMap((item) => [item.income, item.expenses]));
  const categoryTotal = categories.reduce((total, item) => total + item.amount, 0);

  return (
    <main className="min-h-screen overflow-hidden">
      <div className="page-shell">
        <header className="relative z-10 mb-8 flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <span className="badge">Trang chủ</span>
            <h1 className="mt-4 text-5xl leading-none sm:text-6xl">Bảng điều khiển tài chính.</h1>
            <p className="mt-4 text-[color:var(--muted)]">
              Đang đăng nhập với <span className="text-[color:var(--accent-strong)]">{user?.email}</span>
            </p>
          </div>
          <button className="ghost-button px-6" type="button" onClick={logout}>
            Đăng xuất
          </button>
        </header>

        <section className="page-grid relative z-10 grid-cols-1 xl:grid-cols-[1.45fr_0.85fr]">
          <div className="grid gap-6">
            <div className="grid gap-4 md:grid-cols-3">
              <MetricCard label="Thu nhập" value="127,5tr" delta="+8,2%" tone="positive" />
              <MetricCard label="Chi tiêu" value="85,5tr" delta="-3,4%" tone="warning" />
              <MetricCard label="Tiết kiệm ròng" value={`${savings.toLocaleString()}tr`} delta="Đã tiết kiệm 32,9%" tone="positive" />
            </div>

            <div className="panel">
              <div className="panel-header">
                <div>
                  <h2 className="text-3xl">Nhịp dòng tiền</h2>
                  <p className="mt-2 text-sm text-[color:var(--muted)]">So sánh thu nhập và chi tiêu trong 6 tháng gần nhất.</p>
                </div>
                <span className="badge">Theo dõi mẫu</span>
              </div>
              <div className="mt-8 flex h-72 items-end gap-4 overflow-hidden rounded-[22px] border border-white/10 bg-black/20 p-5">
                {monthlyCashflow.map((item) => (
                  <div className="flex flex-1 flex-col items-center gap-3" key={item.month}>
                    <div className="flex h-56 w-full items-end justify-center gap-2">
                      <div
                        className="w-full max-w-8 rounded-t-full bg-gradient-to-t from-[#d79a42] to-[#ffe2ad] shadow-[0_0_24px_rgba(242,195,124,0.22)]"
                        style={{ height: `${(item.income / maxCashflow) * 100}%` }}
                      />
                      <div
                        className="w-full max-w-8 rounded-t-full bg-gradient-to-t from-[#475569] to-[#93a4ba]"
                        style={{ height: `${(item.expenses / maxCashflow) * 100}%` }}
                      />
                    </div>
                    <span className="text-xs uppercase tracking-[0.2em] text-[color:var(--muted)]">{item.month}</span>
                  </div>
                ))}
              </div>
              <div className="mt-5 flex gap-5 text-sm text-[color:var(--muted)]">
                <span><i className="mr-2 inline-block h-3 w-3 rounded-full bg-[#f2c37c]" />Thu nhập</span>
                <span><i className="mr-2 inline-block h-3 w-3 rounded-full bg-[#93a4ba]" />Chi tiêu</span>
              </div>
            </div>

            <div className="grid gap-6 lg:grid-cols-2">
              <div className="panel">
                <h2 className="text-3xl">Phân bổ danh mục</h2>
                <div className="mt-7 grid gap-4">
                  {categories.map((item) => (
                    <div key={item.name}>
                      <div className="mb-2 flex justify-between text-sm">
                        <span>{item.name}</span>
                        <span className="text-[color:var(--muted)]">{item.amount.toLocaleString()}k</span>
                      </div>
                      <div className="h-3 overflow-hidden rounded-full bg-white/10">
                        <div
                          className="h-full rounded-full"
                          style={{ width: `${(item.amount / categoryTotal) * 100}%`, background: item.color }}
                        />
                      </div>
                    </div>
                  ))}
                </div>
              </div>

              <div className="panel">
                <h2 className="text-3xl">Mục tiêu tiết kiệm</h2>
                <div className="mt-8 grid place-items-center">
                  <div className="relative grid h-52 w-52 place-items-center rounded-full bg-[conic-gradient(#f2c37c_0_68%,rgba(255,255,255,0.09)_68%_100%)]">
                    <div className="grid h-36 w-36 place-items-center rounded-full bg-[#111722] text-center shadow-inner">
                      <div>
                        <p className="text-4xl font-semibold">68%</p>
                        <p className="mt-1 text-xs uppercase tracking-[0.2em] text-[color:var(--muted)]">Mục tiêu</p>
                      </div>
                    </div>
                  </div>
                </div>
                <p className="mt-6 text-center text-sm leading-6 text-[color:var(--muted)]">
                  Bạn còn cách mục tiêu quỹ dự phòng tháng này khoảng 20,5 triệu.
                </p>
              </div>
            </div>
          </div>

          <aside className="grid gap-6">
            <div className="panel">
              <h2 className="text-3xl">Tín hiệu chi tiêu</h2>
              <div className="mt-7 space-y-5">
                <Pulse label="Sức khỏe ngân sách" value="Rất tốt" percent={82} />
                <Pulse label="Hóa đơn định kỳ" value="Ổn định" percent={64} />
                <Pulse label="Rủi ro mua sắm vội" value="Thấp" percent={28} />
              </div>
            </div>

            <div className="panel">
              <div className="panel-header">
                <h2 className="text-3xl">Giao dịch gần đây</h2>
                <span className="badge">Tháng 6</span>
              </div>
              <div className="mt-6 divide-y divide-white/10">
                {transactions.map(([name, category, amount, date]) => (
                  <div className="flex items-center justify-between gap-4 py-4" key={`${name}-${date}`}>
                    <div>
                      <p className="font-medium">{name}</p>
                      <p className="mt-1 text-xs uppercase tracking-[0.18em] text-[color:var(--muted)]">{category} / {date}</p>
                    </div>
                    <span className={amount.startsWith("+") ? "text-emerald-300" : "text-rose-200"}>{amount}</span>
                  </div>
                ))}
              </div>
            </div>
          </aside>
        </section>
      </div>
    </main>
  );
}

function MetricCard({ label, value, delta, tone }: { label: string; value: string; delta: string; tone: "positive" | "warning" }) {
  return (
    <div className="panel p-5">
      <p className="text-xs uppercase tracking-[0.24em] text-[color:var(--muted)]">{label}</p>
      <div className="mt-4 flex items-end justify-between gap-4">
        <p className="text-3xl font-semibold">{value}</p>
        <span className={tone === "positive" ? "text-sm text-emerald-300" : "text-sm text-amber-200"}>{delta}</span>
      </div>
    </div>
  );
}

function Pulse({ label, value, percent }: { label: string; value: string; percent: number }) {
  return (
    <div>
      <div className="mb-2 flex justify-between text-sm">
        <span>{label}</span>
        <span className="text-[color:var(--accent-strong)]">{value}</span>
      </div>
      <div className="h-2 overflow-hidden rounded-full bg-white/10">
        <div className="h-full rounded-full bg-gradient-to-r from-[#f2c37c] to-[#7dd3fc]" style={{ width: `${percent}%` }} />
      </div>
    </div>
  );
}

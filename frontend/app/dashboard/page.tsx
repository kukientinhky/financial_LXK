"use client";

import { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";

type CurrentUser = {
  userId: string | null;
  email: string | null;
};

type AnalyticsSummary = {
  totalIncome: number;
  totalExpense: number;
  net: number;
  currency: string;
  cashflowByMonth: CashflowByMonth[];
  expenseByCategory: ExpenseByCategory[];
  reasonableness: Reasonableness;
};

type CashflowByMonth = {
  month: string;
  income: number;
  expense: number;
  net: number;
};

type ExpenseByCategory = {
  category: string;
  amount: number;
  percent: number;
};

type Reasonableness = {
  status: string;
  message: string;
  expenseRatio: number | null;
  categoryWarnings: CategoryWarning[];
};

type CategoryWarning = {
  category: string;
  amount: number;
  percent: number;
  message: string;
};

type Transaction = {
  id: string;
  type: "income" | "expense" | string;
  amount: number;
  currency: string;
  category: string;
  note: string | null;
  source: string | null;
  occurredAt: string;
  createdAt: string;
};

type ChatMessage = {
  id: string;
  role: "user" | "assistant" | string;
  content: string;
  intent: AgentIntent | string | null;
  transactionId: string | null;
  createdAt: string;
  pending?: boolean;
  historySaveFailed?: boolean;
  warning?: string | null;
};

type AgentResponse = {
  reply: string;
  intent: AgentIntent | string | null;
  transaction: Record<string, unknown> | null;
  history_save_failed: boolean;
  warning: string | null;
};

type AgentIntent = "income" | "expense";

const tokenStorageKey = "expensecraft_access_token";
const legacyUserIdStorageKey = "expensecraft_user_id";
const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";
const agentUrl = process.env.NEXT_PUBLIC_AGENT_URL ?? "http://localhost:8000";

class AuthExpiredError extends Error {}

export default function DashboardPage() {
  const router = useRouter();
  const chatEndRef = useRef<HTMLDivElement | null>(null);
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [authChecked, setAuthChecked] = useState(false);
  const [summary, setSummary] = useState<AnalyticsSummary | null>(null);
  const [transactions, setTransactions] = useState<Transaction[]>([]);
  const [chatMessages, setChatMessages] = useState<ChatMessage[]>([]);
  const [dashboardError, setDashboardError] = useState("");
  const [chatError, setChatError] = useState("");
  const [chatWarning, setChatWarning] = useState("");
  const [isDashboardLoading, setIsDashboardLoading] = useState(false);
  const [isChatLoading, setIsChatLoading] = useState(false);
  const [chatInput, setChatInput] = useState("");
  const [isSendingChat, setIsSendingChat] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const analyticsRange = useMemo(() => getAnalyticsRange(), []);

  const logout = useCallback(() => {
    localStorage.removeItem(tokenStorageKey);
    localStorage.removeItem(legacyUserIdStorageKey);
    router.replace("/");
  }, [router]);

  const handleAuthError = useCallback(() => {
    localStorage.removeItem(tokenStorageKey);
    localStorage.removeItem(legacyUserIdStorageKey);
    router.replace("/");
  }, [router]);

  const loadDashboardData = useCallback(
    async (activeToken: string) => {
      setIsDashboardLoading(true);
      setDashboardError("");

      try {
        const [summaryResult, transactionResult] = await Promise.all([
          fetchJson<AnalyticsSummary>(
            `${apiBaseUrl}/api/analytics/summary?from=${analyticsRange.from}&to=${analyticsRange.to}`,
            activeToken,
          ),
          fetchJson<Transaction[]>(`${apiBaseUrl}/api/transactions?limit=20`, activeToken),
        ]);

        setSummary(summaryResult);
        setTransactions(transactionResult);
      } catch (error) {
        if (error instanceof AuthExpiredError) {
          handleAuthError();
          return;
        }

        setDashboardError(error instanceof Error ? error.message : "Không thể tải dữ liệu tài chính.");
      } finally {
        setIsDashboardLoading(false);
      }
    },
    [analyticsRange.from, analyticsRange.to, handleAuthError],
  );

  const loadChatMessages = useCallback(
    async (activeToken: string) => {
      setIsChatLoading(true);
      setChatError("");

      try {
        const result = await fetchJson<ChatMessage[]>(`${apiBaseUrl}/api/chat/messages?limit=50`, activeToken);
        setChatMessages((current) => mergePersistedChatWithLocalWarnings(result, current));
      } catch (error) {
        if (error instanceof AuthExpiredError) {
          handleAuthError();
          return;
        }

        setChatError(error instanceof Error ? error.message : "Không thể tải lịch sử trò chuyện.");
      } finally {
        setIsChatLoading(false);
      }
    },
    [handleAuthError],
  );

  const refreshDashboardContext = useCallback(
    async (activeToken: string) => {
      await Promise.all([loadDashboardData(activeToken), loadChatMessages(activeToken)]);
    },
    [loadDashboardData, loadChatMessages],
  );

  useEffect(() => {
    const storedToken = localStorage.getItem(tokenStorageKey);

    if (!storedToken) {
      router.replace("/");
      return;
    }

    const activeToken = storedToken;

    async function loadInitialData() {
      try {
        const currentUser = await fetchJson<CurrentUser>(`${apiBaseUrl}/api/users/me`, activeToken);
        setUser(currentUser);
        setAuthChecked(true);
        await refreshDashboardContext(activeToken);
      } catch (error) {
        setAuthChecked(true);

        if (error instanceof AuthExpiredError) {
          handleAuthError();
          return;
        }

        setDashboardError(error instanceof Error ? error.message : "Không thể xác thực phiên đăng nhập.");
      }
    }

    loadInitialData();
  }, [handleAuthError, refreshDashboardContext, router]);

  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ block: "end" });
  }, [chatMessages]);

  async function sendChatMessage(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const activeToken = localStorage.getItem(tokenStorageKey);
    const message = chatInput.trim();

    if (!activeToken) {
      setChatError("Bạn cần đăng nhập lại để trò chuyện với trợ lý.");
      handleAuthError();
      return;
    }

    if (!message || isSendingChat) {
      return;
    }

    const localUserMessage: ChatMessage = {
      id: `local-user-${Date.now()}`,
      role: "user",
      content: message,
      intent: null,
      transactionId: null,
      createdAt: new Date().toISOString(),
      pending: true,
    };

    setChatInput("");
    setChatError("");
    setChatWarning("");
    setIsSendingChat(true);
    setChatMessages((current) => [...current, localUserMessage]);

    try {
      const response = await fetch(`${agentUrl}/api/chat`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${activeToken}`,
        },
        body: JSON.stringify({ message }),
      });

      if (response.status === 401) {
        throw new AuthExpiredError("Phiên đăng nhập đã hết hạn.");
      }

      if (!response.ok) {
        throw new Error(await readError(response));
      }

      const result = (await response.json()) as AgentResponse;
      const historySaveFailed = result.history_save_failed === true;
      const warning = historySaveFailed ? buildHistorySaveWarning(result.warning) : null;
      const localAssistantMessage: ChatMessage = {
        id: `local-assistant-${Date.now()}`,
        role: "assistant",
        content: result.reply,
        intent: normalizeAgentIntent(result.intent),
        transactionId: getTransactionId(result.transaction),
        createdAt: new Date().toISOString(),
        pending: !historySaveFailed,
        historySaveFailed,
        warning,
      };

      setChatMessages((current) => [...current, localAssistantMessage]);
      if (warning) {
        setChatWarning(warning);
      }
      await refreshDashboardContext(activeToken);
      if (historySaveFailed) {
        setChatMessages((current) => upsertLocalWarningMessage(current, localAssistantMessage));
      }
    } catch (error) {
      if (error instanceof AuthExpiredError) {
        handleAuthError();
        return;
      }

      setChatError(error instanceof Error ? error.message : "Không thể gửi tin nhắn đến trợ lý.");
      setChatMessages((current) => current.map((item) => (item.id === localUserMessage.id ? { ...item, pending: false } : item)));
    } finally {
      setIsSendingChat(false);
    }
  }

  async function deleteTransaction(transaction: Transaction) {
    const activeToken = localStorage.getItem(tokenStorageKey);

    if (!activeToken) {
      handleAuthError();
      return;
    }

    const accepted = window.confirm(
      `Xóa giao dịch chi tiêu "${transaction.note || transaction.category}"? Hành động này sẽ cập nhật lại dashboard.`,
    );

    if (!accepted) {
      return;
    }

    setDeletingId(transaction.id);
    setDashboardError("");

    try {
      const response = await fetch(`${apiBaseUrl}/api/transactions/${transaction.id}`, {
        method: "DELETE",
        headers: { Authorization: `Bearer ${activeToken}` },
      });

      if (response.status === 401) {
        throw new AuthExpiredError("Phiên đăng nhập đã hết hạn.");
      }

      if (!response.ok) {
        throw new Error(await readError(response));
      }

      await refreshDashboardContext(activeToken);
    } catch (error) {
      if (error instanceof AuthExpiredError) {
        handleAuthError();
        return;
      }

      setDashboardError(error instanceof Error ? error.message : "Không thể xóa giao dịch.");
    } finally {
      setDeletingId(null);
    }
  }

  if (!authChecked) {
    return (
      <main className="grid min-h-screen place-items-center bg-[color:var(--bg)] text-[color:var(--muted)]">
        Đang chuẩn bị bảng điều khiển tài chính...
      </main>
    );
  }

  const cashflow = normalizeCashflow(summary?.cashflowByMonth ?? [], analyticsRange.from);
  const maxCashflow = Math.max(1, ...cashflow.flatMap((item) => [item.income, item.expense]));
  const categories = summary?.expenseByCategory ?? [];
  const reasonableness = summary?.reasonableness ?? null;
  const expenseRatioPercent = Math.min(100, Math.round((reasonableness?.expenseRatio ?? 0) * 100));

  return (
    <main className="min-h-screen overflow-hidden bg-[color:var(--bg)]">
      <div className="page-shell">
        <header className="relative z-10 mb-8 flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <span className="badge">Trang chủ</span>
            <h1 className="mt-4 text-[2rem] font-bold leading-[2.5rem] text-[color:var(--ink)] sm:text-5xl sm:leading-tight">
              Bảng điều khiển tài chính
            </h1>
            <p className="mt-4 text-[color:var(--muted)]">
              Đang đăng nhập với <span className="font-medium text-[color:var(--primary)]">{user?.email ?? "tài khoản của bạn"}</span>
            </p>
          </div>
          <button className="ghost-button px-6" type="button" onClick={logout}>
            Đăng xuất
          </button>
        </header>

        {dashboardError ? <AlertMessage message={dashboardError} /> : null}

        <section className="page-grid relative z-10 grid-cols-1 xl:grid-cols-[1.35fr_0.95fr]">
          <div className="grid gap-6">
            <div className="grid gap-4 md:grid-cols-3">
              <MetricCard
                label="Thu nhập"
                value={formatCurrency(summary?.totalIncome ?? 0, summary?.currency)}
                helper={summary ? "Từ giao dịch thực tế" : "Đang chờ dữ liệu"}
                tone="positive"
              />
              <MetricCard
                label="Chi tiêu"
                value={formatCurrency(summary?.totalExpense ?? 0, summary?.currency)}
                helper={reasonableness?.expenseRatio !== null && reasonableness ? `Bằng ${expenseRatioPercent}% thu nhập` : "Cần thêm dữ liệu thu nhập"}
                tone="warning"
              />
              <MetricCard
                label="Dòng tiền ròng"
                value={formatCurrency(summary?.net ?? 0, summary?.currency)}
                helper={(summary?.net ?? 0) >= 0 ? "Đang dương" : "Đang âm"}
                tone={(summary?.net ?? 0) >= 0 ? "positive" : "danger"}
              />
            </div>

            <div className="panel">
              <div className="panel-header flex-col items-start sm:flex-row sm:items-center">
                <div>
                  <h2 className="text-2xl font-semibold text-[color:var(--ink)]">Dòng tiền theo tháng</h2>
                  <p className="mt-2 text-sm text-[color:var(--muted)]">Thu nhập và chi tiêu trong 6 tháng gần nhất từ dữ liệu thật.</p>
                </div>
                <span className="badge">{formatRange(analyticsRange.from, analyticsRange.to)}</span>
              </div>

              {isDashboardLoading && !summary ? (
                <LoadingState label="Đang tải biểu đồ dòng tiền..." />
              ) : cashflow.every((item) => item.income === 0 && item.expense === 0) ? (
                <EmptyState label="Chưa có giao dịch trong khoảng thời gian này." />
              ) : (
                <div className="mt-8 flex h-72 items-end gap-3 overflow-x-auto rounded-lg border border-[color:var(--line)] bg-[color:var(--panel-soft)] p-4 sm:gap-4">
                  {cashflow.map((item) => (
                    <div className="flex min-w-16 flex-1 flex-col items-center gap-3" key={item.month}>
                      <div className="flex h-52 w-full items-end justify-center gap-2">
                        <div
                          className="w-full max-w-8 rounded-t-md bg-[color:var(--success)]"
                          title={`Thu nhập ${formatCurrency(item.income, summary?.currency)}`}
                          style={{ height: `${(item.income / maxCashflow) * 100}%` }}
                        />
                        <div
                          className="w-full max-w-8 rounded-t-md bg-[color:var(--danger)]"
                          title={`Chi tiêu ${formatCurrency(item.expense, summary?.currency)}`}
                          style={{ height: `${(item.expense / maxCashflow) * 100}%` }}
                        />
                      </div>
                      <span className="text-xs font-medium uppercase tracking-[0.08em] text-[color:var(--muted)]">{formatMonth(item.month)}</span>
                    </div>
                  ))}
                </div>
              )}

              <div className="mt-5 flex flex-wrap gap-5 text-sm text-[color:var(--muted)]">
                <span><i className="mr-2 inline-block h-3 w-3 rounded-sm bg-[color:var(--success)]" />Thu nhập</span>
                <span><i className="mr-2 inline-block h-3 w-3 rounded-sm bg-[color:var(--danger)]" />Chi tiêu</span>
              </div>
            </div>

            <div className="grid gap-6 lg:grid-cols-2">
              <div className="panel">
                <h2 className="text-2xl font-semibold text-[color:var(--ink)]">Chi tiêu theo danh mục</h2>
                {isDashboardLoading && !summary ? (
                  <LoadingState label="Đang phân tích danh mục..." />
                ) : categories.length === 0 ? (
                  <EmptyState label="Chưa có khoản chi nào để phân bổ." />
                ) : (
                  <div className="mt-7 grid gap-4">
                    {categories.map((item, index) => (
                      <div key={item.category}>
                        <div className="mb-2 flex justify-between gap-4 text-sm">
                          <span className="font-medium text-[color:var(--ink)]">{item.category}</span>
                          <span className="text-right text-[color:var(--muted)]">
                            {formatCurrency(item.amount, summary?.currency)} · {formatPercent(item.percent)}
                          </span>
                        </div>
                        <div className="h-3 overflow-hidden rounded-md bg-[color:var(--panel-soft)]">
                          <div
                            className="h-full rounded-md"
                            style={{ width: `${Math.min(100, item.percent)}%`, background: categoryColor(index) }}
                          />
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              <div className="panel">
                <h2 className="text-2xl font-semibold text-[color:var(--ink)]">Độ hợp lý chi tiêu</h2>
                {isDashboardLoading && !summary ? (
                  <LoadingState label="Đang đánh giá chi tiêu..." />
                ) : reasonableness ? (
                  <div className="mt-7 grid gap-5">
                    <div className="rounded-lg border border-[color:var(--line)] bg-[color:var(--panel-soft)] p-4">
                      <p className={`text-sm font-semibold ${reasonTone(reasonableness.status)}`}>{reasonLabel(reasonableness.status)}</p>
                      <p className="mt-2 text-sm leading-6 text-[color:var(--muted)]">{reasonDescription(reasonableness)}</p>
                    </div>
                    <div>
                      <div className="mb-2 flex justify-between text-sm">
                        <span>Tỷ lệ chi tiêu / thu nhập</span>
                        <span className="font-medium text-[color:var(--ink)]">{reasonableness.expenseRatio === null ? "Chưa đủ dữ liệu" : `${expenseRatioPercent}%`}</span>
                      </div>
                      <div className="h-3 overflow-hidden rounded-md bg-[color:var(--panel-soft)]">
                        <div className={`h-full rounded-md ${reasonBarColor(reasonableness.status)}`} style={{ width: `${expenseRatioPercent}%` }} />
                      </div>
                    </div>
                    {reasonableness.categoryWarnings.length > 0 ? (
                      <div className="grid gap-2 text-sm text-[color:var(--muted)]">
                        {reasonableness.categoryWarnings.map((warning) => (
                          <p key={warning.category}>• {warning.category} chiếm {formatPercent(warning.percent)} tổng chi tiêu.</p>
                        ))}
                      </div>
                    ) : (
                      <p className="text-sm text-[color:var(--muted)]">Không có danh mục nào vượt ngưỡng cảnh báo.</p>
                    )}
                  </div>
                ) : (
                  <EmptyState label="Chưa có dữ liệu để đánh giá." />
                )}
              </div>
            </div>

            <div className="panel">
              <div className="panel-header flex-col items-start sm:flex-row sm:items-center">
                <div>
                  <h2 className="text-2xl font-semibold text-[color:var(--ink)]">Giao dịch gần đây</h2>
                  <p className="mt-2 text-sm text-[color:var(--muted)]">Xóa khoản chi nhập sai để cập nhật lại số liệu và ngữ cảnh chat.</p>
                </div>
                <span className="badge">20 giao dịch</span>
              </div>

              {isDashboardLoading && transactions.length === 0 ? (
                <LoadingState label="Đang tải giao dịch..." />
              ) : transactions.length === 0 ? (
                <EmptyState label="Chưa có giao dịch nào. Hãy nhắn trợ lý để ghi nhận khoản thu chi đầu tiên." />
              ) : (
                <div className="mt-6 overflow-hidden rounded-lg border border-[color:var(--line)]">
                  <div className="hidden grid-cols-[1.2fr_0.7fr_0.7fr_auto] gap-4 bg-[color:var(--panel-soft)] px-4 py-3 text-sm font-medium text-[color:var(--ink)] md:grid">
                    <span>Giao dịch</span>
                    <span>Danh mục</span>
                    <span className="text-right">Số tiền</span>
                    <span className="text-right">Thao tác</span>
                  </div>
                  <div className="divide-y divide-[color:var(--line)]">
                    {transactions.map((transaction) => (
                      <div className="grid gap-3 px-4 py-4 md:grid-cols-[1.2fr_0.7fr_0.7fr_auto] md:items-center" key={transaction.id}>
                        <div>
                          <p className="font-medium text-[color:var(--ink)]">{transaction.note || transaction.category}</p>
                          <p className="mt-1 text-sm text-[color:var(--muted)]">{formatDate(transaction.occurredAt)} · {transaction.source || "manual"}</p>
                        </div>
                        <p className="text-sm text-[color:var(--muted)]">{transaction.category}</p>
                        <p className={`font-semibold md:text-right ${transaction.type === "income" ? "text-[color:var(--success)]" : "text-[color:var(--danger)]"}`}>
                          {transaction.type === "income" ? "+" : "-"}{formatCurrency(transaction.amount, transaction.currency)}
                        </p>
                        <div className="flex justify-start md:justify-end">
                          {transaction.type === "expense" ? (
                            <button
                              className="danger-button"
                              type="button"
                              disabled={deletingId === transaction.id}
                              onClick={() => deleteTransaction(transaction)}
                            >
                              {deletingId === transaction.id ? "Đang xóa" : "Xóa"}
                            </button>
                          ) : (
                            <span className="text-sm text-[color:var(--muted)]">—</span>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>

          <aside className="grid content-start gap-6">
            <div className="panel">
              <div className="panel-header flex-col items-start sm:flex-row sm:items-center xl:flex-col xl:items-start 2xl:flex-row 2xl:items-center">
                <div>
                  <h2 className="text-2xl font-semibold text-[color:var(--ink)]">Trợ lý tài chính</h2>
                  <p className="mt-2 text-sm text-[color:var(--muted)]">Nhắn bằng tiếng Việt để ghi nhận thu chi và xem lại lịch sử.</p>
                </div>
                <span className="badge">Chatbot</span>
              </div>

              <div className="mt-6 flex max-h-[520px] min-h-80 flex-col gap-3 overflow-y-auto rounded-lg border border-[color:var(--line)] bg-[color:var(--panel-soft)] p-4">
                {isChatLoading && chatMessages.length === 0 ? (
                  <LoadingState label="Đang tải lịch sử trò chuyện..." />
                ) : chatMessages.length === 0 ? (
                  <EmptyState label="Chưa có tin nhắn. Ví dụ: “tôi vừa chuyển 20k tiền ăn sáng”." />
                ) : (
                  chatMessages.map((message) => (
                    <div
                      className={`max-w-[88%] rounded-lg px-4 py-3 text-sm leading-6 ${
                        message.role === "user"
                          ? "ml-auto bg-[color:var(--primary)] text-[color:var(--on-primary)]"
                          : "mr-auto border border-[color:var(--line)] bg-[color:var(--panel)] text-[color:var(--ink)]"
                      }`}
                      key={message.id}
                    >
                      <p>{message.content}</p>
                      {message.historySaveFailed && message.warning ? (
                        <div className="mt-3 rounded-md border border-[color:var(--warning)] bg-[color:var(--panel-soft)] p-3 text-[color:var(--ink)]">
                          <p className="font-semibold text-[color:var(--warning)]">Giao dịch đã được lưu</p>
                          <p className="mt-1 text-xs leading-5 text-[color:var(--muted)]">{message.warning}</p>
                        </div>
                      ) : null}
                      <p className={`mt-2 text-xs ${message.role === "user" ? "text-[color:var(--on-primary)] opacity-80" : "text-[color:var(--muted)]"}`}>
                        {message.pending ? "Đang đồng bộ" : formatTime(message.createdAt)}
                      </p>
                    </div>
                  ))
                )}
                <div ref={chatEndRef} />
              </div>

              {chatError ? <AlertMessage message={chatError} /> : null}
              {chatWarning ? <WarningMessage message={chatWarning} /> : null}

              <form className="mt-5 grid gap-3" onSubmit={sendChatMessage}>
                <div className="field">
                  <label htmlFor="chat-message">Tin nhắn</label>
                  <textarea
                    id="chat-message"
                    value={chatInput}
                    onChange={(event) => setChatInput(event.target.value)}
                    placeholder="Ví dụ: tôi vừa chuyển 20k tiền ăn sáng"
                    disabled={isSendingChat}
                  />
                </div>
                <button className="action-button" type="submit" disabled={isSendingChat || !chatInput.trim()}>
                  {isSendingChat ? "Đang gửi..." : "Gửi cho trợ lý"}
                </button>
              </form>
            </div>
          </aside>
        </section>
      </div>
    </main>
  );
}

function MetricCard({
  label,
  value,
  helper,
  tone,
}: {
  label: string;
  value: string;
  helper: string;
  tone: "positive" | "warning" | "danger";
}) {
  const toneClass = tone === "positive" ? "text-[color:var(--success)]" : tone === "warning" ? "text-[color:var(--warning)]" : "text-[color:var(--danger)]";

  return (
    <div className="panel">
      <p className="text-sm font-medium text-[color:var(--muted)]">{label}</p>
      <div className="mt-4 grid gap-2">
        <p className="text-3xl font-semibold text-[color:var(--ink)]">{value}</p>
        <span className={`text-sm ${toneClass}`}>{helper}</span>
      </div>
    </div>
  );
}

function AlertMessage({ message }: { message: string }) {
  return (
    <div className="relative z-10 mb-6 rounded-lg border border-[color:var(--danger)] bg-[color:var(--panel)] px-4 py-3 text-sm text-[color:var(--danger)]">
      {message}
    </div>
  );
}

function WarningMessage({ message }: { message: string }) {
  return (
    <div className="relative z-10 mt-4 rounded-lg border border-[color:var(--warning)] bg-[color:var(--panel)] px-4 py-3 text-sm">
      <p className="font-semibold text-[color:var(--warning)]">Giao dịch đã được lưu</p>
      <p className="mt-1 text-[color:var(--muted)]">{message}</p>
    </div>
  );
}

function LoadingState({ label }: { label: string }) {
  return <div className="mt-6 rounded-lg border border-[color:var(--line)] bg-[color:var(--panel-soft)] p-4 text-sm text-[color:var(--muted)]">{label}</div>;
}

function EmptyState({ label }: { label: string }) {
  return <div className="mt-6 rounded-lg border border-[color:var(--line)] bg-[color:var(--panel-soft)] p-4 text-sm text-[color:var(--muted)]">{label}</div>;
}

function buildHistorySaveWarning(warning: string | null) {
  const retryReminder = "Giao dịch tiền đã được lưu thành công; hãy kiểm tra danh sách giao dịch gần đây trước khi gửi lại để tránh ghi trùng.";
  const normalizedWarning = warning?.trim();

  return normalizedWarning ? `${normalizedWarning} ${retryReminder}` : `Phản hồi trợ lý không lưu được vào lịch sử. ${retryReminder}`;
}

function mergePersistedChatWithLocalWarnings(persistedMessages: ChatMessage[], currentMessages: ChatMessage[]) {
  const localWarningMessages = currentMessages.filter((message) => message.historySaveFailed);

  if (localWarningMessages.length === 0) {
    return persistedMessages;
  }

  const persistedIds = new Set(persistedMessages.map((message) => message.id));
  return sortChatMessages([...persistedMessages, ...localWarningMessages.filter((message) => !persistedIds.has(message.id))]);
}

function upsertLocalWarningMessage(messages: ChatMessage[], warningMessage: ChatMessage) {
  const stableWarningMessage = {
    ...warningMessage,
    pending: false,
    historySaveFailed: true,
  };
  const exists = messages.some((message) => message.id === stableWarningMessage.id);

  if (exists) {
    return messages.map((message) => (message.id === stableWarningMessage.id ? stableWarningMessage : message));
  }

  return sortChatMessages([...messages, stableWarningMessage]);
}

function sortChatMessages(messages: ChatMessage[]) {
  return [...messages].sort((first, second) => Date.parse(first.createdAt) - Date.parse(second.createdAt));
}

async function fetchJson<T>(url: string, token: string): Promise<T> {
  const response = await fetch(url, {
    headers: { Authorization: `Bearer ${token}` },
  });

  if (response.status === 401) {
    throw new AuthExpiredError("Phiên đăng nhập đã hết hạn.");
  }

  if (!response.ok) {
    throw new Error(await readError(response));
  }

  return (await response.json()) as T;
}

async function readError(response: Response) {
  const text = await response.text();

  if (!text) {
    return `Không thể hoàn tất yêu cầu (mã ${response.status}). Vui lòng thử lại.`;
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

function normalizeAgentIntent(intent: AgentResponse["intent"]) {
  if (intent === "income" || intent === "expense") {
    return intent;
  }

  return null;
}

function getAnalyticsRange() {
  const today = new Date();
  const from = new Date(today.getFullYear(), today.getMonth() - 5, 1);

  return {
    from: toDateParam(from),
    to: toDateParam(today),
  };
}

function toDateParam(date: Date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");

  return `${year}-${month}-${day}`;
}

function normalizeCashflow(items: CashflowByMonth[], fromDate: string) {
  const start = new Date(`${fromDate}T00:00:00`);
  const byMonth = new Map(items.map((item) => [item.month, item]));

  return Array.from({ length: 6 }, (_, index) => {
    const date = new Date(start.getFullYear(), start.getMonth() + index, 1);
    const month = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}`;

    return byMonth.get(month) ?? { month, income: 0, expense: 0, net: 0 };
  });
}

function formatCurrency(value: number, currency = "VND") {
  return new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency,
    maximumFractionDigits: 0,
  }).format(value);
}

function formatPercent(value: number) {
  return `${new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 1 }).format(value)}%`;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" }).format(new Date(value));
}

function formatTime(value: string) {
  return new Intl.DateTimeFormat("vi-VN", { hour: "2-digit", minute: "2-digit", day: "2-digit", month: "2-digit" }).format(new Date(value));
}

function formatMonth(value: string) {
  const [year, month] = value.split("-");
  return month && year ? `T${Number(month)}/${year.slice(2)}` : value;
}

function formatRange(from: string, to: string) {
  return `${formatDate(from)} - ${formatDate(to)}`;
}

function categoryColor(index: number) {
  const colors = ["var(--primary)", "var(--accent)", "var(--warning)", "var(--success)", "var(--secondary)", "var(--danger)"];
  return colors[index % colors.length];
}

function reasonLabel(status: string) {
  if (status === "healthy") {
    return "Lành mạnh";
  }
  if (status === "watch") {
    return "Cần theo dõi";
  }
  if (status === "high") {
    return "Chi tiêu cao";
  }
  return "Chưa đủ dữ liệu";
}

function reasonDescription(reasonableness: Reasonableness) {
  if (reasonableness.status === "healthy") {
    return "Mức chi đang hợp lý so với thu nhập trong kỳ.";
  }
  if (reasonableness.status === "watch") {
    return "Chi tiêu đang tiến gần ngưỡng cần kiểm soát. Hãy xem lại các danh mục lớn.";
  }
  if (reasonableness.status === "high") {
    return "Chi tiêu đang cao so với thu nhập. Nên rà soát các khoản chi không cần thiết.";
  }
  return "Cần thêm dữ liệu thu nhập để đánh giá độ hợp lý của chi tiêu.";
}

function reasonTone(status: string) {
  if (status === "healthy") {
    return "text-[color:var(--success)]";
  }
  if (status === "watch") {
    return "text-[color:var(--warning)]";
  }
  if (status === "high") {
    return "text-[color:var(--danger)]";
  }
  return "text-[color:var(--secondary)]";
}

function reasonBarColor(status: string) {
  if (status === "healthy") {
    return "bg-[color:var(--success)]";
  }
  if (status === "watch") {
    return "bg-[color:var(--warning)]";
  }
  if (status === "high") {
    return "bg-[color:var(--danger)]";
  }
  return "bg-[color:var(--secondary)]";
}

function getTransactionId(transaction: AgentResponse["transaction"]) {
  if (!transaction) {
    return null;
  }

  const id = transaction.id ?? transaction.Id;
  return typeof id === "string" ? id : null;
}

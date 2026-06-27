using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseCraft.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionsAndChatMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('public.transactions') IS NOT NULL
                       AND NOT EXISTS (
                           SELECT 1
                           FROM information_schema.columns
                           WHERE table_schema = 'public'
                             AND table_name = 'transactions'
                             AND column_name = 'Id'
                       ) THEN
                        IF to_regclass('public.transactions_legacy') IS NULL THEN
                            ALTER TABLE public.transactions RENAME TO transactions_legacy;
                        ELSE
                            RAISE EXCEPTION 'Legacy transactions table is incompatible and transactions_legacy already exists.';
                        END IF;
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS transactions (
                    "Id" uuid NOT NULL,
                    user_id uuid NOT NULL,
                    type character varying(20) NOT NULL,
                    amount numeric(18,2) NOT NULL,
                    currency character varying(10) NOT NULL,
                    category character varying(100) NOT NULL,
                    note character varying(500),
                    source character varying(100),
                    occurred_at timestamp with time zone NOT NULL,
                    created_at timestamp with time zone NOT NULL,
                    deleted_at timestamp with time zone,
                    CONSTRAINT "PK_transactions" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_transactions_users_user_id" FOREIGN KEY (user_id) REFERENCES users ("Id") ON DELETE CASCADE
                );
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS chat_messages (
                    "Id" uuid NOT NULL,
                    user_id uuid NOT NULL,
                    role character varying(20) NOT NULL,
                    content character varying(4000) NOT NULL,
                    intent character varying(20),
                    transaction_id uuid,
                    created_at timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_chat_messages" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_chat_messages_transactions_transaction_id" FOREIGN KEY (transaction_id) REFERENCES transactions ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_chat_messages_users_user_id" FOREIGN KEY (user_id) REFERENCES users ("Id") ON DELETE CASCADE
                );
                """);

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_chat_messages_transaction_id\" ON chat_messages (transaction_id);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_chat_messages_user_id_created_at\" ON chat_messages (user_id, created_at);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_transactions_deleted_at\" ON transactions (deleted_at);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_transactions_user_id_occurred_at\" ON transactions (user_id, occurred_at);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_messages");

            migrationBuilder.DropTable(
                name: "transactions");
        }
    }
}

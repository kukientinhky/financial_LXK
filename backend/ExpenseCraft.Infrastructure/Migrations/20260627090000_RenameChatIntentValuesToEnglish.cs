using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseCraft.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameChatIntentValuesToEnglish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE chat_messages SET intent = 'income' WHERE intent = 'thu';");
            migrationBuilder.Sql("UPDATE chat_messages SET intent = 'expense' WHERE intent = 'chi';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE chat_messages SET intent = 'thu' WHERE intent = 'income';");
            migrationBuilder.Sql("UPDATE chat_messages SET intent = 'chi' WHERE intent = 'expense';");
        }
    }
}

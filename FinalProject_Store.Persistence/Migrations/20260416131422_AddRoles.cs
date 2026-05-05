using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FinalProject_Store.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_userInRoles_Roles_RoleId",
                table: "userInRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_userInRoles_Users_UserId",
                table: "userInRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_userInRoles",
                table: "userInRoles");

            migrationBuilder.RenameTable(
                name: "userInRoles",
                newName: "UserInRoles");

            migrationBuilder.RenameIndex(
                name: "IX_userInRoles_UserId",
                table: "UserInRoles",
                newName: "IX_UserInRoles_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_userInRoles_RoleId",
                table: "UserInRoles",
                newName: "IX_UserInRoles_RoleId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserInRoles",
                table: "UserInRoles",
                column: "Id");

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1L, "Admin" },
                    { 2L, "Operator" },
                    { 3L, "Customer" }
                });

            migrationBuilder.AddForeignKey(
                name: "FK_UserInRoles_Roles_RoleId",
                table: "UserInRoles",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserInRoles_Users_UserId",
                table: "UserInRoles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserInRoles_Roles_RoleId",
                table: "UserInRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserInRoles_Users_UserId",
                table: "UserInRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserInRoles",
                table: "UserInRoles");

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 1L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 2L);

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: 3L);

            migrationBuilder.RenameTable(
                name: "UserInRoles",
                newName: "userInRoles");

            migrationBuilder.RenameIndex(
                name: "IX_UserInRoles_UserId",
                table: "userInRoles",
                newName: "IX_userInRoles_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_UserInRoles_RoleId",
                table: "userInRoles",
                newName: "IX_userInRoles_RoleId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_userInRoles",
                table: "userInRoles",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_userInRoles_Roles_RoleId",
                table: "userInRoles",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_userInRoles_Users_UserId",
                table: "userInRoles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

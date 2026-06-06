using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAttributeDescriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 0,
                column: "Description",
                value: "A measure of one's raw physical force. Increases the damage of some physical skills and contributes to maximum health.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 1,
                column: "Description",
                value: "A measure of one's resilience and physical fortitude. Contributes to maximum health and defense.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 2,
                column: "Description",
                value: "A measure of one's mental acuity and command of the arcane. Increases the damage of magical skills.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 3,
                column: "Description",
                value: "A measure of one's speed and reflexes. Improves cooldown recovery and contributes to defense.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 4,
                column: "Description",
                value: "A measure of one's precision and finesse. Increases the damage of some physical skills and improves cooldown recovery.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 5,
                column: "Description",
                value: "A measure of one's fortune, influencing various chance-based outcomes.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 6,
                column: "Description",
                value: "The amount of health a character has at the start of a battle.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 7,
                column: "Description",
                value: "A flat reduction applied to all incoming damage.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 8,
                column: "Description",
                value: "A percentage multiplier to the rate at which skills become available again after being used.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 9,
                column: "Description",
                value: "Obsolete. Previously increased the rate at which items were dropped by enemies.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 10,
                column: "Description",
                value: "The percentage chance for an attack to deal increased damage.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 11,
                column: "Description",
                value: "The additional percentage of damage dealt when a critical hit occurs.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 12,
                column: "Description",
                value: "The percentage chance to completely avoid the damage from an incoming attack.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 13,
                column: "Description",
                value: "The percentage chance to block part of the damage from an incoming attack.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 14,
                column: "Description",
                value: "A flat reduction applied to damage received when an attack is blocked.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 0,
                column: "Description",
                value: "A measure of one's raw physical force.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 1,
                column: "Description",
                value: "A measure of one's raw physical force.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 2,
                column: "Description",
                value: "A measure of one's raw physical force.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 3,
                column: "Description",
                value: "A measure of one's raw physical force.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 4,
                column: "Description",
                value: "A measure of one's raw physical force.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 5,
                column: "Description",
                value: "A measure of one's raw physical force.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 6,
                column: "Description",
                value: "A measure of one's raw physical force.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 7,
                column: "Description",
                value: "A measure of one's raw physical force.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 8,
                column: "Description",
                value: "A measure of one's raw physical force.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 9,
                column: "Description",
                value: "A measure of one's raw physical force.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 10,
                column: "Description",
                value: "A measure of one's raw physical force.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 11,
                column: "Description",
                value: "A measure of one's raw physical force.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 12,
                column: "Description",
                value: "A measure of one's raw physical force.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 13,
                column: "Description",
                value: "A measure of one's raw physical force.");

            migrationBuilder.UpdateData(
                table: "Attributes",
                keyColumn: "Id",
                keyValue: 14,
                column: "Description",
                value: "A measure of one's raw physical force.");
        }
    }
}

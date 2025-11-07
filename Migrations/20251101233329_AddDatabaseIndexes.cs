using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTDetectorApi.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "RawData",
                table: "SensorData",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EventType",
                table: "SensorData",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "DeviceId",
                table: "SensorData",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "EventLogs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Level",
                table: "EventLogs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "DeviceId",
                table: "EventLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Devices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Devices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Devices",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "LastSignal",
                table: "Devices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "Devices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            // Create indexes for performance
            migrationBuilder.CreateIndex(
                name: "IX_Devices_LastSeen",
                table: "Devices",
                column: "LastSeen",
                descending: new[] { true });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_Status",
                table: "Devices",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_Type",
                table: "Devices",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_SensorData_Timestamp",
                table: "SensorData",
                column: "Timestamp",
                descending: new[] { true });

            migrationBuilder.CreateIndex(
                name: "IX_SensorData_DeviceId",
                table: "SensorData",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_SensorData_EventType",
                table: "SensorData",
                column: "EventType");

            // Composite index for common query pattern (filter by device + time range)
            migrationBuilder.CreateIndex(
                name: "IX_SensorData_DeviceId_Timestamp",
                table: "SensorData",
                columns: new[] { "DeviceId", "Timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_EventLogs_Timestamp",
                table: "EventLogs",
                column: "Timestamp",
                descending: new[] { true });

            migrationBuilder.CreateIndex(
                name: "IX_EventLogs_Level",
                table: "EventLogs",
                column: "Level");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes
            migrationBuilder.DropIndex(
                name: "IX_Devices_LastSeen",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_Status",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_Type",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_SensorData_Timestamp",
                table: "SensorData");

            migrationBuilder.DropIndex(
                name: "IX_SensorData_DeviceId",
                table: "SensorData");

            migrationBuilder.DropIndex(
                name: "IX_SensorData_EventType",
                table: "SensorData");

            migrationBuilder.DropIndex(
                name: "IX_SensorData_DeviceId_Timestamp",
                table: "SensorData");

            migrationBuilder.DropIndex(
                name: "IX_EventLogs_Timestamp",
                table: "EventLogs");

            migrationBuilder.DropIndex(
                name: "IX_EventLogs_Level",
                table: "EventLogs");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "RawData",
                table: "SensorData",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EventType",
                table: "SensorData",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "DeviceId",
                table: "SensorData",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "EventLogs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "Level",
                table: "EventLogs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "DeviceId",
                table: "EventLogs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Devices",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Devices",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Devices",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "LastSignal",
                table: "Devices",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "Devices",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);
        }
    }
}

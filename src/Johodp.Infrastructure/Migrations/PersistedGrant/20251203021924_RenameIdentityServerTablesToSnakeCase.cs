using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Johodp.Infrastructure.Migrations.PersistedGrant
{
    /// <inheritdoc />
    public partial class RenameIdentityServerTablesToSnakeCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Keys",
                schema: "dbo",
                table: "Keys");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ServerSideSessions",
                schema: "dbo",
                table: "ServerSideSessions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PushedAuthorizationRequests",
                schema: "dbo",
                table: "PushedAuthorizationRequests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PersistedGrants",
                schema: "dbo",
                table: "PersistedGrants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DeviceCodes",
                schema: "dbo",
                table: "DeviceCodes");

            migrationBuilder.RenameTable(
                name: "Keys",
                schema: "dbo",
                newName: "keys",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "ServerSideSessions",
                schema: "dbo",
                newName: "server_side_sessions",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "PushedAuthorizationRequests",
                schema: "dbo",
                newName: "pushed_authorization_requests",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "PersistedGrants",
                schema: "dbo",
                newName: "persisted_grants",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "DeviceCodes",
                schema: "dbo",
                newName: "device_codes",
                newSchema: "dbo");

            migrationBuilder.RenameIndex(
                name: "IX_Keys_Use",
                schema: "dbo",
                table: "keys",
                newName: "IX_keys_Use");

            migrationBuilder.RenameIndex(
                name: "IX_ServerSideSessions_SubjectId",
                schema: "dbo",
                table: "server_side_sessions",
                newName: "IX_server_side_sessions_SubjectId");

            migrationBuilder.RenameIndex(
                name: "IX_ServerSideSessions_SessionId",
                schema: "dbo",
                table: "server_side_sessions",
                newName: "IX_server_side_sessions_SessionId");

            migrationBuilder.RenameIndex(
                name: "IX_ServerSideSessions_Key",
                schema: "dbo",
                table: "server_side_sessions",
                newName: "IX_server_side_sessions_Key");

            migrationBuilder.RenameIndex(
                name: "IX_ServerSideSessions_Expires",
                schema: "dbo",
                table: "server_side_sessions",
                newName: "IX_server_side_sessions_Expires");

            migrationBuilder.RenameIndex(
                name: "IX_ServerSideSessions_DisplayName",
                schema: "dbo",
                table: "server_side_sessions",
                newName: "IX_server_side_sessions_DisplayName");

            migrationBuilder.RenameIndex(
                name: "IX_PushedAuthorizationRequests_ReferenceValueHash",
                schema: "dbo",
                table: "pushed_authorization_requests",
                newName: "IX_pushed_authorization_requests_ReferenceValueHash");

            migrationBuilder.RenameIndex(
                name: "IX_PushedAuthorizationRequests_ExpiresAtUtc",
                schema: "dbo",
                table: "pushed_authorization_requests",
                newName: "IX_pushed_authorization_requests_ExpiresAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_PersistedGrants_SubjectId_SessionId_Type",
                schema: "dbo",
                table: "persisted_grants",
                newName: "IX_persisted_grants_SubjectId_SessionId_Type");

            migrationBuilder.RenameIndex(
                name: "IX_PersistedGrants_SubjectId_ClientId_Type",
                schema: "dbo",
                table: "persisted_grants",
                newName: "IX_persisted_grants_SubjectId_ClientId_Type");

            migrationBuilder.RenameIndex(
                name: "IX_PersistedGrants_Key",
                schema: "dbo",
                table: "persisted_grants",
                newName: "IX_persisted_grants_Key");

            migrationBuilder.RenameIndex(
                name: "IX_PersistedGrants_Expiration",
                schema: "dbo",
                table: "persisted_grants",
                newName: "IX_persisted_grants_Expiration");

            migrationBuilder.RenameIndex(
                name: "IX_PersistedGrants_ConsumedTime",
                schema: "dbo",
                table: "persisted_grants",
                newName: "IX_persisted_grants_ConsumedTime");

            migrationBuilder.RenameIndex(
                name: "IX_DeviceCodes_Expiration",
                schema: "dbo",
                table: "device_codes",
                newName: "IX_device_codes_Expiration");

            migrationBuilder.RenameIndex(
                name: "IX_DeviceCodes_DeviceCode",
                schema: "dbo",
                table: "device_codes",
                newName: "IX_device_codes_DeviceCode");

            migrationBuilder.AddPrimaryKey(
                name: "PK_keys",
                schema: "dbo",
                table: "keys",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_server_side_sessions",
                schema: "dbo",
                table: "server_side_sessions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_pushed_authorization_requests",
                schema: "dbo",
                table: "pushed_authorization_requests",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_persisted_grants",
                schema: "dbo",
                table: "persisted_grants",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_device_codes",
                schema: "dbo",
                table: "device_codes",
                column: "UserCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_keys",
                schema: "dbo",
                table: "keys");

            migrationBuilder.DropPrimaryKey(
                name: "PK_server_side_sessions",
                schema: "dbo",
                table: "server_side_sessions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_pushed_authorization_requests",
                schema: "dbo",
                table: "pushed_authorization_requests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_persisted_grants",
                schema: "dbo",
                table: "persisted_grants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_device_codes",
                schema: "dbo",
                table: "device_codes");

            migrationBuilder.RenameTable(
                name: "keys",
                schema: "dbo",
                newName: "Keys",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "server_side_sessions",
                schema: "dbo",
                newName: "ServerSideSessions",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "pushed_authorization_requests",
                schema: "dbo",
                newName: "PushedAuthorizationRequests",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "persisted_grants",
                schema: "dbo",
                newName: "PersistedGrants",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "device_codes",
                schema: "dbo",
                newName: "DeviceCodes",
                newSchema: "dbo");

            migrationBuilder.RenameIndex(
                name: "IX_keys_Use",
                schema: "dbo",
                table: "Keys",
                newName: "IX_Keys_Use");

            migrationBuilder.RenameIndex(
                name: "IX_server_side_sessions_SubjectId",
                schema: "dbo",
                table: "ServerSideSessions",
                newName: "IX_ServerSideSessions_SubjectId");

            migrationBuilder.RenameIndex(
                name: "IX_server_side_sessions_SessionId",
                schema: "dbo",
                table: "ServerSideSessions",
                newName: "IX_ServerSideSessions_SessionId");

            migrationBuilder.RenameIndex(
                name: "IX_server_side_sessions_Key",
                schema: "dbo",
                table: "ServerSideSessions",
                newName: "IX_ServerSideSessions_Key");

            migrationBuilder.RenameIndex(
                name: "IX_server_side_sessions_Expires",
                schema: "dbo",
                table: "ServerSideSessions",
                newName: "IX_ServerSideSessions_Expires");

            migrationBuilder.RenameIndex(
                name: "IX_server_side_sessions_DisplayName",
                schema: "dbo",
                table: "ServerSideSessions",
                newName: "IX_ServerSideSessions_DisplayName");

            migrationBuilder.RenameIndex(
                name: "IX_pushed_authorization_requests_ReferenceValueHash",
                schema: "dbo",
                table: "PushedAuthorizationRequests",
                newName: "IX_PushedAuthorizationRequests_ReferenceValueHash");

            migrationBuilder.RenameIndex(
                name: "IX_pushed_authorization_requests_ExpiresAtUtc",
                schema: "dbo",
                table: "PushedAuthorizationRequests",
                newName: "IX_PushedAuthorizationRequests_ExpiresAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_persisted_grants_SubjectId_SessionId_Type",
                schema: "dbo",
                table: "PersistedGrants",
                newName: "IX_PersistedGrants_SubjectId_SessionId_Type");

            migrationBuilder.RenameIndex(
                name: "IX_persisted_grants_SubjectId_ClientId_Type",
                schema: "dbo",
                table: "PersistedGrants",
                newName: "IX_PersistedGrants_SubjectId_ClientId_Type");

            migrationBuilder.RenameIndex(
                name: "IX_persisted_grants_Key",
                schema: "dbo",
                table: "PersistedGrants",
                newName: "IX_PersistedGrants_Key");

            migrationBuilder.RenameIndex(
                name: "IX_persisted_grants_Expiration",
                schema: "dbo",
                table: "PersistedGrants",
                newName: "IX_PersistedGrants_Expiration");

            migrationBuilder.RenameIndex(
                name: "IX_persisted_grants_ConsumedTime",
                schema: "dbo",
                table: "PersistedGrants",
                newName: "IX_PersistedGrants_ConsumedTime");

            migrationBuilder.RenameIndex(
                name: "IX_device_codes_Expiration",
                schema: "dbo",
                table: "DeviceCodes",
                newName: "IX_DeviceCodes_Expiration");

            migrationBuilder.RenameIndex(
                name: "IX_device_codes_DeviceCode",
                schema: "dbo",
                table: "DeviceCodes",
                newName: "IX_DeviceCodes_DeviceCode");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Keys",
                schema: "dbo",
                table: "Keys",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ServerSideSessions",
                schema: "dbo",
                table: "ServerSideSessions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PushedAuthorizationRequests",
                schema: "dbo",
                table: "PushedAuthorizationRequests",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PersistedGrants",
                schema: "dbo",
                table: "PersistedGrants",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DeviceCodes",
                schema: "dbo",
                table: "DeviceCodes",
                column: "UserCode");
        }
    }
}

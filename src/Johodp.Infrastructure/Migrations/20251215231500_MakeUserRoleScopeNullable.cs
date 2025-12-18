using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

#nullable disable

namespace Johodp.Infrastructure.Migrations
{
    [DbContext(typeof(Johodp.Infrastructure.Persistence.DbContext.JohodpDbContext))]
    [Migration("20251215231500_MakeUserRoleScopeNullable")]
    public partial class MakeUserRoleScopeNullable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: changes are included in 20251216000000_Initial
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: consolidated into 20251216000000_Initial
        }
    }
}

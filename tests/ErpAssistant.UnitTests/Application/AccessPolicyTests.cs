using ErpAssistant.Application.Abstractions;
using FluentAssertions;

namespace ErpAssistant.UnitTests.Application;

public class AccessPolicyTests
{
    private sealed record User(bool IsAuthenticated, bool IsSystem, IReadOnlyCollection<string> Roles, int? SalesRepresentativeId)
        : ICurrentUser
    {
        public string? UserId => "u";
        public string? UserName => "u";
    }

    private static ICurrentUser With(params string[] roles) => new User(true, false, roles, null);

    private sealed record DriverUser(int? DriverId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public bool IsSystem => false;
        public string? UserId => "d";
        public string? UserName => "d";
        public IReadOnlyCollection<string> Roles => [ErpAssistant.Application.Abstractions.Roles.Driver];
        public int? SalesRepresentativeId => null;
    }

    [Fact]
    public void Driver_sees_no_customer_or_order_rows_but_own_deliveries()
    {
        ICurrentUser driver = new DriverUser(5);

        AccessPolicy.RowFilterFor(driver).Should().Be(AccessPolicy.NoAccess);
        AccessPolicy.DriverFilterFor(driver).Should().Be(5);
        driver.CanSeeFinancials.Should().BeFalse();
    }

    [Fact]
    public void Driver_without_link_sees_no_deliveries()
    {
        AccessPolicy.DriverFilterFor(new DriverUser(null)).Should().Be(AccessPolicy.NoAccess);
    }

    [Fact]
    public void Sales_rep_has_no_driver_scope_and_admin_is_unfiltered()
    {
        AccessPolicy.DriverFilterFor(new User(true, false, [Roles.SalesRep], 4)).Should().Be(AccessPolicy.NoAccess);
        AccessPolicy.DriverFilterFor(With(Roles.Admin)).Should().BeNull();
        AccessPolicy.DriverFilterFor(SystemUser.Instance).Should().BeNull();
        AccessPolicy.DriverFilterFor(new User(false, false, [], null)).Should().Be(AccessPolicy.NoAccess);
    }

    [Fact]
    public void Sales_rep_is_filtered_to_own_rows()
    {
        ICurrentUser rep = new User(true, false, [Roles.SalesRep], 4);
        AccessPolicy.RowFilterFor(rep).Should().Be(4);
    }

    [Fact]
    public void Sales_rep_without_link_sees_nothing()
    {
        AccessPolicy.RowFilterFor(With(Roles.SalesRep)).Should().Be(AccessPolicy.NoAccess);
    }

    [Fact]
    public void Unauthenticated_user_sees_nothing()
    {
        AccessPolicy.RowFilterFor(new User(false, false, [], null)).Should().Be(AccessPolicy.NoAccess);
    }

    [Fact]
    public void User_without_known_role_sees_nothing()
    {
        AccessPolicy.RowFilterFor(With("Misafir")).Should().Be(AccessPolicy.NoAccess);
        AccessPolicy.DepartmentsFor(With("Misafir")).Should().BeEmpty();
    }

    [Fact]
    public void Admin_and_system_are_unfiltered()
    {
        AccessPolicy.RowFilterFor(With(Roles.Admin)).Should().BeNull();
        AccessPolicy.RowFilterFor(SystemUser.Instance).Should().BeNull();
        AccessPolicy.DepartmentsFor(With(Roles.Admin)).Should().BeNull();
    }

    [Fact]
    public void Warehouse_cannot_see_financials_and_reads_warehouse_and_quality_docs()
    {
        var user = With(Roles.Warehouse);
        user.CanSeeFinancials.Should().BeFalse();
        user.DepartmentCodes.Should().BeEquivalentTo(["DEPO", "KALITE"]);
    }
}

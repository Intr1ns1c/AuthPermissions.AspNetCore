﻿// Copyright (c) 2021 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT license. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using AuthPermissions;
using AuthPermissions.AdminCode;
using AuthPermissions.AdminCode.Services;
using AuthPermissions.SetupCode;
using Example6.SingleLevelSharding.EfCoreCode;
using Microsoft.EntityFrameworkCore;
using Test.TestHelpers;
using Xunit;
using Xunit.Abstractions;
using Xunit.Extensions.AssertExtensions;

namespace Test.UnitTests.TestAuthPermissionsAdmin
{
    public class TestTenantChangeServiceShardingSingle
    {
        private readonly ITestOutputHelper _output;

        public TestTenantChangeServiceShardingSingle(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task TestAddSingleTenantAsyncToMainDatabaseOk()
        {
            //SETUP
            using var contexts = new ShardingSingleLevelTenantChangeSqlServerSetup(this);
            await contexts.AuthPContext.SetupSingleShardingTenantsInDb(contexts.MainContext);
            contexts.AuthPContext.ChangeTracker.Clear();

            var changeServiceFactory = new StubChangeChangeServiceFactory(contexts.MainContext, this);
            var service = new AuthTenantAdminService(contexts.AuthPContext,
                new AuthPermissionsOptions { TenantType = TenantTypes.SingleLevel | TenantTypes.AddSharding },
                changeServiceFactory, null);

            //ATTEMPT
            var status = await service.AddSingleTenantAsync("Tenant4", null, false);

            //VERIFY
            status.IsValid.ShouldBeTrue(status.GetAllErrors());
            contexts.MainContext.ChangeTracker.Clear();
            var companies = contexts.MainContext.Companies.IgnoreQueryFilters().ToList();
            companies.Count.ShouldEqual(4);
            companies.Last().CompanyName.ShouldEqual("Tenant4");
        }

        [Fact]
        public async Task TestAddSingleTenantAsyncToOtherDatabaseHasOwnDbOk()
        {
            //SETUP
            using var contexts = new ShardingSingleLevelTenantChangeSqlServerSetup(this);
            await contexts.AuthPContext.SetupSingleShardingTenantsInDb(contexts.MainContext);
            contexts.AuthPContext.ChangeTracker.Clear();

            var changeServiceFactory = new StubChangeChangeServiceFactory(contexts.MainContext, this);
            var service = new AuthTenantAdminService(contexts.AuthPContext,
                new AuthPermissionsOptions { TenantType = TenantTypes.SingleLevel | TenantTypes.AddSharding },
                changeServiceFactory, null);

            //ATTEMPT
            var status = await service.AddSingleTenantAsync("Tenant4", null, true, "OtherConnection");

            //VERIFY
            status.IsValid.ShouldBeTrue(status.GetAllErrors());
            contexts.MainContext.ChangeTracker.Clear();
            var mainCompanies = contexts.MainContext.Companies.IgnoreQueryFilters().ToList();
            mainCompanies.Count.ShouldEqual(3);
            contexts.OtherContext.DataKey.ShouldEqual(MultiTenantExtensions.DataKeyNoQueryFilter);
            var otherCompanies = contexts.OtherContext.Companies.ToList();
            otherCompanies.Single().CompanyName.ShouldEqual("Tenant4");
        }

        [Fact]
        public async Task TestUpdateNameSingleTenantAsyncOk()
        {
            //SETUP
            using var contexts = new ShardingSingleLevelTenantChangeSqlServerSetup(this);
            var tenantIds = await contexts.AuthPContext.SetupSingleShardingTenantsInDb(contexts.MainContext);
            contexts.AuthPContext.ChangeTracker.Clear();

            var changeServiceFactory = new StubChangeChangeServiceFactory(contexts.MainContext, this);
            var service = new AuthTenantAdminService(contexts.AuthPContext,
                new AuthPermissionsOptions { TenantType = TenantTypes.SingleLevel | TenantTypes.AddSharding },
                changeServiceFactory, null);

            //ATTEMPT
            var status = await service.UpdateTenantNameAsync(tenantIds[1], "New Tenant");

            //VERIFY
            status.IsValid.ShouldBeTrue(status.GetAllErrors());
            contexts.MainContext.ChangeTracker.Clear();
            var companies = contexts.MainContext.Companies.IgnoreQueryFilters().ToList();
            companies.Select(x => x.CompanyName).ShouldEqual(new[] { "Tenant1", "New Tenant", "Tenant3" });
        }

        [Fact]
        public async Task TestDeleteSingleTenantAsync()
        {
            //SETUP
            using var contexts = new ShardingSingleLevelTenantChangeSqlServerSetup(this);
            var tenantIds = await contexts.AuthPContext.SetupSingleShardingTenantsInDb(contexts.MainContext);
            contexts.AuthPContext.ChangeTracker.Clear();

            var changeServiceFactory = new StubChangeChangeServiceFactory(contexts.MainContext, this);
            var service = new AuthTenantAdminService(contexts.AuthPContext,
                new AuthPermissionsOptions { TenantType = TenantTypes.SingleLevel | TenantTypes.AddSharding },
                changeServiceFactory, null);

            //ATTEMPT
            var status = await service.DeleteTenantAsync(tenantIds[1]);

            //VERIFY
            status.IsValid.ShouldBeTrue(status.GetAllErrors());
            var companies = contexts.MainContext.Companies.IgnoreQueryFilters().ToList();
            companies.Select(x => x.CompanyName).ShouldEqual(new[] { "Tenant1", "Tenant3" });
        }

        [Fact]
        public async Task TestDeleteSingleTenantAsyncCheckReturn()
        {
            //SETUP
            using var contexts = new ShardingSingleLevelTenantChangeSqlServerSetup(this);
            var tenantIds = await contexts.AuthPContext.SetupSingleShardingTenantsInDb(contexts.MainContext);
            contexts.AuthPContext.ChangeTracker.Clear();

            var changeServiceFactory = new StubChangeChangeServiceFactory(contexts.MainContext, this);
            var service = new AuthTenantAdminService(contexts.AuthPContext,
                new AuthPermissionsOptions { TenantType = TenantTypes.SingleLevel | TenantTypes.AddSharding },
                changeServiceFactory, null);

            //ATTEMPT
            var status = await service.DeleteTenantAsync(tenantIds[1]);

            //VERIFY
            status.IsValid.ShouldBeTrue(status.GetAllErrors());
            var deletedId = ((ShardingTenantChangeService)status.Result).DeletedTenantId;
            deletedId.ShouldEqual(tenantIds[1]);
        }

        [Fact]
        public async Task TestMoveToDifferentDatabaseAsync()
        {
            //SETUP
            using var contexts = new ShardingSingleLevelTenantChangeSqlServerSetup(this);
            var tenantIds = await contexts.AuthPContext.SetupSingleShardingTenantsInDb(contexts.MainContext);
            contexts.AuthPContext.ChangeTracker.Clear();

            var changeServiceFactory = new StubChangeChangeServiceFactory(contexts.MainContext, this);
            var service = new AuthTenantAdminService(contexts.AuthPContext,
                new AuthPermissionsOptions { TenantType = TenantTypes.SingleLevel | TenantTypes.AddSharding },
                changeServiceFactory, null);

            //ATTEMPT
            var status = await service.MoveToDifferentDatabaseAsync(tenantIds[1], true, "OtherConnection");

            //VERIFY
            status.IsValid.ShouldBeTrue(status.GetAllErrors());
            status.IsValid.ShouldBeTrue(status.GetAllErrors());
            contexts.MainContext.ChangeTracker.Clear();
            var mainCompanies = contexts.MainContext.Companies.IgnoreQueryFilters().ToList();
            mainCompanies.Count.ShouldEqual(2);
            contexts.OtherContext.DataKey.ShouldEqual(MultiTenantExtensions.DataKeyNoQueryFilter);
            var query = contexts.OtherContext.Companies;
            _output.WriteLine(query.ToQueryString());
            var otherCompanies = query.ToList();
            otherCompanies.Single().CompanyName.ShouldEqual("Tenant2");
        }

    }
}
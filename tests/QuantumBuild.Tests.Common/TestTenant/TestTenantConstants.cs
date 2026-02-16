namespace QuantumBuild.Tests.Common.TestTenant;

/// <summary>
/// Constants for the automated test tenant. All GUIDs are deterministic for reliable assertions.
/// These are separate from the default QUANTUMBUILD tenant to allow testing in isolation.
/// </summary>
public static class TestTenantConstants
{
    // ==================== TENANT ====================
    public static readonly Guid TenantId = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");
    public const string TenantName = "Automated Test Tenant";
    public const string TenantCode = "TEST";

    // ==================== USERS ====================
    public static class Users
    {
        public static class Admin
        {
            public static readonly Guid Id = Guid.Parse("AAAAAAAA-0001-0001-0001-000000000001");
            public const string Email = "admin@test.quantumbuild.ie";
            public const string Password = "TestAdmin123!";
            public const string FirstName = "Test";
            public const string LastName = "Admin";
        }

        public static class SiteManager
        {
            public static readonly Guid Id = Guid.Parse("AAAAAAAA-0001-0001-0001-000000000002");
            public const string Email = "manager@test.quantumbuild.ie";
            public const string Password = "TestManager123!";
            public const string FirstName = "Test";
            public const string LastName = "Manager";
        }

        public static class Warehouse
        {
            public static readonly Guid Id = Guid.Parse("AAAAAAAA-0001-0001-0001-000000000003");
            public const string Email = "warehouse@test.quantumbuild.ie";
            public const string Password = "TestWarehouse123!";
            public const string FirstName = "Test";
            public const string LastName = "Warehouse";
        }

        public static class Operator
        {
            public static readonly Guid Id = Guid.Parse("AAAAAAAA-0001-0001-0001-000000000004");
            public const string Email = "operator@test.quantumbuild.ie";
            public const string Password = "TestOperator123!";
            public const string FirstName = "Test";
            public const string LastName = "Operator";
        }

        public static class Finance
        {
            public static readonly Guid Id = Guid.Parse("AAAAAAAA-0001-0001-0001-000000000005");
            public const string Email = "finance@test.quantumbuild.ie";
            public const string Password = "TestFinance123!";
            public const string FirstName = "Test";
            public const string LastName = "Finance";
        }
    }

    // ==================== SITES ====================
    public static class Sites
    {
        public static readonly Guid MainSite = Guid.Parse("AAAAAAAA-0002-0001-0001-000000000001");
        public const string MainSiteCode = "TEST001";
        public const string MainSiteName = "Test Main Site";

        public static readonly Guid SecondarySite = Guid.Parse("AAAAAAAA-0002-0001-0001-000000000002");
        public const string SecondarySiteCode = "TEST002";
        public const string SecondarySiteName = "Test Secondary Site";

        public static readonly Guid InactiveSite = Guid.Parse("AAAAAAAA-0002-0001-0001-000000000003");
        public const string InactiveSiteCode = "TEST003";
        public const string InactiveSiteName = "Test Inactive Site";

        // GPS coordinates for geofencing tests
        public const decimal MainSiteLatitude = 53.3498m;
        public const decimal MainSiteLongitude = -6.2603m;
        public const decimal SecondarySiteLatitude = 51.8985m;
        public const decimal SecondarySiteLongitude = -8.4756m;
    }

    // ==================== EMPLOYEES ====================
    public static class Employees
    {
        public static readonly Guid Employee1 = Guid.Parse("AAAAAAAA-0003-0001-0001-000000000001");
        public const string Employee1Code = "EMP001";
        public const string Employee1FirstName = "John";
        public const string Employee1LastName = "Test";

        public static readonly Guid Employee2 = Guid.Parse("AAAAAAAA-0003-0001-0001-000000000002");
        public const string Employee2Code = "EMP002";
        public const string Employee2FirstName = "Jane";
        public const string Employee2LastName = "Test";

        public static readonly Guid Employee3 = Guid.Parse("AAAAAAAA-0003-0001-0001-000000000003");
        public const string Employee3Code = "EMP003";
        public const string Employee3FirstName = "Bob";
        public const string Employee3LastName = "Test";

        public static readonly Guid ManagerEmployee = Guid.Parse("AAAAAAAA-0003-0001-0001-000000000004");
        public const string ManagerEmployeeCode = "EMP004";
        public const string ManagerEmployeeFirstName = "Manager";
        public const string ManagerEmployeeLastName = "Test";

        public static readonly Guid OperatorEmployee = Guid.Parse("AAAAAAAA-0003-0001-0001-000000000005");
        public const string OperatorEmployeeCode = "EMP005";
        public const string OperatorEmployeeFirstName = "Operator";
        public const string OperatorEmployeeLastName = "Test";

        public static readonly Guid InactiveEmployee = Guid.Parse("AAAAAAAA-0003-0001-0001-000000000006");
        public const string InactiveEmployeeCode = "EMP006";
        public const string InactiveEmployeeFirstName = "Inactive";
        public const string InactiveEmployeeLastName = "Test";
    }

    // ==================== COMPANIES ====================
    public static class Companies
    {
        public static readonly Guid CustomerCompany1 = Guid.Parse("AAAAAAAA-000D-0001-0001-000000000001");
        public const string CustomerCompany1Name = "Test Customer Ltd";

        public static readonly Guid CustomerCompany2 = Guid.Parse("AAAAAAAA-000D-0001-0001-000000000002");
        public const string CustomerCompany2Name = "Test Construction Inc";

        public static readonly Guid InactiveCompany = Guid.Parse("AAAAAAAA-000D-0001-0001-000000000003");
        public const string InactiveCompanyName = "Inactive Company Ltd";
    }

    // ==================== CONTACTS ====================
    public static class Contacts
    {
        public static readonly Guid Contact1 = Guid.Parse("AAAAAAAA-000E-0001-0001-000000000001");
        public const string Contact1FirstName = "Contact";
        public const string Contact1LastName = "One";
        public const string Contact1Email = "contact1@test.com";

        public static readonly Guid Contact2 = Guid.Parse("AAAAAAAA-000E-0001-0001-000000000002");
        public const string Contact2FirstName = "Contact";
        public const string Contact2LastName = "Two";
        public const string Contact2Email = "contact2@test.com";
    }

    // ==================== STOCK MANAGEMENT ====================
    public static class StockManagement
    {
        public static class Categories
        {
            public static readonly Guid Safety = Guid.Parse("AAAAAAAA-0004-0001-0001-000000000001");
            public const string SafetyName = "Safety Equipment";

            public static readonly Guid Tools = Guid.Parse("AAAAAAAA-0004-0001-0001-000000000002");
            public const string ToolsName = "Tools";

            public static readonly Guid Materials = Guid.Parse("AAAAAAAA-0004-0001-0001-000000000003");
            public const string MaterialsName = "Building Materials";

            public static readonly Guid Inactive = Guid.Parse("AAAAAAAA-0004-0001-0001-000000000004");
            public const string InactiveName = "Inactive Category";
        }

        public static class Suppliers
        {
            public static readonly Guid Supplier1 = Guid.Parse("AAAAAAAA-0005-0001-0001-000000000001");
            public const string Supplier1Name = "Test Supplier One";
            public const string Supplier1Code = "SUP001";

            public static readonly Guid Supplier2 = Guid.Parse("AAAAAAAA-0005-0001-0001-000000000002");
            public const string Supplier2Name = "Test Supplier Two";
            public const string Supplier2Code = "SUP002";

            public static readonly Guid InactiveSupplier = Guid.Parse("AAAAAAAA-0005-0001-0001-000000000003");
            public const string InactiveSupplierName = "Inactive Supplier";
            public const string InactiveSupplierCode = "SUP003";
        }

        public static class Products
        {
            public static readonly Guid HardHat = Guid.Parse("AAAAAAAA-0006-0001-0001-000000000001");
            public const string HardHatSku = "PPE-HAT-001";
            public const string HardHatName = "Hard Hat";
            public const decimal HardHatCostPrice = 15.00m;
            public const decimal HardHatSellPrice = 25.00m;

            public static readonly Guid SafetyVest = Guid.Parse("AAAAAAAA-0006-0001-0001-000000000002");
            public const string SafetyVestSku = "PPE-VEST-001";
            public const string SafetyVestName = "High-Vis Safety Vest";
            public const decimal SafetyVestCostPrice = 12.00m;
            public const decimal SafetyVestSellPrice = 20.00m;

            public static readonly Guid Gloves = Guid.Parse("AAAAAAAA-0006-0001-0001-000000000003");
            public const string GlovesSku = "PPE-GLV-001";
            public const string GlovesName = "Safety Gloves";
            public const decimal GlovesCostPrice = 8.00m;
            public const decimal GlovesSellPrice = 15.00m;

            public static readonly Guid PowerDrill = Guid.Parse("AAAAAAAA-0006-0001-0001-000000000004");
            public const string PowerDrillSku = "TOOL-DRL-001";
            public const string PowerDrillName = "Power Drill";
            public const decimal PowerDrillCostPrice = 85.00m;
            public const decimal PowerDrillSellPrice = 120.00m;

            public static readonly Guid ScrewSet = Guid.Parse("AAAAAAAA-0006-0001-0001-000000000005");
            public const string ScrewSetSku = "TOOL-SCR-001";
            public const string ScrewSetName = "Screwdriver Set";
            public const decimal ScrewSetCostPrice = 25.00m;
            public const decimal ScrewSetSellPrice = 40.00m;

            public static readonly Guid Cement = Guid.Parse("AAAAAAAA-0006-0001-0001-000000000006");
            public const string CementSku = "MAT-CEM-001";
            public const string CementName = "Cement Bag (25kg)";
            public const decimal CementCostPrice = 6.00m;
            public const decimal CementSellPrice = 10.00m;

            public static readonly Guid Lumber = Guid.Parse("AAAAAAAA-0006-0001-0001-000000000007");
            public const string LumberSku = "MAT-LUM-001";
            public const string LumberName = "Timber 2x4";
            public const decimal LumberCostPrice = 4.50m;
            public const decimal LumberSellPrice = 8.00m;

            public static readonly Guid Nails = Guid.Parse("AAAAAAAA-0006-0001-0001-000000000008");
            public const string NailsSku = "MAT-NAL-001";
            public const string NailsName = "Nails (Box)";
            public const decimal NailsCostPrice = 3.00m;
            public const decimal NailsSellPrice = 5.50m;

            public static readonly Guid Paint = Guid.Parse("AAAAAAAA-0006-0001-0001-000000000009");
            public const string PaintSku = "MAT-PNT-001";
            public const string PaintName = "Paint (5L)";
            public const decimal PaintCostPrice = 22.00m;
            public const decimal PaintSellPrice = 35.00m;

            public static readonly Guid Brushes = Guid.Parse("AAAAAAAA-0006-0001-0001-000000000010");
            public const string BrushesSku = "TOOL-BRS-001";
            public const string BrushesName = "Paint Brushes (Set)";
            public const decimal BrushesCostPrice = 10.00m;
            public const decimal BrushesSellPrice = 18.00m;

            public static readonly Guid InactiveProduct = Guid.Parse("AAAAAAAA-0006-0001-0001-000000000011");
            public const string InactiveProductSku = "INACTIVE-001";
            public const string InactiveProductName = "Discontinued Product";
        }

        public static class Locations
        {
            public static readonly Guid MainWarehouse = Guid.Parse("AAAAAAAA-0007-0001-0001-000000000001");
            public const string MainWarehouseName = "Test Main Warehouse";
            public const string MainWarehouseCode = "WH-MAIN";

            public static readonly Guid SiteStorage = Guid.Parse("AAAAAAAA-0007-0001-0001-000000000002");
            public const string SiteStorageName = "Test Site Storage";
            public const string SiteStorageCode = "WH-SITE";

            public static readonly Guid Van1 = Guid.Parse("AAAAAAAA-0007-0001-0001-000000000003");
            public const string Van1Name = "Test Van 1";
            public const string Van1Code = "VAN-001";
        }

        public static class BayLocations
        {
            public static readonly Guid BayA1 = Guid.Parse("AAAAAAAA-0007-0002-0001-000000000001");
            public const string BayA1Code = "A-1";

            public static readonly Guid BayA2 = Guid.Parse("AAAAAAAA-0007-0002-0001-000000000002");
            public const string BayA2Code = "A-2";

            public static readonly Guid BayB1 = Guid.Parse("AAAAAAAA-0007-0002-0001-000000000003");
            public const string BayB1Code = "B-1";
        }

        public static class StockOrders
        {
            public static readonly Guid DraftOrder = Guid.Parse("AAAAAAAA-0008-0001-0001-000000000001");
            public const string DraftOrderReference = "SO-TEST-001";

            public static readonly Guid SubmittedOrder = Guid.Parse("AAAAAAAA-0008-0001-0001-000000000002");
            public const string SubmittedOrderReference = "SO-TEST-002";

            public static readonly Guid ApprovedOrder = Guid.Parse("AAAAAAAA-0008-0001-0001-000000000003");
            public const string ApprovedOrderReference = "SO-TEST-003";

            public static readonly Guid CompletedOrder = Guid.Parse("AAAAAAAA-0008-0001-0001-000000000004");
            public const string CompletedOrderReference = "SO-TEST-004";

            public static readonly Guid RejectedOrder = Guid.Parse("AAAAAAAA-0008-0001-0001-000000000005");
            public const string RejectedOrderReference = "SO-TEST-005";
        }

        public static class PurchaseOrders
        {
            public static readonly Guid DraftPO = Guid.Parse("AAAAAAAA-0009-0001-0001-000000000001");
            public const string DraftPOReference = "PO-TEST-001";

            public static readonly Guid ConfirmedPO = Guid.Parse("AAAAAAAA-0009-0001-0001-000000000002");
            public const string ConfirmedPOReference = "PO-TEST-002";

            public static readonly Guid ReceivedPO = Guid.Parse("AAAAAAAA-0009-0001-0001-000000000003");
            public const string ReceivedPOReference = "PO-TEST-003";
        }

        public static class Stocktakes
        {
            public static readonly Guid DraftStocktake = Guid.Parse("AAAAAAAA-000A-0001-0001-000000000001");
            public const string DraftStocktakeReference = "ST-TEST-001";

            public static readonly Guid InProgressStocktake = Guid.Parse("AAAAAAAA-000A-0001-0001-000000000002");
            public const string InProgressStocktakeReference = "ST-TEST-002";

            public static readonly Guid CompletedStocktake = Guid.Parse("AAAAAAAA-000A-0001-0001-000000000003");
            public const string CompletedStocktakeReference = "ST-TEST-003";
        }
    }

    // ==================== PROPOSALS ====================
    public static class Proposals
    {
        public static class ProductKits
        {
            public static readonly Guid SafetyKit = Guid.Parse("AAAAAAAA-000B-0001-0001-000000000001");
            public const string SafetyKitName = "Test Safety Kit";

            public static readonly Guid ToolKit = Guid.Parse("AAAAAAAA-000B-0001-0001-000000000002");
            public const string ToolKitName = "Test Tool Kit";

            public static readonly Guid InactiveKit = Guid.Parse("AAAAAAAA-000B-0001-0001-000000000003");
            public const string InactiveKitName = "Inactive Kit";
        }

        public static class ProposalRecords
        {
            public static readonly Guid DraftProposal = Guid.Parse("AAAAAAAA-000C-0001-0001-000000000001");
            public const string DraftProposalReference = "PRO-TEST-001";

            public static readonly Guid SubmittedProposal = Guid.Parse("AAAAAAAA-000C-0001-0001-000000000002");
            public const string SubmittedProposalReference = "PRO-TEST-002";

            public static readonly Guid ApprovedProposal = Guid.Parse("AAAAAAAA-000C-0001-0001-000000000003");
            public const string ApprovedProposalReference = "PRO-TEST-003";

            public static readonly Guid WonProposal = Guid.Parse("AAAAAAAA-000C-0001-0001-000000000004");
            public const string WonProposalReference = "PRO-TEST-004";

            public static readonly Guid LostProposal = Guid.Parse("AAAAAAAA-000C-0001-0001-000000000005");
            public const string LostProposalReference = "PRO-TEST-005";

            public static readonly Guid ProposalWithRevisions = Guid.Parse("AAAAAAAA-000C-0001-0001-000000000006");
            public const string ProposalWithRevisionsReference = "PRO-TEST-006";
        }
    }

    // ==================== SITE ATTENDANCE ====================
    public static class SiteAttendance
    {
        public static readonly Guid Settings = Guid.Parse("AAAAAAAA-000F-0001-0001-000000000001");

        public static class Events
        {
            public static readonly Guid TodayCheckIn = Guid.Parse("AAAAAAAA-0010-0001-0001-000000000001");
            public static readonly Guid TodayCheckOut = Guid.Parse("AAAAAAAA-0010-0001-0001-000000000002");
            public static readonly Guid YesterdayCheckIn = Guid.Parse("AAAAAAAA-0010-0001-0001-000000000003");
            public static readonly Guid YesterdayCheckOut = Guid.Parse("AAAAAAAA-0010-0001-0001-000000000004");
        }

        public static class Summaries
        {
            public static readonly Guid YesterdaySummary = Guid.Parse("AAAAAAAA-0011-0001-0001-000000000001");
            public static readonly Guid LastWeekSummary = Guid.Parse("AAAAAAAA-0011-0001-0001-000000000002");
        }

        public static class BankHolidays
        {
            public static readonly Guid Christmas = Guid.Parse("AAAAAAAA-0012-0001-0001-000000000001");
            public static readonly Guid NewYear = Guid.Parse("AAAAAAAA-0012-0001-0001-000000000002");
            public static readonly Guid StPatricks = Guid.Parse("AAAAAAAA-0012-0001-0001-000000000003");
        }

        public static class Devices
        {
            public static readonly Guid Device1 = Guid.Parse("AAAAAAAA-0013-0001-0001-000000000001");
            public const string Device1Identifier = "test-device-001";

            public static readonly Guid Device2 = Guid.Parse("AAAAAAAA-0013-0001-0001-000000000002");
            public const string Device2Identifier = "test-device-002";
        }
    }

    // ==================== TOOLBOX TALKS ====================
    public static class ToolboxTalks
    {
        public static readonly Guid Settings = Guid.Parse("AAAAAAAA-0014-0001-0001-000000000001");

        public static class Talks
        {
            public static readonly Guid BasicTalk = Guid.Parse("AAAAAAAA-0015-0001-0001-000000000001");
            public const string BasicTalkTitle = "Test Basic Talk";

            public static readonly Guid TalkWithQuiz = Guid.Parse("AAAAAAAA-0015-0001-0001-000000000002");
            public const string TalkWithQuizTitle = "Test Talk with Quiz";

            public static readonly Guid TalkWithVideo = Guid.Parse("AAAAAAAA-0015-0001-0001-000000000003");
            public const string TalkWithVideoTitle = "Test Talk with Video";

            public static readonly Guid MonthlyTalk = Guid.Parse("AAAAAAAA-0015-0001-0001-000000000004");
            public const string MonthlyTalkTitle = "Test Monthly Talk";

            public static readonly Guid InactiveTalk = Guid.Parse("AAAAAAAA-0015-0001-0001-000000000005");
            public const string InactiveTalkTitle = "Inactive Talk";
        }

        public static class Schedules
        {
            public static readonly Guid CompletedSchedule = Guid.Parse("AAAAAAAA-0016-0001-0001-000000000001");
            public static readonly Guid ActiveSchedule = Guid.Parse("AAAAAAAA-0016-0001-0001-000000000002");
            public static readonly Guid FutureSchedule = Guid.Parse("AAAAAAAA-0016-0001-0001-000000000003");
            public static readonly Guid CancelledSchedule = Guid.Parse("AAAAAAAA-0016-0001-0001-000000000004");
        }

        public static class ScheduledTalks
        {
            public static readonly Guid PendingTalk = Guid.Parse("AAAAAAAA-0017-0001-0001-000000000001");
            public static readonly Guid InProgressTalk = Guid.Parse("AAAAAAAA-0017-0001-0001-000000000002");
            public static readonly Guid CompletedTalk = Guid.Parse("AAAAAAAA-0017-0001-0001-000000000003");
            public static readonly Guid OverdueTalk = Guid.Parse("AAAAAAAA-0017-0001-0001-000000000004");
            public static readonly Guid CancelledTalk = Guid.Parse("AAAAAAAA-0017-0001-0001-000000000005");
        }
    }
}

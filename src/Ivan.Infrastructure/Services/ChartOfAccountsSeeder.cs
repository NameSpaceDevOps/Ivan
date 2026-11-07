using Ivan.Application.Common.Interfaces;
using Ivan.Application.Services;
using Ivan.Domain.Entities;
using Ivan.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ivan.Infrastructure.Services;

public class ChartOfAccountsSeeder : IChartOfAccountsSeeder
{
    private readonly IIvanDbContext _db;

    public ChartOfAccountsSeeder(IIvanDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync(Guid householdId, ChartPreset preset = ChartPreset.Full, string currency = "IRR", CancellationToken ct = default)
    {
        var existingCount = await _db.Accounts.CountAsync(ct);
        if (existingCount > 0)
            return; // Already seeded

        var accounts = new List<Account>();

        // Always create mandatory system accounts
        var openingBalances = new Account
        {
            HouseholdId = householdId,
            Code = "3000",
            Name = "Opening Balances",
            PersianName = "تراز افتتاحی",
            Type = AccountType.Equity,
            SubType = AccountSubType.System,
            Currency = currency,
            IsSystem = true,
            IsGroup = false
        };
        accounts.Add(openingBalances);

        var retainedSurplus = new Account
        {
            HouseholdId = householdId,
            Code = "3100",
            Name = "Retained Surplus",
            PersianName = "مازاد انباشته",
            Type = AccountType.Equity,
            SubType = AccountSubType.System,
            Currency = currency,
            IsSystem = true,
            IsGroup = false
        };
        accounts.Add(retainedSurplus);

        var uncategorized = new Account
        {
            HouseholdId = householdId,
            Code = "6990",
            Name = "Uncategorized",
            PersianName = "طبقه‌بندی‌نشده",
            Type = AccountType.Expense,
            SubType = AccountSubType.System,
            Currency = currency,
            IsSystem = true,
            IsGroup = false
        };
        accounts.Add(uncategorized);

        if (preset == ChartPreset.Empty)
        {
            _db.Accounts.AddRange(accounts);
            await _db.SaveChangesAsync(ct);
            return;
        }

        // Helper to add group and leaf accounts
        Account AddGroup(string code, string name, string faName, AccountType type, AccountSubType subType)
        {
            var grp = new Account
            {
                HouseholdId = householdId,
                Code = code,
                Name = name,
                PersianName = faName,
                Type = type,
                SubType = subType,
                Currency = currency,
                IsGroup = true
            };
            accounts.Add(grp);
            return grp;
        }

        void AddLeaf(Account group, string code, string name, string faName)
        {
            accounts.Add(new Account
            {
                HouseholdId = householdId,
                Code = code,
                Name = name,
                PersianName = faName,
                Type = group.Type,
                SubType = group.SubType,
                Currency = currency,
                ParentAccountId = group.Id,
                IsGroup = false
            });
        }

        // 1. ASSETS
        var gCash = AddGroup("1000", "Cash", "نقد", AccountType.Asset, AccountSubType.Cash);
        AddLeaf(gCash, "1001", "Cash on Hand", "پول نقد");
        AddLeaf(gCash, "1002", "Petty Cash", "تنخواه گردان");

        var gBank = AddGroup("1010", "Bank Accounts", "حساب‌های بانکی", AccountType.Asset, AccountSubType.Bank);
        AddLeaf(gBank, "1011", "Checking Account", "حساب جاری");
        AddLeaf(gBank, "1012", "Savings Account", "حساب پس‌انداز");

        var gCard = AddGroup("1020", "Cards & Wallets", "کارت‌ها و کیف پول‌ها", AccountType.Asset, AccountSubType.Card);
        AddLeaf(gCard, "1021", "Debit/Gift Card", "کارت نقدی و هدیه");
        AddLeaf(gCard, "1022", "Digital Wallet", "کیف پول دیجیتال");

        var gKid = AddGroup("1030", "Kid Wallets", "کیف پول فرزندان", AccountType.Asset, AccountSubType.Wallet);
        AddLeaf(gKid, "1031", "Default Kid Wallet", "کیف پول فرزند");

        var gRec = AddGroup("1100", "Receivables", "مطالبات و چک‌های دریافتی", AccountType.Asset, AccountSubType.Receivable);
        AddLeaf(gRec, "1101", "Loans Given", "وام‌های پرداخت‌شده به دیگران");
        AddLeaf(gRec, "1102", "Pending Cheques Received", "چک‌های دریافتی در جریان");

        if (preset == ChartPreset.Full)
        {
            var gInv = AddGroup("1200", "Savings & Investments", "پس‌انداز و سرمایه‌گذاری", AccountType.Asset, AccountSubType.Category);
            AddLeaf(gInv, "1201", "Term Deposit", "سپرده‌های بانکی");
            AddLeaf(gInv, "1202", "Gold & Coins", "طلا و سکه");

            var gProp = AddGroup("1300", "Valuables & Property", "دارایی‌ها و اموال", AccountType.Asset, AccountSubType.Category);
            AddLeaf(gProp, "1301", "Real Estate", "املاک و مستقلات");
            AddLeaf(gProp, "1302", "Vehicles", "خودرو");
        }

        // 2. LIABILITIES
        var gLoan = AddGroup("2000", "Loans & Debts", "وام‌ها و بدهی‌ها", AccountType.Liability, AccountSubType.Loan);
        AddLeaf(gLoan, "2001", "Bank Loan", "وام بانکی");
        AddLeaf(gLoan, "2002", "Personal Loan", "قرض از اشخاص");

        var gCheqOut = AddGroup("2100", "Cheques Issued", "چک‌های پرداختنی", AccountType.Liability, AccountSubType.Payable);
        AddLeaf(gCheqOut, "2101", "Pending Cheques Issued", "چک‌های صادرشده در جریان");

        var gPay = AddGroup("2200", "Payables", "بدهی‌های جاری", AccountType.Liability, AccountSubType.Payable);
        AddLeaf(gPay, "2201", "Bills Due", "قبوض پرداخت‌نشده");

        // 3. INCOME
        var gSal = AddGroup("4000", "Salary & Wages", "حقوق و دستمزد", AccountType.Income, AccountSubType.Category);
        AddLeaf(gSal, "4001", "Base Salary", "حقوق پایه");
        AddLeaf(gSal, "4002", "Bonus & Eidi", "پاداش و عیدی");

        var gBiz = AddGroup("4100", "Business & Freelance", "کسب‌وکار و کار آزاد", AccountType.Income, AccountSubType.Category);
        AddLeaf(gBiz, "4101", "Business Profit", "سود کسب‌وکار");
        AddLeaf(gBiz, "4102", "Freelance", "کار پروژه‌ای");

        var gOtherInc = AddGroup("4900", "Other Income", "سایر درآمدها", AccountType.Income, AccountSubType.Category);
        AddLeaf(gOtherInc, "4901", "Gifts & Misc Income", "هدیه و متفرقه");

        // 4. EXPENSES
        var gHouse = AddGroup("5000", "Housing", "مسکن", AccountType.Expense, AccountSubType.Category);
        AddLeaf(gHouse, "5001", "Rent / Mortgage", "اجاره یا قسط مسکن");
        AddLeaf(gHouse, "5002", "Building Maintenance Fee", "شارژ ساختمان");

        var gUtil = AddGroup("5100", "Utilities", "قبوض و انشعابات", AccountType.Expense, AccountSubType.Category);
        AddLeaf(gUtil, "5101", "Electricity Bill", "قبض برق");
        AddLeaf(gUtil, "5102", "Water Bill", "قبض آب");
        AddLeaf(gUtil, "5103", "Gas Bill", "قبض گاز");
        AddLeaf(gUtil, "5104", "Internet & Mobile", "اینترنت و تلفن");

        var gFood = AddGroup("5200", "Food & Groceries", "خوراک و خواربار", AccountType.Expense, AccountSubType.Category);
        AddLeaf(gFood, "5201", "Supermarket & Groceries", "خواربار و سوپرمارکت");
        AddLeaf(gFood, "5202", "Dining Out & Fast Food", "رستوران و سفارش غذا");

        var gTrans = AddGroup("5300", "Transportation", "حمل‌ونقل", AccountType.Expense, AccountSubType.Category);
        AddLeaf(gTrans, "5301", "Fuel & Gas", "بنزین و سوخت");
        AddLeaf(gTrans, "5302", "Vehicle Repair & Service", "سرویس و تعمیر خودرو");
        AddLeaf(gTrans, "5303", "Taxi & Public Transit", "تاکسی و مترو");

        var gHealth = AddGroup("5400", "Health & Medical", "سلامت و درمان", AccountType.Expense, AccountSubType.Category);
        AddLeaf(gHealth, "5401", "Doctor & Clinic", "ویزیت و درمانگاه");
        AddLeaf(gHealth, "5402", "Pharmacy & Medicine", "دارو و مکمل");

        var gKids = AddGroup("5500", "Children & Education", "فرزندان و آموزش", AccountType.Expense, AccountSubType.Category);
        AddLeaf(gKids, "5501", "Tuition & School", "شهریه مدرسه و کلاس");
        AddLeaf(gKids, "5502", "Books & Stationery", "کتاب و نوشت‌افزار");
        AddLeaf(gKids, "5503", "Kids Allowance", "پول توجیبی");

        if (preset == ChartPreset.Full)
        {
            var gMaint = AddGroup("5600", "Home Upkeep", "نگهداری منزل", AccountType.Expense, AccountSubType.Category);
            AddLeaf(gMaint, "5601", "Home Repairs", "تعمیرات لوازم و منزل");
            AddLeaf(gMaint, "5602", "Cleaning & Domestic", "نظافت و خدمات منزل");

            var gCloth = AddGroup("5700", "Clothing", "پوشاک", AccountType.Expense, AccountSubType.Category);
            AddLeaf(gCloth, "5701", "Clothes & Shoes", "لباس و کفش");

            var gLeisure = AddGroup("5900", "Leisure & Travel", "تفریح و سفر", AccountType.Expense, AccountSubType.Category);
            AddLeaf(gLeisure, "5901", "Travel & Vacation", "سفر و اقامت");
            AddLeaf(gLeisure, "5902", "Entertainment", "سینما، کتاب و سرگرمی");

            var gFin = AddGroup("6100", "Financial Costs", "هزینه‌های مالی", AccountType.Expense, AccountSubType.Category);
            AddLeaf(gFin, "6101", "Bank Charges & Interest", "کارمزد بانکی و سود وام");

            var gPets = AddGroup("6300", "Pets", "حیوانات خانگی", AccountType.Expense, AccountSubType.Category);
            AddLeaf(gPets, "6301", "Pet Food & Supplies", "غذا و لوازم پت");
            AddLeaf(gPets, "6302", "Veterinary", "دامپزشکی");
        }

        var gMisc = AddGroup("6900", "Miscellaneous", "متفرقه", AccountType.Expense, AccountSubType.Category);
        AddLeaf(gMisc, "6901", "General Expenses", "سایر هزینه‌های پیش‌بینی‌نشده");

        _db.Accounts.AddRange(accounts);
        await _db.SaveChangesAsync(ct);
    }
}

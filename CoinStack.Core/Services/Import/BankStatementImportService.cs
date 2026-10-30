using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CoinStack.Data;
using CoinStack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoinStack.Services.Import;

/// <summary>A parsed row mapped onto CoinStack's transaction model, ready for review.</summary>
public sealed class ImportCandidate
{
    public DateTime DateUtc { get; set; }

    public decimal Amount { get; set; }

    public TransactionType Type { get; set; }

    public string Description { get; set; } = "";

    public string? RawDescription { get; set; }

    public string? CategoryName { get; set; }

    public ExpenseKind ExpenseKind { get; set; } = ExpenseKind.Discretionary;

    public string MerchantName { get; set; } = "";

    public string NormalizedKey { get; set; } = "";

    public string MerchantIcon { get; set; } = "fa-solid fa-store";

    public bool IsAvoidableFee { get; set; }

    public string? FeeType { get; set; }

    public bool IsPassThrough { get; set; }

    public string Fingerprint { get; set; } = "";

    /// <summary>Direction as printed by the bank; unlike <see cref="Type"/> it never changes with classification rules.</summary>
    public bool IsCredit { get; set; }

    public bool Include { get; set; } = true;

    public bool AlreadyExists { get; set; }

    public string SkipReason { get; set; } = "";

    /// <summary>The category the app suggested. Moving the whole merchant to another one at import is remembered.</summary>
    public string? SuggestedCategoryName { get; set; }

    /// <summary>The category is the one the person chose for this merchant before.</summary>
    public bool CategoryRemembered { get; set; }
}

/// <summary>Everything the UI needs to review one statement before committing.</summary>
public sealed class StatementImportPreview
{
    public string BankFormat { get; set; } = "";
    public string? FileName { get; set; }
    public string? AccountNumber { get; set; }

    public DateTime? PeriodStartUtc { get; set; }
    public DateTime? PeriodEndUtc { get; set; }

    public decimal? OpeningBalance { get; set; }
    public decimal? ClosingBalance { get; set; }

    public int RowsParsed { get; set; }
    public int InformationalRowsSkipped { get; set; }

    public decimal PeakOverdraft { get; set; }
    public int DaysInOverdraft { get; set; }

    public decimal TotalAvoidableBankFees { get; set; }
    public int AvoidableBankFeeCount { get; set; }

    public decimal? DetectedSalaryAmount { get; set; }
    public int? DetectedSalaryPayday { get; set; }

    public List<ImportCandidate> Candidates { get; } = [];
    public List<GroupedMerchantCandidate> GroupedMerchants { get; } = [];
    public List<RecurringProposal> RecurringProposals { get; } = [];
    public List<string> Warnings { get; } = [];

    public decimal TotalIncome => Candidates.Where(c => c.Type == TransactionType.Income).Sum(c => c.Amount);
    public decimal TotalExpenses => Candidates.Where(c => c.Type == TransactionType.Expense).Sum(c => c.Amount);
    public decimal TotalTransfers => Candidates.Where(c => c.Type == TransactionType.Transfer).Sum(c => c.Amount);
    public int TransferCount => Candidates.Count(c => c.Type == TransactionType.Transfer);
    public int IncomeCount => Candidates.Count(c => c.Type == TransactionType.Income);
    public int ExpenseCount => Candidates.Count(c => c.Type == TransactionType.Expense);
    public int NewCount => Candidates.Count(c => !c.AlreadyExists);
    public int DuplicateCount => Candidates.Count(c => c.AlreadyExists);

    /// <summary>How many months the statement covers, so a three-month statement's totals can be shown per month.</summary>
    public int MonthsCovered
    {
        get
        {
            var first = PeriodStartUtc ?? (Candidates.Count > 0 ? Candidates.Min(c => c.DateUtc) : null);
            var last = PeriodEndUtc ?? (Candidates.Count > 0 ? Candidates.Max(c => c.DateUtc) : null);
            return first is { } from && last is { } to ? MonthlyTotals.MonthsCovered(from, to) : 1;
        }
    }

    public decimal PerMonth(decimal total) => Math.Round(total / MonthsCovered, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Outcome of committing a preview to the database.</summary>
public sealed class StatementImportResult
{
    public int ImportId { get; set; }
    public int ImportedCount { get; set; }
    public int DuplicateCount { get; set; }
    public int TransferCount { get; set; }
    public decimal? ClosingBalance { get; set; }
    public DateTime? BalanceAsOfUtc { get; set; }

    /// <summary>False when the file had no balance, or a newer balance was already on file.</summary>
    public bool BalanceUpdated { get; set; }

    /// <summary>False when an older statement was imported, so income and payday were left alone.</summary>
    public bool SettingsUpdated { get; set; }
    public decimal? DetectedMonthlySalary { get; set; }
    public int SyntheticIncomeRemoved { get; set; }
    public int SubscriptionsCreated { get; set; }
    /// <summary>Existing subscriptions the imported transactions were linked to; their cost is left as it was.</summary>
    public int SubscriptionsLinked { get; set; }
    public int BucketsCreated { get; set; }
}

/// <summary>What reverting an import removed.</summary>
public sealed record StatementRevertResult(int TransactionsRemoved, int SubscriptionsRemoved, int BucketsRemoved);

public interface IBankStatementImportService
{
    /// <summary>Parses the PDF, classifies rows, aggregates merchant groups, and checks them against existing data.</summary>
    Task<StatementImportPreview?> BuildPreviewAsync(Stream pdfStream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Combines several uploaded files into one preview: rows found in more than one file are kept once,
    /// and recurring payments are detected across all of them.
    /// </summary>
    Task<StatementImportPreview> MergePreviewsAsync(IReadOnlyList<StatementImportPreview> previews, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the selected candidates as transactions, grouped under a statement import record.
    /// Everything is saved in one database transaction: a failure part-way leaves nothing behind.
    /// </summary>
    Task<StatementImportResult> ImportAsync(StatementImportPreview preview, CancellationToken cancellationToken = default);

    Task<List<StatementImport>> GetImportHistoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every transaction created by one import, plus the subscriptions and buckets it created,
    /// and marks it reverted.
    /// </summary>
    Task<StatementRevertResult> RevertImportAsync(int importId, CancellationToken cancellationToken = default);

    /// <summary>
    /// After a statement has been imported: drop the app-generated "Monthly Income" rows,
    /// and if the user has never set a bank snapshot, copy the latest statement closing balance
    /// into settings so the dashboard matches the bank.
    /// </summary>
    Task ReconcileAppWithBankDataAsync(CancellationToken cancellationToken = default);

    Task<int> ScanAndCreateRecurringAsync(CancellationToken cancellationToken = default);
}

public sealed class BankStatementImportService : IBankStatementImportService
{
    private readonly IDbContextFactory<CoinStackDbContext> _dbFactory;
    private readonly IBankFeed _bankFeed;

    public BankStatementImportService(IDbContextFactory<CoinStackDbContext> dbFactory, IBankFeed bankFeed)
    {
        _dbFactory = dbFactory;
        _bankFeed = bankFeed;
    }

    public async Task<StatementImportPreview?> BuildPreviewAsync(
        Stream pdfStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ParsedStatement statement;

        try
        {
            statement = _bankFeed.Parse(pdfStream, fileName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }

        if (statement.Transactions.Count == 0)
        {
            return null;
        }

        var preview = new StatementImportPreview
        {
            BankFormat = statement.BankFormat,
            FileName = fileName,
            AccountNumber = statement.AccountNumber,
            PeriodStartUtc = statement.PeriodStartUtc,
            PeriodEndUtc = statement.PeriodEndUtc,
            OpeningBalance = statement.OpeningBalance,
            ClosingBalance = statement.ClosingBalance,
            RowsParsed = statement.Transactions.Count + statement.InformationalRowsSkipped,
            InformationalRowsSkipped = statement.InformationalRowsSkipped
        };

        preview.Warnings.AddRange(statement.Warnings);

        // 1. Calculate overdraft stats from balance rows
        var balances = statement.Transactions
            .Where(t => t.BalanceAfter is not null)
            .Select(t => (t.DateUtc.Date, Balance: t.BalanceAfter!.Value))
            .ToList();

        if (statement.OpeningBalance is not null)
        {
            balances.Insert(0, (statement.PeriodStartUtc?.Date ?? DateTime.UtcNow.Date, statement.OpeningBalance.Value));
        }

        var negativeBalances = balances.Where(b => b.Balance < 0).ToList();
        if (negativeBalances.Count > 0)
        {
            preview.PeakOverdraft = negativeBalances.Min(b => b.Balance);
            preview.DaysInOverdraft = negativeBalances.Select(b => b.Date).Distinct().Count();
        }

        // 2. Classify candidates using MerchantNormalizer
        foreach (var parsed in statement.Transactions)
        {
            preview.Candidates.Add(Classify(parsed));
        }

        AssignFingerprints(preview.Candidates);

        // 3. Detect Pass-Throughs (e.g., 50,000 multicat in followed by 47,000 out to Pa on 07 Apr)
        DetectPassThroughs(preview.Candidates);

        // 4. Calculate Avoidable Bank Fees
        preview.TotalAvoidableBankFees = preview.Candidates.Where(c => c.IsAvoidableFee).Sum(c => c.Amount);
        preview.AvoidableBankFeeCount = preview.Candidates.Count(c => c.IsAvoidableFee);

        // 5. Detect Salary & Payday
        var salaryCandidates = preview.Candidates
            .Where(c => c.Type == TransactionType.Income && TransactionConventions.LooksLikeSalary(c.RawDescription ?? c.Description))
            .ToList();

        if (salaryCandidates.Count > 0)
        {
            // Per month, not per payment: three months of salary is one salary, and so are two fortnightly payments.
            preview.DetectedSalaryAmount = MonthlyTotals.Typical(salaryCandidates.Select(c => (c.DateUtc, c.Amount)));
            preview.DetectedSalaryPayday = BudgetPeriod.DetectPayday(salaryCandidates.Select(c => c.DateUtc));
        }

        // 6. Check existing fingerprints in DB (current scheme, plus the legacy scheme for older imports)
        var fingerprints = preview.Candidates
            .Select(c => c.Fingerprint)
            .Concat(preview.Candidates.SelectMany(LegacyFingerprints))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var existingFingerprints = await db.Transactions
            .AsNoTracking()
            .Where(t => t.ImportFingerprint != null && fingerprints.Contains(t.ImportFingerprint!))
            .Select(t => t.ImportFingerprint!)
            .ToListAsync(cancellationToken);

        MarkExisting(preview.Candidates, existingFingerprints);

        foreach (var candidate in preview.Candidates)
        {
            if (candidate.AlreadyExists)
            {
                candidate.Include = false;
                candidate.SkipReason = "Already imported";
            }
            else if (candidate.Type == TransactionType.Transfer)
            {
                candidate.Include = false;
                candidate.SkipReason = "Transfer between own accounts";
            }
        }

        // The person's own categories for merchants they moved before win over the defaults.
        var rules = await MerchantRuleBook.LoadAsync(db, cancellationToken);
        foreach (var candidate in preview.Candidates.Where(c => c.Type != TransactionType.Transfer))
        {
            if (rules.CategoryFor(candidate.NormalizedKey) is { } remembered)
            {
                candidate.CategoryName = remembered;
                candidate.CategoryRemembered = true;
            }

            candidate.SuggestedCategoryName = candidate.CategoryName;
        }

        // 7. Aggregate into Grouped Merchant Views
        BuildGroupedMerchants(preview);

        await DetectRecurringProposalsAsync(db, preview, cancellationToken);

        if (preview.ClosingBalance is null)
        {
            preview.Warnings.Add("This file has no running balance. Transactions will still import; set today's FNB balance with Update balance so cash matches the bank.");
        }

        if (preview.PeriodEndUtc is { } end && (DateTime.UtcNow.Date - end.Date).TotalDays > 21)
        {
            preview.Warnings.Add($"This file only goes to {end:dd MMM yyyy}. Monthly PDFs often lag. Download transaction history (CSV/OFX) from the FNB app or online banking for the missing weeks.");
        }

        return preview;
    }

    /// <summary>Finds recurring payments across what is already saved plus the rows this preview will add.</summary>
    private static async Task DetectRecurringProposalsAsync(
        CoinStackDbContext db,
        StatementImportPreview preview,
        CancellationToken cancellationToken)
    {
        var existingTransactions = await db.Transactions
            .AsNoTracking()
            .Include(t => t.Category)
            .ToListAsync(cancellationToken);

        var fromPreview = preview.Candidates
            .Where(c => c.Include)
            .Select(c => new Transaction
            {
                Description = c.Description,
                Notes = c.RawDescription,
                Amount = c.Amount,
                Type = c.Type,
                OccurredAtUtc = c.DateUtc
            });

        preview.RecurringProposals.Clear();
        preview.RecurringProposals.AddRange(
            RecurringMerchantDetector.DetectFromTransactions(existingTransactions.Concat(fromPreview).ToList()));

        await ApplyDismissalsAndExistingAsync(db, preview.RecurringProposals, cancellationToken);
    }

    public async Task<StatementImportPreview> MergePreviewsAsync(
        IReadOnlyList<StatementImportPreview> previews,
        CancellationToken cancellationToken = default)
    {
        if (previews.Count == 1)
        {
            return previews[0];
        }

        var byStart = previews.OrderBy(p => p.PeriodStartUtc ?? DateTime.MaxValue).ToList();
        var byEnd = previews.OrderByDescending(p => p.PeriodEndUtc ?? DateTime.MinValue).ToList();

        var merged = new StatementImportPreview
        {
            BankFormat = previews[0].BankFormat,
            FileName = string.Join(", ", previews.Select(p => p.FileName ?? "Statement").Distinct()),
            AccountNumber = previews[0].AccountNumber,
            PeriodStartUtc = previews.Min(p => p.PeriodStartUtc),
            PeriodEndUtc = previews.Max(p => p.PeriodEndUtc),
            OpeningBalance = byStart[0].OpeningBalance,
            ClosingBalance = byEnd[0].ClosingBalance,
            RowsParsed = previews.Sum(p => p.RowsParsed),
            InformationalRowsSkipped = previews.Sum(p => p.InformationalRowsSkipped),
            PeakOverdraft = previews.Min(p => p.PeakOverdraft),
            DaysInOverdraft = previews.Sum(p => p.DaysInOverdraft),
            TotalAvoidableBankFees = previews.Sum(p => p.TotalAvoidableBankFees),
            AvoidableBankFeeCount = previews.Sum(p => p.AvoidableBankFeeCount),
            DetectedSalaryAmount = byEnd.FirstOrDefault(p => p.DetectedSalaryAmount is not null)?.DetectedSalaryAmount,
            DetectedSalaryPayday = byEnd.FirstOrDefault(p => p.DetectedSalaryPayday is not null)?.DetectedSalaryPayday
        };

        merged.Warnings.AddRange(previews.SelectMany(p => p.Warnings).Distinct());

        var seenFingerprints = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in previews.SelectMany(p => p.Candidates))
        {
            if (!seenFingerprints.Add(candidate.Fingerprint))
            {
                candidate.Include = false;
                candidate.AlreadyExists = true;
                candidate.SkipReason = "Duplicate of another uploaded statement";
            }

            merged.Candidates.Add(candidate);
        }

        BuildGroupedMerchants(merged);

        // Detect across every file together: a monthly debit order seen once per statement only looks
        // recurring when the statements are combined.
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await DetectRecurringProposalsAsync(db, merged, cancellationToken);

        return merged;
    }

    private static void DetectPassThroughs(List<ImportCandidate> candidates)
    {
        var largeCredits = candidates
            .Where(c => c.Type == TransactionType.Income && c.Amount >= 5000m && !c.Description.Contains("Salary", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var credit in largeCredits)
        {
            var matchingDebit = candidates.FirstOrDefault(d =>
                d.Type == TransactionType.Expense
                && Math.Abs((d.DateUtc - credit.DateUtc).TotalDays) <= 2
                && d.Amount >= credit.Amount * 0.8m
                && d.Amount <= credit.Amount * 1.1m);

            if (matchingDebit is not null)
            {
                credit.IsPassThrough = true;
                matchingDebit.IsPassThrough = true;
            }
        }
    }

    public static void BuildGroupedMerchants(StatementImportPreview preview)
    {
        preview.GroupedMerchants.Clear();

        var groups = preview.Candidates
            .GroupBy(c => c.NormalizedKey)
            .ToList();

        foreach (var g in groups)
        {
            var first = g.First();
            var grouped = new GroupedMerchantCandidate
            {
                MerchantName = first.MerchantName,
                NormalizedKey = first.NormalizedKey,
                CategoryName = first.CategoryName,
                SuggestedCategoryName = first.SuggestedCategoryName,
                CategoryRemembered = first.CategoryRemembered,
                IconClass = first.MerchantIcon,
                Type = first.Type,
                TotalAmount = g.Sum(c => c.Amount),
                IsAvoidableFee = g.Any(c => c.IsAvoidableFee),
                FeeType = g.FirstOrDefault(c => c.FeeType is not null)?.FeeType,
                IsPassThrough = g.Any(c => c.IsPassThrough)
            };

            grouped.Items.AddRange(g.OrderByDescending(c => c.DateUtc));

            // Monthly breakdown
            var byMonth = g
                .GroupBy(c => new DateTime(c.DateUtc.Year, c.DateUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc))
                .OrderBy(mg => mg.Key);

            foreach (var monthGroup in byMonth)
            {
                grouped.MonthlyBreakdowns.Add(new MerchantMonthlySpend(
                    monthGroup.Key.ToString("MMM yy"),
                    monthGroup.Key,
                    monthGroup.Sum(c => c.Amount),
                    monthGroup.Count()));
            }

            preview.GroupedMerchants.Add(grouped);
        }

        preview.GroupedMerchants.Sort((a, b) => b.TotalAmount.CompareTo(a.TotalAmount));
    }

    public async Task<StatementImportResult> ImportAsync(
        StatementImportPreview preview,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Several saves happen below (the import row first, for its id); one database transaction makes
        // them all-or-nothing, so a failure can't leave an import with half its rows or subscriptions.
        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var categories = await db.Categories
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var categoryByName = categories.ToDictionary(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase);
        var account = await AccountService.GetOrCreateDefaultAsync(db, cancellationToken);

        var import = new StatementImport
        {
            FileName = preview.FileName ?? "Import",
            BankFormat = preview.BankFormat,
            AccountNumber = preview.AccountNumber,
            AccountId = account.Id,
            PeriodStartUtc = preview.PeriodStartUtc,
            PeriodEndUtc = preview.PeriodEndUtc,
            OpeningBalance = preview.OpeningBalance,
            ClosingBalance = preview.ClosingBalance,
            RowsParsed = preview.RowsParsed,
            Status = StatementImportStatus.Imported
        };

        db.StatementImports.Add(import);
        await db.SaveChangesAsync(cancellationToken);

        var toImport = preview.Candidates.Where(c => c.Include && !c.AlreadyExists).ToList();

        foreach (var candidate in toImport)
        {
            db.Transactions.Add(new Transaction
            {
                OccurredAtUtc = candidate.DateUtc,
                Amount = candidate.Amount,
                Type = candidate.Type,
                AccountId = account.Id,
                Description = candidate.Description,
                Notes = candidate.RawDescription != candidate.Description ? candidate.RawDescription : null,
                CategoryId = candidate.CategoryName is not null && categoryByName.TryGetValue(candidate.CategoryName, out var categoryId)
                    ? categoryId
                    : null,
                ExpenseKind = candidate.ExpenseKind,
                Source = TransactionConventions.BankSource,
                ImportFingerprint = candidate.Fingerprint,
                StatementImportId = import.Id
            });
        }

        import.TransactionsImported = toImport.Count(c => c.Type != TransactionType.Transfer);
        import.DuplicatesSkipped = preview.DuplicateCount;
        import.TransfersSkipped = preview.TransferCount;

        // A merchant moved to another category as a whole is remembered for the next import. Changing
        // one row of a merchant is treated as a one-off.
        foreach (var merchant in toImport.Where(c => c.Type != TransactionType.Transfer).GroupBy(c => c.NormalizedKey))
        {
            var chosen = merchant.Select(c => c.CategoryName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var suggested = merchant.First().SuggestedCategoryName;
            if (chosen is [{ } name]
                && suggested is not null
                && !string.Equals(name, suggested, StringComparison.OrdinalIgnoreCase)
                && categoryByName.TryGetValue(name, out var chosenCategoryId))
            {
                await MerchantRuleBook.RememberAsync(db, merchant.Key, merchant.First().MerchantName, chosenCategoryId, cancellationToken);
            }
        }

        // Saved now so the recurring step below finds these rows and links them to their subscriptions.
        await db.SaveChangesAsync(cancellationToken);

        var salaryPayments = toImport
            .Concat(preview.Candidates.Where(c => c.AlreadyExists))
            .Where(c => c.Type == TransactionType.Income && TransactionConventions.LooksLikeSalary(c.RawDescription ?? c.Description))
            .Select(c => (c.DateUtc, c.Amount))
            .ToList();

        var detectedSalary = salaryPayments.Count > 0 ? MonthlyTotals.Typical(salaryPayments) : (decimal?)null;

        var payday = BudgetPeriod.DetectPayday(toImport
            .Concat(preview.Candidates.Where(c => c.AlreadyExists))
            .Where(c => c.Type == TransactionType.Income && TransactionConventions.LooksLikeSalary(c.RawDescription ?? c.Description))
            .Select(c => c.DateUtc));

        // Without a printed period end, the closing balance applies at the last transaction date, not "now".
        var statementEnd = preview.PeriodEndUtc
                           ?? (preview.Candidates.Count > 0 ? preview.Candidates.Max(c => c.DateUtc) : (DateTime?)null);
        var balanceAsOf = statementEnd?.Date.AddDays(1).AddTicks(-1);

        // Only the most recent statement may drive income and payday; an older one fills in history.
        var newestOtherEnd = await db.StatementImports
            .AsNoTracking()
            .Where(i => i.Id != import.Id && i.Status == StatementImportStatus.Imported && i.PeriodEndUtc != null)
            .MaxAsync(i => i.PeriodEndUtc, cancellationToken);
        var isNewestStatement = newestOtherEnd is null || statementEnd is null || statementEnd.Value >= newestOtherEnd.Value;

        var syntheticsRemoved = await RemoveSyntheticMonthlyIncomeAsync(db, cancellationToken);
        var balanceUpdated = await SyncSettingsFromStatementAsync(
            db, account, preview.ClosingBalance, balanceAsOf, isNewestStatement ? detectedSalary : null,
            isNewestStatement ? payday : 0, cancellationToken);

        var recurringStats = await ApplyRecurringProposalsAsync(db, preview.RecurringProposals, import.Id, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return new StatementImportResult
        {
            ImportId = import.Id,
            ImportedCount = import.TransactionsImported,
            DuplicateCount = preview.DuplicateCount,
            TransferCount = preview.TransferCount,
            ClosingBalance = import.ClosingBalance,
            BalanceAsOfUtc = balanceAsOf,
            BalanceUpdated = balanceUpdated,
            SettingsUpdated = isNewestStatement,
            DetectedMonthlySalary = detectedSalary,
            SyntheticIncomeRemoved = syntheticsRemoved,
            SubscriptionsCreated = recurringStats.Created,
            SubscriptionsLinked = recurringStats.Linked,
            BucketsCreated = recurringStats.BucketsCreated
        };
    }

    public async Task<List<StatementImport>> GetImportHistoryAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.StatementImports
            .AsNoTracking()
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<StatementRevertResult> RevertImportAsync(int importId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var import = await db.StatementImports.FirstOrDefaultAsync(i => i.Id == importId, cancellationToken);
        if (import is null || import.Status == StatementImportStatus.Reverted)
        {
            return new StatementRevertResult(0, 0, 0);
        }

        var transactions = await db.Transactions
            .Where(t => t.StatementImportId == importId)
            .ToListAsync(cancellationToken);
        var subscriptions = await db.Subscriptions
            .Where(s => s.CreatedByStatementImportId == importId)
            .ToListAsync(cancellationToken);
        var buckets = await db.Buckets
            .Where(b => b.CreatedByStatementImportId == importId)
            .ToListAsync(cancellationToken);

        // Other transactions linked to these lose the link (the foreign keys are set to null).
        db.Transactions.RemoveRange(transactions);
        db.Subscriptions.RemoveRange(subscriptions);
        db.Buckets.RemoveRange(buckets);
        import.Status = StatementImportStatus.Reverted;
        await db.SaveChangesAsync(cancellationToken);

        return new StatementRevertResult(transactions.Count, subscriptions.Count, buckets.Count);
    }

    public async Task ReconcileAppWithBankDataAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Legacy placeholder income is cleaned up once at startup (DatabaseInitializer), not per page view.
        var settings = await db.AppSettings.FirstOrDefaultAsync(cancellationToken);
        var account = await AccountService.GetOrCreateDefaultAsync(db, cancellationToken);

        // Files without a running balance store null; never turn that into an R0 bank balance.
        var latestImport = await db.StatementImports
            .AsNoTracking()
            .Where(i => i.Status == StatementImportStatus.Imported
                        && i.ClosingBalance != null
                        && (i.AccountId == null || i.AccountId == account.Id))
            .OrderByDescending(i => i.PeriodEndUtc ?? i.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (account.BalanceAsOfUtc is null && latestImport?.ClosingBalance is { } closing)
        {
            account.Balance = closing;
            account.BalanceAsOfUtc = latestImport.PeriodEndUtc?.Date.AddDays(1).AddTicks(-1) ?? latestImport.CreatedAtUtc;
        }

        if (settings is not null && settings.MonthlyIncome <= 0m)
        {
            var salaryAmounts = await db.Transactions
                .AsNoTracking()
                .Where(t => t.Type == TransactionType.Income
                            && t.Source == TransactionConventions.BankSource)
                .ToListAsync(cancellationToken);

            var salaries = salaryAmounts
                .Where(t => TransactionConventions.LooksLikeSalary(t.Description)
                            || (t.Notes is not null && TransactionConventions.LooksLikeSalary(t.Notes)))
                .Select(t => (t.OccurredAtUtc, t.Amount))
                .ToList();

            if (salaries.Count > 0)
            {
                settings.MonthlyIncome = MonthlyTotals.Typical(salaries);

                var payday = BudgetPeriod.DetectPayday(salaryAmounts
                    .Where(t => TransactionConventions.LooksLikeSalary(t.Description)
                                || (t.Notes is not null && TransactionConventions.LooksLikeSalary(t.Notes)))
                    .Select(t => t.OccurredAtUtc));

                if (payday is >= 1 and <= BudgetPeriod.LastPayday && settings.MonthStartDay == 1)
                {
                    settings.MonthStartDay = payday;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Task<int> RemoveSyntheticMonthlyIncomeAsync(CoinStackDbContext db, CancellationToken cancellationToken)
        => DatabaseInitializer.RemoveLegacySyntheticMonthlyIncomeAsync(db, cancellationToken);

    /// <summary>
    /// Applies the statement's closing balance to its account only when it is newer than the balance already
    /// on file (an earlier import or a manual "Update balance"). Returns whether the balance was applied.
    /// Pass a null salary and payday 0 to leave income settings untouched.
    /// </summary>
    private static async Task<bool> SyncSettingsFromStatementAsync(
        CoinStackDbContext db,
        Account account,
        decimal? closingBalance,
        DateTime? balanceAsOfUtc,
        decimal? detectedSalary,
        int payday,
        CancellationToken cancellationToken)
    {
        var settings = await db.AppSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings is null)
        {
            settings = new AppSettings();
            db.AppSettings.Add(settings);
        }

        var balanceUpdated = false;
        if (closingBalance is not null)
        {
            var asOf = balanceAsOfUtc ?? DateTime.UtcNow;
            if (account.BalanceAsOfUtc is null || asOf > account.BalanceAsOfUtc.Value)
            {
                account.Balance = closingBalance.Value;
                account.BalanceAsOfUtc = asOf;
                balanceUpdated = true;
            }
        }

        if (detectedSalary is > 0)
        {
            settings.MonthlyIncome = detectedSalary.Value;
        }

        if (payday is >= 1 and <= BudgetPeriod.LastPayday)
        {
            settings.MonthStartDay = payday;
        }

        return balanceUpdated;
    }

    public async Task<int> ScanAndCreateRecurringAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var transactions = await db.Transactions
            .Include(t => t.Category)
            .ToListAsync(cancellationToken);

        var proposals = RecurringMerchantDetector.DetectFromTransactions(transactions);
        var stats = await ApplyRecurringProposalsAsync(db, proposals, statementImportId: null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return stats.Created + stats.Linked;
    }

    /// <summary>
    /// Creates the subscriptions and buckets the user kept ticked, and links the merchant's transactions
    /// to them. Existing ones are linked but never changed: the user may have edited the cost or
    /// allocation. Merchants whose subscription or bucket the user deleted are not created again.
    /// </summary>
    /// <returns>Subscriptions created, existing subscriptions linked, and buckets created.</returns>
    private static async Task<(int Created, int Linked, int BucketsCreated)> ApplyRecurringProposalsAsync(
        CoinStackDbContext db,
        IReadOnlyList<RecurringProposal> proposals,
        int? statementImportId,
        CancellationToken cancellationToken)
    {
        var created = 0;
        var linked = 0;
        var bucketsCreated = 0;

        var subscriptions = await db.Subscriptions.ToListAsync(cancellationToken);
        var buckets = await db.Buckets.ToListAsync(cancellationToken);
        var categories = await db.Categories.ToListAsync(cancellationToken);
        var dismissed = await LoadDismissalsAsync(db, cancellationToken);
        var transactions = await db.Transactions
            .Where(t => t.Type == TransactionType.Expense)
            .ToListAsync(cancellationToken);

        foreach (var proposal in proposals)
        {
            dismissed.TryGetValue(proposal.NormalizedKey, out var dismissal);
            var wantsSubscription = proposal.CreateSubscription && dismissal?.SubscriptionDismissed != true;
            var wantsBucket = proposal.CreateBucket && dismissal?.BucketDismissed != true;

            if (!wantsSubscription && !wantsBucket)
            {
                continue;
            }

            Subscription? subscription = null;
            if (wantsSubscription)
            {
                subscription = subscriptions.FirstOrDefault(s =>
                    string.Equals(s.Name, proposal.MerchantName, StringComparison.OrdinalIgnoreCase));

                if (subscription is null)
                {
                    subscription = new Subscription
                    {
                        Name = proposal.MerchantName,
                        Category = proposal.CategoryName,
                        Cycle = proposal.Cycle,
                        Cost = proposal.MedianAmount,
                        Status = SubscriptionStatus.Active,
                        DebitOrderDay = proposal.DebitOrderDay,
                        CreatedByStatementImportId = statementImportId
                    };
                    db.Subscriptions.Add(subscription);
                    subscriptions.Add(subscription);
                    created++;
                    await db.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    linked++;
                }
            }

            Bucket? bucket = null;
            if (wantsBucket)
            {
                bucket = buckets.FirstOrDefault(b =>
                    string.Equals(b.Name, proposal.MerchantName, StringComparison.OrdinalIgnoreCase));

                if (bucket is null)
                {
                    var categoryId = categories
                        .FirstOrDefault(c => string.Equals(c.Name, proposal.CategoryName, StringComparison.OrdinalIgnoreCase))
                        ?.Id;

                    bucket = new Bucket
                    {
                        Name = proposal.MerchantName,
                        AllocatedAmount = proposal.MonthlyAllocation,
                        CategoryId = categoryId,
                        SortOrder = buckets.Count,
                        CreatedByStatementImportId = statementImportId
                    };
                    db.Buckets.Add(bucket);
                    buckets.Add(bucket);
                    bucketsCreated++;
                    await db.SaveChangesAsync(cancellationToken);
                }
            }

            foreach (var tx in transactions)
            {
                var key = MerchantNormalizer.Normalize(tx.Description, TransactionType.Expense).NormalizedKey;
                if (!string.Equals(key, proposal.NormalizedKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Never move a transaction the user already filed somewhere else.
                if (subscription is not null && tx.SubscriptionId is null)
                {
                    tx.SubscriptionId = subscription.Id;
                }

                if (bucket is not null && tx.BucketId is null)
                {
                    tx.BucketId = bucket.Id;
                }
            }
        }

        return (created, linked, bucketsCreated);
    }

    private static async Task<Dictionary<string, DismissedRecurringMerchant>> LoadDismissalsAsync(
        CoinStackDbContext db,
        CancellationToken cancellationToken)
    {
        var rows = await db.DismissedRecurringMerchants.AsNoTracking().ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.NormalizedKey, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Untick what the user deleted before, and flag what already exists, so the preview shows what an
    /// import will really do. Merchants dismissed for both are dropped from the list.
    /// </summary>
    private static async Task ApplyDismissalsAndExistingAsync(
        CoinStackDbContext db,
        List<RecurringProposal> proposals,
        CancellationToken cancellationToken)
    {
        var dismissed = await LoadDismissalsAsync(db, cancellationToken);
        var subscriptionNames = (await db.Subscriptions.AsNoTracking().Select(s => s.Name).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var bucketNames = (await db.Buckets.AsNoTracking().Select(b => b.Name).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        proposals.RemoveAll(p => dismissed.TryGetValue(p.NormalizedKey, out var d) && d.SubscriptionDismissed && d.BucketDismissed);

        foreach (var proposal in proposals)
        {
            if (dismissed.TryGetValue(proposal.NormalizedKey, out var dismissal))
            {
                proposal.CreateSubscription &= !dismissal.SubscriptionDismissed;
                proposal.CreateBucket &= !dismissal.BucketDismissed;
            }

            proposal.SubscriptionExists = subscriptionNames.Contains(proposal.MerchantName);
            proposal.BucketExists = bucketNames.Contains(proposal.MerchantName);
        }
    }

    private static ImportCandidate Classify(ParsedStatementTransaction parsed)
    {
        var description = parsed.Description;
        var upper = description.ToUpperInvariant();

        var type = parsed.IsCredit ? TransactionType.Income : TransactionType.Expense;
        var expenseKind = ExpenseKind.Discretionary;

        // Internal transfers: money moving between the account holder's own pockets.
        var isOwnTransfer = parsed.IsCredit
            ? upper.Contains("TRANSFER FROM") || upper.Contains("ADT CASH DEPOSIT")
            : upper.Contains("TO INVEST")
              || upper.Contains("SCHD TRXN NO AV BAL")
              || (upper.Contains("PAYMENT TO SAVINGS") && !upper.Contains("PAYMENT TO PA"));

        if (isOwnTransfer)
        {
            type = TransactionType.Transfer;
        }

        if (type == TransactionType.Expense
            && (upper.StartsWith("DEBICHECK")
                || upper.Contains("TRANSFER TO CREDIT")
                || upper.Contains("FNBCC")
                || upper.Contains("WESBANK")
                || IsBankChargeRow(description)))
        {
            expenseKind = ExpenseKind.Mandatory;
        }

        var merchant = MerchantNormalizer.Normalize(description, type);

        return new ImportCandidate
        {
            DateUtc = parsed.DateUtc,
            Amount = Math.Abs(parsed.Amount),
            IsCredit = parsed.IsCredit,
            Type = type,
            Description = merchant.CanonicalName,
            RawDescription = description,
            MerchantName = merchant.CanonicalName,
            NormalizedKey = merchant.NormalizedKey,
            MerchantIcon = merchant.IconClass,
            CategoryName = type != TransactionType.Transfer ? merchant.CategoryName : null,
            ExpenseKind = expenseKind,
            IsAvoidableFee = merchant.IsAvoidableFee,
            FeeType = merchant.FeeType
        };
    }

    private static bool IsBankChargeRow(string description)
    {
        return description.Equals("Bank Charges", StringComparison.OrdinalIgnoreCase)
               || description.StartsWith("Bank Credit", StringComparison.OrdinalIgnoreCase)
               || description.Contains("Service Fees", StringComparison.OrdinalIgnoreCase)
               || description.Contains("Purchase Via eBucks.Com Pay In Eb", StringComparison.OrdinalIgnoreCase);
    }

    private const string FingerprintPrefix = "v2:";

    /// <summary>
    /// Fingerprints every candidate from the bank's own data only: date, direction, invariant-culture
    /// amount and the raw description, so normaliser or classification changes never make a re-import
    /// look new. Identical rows on the same day (two R35 coffees) get an occurrence number, so they
    /// stay distinct and re-importing the same file still matches each one.
    /// </summary>
    internal static void AssignFingerprints(IEnumerable<ImportCandidate> candidates)
    {
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var candidate in candidates.OrderBy(c => c.DateUtc))
        {
            var key = string.Join('|',
                candidate.DateUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                candidate.IsCredit ? "C" : "D",
                candidate.Amount.ToString("F2", CultureInfo.InvariantCulture),
                NormalizeForFingerprint(candidate.RawDescription ?? candidate.Description));

            var occurrence = occurrences.GetValueOrDefault(key) + 1;
            occurrences[key] = occurrence;

            candidate.Fingerprint = FingerprintPrefix + Hash($"{key}|#{occurrence}");
        }
    }

    /// <summary>
    /// Marks candidates already in the database. Legacy fingerprints are matched as a multiset:
    /// two stored rows with the same legacy hash cover at most two candidates, never three.
    /// </summary>
    internal static void MarkExisting(IEnumerable<ImportCandidate> candidates, IEnumerable<string> storedFingerprints)
    {
        var remaining = storedFingerprints
            .GroupBy(f => f, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        foreach (var candidate in candidates.OrderBy(c => c.DateUtc))
        {
            candidate.AlreadyExists = remaining.ContainsKey(candidate.Fingerprint);
            if (candidate.AlreadyExists)
            {
                continue;
            }

            foreach (var legacy in LegacyFingerprints(candidate))
            {
                if (remaining.TryGetValue(legacy, out var count) && count > 0)
                {
                    remaining[legacy] = count - 1;
                    candidate.AlreadyExists = true;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// The pre-v2 scheme hashed the canonical merchant name and formatted the amount with whatever
    /// culture was current, so both decimal separators are tried.
    /// </summary>
    private static IEnumerable<string> LegacyFingerprints(ImportCandidate candidate)
    {
        var date = candidate.DateUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var dotAmount = candidate.Amount.ToString("F2", CultureInfo.InvariantCulture);
        var description = candidate.Description.ToUpperInvariant();

        yield return Hash($"{date}|{dotAmount}|{candidate.Type}|{description}");
        yield return Hash($"{date}|{dotAmount.Replace('.', ',')}|{candidate.Type}|{description}");
    }

    private static string NormalizeForFingerprint(string description)
        => string.Join(' ', description.ToUpperInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Hash(string raw)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..32];
}

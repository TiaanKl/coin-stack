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

    public bool Include { get; set; } = true;

    public bool AlreadyExists { get; set; }

    public string SkipReason { get; set; } = "";
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
}

/// <summary>Outcome of committing a preview to the database.</summary>
public sealed class StatementImportResult
{
    public int ImportId { get; set; }
    public int ImportedCount { get; set; }
    public int DuplicateCount { get; set; }
    public int TransferCount { get; set; }
    public decimal ClosingBalance { get; set; }
    public DateTime? BalanceAsOfUtc { get; set; }
    public decimal? DetectedMonthlySalary { get; set; }
    public int SyntheticIncomeRemoved { get; set; }
    public int SubscriptionsCreated { get; set; }
    public int SubscriptionsUpdated { get; set; }
    public int BucketsCreated { get; set; }
}

public interface IBankStatementImportService
{
    /// <summary>Parses the PDF, classifies rows, aggregates merchant groups, and checks them against existing data.</summary>
    Task<StatementImportPreview?> BuildPreviewAsync(Stream pdfStream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>Commits the selected candidates as transactions, grouped under a statement import record.</summary>
    Task<StatementImportResult> ImportAsync(StatementImportPreview preview, CancellationToken cancellationToken = default);

    Task<List<StatementImport>> GetImportHistoryAsync(CancellationToken cancellationToken = default);

    /// <summary>Deletes every transaction created by one import and marks it reverted.</summary>
    Task<int> RevertImportAsync(int importId, CancellationToken cancellationToken = default);

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
            var candidate = Classify(parsed);
            candidate.Fingerprint = BuildFingerprint(candidate);
            preview.Candidates.Add(candidate);
        }

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
            preview.DetectedSalaryAmount = Math.Round(salaryCandidates.Average(c => c.Amount), 2, MidpointRounding.AwayFromZero);
            preview.DetectedSalaryPayday = salaryCandidates
                .GroupBy(c => c.DateUtc.Day)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();
        }

        // 6. Check existing fingerprints in DB
        var fingerprints = preview.Candidates.Select(c => c.Fingerprint).ToList();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var existingFingerprints = await db.Transactions
            .AsNoTracking()
            .Where(t => t.ImportFingerprint != null && fingerprints.Contains(t.ImportFingerprint!))
            .Select(t => t.ImportFingerprint!)
            .ToListAsync(cancellationToken);

        var existing = existingFingerprints.ToHashSet(StringComparer.Ordinal);

        foreach (var candidate in preview.Candidates)
        {
            candidate.AlreadyExists = existing.Contains(candidate.Fingerprint);

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

        // 7. Aggregate into Grouped Merchant Views
        BuildGroupedMerchants(preview);

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

        preview.RecurringProposals.AddRange(
            RecurringMerchantDetector.DetectFromTransactions(existingTransactions.Concat(fromPreview).ToList()));

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

        var categories = await db.Categories
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var categoryByName = categories.ToDictionary(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase);

        var import = new StatementImport
        {
            FileName = preview.FileName ?? "Import",
            BankFormat = preview.BankFormat,
            AccountNumber = preview.AccountNumber,
            PeriodStartUtc = preview.PeriodStartUtc,
            PeriodEndUtc = preview.PeriodEndUtc,
            OpeningBalance = preview.OpeningBalance ?? 0m,
            ClosingBalance = preview.ClosingBalance ?? 0m,
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

        var salaryAmounts = toImport
            .Concat(preview.Candidates.Where(c => c.AlreadyExists))
            .Where(c => c.Type == TransactionType.Income && TransactionConventions.LooksLikeSalary(c.RawDescription ?? c.Description))
            .Select(c => c.Amount)
            .ToList();

        var detectedSalary = salaryAmounts.Count > 0
            ? Math.Round(salaryAmounts.Average(), 2, MidpointRounding.AwayFromZero)
            : (decimal?)null;

        var payday = toImport
            .Concat(preview.Candidates.Where(c => c.AlreadyExists))
            .Where(c => c.Type == TransactionType.Income && TransactionConventions.LooksLikeSalary(c.RawDescription ?? c.Description))
            .Select(c => c.DateUtc.Day)
            .GroupBy(day => day)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        var balanceAsOf = preview.PeriodEndUtc?.Date.AddDays(1).AddTicks(-1);

        var syntheticsRemoved = await RemoveSyntheticMonthlyIncomeAsync(db, cancellationToken);
        await SyncSettingsFromStatementAsync(db, preview.ClosingBalance, balanceAsOf, detectedSalary, payday, cancellationToken);

        var recurringStats = await ApplyRecurringProposalsAsync(db, preview.RecurringProposals, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return new StatementImportResult
        {
            ImportId = import.Id,
            ImportedCount = import.TransactionsImported,
            DuplicateCount = preview.DuplicateCount,
            TransferCount = preview.TransferCount,
            ClosingBalance = import.ClosingBalance,
            BalanceAsOfUtc = balanceAsOf,
            DetectedMonthlySalary = detectedSalary,
            SyntheticIncomeRemoved = syntheticsRemoved,
            SubscriptionsCreated = recurringStats.Created,
            SubscriptionsUpdated = recurringStats.Updated,
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

    public async Task<int> RevertImportAsync(int importId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var import = await db.StatementImports.FirstOrDefaultAsync(i => i.Id == importId, cancellationToken);
        if (import is null || import.Status == StatementImportStatus.Reverted)
        {
            return 0;
        }

        var transactions = await db.Transactions
            .Where(t => t.StatementImportId == importId)
            .ToListAsync(cancellationToken);

        db.Transactions.RemoveRange(transactions);
        import.Status = StatementImportStatus.Reverted;
        await db.SaveChangesAsync(cancellationToken);

        return transactions.Count;
    }

    public async Task ReconcileAppWithBankDataAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        await RemoveSyntheticMonthlyIncomeAsync(db, cancellationToken);

        var settings = await db.AppSettings.FirstOrDefaultAsync(cancellationToken);
        var latestImport = await db.StatementImports
            .AsNoTracking()
            .Where(i => i.Status == StatementImportStatus.Imported)
            .OrderByDescending(i => i.PeriodEndUtc ?? i.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is not null && settings.BankBalanceAsOfUtc is null && latestImport is not null)
        {
            var asOf = latestImport.PeriodEndUtc?.Date.AddDays(1).AddTicks(-1) ?? latestImport.CreatedAtUtc;
            settings.CurrentBankBalance = latestImport.ClosingBalance;
            settings.BankBalanceAsOfUtc = asOf;
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
                .Select(t => t.Amount)
                .ToList();

            if (salaries.Count > 0)
            {
                settings.MonthlyIncome = Math.Round(salaries.Average(), 2, MidpointRounding.AwayFromZero);

                var payday = salaryAmounts
                    .Where(t => TransactionConventions.LooksLikeSalary(t.Description)
                                || (t.Notes is not null && TransactionConventions.LooksLikeSalary(t.Notes)))
                    .Select(t => t.OccurredAtUtc.Day)
                    .GroupBy(day => day)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .FirstOrDefault();

                if (payday is >= 1 and <= 28 && settings.MonthStartDay == 1)
                {
                    settings.MonthStartDay = payday;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<int> RemoveSyntheticMonthlyIncomeAsync(CoinStackDbContext db, CancellationToken cancellationToken)
    {
        var synthetics = await db.Transactions
            .Where(t => t.Type == TransactionType.Income
                        && t.Description == TransactionConventions.SyntheticMonthlyIncomeDescription
                        && t.Source != TransactionConventions.BankSource)
            .ToListAsync(cancellationToken);

        if (synthetics.Count == 0)
        {
            return 0;
        }

        db.Transactions.RemoveRange(synthetics);
        return synthetics.Count;
    }

    private static async Task SyncSettingsFromStatementAsync(
        CoinStackDbContext db,
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

        if (closingBalance is not null)
        {
            settings.CurrentBankBalance = closingBalance.Value;
            settings.BankBalanceAsOfUtc = balanceAsOfUtc ?? DateTime.UtcNow;
        }

        if (detectedSalary is > 0)
        {
            settings.MonthlyIncome = detectedSalary.Value;
        }

        if (payday is >= 1 and <= 28)
        {
            settings.MonthStartDay = payday;
        }
    }

    public async Task<int> ScanAndCreateRecurringAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var transactions = await db.Transactions
            .Include(t => t.Category)
            .ToListAsync(cancellationToken);

        var proposals = RecurringMerchantDetector.DetectFromTransactions(transactions);
        var stats = await ApplyRecurringProposalsAsync(db, proposals, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return stats.Created + stats.Updated;
    }

    private static async Task<(int Created, int Updated, int BucketsCreated)> ApplyRecurringProposalsAsync(
        CoinStackDbContext db,
        IReadOnlyList<RecurringProposal> proposals,
        CancellationToken cancellationToken)
    {
        var created = 0;
        var updated = 0;
        var bucketsCreated = 0;

        var subscriptions = await db.Subscriptions.ToListAsync(cancellationToken);
        var buckets = await db.Buckets.ToListAsync(cancellationToken);
        var categories = await db.Categories.ToListAsync(cancellationToken);
        var transactions = await db.Transactions
            .Where(t => t.Type == TransactionType.Expense)
            .ToListAsync(cancellationToken);

        foreach (var proposal in proposals)
        {
            if (!proposal.CreateSubscription && !proposal.CreateBucket)
            {
                continue;
            }

            Subscription? subscription = null;
            if (proposal.CreateSubscription)
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
                        DebitOrderDay = proposal.DebitOrderDay
                    };
                    db.Subscriptions.Add(subscription);
                    subscriptions.Add(subscription);
                    created++;
                    await db.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    subscription.Cost = proposal.MedianAmount;
                    subscription.Cycle = proposal.Cycle;
                    subscription.DebitOrderDay = proposal.DebitOrderDay;
                    if (string.IsNullOrWhiteSpace(subscription.Category))
                    {
                        subscription.Category = proposal.CategoryName;
                    }

                    updated++;
                }
            }

            Bucket? bucket = null;
            if (proposal.CreateBucket)
            {
                bucket = buckets.FirstOrDefault(b =>
                    string.Equals(b.Name, proposal.MerchantName, StringComparison.OrdinalIgnoreCase));

                var categoryId = categories
                    .FirstOrDefault(c => string.Equals(c.Name, proposal.CategoryName, StringComparison.OrdinalIgnoreCase))
                    ?.Id;

                if (bucket is null)
                {
                    bucket = new Bucket
                    {
                        Name = proposal.MerchantName,
                        AllocatedAmount = proposal.MonthlyAllocation,
                        CategoryId = categoryId,
                        SortOrder = buckets.Count
                    };
                    db.Buckets.Add(bucket);
                    buckets.Add(bucket);
                    bucketsCreated++;
                    await db.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    bucket.AllocatedAmount = proposal.MonthlyAllocation;
                    if (bucket.CategoryId is null)
                    {
                        bucket.CategoryId = categoryId;
                    }
                }
            }

            foreach (var tx in transactions)
            {
                var key = MerchantNormalizer.Normalize(tx.Description, TransactionType.Expense).NormalizedKey;
                if (!string.Equals(key, proposal.NormalizedKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (subscription is not null)
                {
                    tx.SubscriptionId = subscription.Id;
                }

                if (bucket is not null)
                {
                    tx.BucketId = bucket.Id;
                }
            }
        }

        return (created, updated, bucketsCreated);
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

    private static string BuildFingerprint(ImportCandidate candidate)
    {
        var raw = $"{candidate.DateUtc:yyyyMMdd}|{candidate.Amount:F2}|{candidate.Type}|{candidate.Description.ToUpperInvariant()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..32];
    }
}

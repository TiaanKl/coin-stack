var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.FinanceManager>("FinanceManager");

builder.Build().Run();

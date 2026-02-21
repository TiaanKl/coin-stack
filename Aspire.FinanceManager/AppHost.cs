var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.CoinStack>("CoinStack");

builder.Build().Run();

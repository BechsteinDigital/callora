return await Callora.Host.Cli.Application.CalloraCliApplication.RunAsync(
    args,
    Console.Out,
    Console.Error,
    Directory.GetCurrentDirectory(),
    CancellationToken.None).ConfigureAwait(false);

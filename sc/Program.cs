using sc;

IHost host = Host.CreateDefaultBuilder(args)
   .UseWindowsService()
   .UseSystemd()
   .ConfigureAppConfiguration(conf =>
   {
       conf.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
   })
   .ConfigureServices(services =>
   {
       services.AddHostedService<ServiceManager>();
   })
   .ConfigureLogging(
       logging =>
       {
           logging.ClearProviders();
           logging.AddConsole();
           logging.AddDebug();
           logging.AddEventSourceLogger();

       })
   .Build();
await host.RunAsync(Token.mytoken.Token);

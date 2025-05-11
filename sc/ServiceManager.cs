using System.Diagnostics;

namespace sc
{


    public class WorkerConfigDetails
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? AppPath { get; set; }
        public string? AppParams { get; set; }
        public int ProcessId { get; set; }

    }

    public class RestartDetails
    {
        public bool? RestartAppAutomatically { get; set; }
        public int RestartDelay { get; set; }
    }

     
    public class ServiceManager : BackgroundService
    {
        private readonly ILogger<ServiceManager> _logger;
        public IConfigurationRoot Configuration { get; set; }

        public ServiceManager(ILogger<ServiceManager> logger, IConfiguration config)
        {
            _logger = logger;
            Configuration = (IConfigurationRoot)config;
        }
        private IEnumerable<IConfigurationSection> GetWorkers()
        {
            return Configuration.GetSection("Configs:Workers").GetChildren();
        }

        private string? GetPath(IConfigurationSection workerConfig)
        {
            return workerConfig.GetValue<string>("AppPath");
        }
        private string? GetAppParams(IConfigurationSection workerConfig)
        {
            return workerConfig.GetValue<string>("AppParams");
        }
        private string? GetWorkerName(IConfigurationSection workerConfig)
        {
            return workerConfig.GetValue<string>("Name");
        }
        private string? GetWorkerDescription(IConfigurationSection workerConfig)
        {
            return workerConfig.GetValue<string>("Description");
        }
        public string? GetFileName(string path)
        {
            if (CheckPath(path))
            {
                return Path.GetFileName(path);
            }
            else
            {
                return null; // Explicitly return null for clarity
            }
        }
        private bool CheckPath(string path)
        {
            return File.Exists(path);
        }
        private static int StartProcess(string filePath, string appParams)
        {
            
            string fileExtension = Path.GetExtension(filePath);
            int processId = -1; // Default value for processId

            if (fileExtension.ToLower() == ".ps1")
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-File \"{filePath}\" {appParams}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true

                };

                using (var process = new Process { StartInfo = processInfo })
                {
                    process.Start();
                    processId = process.Id; // Capture the process ID
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();                    
                }
            }
            else
            {
                var process = new Process();

                // For other file types, use the default process start
                if (!string.IsNullOrEmpty(appParams))
                {
                    process.StartInfo.FileName = filePath;
                    process.StartInfo.Arguments = appParams;
                }
                else
                {
                    process.StartInfo.FileName = filePath;
                }
                process.Start();
                processId = process.Id;
            }
            return processId; // Return the process ID
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
#if (DEBUG)
            Debugger.Launch();
#endif
            Process myProcess;
            List<RestartDetails> restartDetails = new List<RestartDetails>();
            List<WorkerConfigDetails> workers = new List<WorkerConfigDetails>();

            // Use object initializer syntax to populate RestartDetails properties
            restartDetails.Add(new RestartDetails
            {
                RestartAppAutomatically = bool.TryParse(Configuration.GetSection("Configs:RestartAppAutomatically")?.Value, out bool restartAutomatically) ? restartAutomatically : false,
                RestartDelay = int.TryParse(Configuration.GetSection("Configs:RestartDelay")?.Value, out int delay) ? delay : 0
            });

            try
            {
                foreach (var workerConfig in GetWorkers())
                {

                    var worker = new WorkerConfigDetails
                    {
                        Name = GetWorkerName(workerConfig),
                        Description = GetWorkerDescription(workerConfig),
                        AppPath = GetPath(workerConfig),
                        AppParams = GetAppParams(workerConfig)
                    };

                    if (worker.AppPath == null)
                    {
                        _logger.LogError("File path is null. Skipping this worker.");
                        continue;
                    }
                    _logger.LogInformation($"Starting {worker.Description}");

                    worker.ProcessId = StartProcess(worker.AppPath, worker.AppParams ?? string.Empty);
                    if (worker.ProcessId == -1)

                        _logger.LogInformation($"Started {worker.Description} with Process ID: {worker.ProcessId}");

                    workers.Add(worker);

                    await Task.Delay(1000, stoppingToken);
                }

                while (!stoppingToken.IsCancellationRequested)
                {

                    var restart = restartDetails.FirstOrDefault();
                    foreach (var worker in workers)
                    {
                        try
                        {
                            myProcess = Process.GetProcessById(worker.ProcessId);
                            await Task.Delay(TimeSpan.FromSeconds(1));

                        }
                        catch(Exception ex)
                        {
                            if (restart?.RestartAppAutomatically == true)
                            {
                                await Task.Delay(TimeSpan.FromMilliseconds(restart.RestartDelay));
                                _logger.LogWarning($"Process {worker.Description} with ID {worker.ProcessId} not found. Restarting...");
                                worker.ProcessId = StartProcess(worker.AppPath, worker.AppParams ?? string.Empty);
                                _logger.LogInformation($"Restarted {worker.Description} with Process ID: {worker.ProcessId}");
                                continue;
                            }
                            else
                            {
                                Token.mytoken.Cancel();
                                _logger.LogInformation("Process stopped");
                                _logger.LogError($"An error occurred while starting the process: {ex.Message}");
                                break; //"fileName" process stopped so service is also stopped
                            }

                            
                        }
                    }
                }
                _logger.LogInformation("Service Manager is stopping...");

                foreach (var worker in workers)
                {
                    myProcess = Process.GetProcessById(worker.ProcessId);
                    myProcess.Kill();
                    _logger.LogInformation($"Stopped {worker.Description} with Process ID: {worker.ProcessId}");
                    
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while stopping the process: {ex.Message}");
            }
         }

     }
        
   
};

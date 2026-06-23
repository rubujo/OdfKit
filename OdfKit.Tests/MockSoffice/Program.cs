using System;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace MockSoffice
{
    class Program
    {
        static int Main(string[] args)
        {
            string? outDir = null;
            string format = "pdf";
            bool simulateTimeout = false;
            bool simulateError = false;

            bool holdLock = false;
            string? lockPath = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--outdir" && i + 1 < args.Length)
                {
                    outDir = args[i + 1];
                }
                else if (args[i] == "--convert-to" && i + 1 < args.Length)
                {
                    format = args[i + 1];
                }
                else if (args[i].Contains("simulate-timeout"))
                {
                    simulateTimeout = true;
                }
                else if (args[i].Contains("simulate-error"))
                {
                    simulateError = true;
                }
                else if (args[i] == "--hold-lock" && i + 1 < args.Length)
                {
                    holdLock = true;
                    lockPath = args[i + 1];
                }
            }

            if (holdLock && !string.IsNullOrEmpty(lockPath))
            {
                try
                {
                    using (var fs = new FileStream(lockPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        fs.WriteByte(1);
                        fs.Flush();
                        Thread.Sleep(10000); // Hold lock for 10 seconds
                    }
                }
                catch { }
                return 0;
            }

            if (simulateTimeout)
            {
                // Spawn a child process to simulate soffice.bin holding lock
                if (!string.IsNullOrEmpty(outDir))
                {
                    try
                    {
                        Directory.CreateDirectory(outDir);
                        string childLockPath = Path.Combine(outDir, "child.lock");
                        string? processPath = Environment.ProcessPath;
                        if (!string.IsNullOrEmpty(processPath))
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = processPath,
                                Arguments = $"--hold-lock \"{childLockPath}\"",
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            Process.Start(psi);
                        }
                    }
                    catch { }
                }

                Thread.Sleep(5000); // Sleep to trigger timeout (test timeout is shorter, e.g. 1s)
            }

            if (simulateError)
            {
                Console.Error.WriteLine("Simulated soffice error.");
                return 1;
            }

            if (!string.IsNullOrEmpty(outDir))
            {
                Directory.CreateDirectory(outDir);
                
                // Dump arguments to file for test verification
                string argsPath = Path.Combine(outDir, "arguments.txt");
                File.WriteAllLines(argsPath, args);

                string filePath = Path.Combine(outDir, "document." + format);
                if (format.StartsWith("pdf"))
                {
                    File.WriteAllText(filePath, "%PDF-1.4\n%mock pdf");
                }
                else
                {
                    File.WriteAllText(filePath, "mock image content");
                }
            }

            if (format != null && format.Contains("delay"))
            {
                Thread.Sleep(2000);
            }
            else if (!string.IsNullOrEmpty(outDir))
            {
                // 在非 Windows 平台（如 Linux CI）上，唯讀開啟檔案並不會鎖定檔案以阻止刪除（unlink）。
                // 我們在此稍作延遲，以確保測試的背景監聽器（watcherTask）有足夠時間在目錄被清理前讀取 arguments.txt。
                Thread.Sleep(500);
            }

            return 0;
        }
    }
}

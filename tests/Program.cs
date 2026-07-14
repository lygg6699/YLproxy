using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Text.Json;
using YLproxy.Core;
using YLproxy.Models;
using YLproxy.Proxy;

namespace YLproxy.Test;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("Task 7.8 - Proxy Process Crash Detection Test");
        Console.WriteLine("========================================");
        Console.WriteLine();

        Console.WriteLine("Objective: Verify system detects proxy process crash within 5 seconds");
        Console.WriteLine();

        // Create test proxies
        var proxies = new List<ProxyItem>
        {
            new()
            {
                Id = 1,
                Name = "Test Proxy 1",
                RemoteHost = "107.150.105.8",
                RemotePort = 998,
                Username = "643fc92ab257",
                Password = "qzpx3yztmnao3yjhcqyk",
                LocalHost = "127.0.0.1",
                LocalPort = 9001,
                Status = ProxyStatus.Stopped,
                CreateTime = DateTime.UtcNow
            },
            new()
            {
                Id = 2,
                Name = "Test Proxy 2",
                RemoteHost = "107.150.105.8",
                RemotePort = 1309,
                Username = "c00b86fa9973",
                Password = "qzpx3yztmnao3yjhcqyk",
                LocalHost = "127.0.0.1",
                LocalPort = 9002,
                Status = ProxyStatus.Stopped,
                CreateTime = DateTime.UtcNow
            }
        };

        var logs = new List<string>();

        // Action functions for MonitorService
        Func<IReadOnlyList<ProxyItem>> getProxies = () => proxies.AsReadOnly();
        Action<string> logAction = (msg) =>
        {
            Console.WriteLine(msg);
            logs.Add(msg);
        };
        Action refreshAction = () =>
        {
            Console.WriteLine("[MONITOR] Proxies refreshed");
        };

        Console.WriteLine("Step 1: Starting proxies...");
        Console.WriteLine();

        try
        {
            // Start proxies
            foreach (var proxy in proxies)
            {
                proxy.Status = ProxyStatus.Stopped;
                ProxyProcessManager.Start(proxy);
                proxy.Status = ProxyStatus.Running;
                Console.WriteLine($"[SUCCESS] {proxy.Name} started (PID tracking active)");
            }

            Console.WriteLine();
            Console.WriteLine("Step 2: Verifying 3proxy processes...");
            Console.WriteLine();

            // Verify processes are running
            foreach (var proxy in proxies)
            {
                bool isRunning = ProxyProcessManager.IsRunning(proxy);
                Console.WriteLine($"  {proxy.Name}: {(isRunning ? "RUNNING" : "STOPPED")} - Status: {proxy.Status}");
            }

            Console.WriteLine();
            Console.WriteLine("Step 3: Initializing MonitorService...");
            Console.WriteLine();

            // Start MonitorService
            using var monitor = new MonitorService(getProxies, logAction, refreshAction);

            Console.WriteLine("[OK] MonitorService initialized (5-second monitoring interval)");
            Console.WriteLine();

            Console.WriteLine("Step 4: Waiting for initial monitor cycle...");
            Thread.Sleep(1000);
            Console.WriteLine();

            // Get 3proxy process for manual termination
            var processesToKill = Process.GetProcessesByName("3proxy");
            if (processesToKill.Length == 0)
            {
                Console.WriteLine("[ERROR] No 3proxy processes found!");
                return;
            }

            Console.WriteLine($"[FOUND] {processesToKill.Length} 3proxy process(es) found");
            foreach (var p in processesToKill)
            {
                Console.WriteLine($"  - PID {p.Id}: {p.ProcessName}");
            }

            Console.WriteLine();
            Console.WriteLine("Step 5: Executing Crash Test");
            Console.WriteLine();

            var killTime = DateTime.Now;
            Console.WriteLine($"KILL TIME (T0): {killTime:HH:mm:ss.fff}");

            // Force kill processes
            Console.WriteLine("Terminating 3proxy.exe processes...");
            foreach (var p in processesToKill)
            {
                try
                {
                    p.Kill(true);
                    Console.WriteLine($"  [KILLED] PID {p.Id}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [ERROR] Failed to kill PID {p.Id}: {ex.Message}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("Step 6: Monitoring for Status Update");
            Console.WriteLine();
            Console.WriteLine("Waiting for MonitorService to detect crash...");
            Console.WriteLine();

            // Wait and monitor
            DateTime? updateTime = null;
            for (int i = 0; i <= 7; i++)
            {
                Console.Write($"  T+{i} seconds");

                // Check if status changed to Failed
                bool allFailed = true;
                foreach (var proxy in proxies)
                {
                    if (proxy.Status != ProxyStatus.Failed)
                    {
                        allFailed = false;
                        break;
                    }
                }

                if (allFailed && i > 0)
                {
                    updateTime = DateTime.Now;
                    Console.WriteLine(" - STATUS UPDATED TO FAILED!");
                    break;
                }
                else if (i == 0)
                {
                    Console.WriteLine(" - (baseline check)");
                }
                else
                {
                    Console.WriteLine(" - Still monitoring...");
                }

                Thread.Sleep(1000);
            }

            Console.WriteLine();
            Console.WriteLine("Step 7: Test Results");
            Console.WriteLine();

            if (updateTime.HasValue)
            {
                var delay = (updateTime.Value - killTime).TotalSeconds;
                Console.WriteLine($"Kill Time (T0): {killTime:HH:mm:ss.fff}");
                Console.WriteLine($"Update Time (T1): {updateTime:HH:mm:ss.fff}");
                Console.WriteLine($"Detection Delay: {delay:F3} seconds");
                Console.WriteLine();

                if (delay >= 0 && delay <= 5)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"PASS: Detected crash within 5 seconds (delay: {delay:F3}s)");
                    Console.ResetColor();
                }
                else if (delay < 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("ERROR: Status updated before process was killed!");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"FAIL: Delay {delay:F3}s exceeds 5-second requirement");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAIL: Status was not updated to Failed within 7 seconds");
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.WriteLine("Final Status:");
            foreach (var proxy in proxies)
            {
                Console.WriteLine($"  {proxy.Name}: {proxy.Status}");
            }

            Console.WriteLine();
            Console.WriteLine("Logs:");
            foreach (var log in logs)
            {
                Console.WriteLine($"  {log}");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Test failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("Test Complete");
        Console.WriteLine("========================================");
    }
}

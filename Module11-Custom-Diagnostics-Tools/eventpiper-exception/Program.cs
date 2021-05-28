using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Threading;

namespace eventpiper
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Missing argument: process to start or PID");
                return;
            }
            using var cts = new CancellationTokenSource();

            int.TryParse(args[0], out var pid);
            var (proc, client) = pid == 0 ? StartNewProcess(args) : AttachToProcess(pid);

            var providers = new[] {
                new EventPipeProvider("Microsoft-Windows-DotNETRuntime", EventLevel.Informational, 0x8000) // exceptions only
            };

            using var session = client.StartEventPipeSession(providers, false /* no rundown events */);

            Console.CancelKeyPress += (o, ev) => { ev.Cancel = true; session.Stop(); };

            if (pid == 0) {
                UnofficialDiagnosticsClientApi.ResumeRuntime(client);
            }

            using var eventSource = new EventPipeEventSource(session.EventStream);

            eventSource.Clr.ExceptionStart += Clr_ExceptionStart;

            eventSource.Process();
        }

        static (Process process, DiagnosticsClient client) AttachToProcess(int pid)
        {
            return (Process.GetProcessById(pid), new DiagnosticsClient(pid));
        }

        static (Process process, DiagnosticsClient client) StartNewProcess(string[] args)
        {
            var diagPortName = $"eventpiper-{Process.GetCurrentProcess().Id}-{DateTime.Now:yyyyMMdd_HHmmss}.socket";
            var server = UnofficialDiagnosticsClientApi.CreateReversedServer(diagPortName);

            UnofficialDiagnosticsClientApi.Start(server);

            var startInfo = new ProcessStartInfo(args[0], string.Join(' ', args, 1, args.Length - 1)) {
                UseShellExecute = false,
                CreateNoWindow = false
            };
            startInfo.Environment.Add("DOTNET_DiagnosticPorts", diagPortName);

            var proc = Process.Start(startInfo);
            var client = UnofficialDiagnosticsClientApi.WaitForProcessToConnect(server, proc.Id);

            return (proc, client);
        }
        public static void Clr_ExceptionStart(ExceptionTraceData ev)
        {
            Console.WriteLine($"Exception event: [{ev.ExceptionType}] '{ev.ExceptionMessage}'");
        }
    }
}

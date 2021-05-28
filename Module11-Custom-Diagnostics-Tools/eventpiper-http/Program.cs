using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
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
                new EventPipeProvider("Microsoft-Diagnostics-DiagnosticSource", EventLevel.Verbose, 0x3L,
                    new Dictionary<string, string>() {
                        ["FilterAndPayloadSpecs"] = "HttpHandlerDiagnosticListener/System.Net.Http.Request:-Request.RequestUri;Request.Method;LoggingRequestId\n" +
                                                    "HttpHandlerDiagnosticListener/System.Net.Http.Response:-Response.StatusCode;LoggingRequestId"
                })
            };

            using var session = client.StartEventPipeSession(providers, false);

            Console.CancelKeyPress += (o, ev) => { ev.Cancel = true; session.Stop(); };

            if (pid == 0)
            {
                UnofficialDiagnosticsClientApi.ResumeRuntime(client);
            }

            using var eventSource = new EventPipeEventSource(session.EventStream);

            eventSource.Dynamic.All += OnEvent;

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

        public static void OnEvent(TraceEvent ev)
        {
            if (ev.EventName == "Event")
            {
                var diagSourceEventName = ev.PayloadStringByName("EventName");
                var args = (IDictionary<string, object>[])ev.PayloadByName("Arguments");

                var requestId = (string)args.Single(d => (string)d["Key"] == "LoggingRequestId")["Value"];
                if (diagSourceEventName == "System.Net.Http.Request")
                {
                    var uri = (string)args.Single(d => (string)d["Key"] == "RequestUri")["Value"];
                    var method = (string)args.Single(d => (string)d["Key"] == "Method")["Value"];
                    Console.WriteLine($"{ev.TimeStampRelativeMSec:0.000}ms Request#{requestId} - {method} {uri}");
                }
                else
                {
                    // diagSourceEventName == "System.Net.Http.Response"
                    var statusCode = (string)args.Single(d => (string)d["Key"] == "StatusCode")["Value"];
                    Console.WriteLine($"{ev.TimeStampRelativeMSec:0.000}ms Response#{requestId} - {statusCode}");
                }
            }
        }
    }
}

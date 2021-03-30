using System;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace requestapp
{
    class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetConsoleOutputCP(int codePage);


        static async Task Main(string[] args)
        {
            if (!SetConsoleOutputCP(852)) { throw new Win32Exception(); }

            using var client = new HttpClient();

            var resp = await client.GetAsync("https://asyncexpert.com/");

            Console.WriteLine(await resp.Content.ReadAsStringAsync());
        }
    }
}

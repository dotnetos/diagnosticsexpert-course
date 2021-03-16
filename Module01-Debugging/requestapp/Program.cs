using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace requestapp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using var client = new HttpClient();

            var resp = await client.GetAsync("https://asyncexpert.com/");

            Console.WriteLine(await resp.Content.ReadAsStringAsync());
        }
    }
}

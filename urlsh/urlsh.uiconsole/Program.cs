using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using urlsh.data.sqlite;
using urlsh.manager;
//
namespace urlsh.uiconsole
{
    class Program
    {
        public static string Ask(string text)
        {
            Console.Out.Write(text);
            string input = Console.ReadLine();
            return input;
        }
        static void Main(string[] args)
        {
            new App().MainMenu(
                new ICommand[]
                {
                    new DeleteAllUrls()
                });
        }
    }
}

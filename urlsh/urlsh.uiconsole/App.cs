using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using urlsh.data.sqlite;

namespace urlsh.uiconsole
{
    public class App
    {
        public string GuidToBase64(Guid guid)
        {
            return Convert.ToBase64String(guid.ToByteArray());
        }

        public Guid Base64ToGuid(string base64)
        {
            Guid guid = default(Guid);

            try
            {
                guid = new Guid(Convert.FromBase64String(base64));
            }
            catch (Exception ex)
            {
                throw new Exception("Bad Base64 conversion to GUID", ex);
            }

            return guid;
        }
        public static string Ask(string text)
        {
            Console.Out.Write(text);
            string input = Console.ReadLine();
            return input;
        }
        public void MainMenu(ICommand[] commands)
        {
            var builder = new ConfigurationBuilder()
.SetBasePath(Directory.GetCurrentDirectory())
#if DEBUG
            .AddJsonFile("appsettings.Debug.json");
#else
            .AddJsonFile("appsettings.Release.json");
#endif
            var configuration = builder.Build();
            RepoUrl repoUrl = new RepoUrl(configuration["connectionString"]);

            int commandIndex = -1;
            do
            {
                Console.WriteLine($"ZipMy.Link Console {Environment.UserName} on {configuration["connectionString"]}");
                Console.WriteLine("What do you want to do?");
                // This loop creates a list of commands:
                for (int i = 0; i < commands.Length; i++)
                    Console.WriteLine("{0}. {1}", i + 1, commands[i].Description);
                Console.WriteLine("0. to quit.");

                int.TryParse(Console.ReadLine(), out commandIndex);
                //tryGetInt(out commandIndex);
                if (commandIndex > 0 && commandIndex <= commands.Length)
                {
                    Console.Clear();
                    commands[commandIndex - 1].Execute(repoUrl);
                    Console.Out.WriteLine("DONE.");
                }
                while (Console.KeyAvailable)//flush input
                    Console.ReadKey(true);
            } while (commandIndex != 0);
        }
    }
}

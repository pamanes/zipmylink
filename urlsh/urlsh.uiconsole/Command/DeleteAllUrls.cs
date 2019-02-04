using System;
using System.Collections.Generic;
using System.Text;
using urlsh.data.sqlite;

namespace urlsh.uiconsole
{
    public class DeleteAllUrls : ICommand
    {
        public string Description => "Delete ALL URLS";
        public void Execute(RepoUrl repo_url)
        {
            try
            {
                repo_url.DeleteAll();
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine(ex.Message);
            }
        }
    }
}

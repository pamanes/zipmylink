using System;
using System.Collections.Generic;
using System.Text;
using urlsh.data.sqlite;

namespace urlsh.uiconsole
{
    public interface ICommand
    {
        string Description { get; }
        void Execute(RepoUrl repo_url);
    }
}

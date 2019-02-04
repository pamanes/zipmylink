using System;
using urlsh.data.sqlite;

namespace urlsh.manager
{
    public class Shortener
    {
        string ConnectionString = string.Empty;
        ShortenerEngine se;
        public Shortener(string conn_string, string salt)
        {
            ConnectionString = conn_string;
            se = new ShortenerEngine(salt);
        }

        public void TestConnect()
        {
            RepoUrl repoURL = new RepoUrl(ConnectionString);
            repoURL.TestConnect();
        }
        public string Shorten(string url)
        {
            Uri uri = null;
            if (string.IsNullOrEmpty(url) || url.Length > 4000 || !Uri.TryCreate(url, UriKind.Absolute, out uri) || null == uri || (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                throw new Exception("Bad input!");
            }
            else
            {
                RepoUrl repoURL = new RepoUrl(ConnectionString);
                long id = repoURL.GetOrInsertUrl(url.Trim());
                if(id == 0)
                {
                    throw new Exception("DB Error!");
                }
                return se.Encode(id);
            }
        }

        public string GetUrl(string url_short)
        {
            if(string.IsNullOrEmpty(url_short) || url_short.Length > 4000)
            {
                throw new Exception("Bad input!");
            }
            long id = se.Decode(url_short);
            RepoUrl repoURL = new RepoUrl(ConnectionString);
            string url = repoURL.GetUrlById(id);
            return url;
        }
    }
}

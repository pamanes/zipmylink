using System;
using System.Data.Common;
using System.Data.SQLite;

namespace urlsh.data.sqlite
{
    public class RepoUrl
    {
        string ConnectionString = string.Empty;
        public RepoUrl(string conn_string)
        {
            ConnectionString = conn_string;
        }
        public void TestConnect()
        {
            using (SQLiteConnection con = new SQLiteConnection(ConnectionString))
            {
                con.Open();
                con.Close();
            }
        }

        public void DeleteAll()
        {
            SQLiteDataLayer dl = new SQLiteDataLayer(ConnectionString);
            try
            {
                dl.BeginTransaction();
                using (DbCommand cmd = dl.PrepareCommandPQ("delete from urls;"))
                    cmd.ExecuteNonQuery();
                dl.Commit();
            }
            catch
            {
                dl.Rollback();
                throw;
            }
        }

        public void InsertUrl(string url)
        {
            SQLiteDataLayer dl = new SQLiteDataLayer(ConnectionString);
            try
            {
                dl.BeginTransaction();
                    try
                    {
                        using (DbCommand cmd = dl.PrepareCommandPQ("insert into urls(url, created) select @url,STRFTIME('%Y-%m-%d %H:%M:%f', 'NOW');", dl.CreateParam("@url", url)))
                            cmd.ExecuteNonQuery();
                    }
                    catch(Exception ex)
                    {
                        if(!ex.Message.Contains("UNIQUE constraint failed: urls.url"))
                        {
                            throw;
                        }
                    }
                dl.Commit();
            }
            catch
            {
                dl.Rollback();
                throw;
            }
        }
        
        public string GetUrlById(long id)
        {
            string url = string.Empty;
            SQLiteDataLayer dl = new SQLiteDataLayer(ConnectionString);
            try
            {
                dl.BeginTransaction();
                using (DbCommand cmd = dl.PrepareCommandPQ("select url from urls where id = @id;", dl.CreateParam("@id", id)))
                {
                    using (DbDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            url = (string)dr["url"];
                        }
                    }
                }
                dl.Commit();
            }
            catch
            {
                dl.Rollback();
                throw;
            }
            return url;
        }
        /// <summary>
        /// gets id (PK from table)
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public long GetOrInsertUrl(string url)
        {
            long id = 0;
            SQLiteDataLayer dl = new SQLiteDataLayer(ConnectionString);
            try
            {
                dl.BeginTransaction();
                using (DbCommand cmd = dl.PrepareCommandPQ("select id from urls where url = @url;", dl.CreateParam("@url", url)))
                {
                    using (DbDataReader dr = cmd.ExecuteReader())
                    {
                        if(dr.Read())
                        {
                            id = (long)dr["id"];
                        }
                    }
                }
                if(id == 0)
                {
                    try
                    {
                        using (DbCommand cmd = dl.PrepareCommandPQ("insert into urls(url, created) select @url,STRFTIME('%Y-%m-%d %H:%M:%f', 'NOW') where not exists(select * from urls where url = @url);", dl.CreateParam("@url", url)))
                            cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        //solve for potential concurrency issues
                        if (!ex.Message.Contains("UNIQUE constraint failed: urls.url"))
                        {
                            throw;
                        }
                    }
                    using (DbCommand cmd = dl.PrepareCommandPQ("select id from urls where url = @url;", dl.CreateParam("@url", url)))
                    {
                        using (DbDataReader dr = cmd.ExecuteReader())
                        {
                            if (dr.Read())
                            {
                                id = (long)dr["id"];
                            }
                        }
                    }
                }    
                else
                {
                    using (DbCommand cmd = dl.PrepareCommandPQ("update urls set hits = hits + 1, accessed = STRFTIME('%Y-%m-%d %H:%M:%f', 'NOW') where id = @id;", dl.CreateParam("@id", id)))
                        cmd.ExecuteNonQuery();
                }
                dl.Commit();
            }
            catch
            {                
                dl.Rollback();
                throw;
            }
            return id;
        }
    }
}

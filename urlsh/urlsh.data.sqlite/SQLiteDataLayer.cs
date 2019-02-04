using System;
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Threading.Tasks;

#region Update history
//Created by: Alejandro Palacios
//Creation date: Jan 02, 2009
//Updates:
//  added method ExecuteSQL to run queries
//  added capability of handling DbType.Text appropriately
//  added BeginTransaction overloading method that receives transaction isolation level
//  (May, 29, 2013) Alejandro Palacios added Disposable(bool) method to dispose appropriately
//  (May, 29, 2013) Alejandro Palacios modify Double.Nan to Double.IsNaN
//Comments/Suggestions: pamanes@gmail.com
#endregion

namespace urlsh.data.sqlite
{
    public class SQLiteDataLayer : IDisposable
    {
        public static Hashtable charTable;
        private object _con_trans_lock = new Object();	// for synronizing transaction object access
        SQLiteConnection _con = null;
        private SQLiteTransaction _con_trans = null;
        private ArrayList _dec2int_off = null;				// list of SQLParameters that do not auto convert Decimal to Int32 datatype
        private static string[] _rc_msg = null;				// store all DataLayer return code messages (0-100)
        private int _last_return_code;
        private double _last_execution_time;
        private static Hashtable _ht_sp_name = null;		// store all stored procedure name translation

        /// <summary>
        /// Static constructor.
        /// </summary>
        static SQLiteDataLayer()
        {
            // DataLayer return code messages (0-100)
            _rc_msg = new string[101];
            _rc_msg[0] = "The operation completed successfully.";
            _rc_msg[1] = "Error creating connection to database.";
            _rc_msg[2] = "Parameter name cannot be null.";
            _rc_msg[3] = "Parameter name cannot be empty string.";
            _rc_msg[4] = "Input parameter value cannot be null. If you want to use database NULL, use DBNull.Value instead.";
            _rc_msg[5] = "VARCHAR2 parameter length too long. Limit is 4000 characters.";
            _rc_msg[6] = "Unsupported DbType for CreateParam().";
            _rc_msg[7] = "Stored Procedure name cannot be null.";
            _rc_msg[8] = "Stored Procedure name cannot be an empty string.";
            _rc_msg[9] = "ExecuteSP parameters cannot be null.";
            _rc_msg[10] = "DataLayer was already disposed and cannot be reused again.";
            _rc_msg[11] = "Error executing stored procedure.";
            _rc_msg[12] = "A transaction has already begun.";
            _rc_msg[13] = "No open transaction.";
            _rc_msg[14] = "SQL statement cannot be null.";
            _rc_msg[15] = "SQL statement cannot be an empty string.";
            _rc_msg[16] = "ExecuteSQL parameters cannot be null.";
            _rc_msg[17] = "Error executing Dynamic SQL.";
            _rc_msg[100] = "Unknown error.";

            // hardcode all stored procedure name translation here (key must be in upper case)
            //_ht_sp_name = new Hashtable();
            //_ht_sp_name.Add("".ToUpper(), "");

            // hardcode all stored procedure name translation here (key must be in upper case)
            _ht_sp_name = new Hashtable();
            //_ht_sp_name.Add("".ToUpper(),"");
            charTable = new Hashtable();
            charTable.Add('\u2018', '\u0027'); //u2018 is left sinfo quotation mark, u0027 is apostrophe
            charTable.Add('\u2019', '\u0027'); //u2019 is right single quotation marj, u0027 is apostrophe
            charTable.Add('\u2022', '\u002E'); //u2022 is bullet, u002E is full stop (period)
            charTable.Add('\u201C', '\u0022'); //u201C is left double quotation mark, u0022 is quotarion mark (double quote)
            charTable.Add('\u201D', '\u0022'); //u201D is right double quotarion mark, u0022 is quotarion mark (double quote)

        }

        public SQLiteDataLayer(string connection_string)
        {
            //when the object is instantiated, we need to initialize connection.
            // setup database connection
            _con = new SQLiteConnection(connection_string);
            _dec2int_off = new ArrayList();
        }
        /// <summary>
        /// Return an input parameter for a VARCHAR2 with the specified name.
        /// </summary>
        /// <param name="name">IN parameter name</param>
        /// <param name="value">string object.
        ///	string must be less than or equal to 4000 chars due to database limit.
        ///	DBNull.Value will be assumed if value=null.
        /// </param>
        /// <returns>SQL parameter</returns>
        /// 
        public SQLiteParameter CreateParam(string name, string value)
        {
            // check if length is too long for this datatype
            if (value != null && value.Length > 4000)
            {
                _last_return_code = 5;
                throw new ArgumentException(_rc_msg[5] + " Parameter name '" + name + "'.");
            }

            // check for null and use DBNull.Value instead
            object obj = value;
            int length = 0;
            if (value == null)
                obj = DBNull.Value;
            else
                length = Math.Max(value.Length, 1);


            return CreateParam(name, obj, length, DbType.String, ParameterDirection.Input);
        }

        /// <summary>
        /// Return an input parameter for a NUMBER with the specified name.
        /// </summary>
        /// <param name="name">IN parameter name</param>
        /// <param name="value">int value</param>
        /// <returns>SQL parameter</returns>
        public SQLiteParameter CreateParam(string name, int value)
        {
            return CreateParam(name, Convert.ToDecimal(value), 0, DbType.Int32, ParameterDirection.Input);
        }

        public SQLiteParameter CreateParam(string name, long value)
        {
            return CreateParam(name, value, 0, DbType.Int64, ParameterDirection.Input);
        }
        /// <summary>
        /// Return an input parameter for a NUMBER with the specified name.
        /// </summary>
        /// <param name="name">IN parameter name</param>
        /// <param name="value">Decimal value</param>
        /// <returns>SQL parameter</returns>
        public SQLiteParameter CreateParam(string name, Decimal value)
        {
            return CreateParam(name, value, 0, DbType.Decimal, ParameterDirection.Input);
        }
        /// <summary>
        /// Return an input parameter for a DOUBLE with the specified name.
        /// </summary>
        /// <param name="name">IN parameter name</param>
        /// <param name="value">double value.
        ///	DBNull.Value will be assumed if value=Double.NaN.
        ///	</param>
        /// <returns>SQL parameter</returns>
        public SQLiteParameter CreateParam(string name, double value)
        {
            // check for Double.NaN and use DBNull.Value instead
            object obj = value;
            //if (value == Double.NaN) replaced by Alejandro Palacios, May, 29, 2012
            if (Double.IsNaN(value))
                obj = DBNull.Value;

            return CreateParam(name, obj, 0, DbType.Double, ParameterDirection.Input);
        }
        /// <summary>
        /// Return an output parameter corresponding to the specified type with the specified name.
        /// You can also specify if all Decimal in this parameter shall be converted into Int32.
        /// </summary>
        /// <param name="name">OUT parameter name.</param>
        /// <param name="type">SqlDbTypes supported are Number</param>
        /// <param name="dec2int">Value indicating whether to convert all Decimal values to Int32</param>
        /// <returns>SQL parameter</returns>
        public DbParameter CreateParam(string name, DbType type, bool dec2int)
        {
            // check for supported SqlDbType
            if (type != DbType.Decimal)
            {
                _last_return_code = 6;
                throw new ArgumentException(_rc_msg[6] + " Parameter name '" + name + "'.");
            }

            DbParameter op = CreateParam(name, null, 4000, type, ParameterDirection.Output);
            if (dec2int == false)
                _dec2int_off.Add(op);
            return op;
        }
        /// <summary>
        /// Return a parameter for the specified name and type.
        /// Note1: for output parameters, please set value to null.
        /// Note2: please first consider using the simplier overloaded versions instead.
        /// </summary>
        /// <param name="name">parameter name</param>
        /// <param name="value">input value (use DBNull.Value for database NULL input; use null for output value)</param>
        /// <param name="type">SQL type info</param>
        /// <param name="direction">parameter direction is input/output or both</param>
        /// <returns>SQL parameter</returns>
        public SQLiteParameter CreateParam(string name, object value, int size, DbType type, ParameterDirection direction)
        {
            // make sure method parameters are workable
            if (name == null)
            {
                _last_return_code = 2;
                throw new ArgumentNullException(_rc_msg[2]);
            }

            if (name == "")
            {
                _last_return_code = 3;
                throw new ArgumentException(_rc_msg[3]);
            }

            if (direction == ParameterDirection.Input && value == null)
            {
                _last_return_code = 4;
                throw new ArgumentNullException(_rc_msg[4] + " Parameter name '" + name + "'.");
            }

            // set output parameters
            _last_return_code = 100;	// in case any unknown error occur (remote possibility)
            SQLiteParameter op = new SQLiteParameter();
            op.ParameterName = name;
            op.Value = value;
            op.DbType = type;
            op.Size = size;
            op.Direction = direction;
            _last_return_code = 0;
            return op;
        }

        /// <summary>
        /// Return an input parameter for a DATETIME with the specified name.
        /// </summary>
        /// <param name="name">IN parameter name</param>
        /// <param name="value">datetime object.
        ///	DBNull.Value will be assumed if value=null.
        ///	</param>
        /// <returns>SQL parameter</returns>
        public SQLiteParameter CreateParam(string name, DateTime value)
        {
            // check for null and use DBNull.Value instead
            object obj = value;
            if (obj == null)
                obj = DBNull.Value;

            return CreateParam(name, obj, 0, DbType.DateTime, ParameterDirection.Input);
        }
        /// <summary>
        /// This method changes the special character so logical characters which the database is 
        /// capable of storing. Need to remove it if the database starts storing Unicode characters
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private object ChangeCharacters(object input)
        {
            //char ch = '\u2019';
            char[] chars = input.ToString().ToCharArray();
            for (int i = 0; i < chars.Length; ++i)
            {
                if (charTable.ContainsKey(chars[i]))
                    chars[i] = (char)charTable[chars[i]];
            }

            return new string(chars);
        }

        /// <summary>
        /// Error code from the last DataLayer CreateParam() or Execute*() call
        /// </summary>
        public int last_return_code
        {
            get
            {
                return _last_return_code;
            }
        }

        /// <summary>
        /// Execution time in milliseconds from the last DataLayer Execute*() call
        /// </summary>
        public double last_execution_time
        {
            get
            {
                return _last_execution_time;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        public DbCommand PrepareCommandPQ(string sql, params SQLiteParameter[] parameters)
        {
            // for recording execution time
            DateTime datetime = DateTime.Now;

            // check if there is an open transaction
            if (_con_trans == null)
            {
                _last_return_code = 13;
                throw new InvalidOperationException(_rc_msg[13]);
            }

            // make sure sql is valid, parameters is not null and current instance is not disposed
            if (sql == null)
            {
                _last_return_code = 14;
                throw new ArgumentNullException(_rc_msg[14]);
            }

            if (sql == "")
            {
                _last_return_code = 15;
                throw new ArgumentException(_rc_msg[15]);
            }
            /*
            if (parameters == null)
            {
                _last_return_code = 16;
                throw new ArgumentNullException(_rc_msg[16]);
            }
            */
            if (_con == null)
            {
                _last_return_code = 10;
                throw new ObjectDisposedException(_rc_msg[10]);
            }

            _last_return_code = 100;	// in case any unknown error occur (remote possibility)

            // create the command
            DbCommand cmd = _con.CreateCommand();
            cmd.CommandTimeout = 300;//5 mins before it times out
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;
            cmd.Transaction = _con_trans;

            foreach (SQLiteParameter op in parameters)
            {
                if (op == null)
                {
                    _last_return_code = 9;
                    throw new ArgumentNullException(_rc_msg[9]);
                }

                // add the parameter
                cmd.Parameters.Add(op);
            }

            return cmd;
        }

        public async Task BeginTransactionAsync(IsolationLevel? isolation_level = null)
        {
            if (_con == null)
                throw new ObjectDisposedException(_rc_msg[10]);

            if (_con_trans != null)
                throw new InvalidOperationException(_rc_msg[12]);

            try
            {
                await _con.OpenAsync();
                if (isolation_level.HasValue)
                    _con_trans = _con.BeginTransaction(isolation_level.Value);
                else
                    _con_trans = _con.BeginTransaction(IsolationLevel.ReadCommitted);
            }
            catch
            {
                _con_trans = null;
                _con.Close();
                throw;
            }
        }

        /// <summary>
        /// Open connection to database and begin a new transaction.
        /// </summary>
        public void BeginTransaction()
        {
            lock (this._con_trans_lock)
            {
                if (_con == null)
                    throw new ObjectDisposedException(_rc_msg[10]);

                if (_con_trans != null)
                    throw new InvalidOperationException(_rc_msg[12]);

                try
                {
                    _con.Open();
                    _con_trans = _con.BeginTransaction();
                }
                catch
                {
                    _con_trans = null;
                    _con.Close();
                    throw;
                }
            }
        }

        /// <summary>
        /// Open connection to database and begin a new transaction.
        /// </summary>
        /// <param name="isolation_level">Transaction isolation level for connection</param>
        public void BeginTransaction(IsolationLevel? isolation_level = null)
        {
            lock (this._con_trans_lock)
            {
                if (_con == null)
                    throw new ObjectDisposedException(_rc_msg[10]);

                if (_con_trans != null)
                    throw new InvalidOperationException(_rc_msg[12]);

                try
                {
                    _con.Open();
                    if (isolation_level.HasValue)
                        _con_trans = _con.BeginTransaction(isolation_level.Value);
                    else
                        _con_trans = _con.BeginTransaction(IsolationLevel.ReadCommitted);//DEFAULT TO READ COMMITTED
                }
                catch
                {
                    _con_trans = null;
                    _con.Close();
                    throw;
                }
            }
        }

        /// <summary>
        /// Generic method to add table(s) from a DataReader into the DataSet.
        /// Each DataReader's ResultSet will correspond to 1 DataTable inside DataSet.
        /// Note1: if the DataReader have more than 1 ResultSet, the table name of the 1st ResultSet
        /// is equal to the base_table_name. Table name for subsequence ResultSet(s) will be in the
        /// form base_table_name__1, base_table_name__2, and so on (note double underscore).
        /// Note2: if the DataSet already contains a table with the same name,
        /// it will throw a DuplicateNameException.
        /// </summary>
        /// <param name="dr">data reader to be added</param>
        /// <param name="base_table_name">table name for the first ResultSet, or base table name for ResultSets thereafter</param>
        /// <param name="ds">data set target</param>
        /// <param name="decimal2int">indicate whether to auto convert all Decimal to Int32 or not</param>
        /// <returns>number of DataTables added to DataSet</returns>
        /// 
        public static int addDataReader(IDataReader dr, string base_table_name, DataSet ds, bool decimal2int)
        {
            int count = 0;	// current number of ResultSet under process
            do
            {
                // Create new data table
                DataTable schemaTable = dr.GetSchemaTable();
                DataTable dt = new DataTable();
                dt.TableName = base_table_name;
                if (count > 0)
                    dt.TableName += "__" + count.ToString();
                count++;

                if (schemaTable != null)
                {
                    // query returning records was executed
                    for (int i = 0; i < schemaTable.Rows.Count; i++)
                    {
                        DataRow dataRow = schemaTable.Rows[i];

                        // Create a column name that is unique in the data table
                        string columnName = (string)dataRow["ColumnName"];

                        // Add the column definition to the data table
                        Type col_type = (Type)dataRow["DataType"];
                        if (decimal2int && col_type == typeof(Decimal))				// convert Decimal column to Int32 column if necessary
                            col_type = typeof(int);
                        DataColumn column = new DataColumn(columnName, col_type);
                        dt.Columns.Add(column);
                    }

                    // Fill the data table we just created
                    while (dr.Read())
                    {
                        DataRow dataRow = dt.NewRow();

                        for (int i = 0; i < dr.FieldCount; i++)
                        {
                            object obj = dr.GetValue(i);

                            if (decimal2int && obj.GetType() == typeof(Decimal))	// convert Decimal to Int32 if necessary
                                obj = Decimal.ToInt32((Decimal)obj);

                            dataRow[i] = obj;
                        }

                        dt.Rows.Add(dataRow);
                    }

                    ds.Tables.Add(dt);
                }
                else
                {
                    // No records were returned
                    DataColumn dc = new DataColumn("RowsAffected");
                    dt.Columns.Add(dc);
                    DataRow dataRow = dt.NewRow();
                    dataRow[0] = dr.RecordsAffected;
                    dt.Rows.Add(dataRow);
                    ds.Tables.Add(dt);
                }
            }
            while (dr.NextResult());
            return count;
        }

        /// <summary>
        /// Commit current transaction and close the connection.
        /// </summary>
        public void Commit()
        {
            lock (this._con_trans_lock)
            {
                if (_con == null)
                    throw new ObjectDisposedException(_rc_msg[10]);

                if (_con_trans == null)
                    throw new InvalidOperationException(_rc_msg[13]);

                try
                {
                    _con_trans.Commit();
                }
                finally
                {
                    _con_trans = null;
                    _con.Close();
                }
            }
        }

        /// <summary>
        /// Rollback current transaction and close the connection.
        /// An application can call Rollback more than one time without generating an exception.
        /// </summary>
        public void Rollback()
        {
            lock (this._con_trans_lock)
            {
                if (_con_trans != null)
                {
                    _con_trans.Rollback();
                    _con_trans = null;
                    _con.Close();
                }
            }
        }

        #region IDisposable Members
        /// <summary>
        /// using block requires the class to implement IDisposable:
        /// using (SQLDataLayer dl = new SQLDataLayer(""))
        ///{
        ///
        ///}
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_con != null)
                {
                    if (_con_trans != null)
                        Rollback();
                    _con.Dispose();
                    _con = null;
                }
            }

            //release native resources here...            
        }


        #endregion
    }
}
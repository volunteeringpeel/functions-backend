using Newtonsoft.Json.Linq;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Threading.Tasks;
using VP_Functions.Helpers;

namespace VP_Functions.API
{
  public class FancyConn : IDisposable
  {

    readonly SqlConnection conn;
    public bool Disposed = false;
    public Exception lastError;

    public FancyConn()
    {
      string connStr = Environment.GetEnvironmentVariable("sqldb_connection");
      this.conn = new SqlConnection(connStr);
      if (conn.State == ConnectionState.Closed)
        this.conn.Open();
    }

    public void Dispose()
    {
      this.Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
      if (this.Disposed) return;
      if (disposing)
      {
        this.conn.Close();
      }
      Disposed = true;
    }

    /// <summary>
    /// Shared connection instance
    /// </summary>
    public static FancyConn Shared;
    public static void EnsureShared()
    {
      if (Shared == null) Shared = new FancyConn();
      if (Shared.conn.State == ConnectionState.Closed)
        Shared.conn.Open();
    }

    /// <summary>
    /// Create a SqlCommand object of a query
    /// </summary>
    /// <param name="query">SQL query</param>
    protected SqlCommand MakeCommand(string query)
    {
      if (this.conn.State == ConnectionState.Closed)
        conn.Open();
      return new SqlCommand(query, this.conn);
    }

    public static (string, Dictionary<string, object>) MakeUpsertQuery(string table, string pk, object pkVal, List<string> cols, JObject values)
    {
      // generate query parts
      int i = 0;
      var equals = new List<string>();
      var insertCols = new List<string>();
      var insertVals = new List<string>();
      var parameters = new Dictionary<string, object>() { { "pkVal", pkVal } };
      foreach (var col in cols)
      {
        if (values.ContainsKey(col))
        {
          equals.Add($"[{col}] = @p{i.ToString()}");
          insertCols.Add($"[{col}]");
          insertVals.Add($"@p{i.ToString()}");
          parameters.Add($"@p{i.ToString()}", values[col].Value<string>());
          i++;
        }
      }
      // short-circuit if there's no valid columns to update
      if (i == 0) return (null, null);

      var query = $@"SET TRANSACTION ISOLATION LEVEL SERIALIZABLE; BEGIN TRANSACTION;
        UPDATE [{table}] SET {string.Join(", ", equals)} WHERE [{pk}] = @pkVal;
        IF @@ROWCOUNT = 0 BEGIN
          INSERT INTO [{table}]({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", insertVals)});
          SELECT SCOPE_IDENTITY();
        END; COMMIT TRANSACTION;";

      return (query, parameters);
    }

    /// <summary>
    /// Execute a SQL query that returns rows as a <see cref="SqlDataReader"/>
    /// </summary>
    /// <param name="query">SQL query to execute</param>
    /// <param name="queryParams">SQL parameters in a key-value format</param>
    public async Task<(bool err, SqlDataReader reader)> Reader(string query, Dictionary<string, object> queryParams = null)
    {
      SqlCommand cmd = null;
      try
      {
        cmd = this.MakeCommand(query);
        if (queryParams != null)
          foreach (var kv in queryParams)
          {
            var value = kv.Value ?? DBNull.Value;
            cmd.Parameters.AddWithValue(kv.Key, value);
          }
        var reader = await cmd.ExecuteReaderAsync();
        return (false, reader);
      }
      catch (SqlException e)
      {
        this.lastError = e;
        return (true, null);
      }
      finally
      {
        if (cmd != null) cmd.Dispose();
      }
    }

    /// <summary>
    /// Execute a SQL query that returns only the first value of the first row
    /// </summary>
    /// <param name="query">SQL query to execute</param>
    /// <param name="queryParams">SQL parameters in key-value format</param>
    public async Task<(bool err, object val)> Scalar(string query, Dictionary<string, object> queryParams = null)
    {
      SqlCommand cmd = null;
      try
      {
        cmd = this.MakeCommand(query);
        if (queryParams != null)
          foreach (var kv in queryParams)
          {
            var value = kv.Value ?? DBNull.Value;
            cmd.Parameters.AddWithValue(kv.Key, value);
          }
        var val = await cmd.ExecuteScalarAsync();
        return (false, val);
      }
      catch (SqlException e)
      {
        this.lastError = e;
        return (true, null);
      }
      finally
      {
        if (cmd != null) cmd.Dispose();
      }
    }

    /// <summary>
    /// Execute a SQL query that does not return any values
    /// </summary>
    /// <param name="query">SQL query to execute</param>
    /// <param name="queryParams">SQL parameters in key-value format</param>
    /// <returns>Number of rows affected</returns>
    public async Task<(bool err, int rows)> NonQuery(string query, Dictionary<string, object> queryParams = null)
    {
      SqlCommand cmd = null;
      try
      {
        cmd = this.MakeCommand(query);
        foreach (var kv in queryParams)
        {
          var value = kv.Value ?? DBNull.Value;
          cmd.Parameters.AddWithValue(kv.Key, value);
        }
        var rows = await cmd.ExecuteNonQueryAsync();
        return (false, rows);
      }
      catch (SqlException e)
      {
        this.lastError = e;
        return (true, 0);
      }
      finally
      {
        if (cmd != null) cmd.Dispose();
      }
    }

    /// <summary>
    /// Get the role level of a given user
    /// </summary>
    /// <param name="email">Email of user to lookup</param>
    /// <returns><see cref="Role"/> if user exists, <see langword="null"/> otherwise</returns>
    public async Task<Role> GetRole(string email)
    {
      // get role from database
      var (_, role) = await this.Scalar("SELECT TOP 1 [role_id] FROM [user] WHERE [email] = @email",
        new Dictionary<string, object>() { { "email", email } });
      return (role == null) ? Role.None : (Role)role;
    }
  }
}

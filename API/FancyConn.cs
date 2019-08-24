using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Threading.Tasks;
using VP_Functions.Models;

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

    /// <summary>
    /// Execute a SQL query that returns rows as a <see cref="SqlDataReader"/>
    /// </summary>
    /// <param name="query">SQL query to execute</param>
    /// <param name="queryParams">SQL parameters in a key-value format</param>
    public async Task<SqlDataReader> Reader(string query, Dictionary<string, object> queryParams = null)
    {
      SqlCommand cmd = null;
      SqlDataReader reader = null;
      try
      {
        cmd = this.MakeCommand(query);
        if (queryParams != null)
          foreach (var kv in queryParams)
          {
            var value = kv.Value ?? DBNull.Value;
            cmd.Parameters.AddWithValue(kv.Key, value);
          }
        reader = await cmd.ExecuteReaderAsync();
      }
      catch (SqlException e)
      {
        this.lastError = e;
        // todo: propogate error
      }
      finally
      {
        if (cmd != null) cmd.Dispose();
      }
      return reader;
    }

    /// <summary>
    /// Execute a SQL query that returns only the first value of the first row
    /// </summary>
    /// <param name="query">SQL query to execute</param>
    /// <param name="queryParams">SQL parameters in key-value format</param>
    public async Task<object> Scalar(string query, Dictionary<string, object> queryParams)
    {
      SqlCommand cmd = null;
      object val = null;
      try
      {
        cmd = this.MakeCommand(query);
        foreach (var kv in queryParams)
        {
          var value = kv.Value ?? DBNull.Value;
          cmd.Parameters.AddWithValue(kv.Key, value);
        }
        val = await cmd.ExecuteScalarAsync();
      }
      catch (SqlException e)
      {
        this.lastError = e;
        // todo: propogate error
      }
      finally
      {
        if (cmd != null) cmd.Dispose();
      }
      return val;
    }

    /// <summary>
    /// Execute a SQL query that does not return any values
    /// </summary>
    /// <param name="query">SQL query to execute</param>
    /// <param name="queryParams">SQL parameters in key-value format</param>
    /// <returns>Number of rows affected</returns>
    public async Task<int> NonQuery(string query, Dictionary<string, object> queryParams = null)
    {
      SqlCommand cmd = null;
      int rows = -1;
      try
      {
        cmd = this.MakeCommand(query);
        foreach (var kv in queryParams)
        {
          var value = kv.Value ?? DBNull.Value;
          cmd.Parameters.AddWithValue(kv.Key, value);
        }
        rows = await cmd.ExecuteNonQueryAsync();
      }
      catch (SqlException e)
      {
        this.lastError = e;
      }
      finally
      {
        if (cmd != null) cmd.Dispose();
      }
      return rows;
    }

    /// <summary>
    /// Get the role level of a given user
    /// </summary>
    /// <param name="email">Email of user to lookup</param>
    /// <returns><see cref="Role"/> if user exists, <see langword="null"/> otherwise</returns>
    public async Task<Role?> GetRole(string email)
    {
      // get role from database
      var role = await this.Scalar("SELECT TOP 1 [role_id] FROM [user] WHERE [email] = @email",
        new Dictionary<string, object>() { { "email", email } });
      return (Role)role;
    }
  }
}

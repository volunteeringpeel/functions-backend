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
    public bool Disposed = false;
    readonly SqlConnection conn;
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

    SqlCommand MakeCommand(string query)
    {
      if (this.conn.State == ConnectionState.Closed)
        conn.Open();
      return new SqlCommand(query, this.conn);
    }

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

    public async Task<int> NonQuery(string query)
    {
      SqlCommand cmd = null;
      int rows = -1;
      try
      {
        cmd = this.MakeCommand(query);
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

    public async Task<Role?> GetRole(string email)
    {
      // get role from database
      var role = await this.Scalar("SELECT TOP 1[role_id] FROM[user] WHERE[email] = @email",
        new Dictionary<string, object>() { { "email", email } });
      return (Role)role;
    }
  }
}

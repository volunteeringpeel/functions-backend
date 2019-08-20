using System;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace VP_Functions.API
{
  public class FancyConn : IDisposable
  {
    public bool Disposed = false;
    SqlConnection conn;
    public Exception lastError;

    public FancyConn()
    {
      string connStr = Environment.GetEnvironmentVariable("sqldb_connection");
      this.conn = new SqlConnection(connStr);
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
        this.conn.Dispose();
      }
      Disposed = true;
    }

    public async Task<SqlDataReader> Reader(string query, Dictionary<string, object> queryParams)
    {
      SqlCommand cmd = null;
      SqlDataReader reader = null;
      try
      {
        cmd = new SqlCommand(query, this.conn);
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

    public async Task<int> NonQuery(string query)
    {
      SqlCommand cmd = null;
      int rows = -1;
      try
      {
        cmd = new SqlCommand(query, this.conn);
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
      var reader = await this.Reader("SELECT TOP 1 [role_id] FROM [user] WHERE [email] = @email",
        new Dictionary<string, object>() { { "email", email } });
      if (reader == null || !reader.HasRows) return null;
      reader.Read();
      var role = (Role)reader[0];
      reader.Close();
      return role;
    }
  }
}

using System;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace VP_Functions.Models
{
  public class FancyConn
  {
    private SqlConnection conn;
    public Exception lastError;

    public FancyConn()
    {
      string connStr = Environment.GetEnvironmentVariable("sqldb_connection");
      this.conn = new SqlConnection(connStr);
      this.conn.Open();
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
      } catch (SqlException e)
      {
        this.lastError = e;
      } finally
      {
        if (cmd != null) cmd.Dispose();
      }
      return rows;
    }
  }
}

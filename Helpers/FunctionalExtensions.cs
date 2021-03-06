﻿using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace VP_Functions
{
  public static class FunctionalExtensions
  {
    [DebuggerStepThrough]
    public static TOut Map<TIn, TOut>(this TIn @this, Func<TIn, TOut> func)
        where TIn : class
    {
      return @this == null ? default(TOut) : func(@this);
    }

    [DebuggerStepThrough]
    public static T Then<T>(this T @this, Action<T> then)
        where T : class
    {
      if (@this != null) then(@this);
      return @this;
    }

    [DebuggerStepThrough]
    public static ICollection<T> AsCollection<T>(this T @this) where T : class
        => @this == null ? new T[0] : new[] { @this };

    [DebuggerStepThrough]
    public static void Each<T>(this IEnumerable<T> @this, Action<T> action)
    {
      if (@this == null) return;
      foreach (var element in @this)
      {
        action(element);
      }
    }

    public async static Task<JObject> GetBodyParameters(this HttpRequest req)
    {
      if (req.ContentType == "application/json")
      {
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        return JObject.Parse(requestBody);
      }
      else
      {
        var formData = await req.ReadFormAsync();
        var data = new JObject();
        foreach (var kv in formData)
        {
          if (StringValues.IsNullOrEmpty(kv.Value))
            data[kv.Key] = JValue.CreateNull();
          else if (kv.Value.Count == 1)
            data.Add(kv.Key, JToken.Parse(kv.Value[0]));
          else data[kv.Key] = new JArray(from v in kv.Value select JToken.Parse(v));
        }
        return data;
      }
    }

    /// <summary>
    /// Create a <see cref="JArray"/> from a <see cref="SqlDataReader"/>
    /// </summary>
    /// <param name="reader">Reader to convert</param>
    /// <returns>Array with column names as properties and data as values</returns>
    public static JArray ToJArray(this SqlDataReader reader)
    {
      var data = new JArray();
      var schema = reader.GetColumnSchema();
      while (reader.Read())
        data.Add(new JObject(from c in schema
                                select new JProperty(c.ColumnName, reader[(int)c.ColumnOrdinal])));
      return data;
    }

    public static string WithTimestamp(this string fn) =>
      Path.GetFileNameWithoutExtension(fn) + "-" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + Path.GetExtension(fn);
  }
}
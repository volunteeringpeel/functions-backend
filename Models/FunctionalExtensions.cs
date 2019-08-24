using System;
using System.Collections.Generic;
using System.Diagnostics;

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
  }
}
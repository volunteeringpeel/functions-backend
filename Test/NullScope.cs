using System;

namespace VP_Functions.Test
{
  public class NullScope : IDisposable
  {
    public static NullScope Instance { get; } = new NullScope();

    private NullScope() { }

    public void Dispose() { }
  }
}
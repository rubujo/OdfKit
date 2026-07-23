using System;

namespace OdfKit.Formula;

internal sealed class OdfFormulaVolatileSession : IOdfFormulaVolatileContext
{
    private readonly Random _random = new();

    internal OdfFormulaVolatileSession()
    {
        EvaluationTimestamp = DateTime.Now;
    }

    public DateTime EvaluationTimestamp { get; }

    public double NextRandomDouble()
    {
        lock (_random)
            return _random.NextDouble();
    }
}

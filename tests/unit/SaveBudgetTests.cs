using System;

static class SaveBudgetTests
{
    public static void Run()
    {
        SaveBudgetMetrics.AssertLimits(10, 50000000);
        Check.Throws<InvalidOperationException>(() => SaveBudgetMetrics.AssertLimits(10.001, 1), "time over budget is rejected independently");
        Check.Throws<InvalidOperationException>(() => SaveBudgetMetrics.AssertLimits(0, 50000001), "one byte over disk budget is rejected");
        Check.Throws<InvalidOperationException>(() => SaveBudgetMetrics.AssertLimits(Double.NaN, 0), "invalid timing cannot pass the gate");
    }
}

namespace ProjectScope.Core;

public static class PercentageAllocator
{
    public static double[] Allocate(IReadOnlyList<long> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Any(value => value < 0)) throw new ArgumentException("Percentage values must be nonnegative.", nameof(values));
        var total = values.Sum();
        if (total == 0) return new double[values.Count];
        var raw = values.Select(value => value / (decimal)total * 1000).ToArray();
        var units = raw.Select(value => (int)decimal.Floor(value)).ToArray();
        var remaining = 1000 - units.Sum();
        foreach (var index in Enumerable.Range(0, values.Count).OrderByDescending(index => raw[index] - units[index]).ThenBy(index => index).Take(remaining)) units[index]++;
        return units.Select(value => value / 10.0).ToArray();
    }
}

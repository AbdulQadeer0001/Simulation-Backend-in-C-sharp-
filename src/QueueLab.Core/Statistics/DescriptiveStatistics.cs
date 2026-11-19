using QueueLab.Core.Common;
using QueueLab.Core.Models;

namespace QueueLab.Core.Statistics;

/// <summary>Summary of the observed NADRA customers (dataset overview).</summary>
public static class DescriptiveStatistics
{
    private static Dictionary<string, object?> Block(IReadOnlyList<double> values, bool includeAverage)
    {
        Dictionary<string, object?> d = new Dictionary<string, object?>();
        if (includeAverage)
        {
            d["average"] = values.Count == 0 ? (double?)null : NumpyMath.Mean(values);
        }

        d["minimum"] = values.Count == 0 ? (double?)null : NumpyMath.Min(values);
        d["maximum"] = values.Count == 0 ? (double?)null : NumpyMath.Max(values);
        return d;
    }

    private static List<double> Collect(IReadOnlyList<Customer> customers, Func<Customer, double?> selector)
    {
        List<double> values = new List<double>();
        foreach (Customer customer in customers)
        {
            double? value = selector(customer);
            if (value.HasValue)
            {
                values.Add(value.Value);
            }
        }

        return values;
    }

    public static Dictionary<string, object?> Summary(IReadOnlyList<Customer> customers)
    {
        List<double> outsideWait = Collect(customers, c => c.OutsideWaitMin);
        List<double> mainWait = Collect(customers, c => c.MainWaitMin);
        List<double> totalTime = Collect(customers, c => c.TotalTimeMin);
        List<double> serviceTimes = Collect(customers, c => c.ServiceTimeMin);
        List<double> arrivalTimes = Collect(customers, c => c.ArrivalMin);

        int completed = customers.Count(c => !c.Reneged && c.ServiceEndMin.HasValue);
        int reneged = customers.Count(c => c.Reneged);

        Dictionary<string, object?> genders = new Dictionary<string, object?>();
        Dictionary<string, int> genderCounts = new Dictionary<string, int>();
        List<string> genderOrder = new List<string>();
        foreach (Customer customer in customers)
        {
            // Python: customer.gender or "Unknown" (an empty string also counts as unknown).
            string gender = string.IsNullOrEmpty(customer.Gender) ? "Unknown" : customer.Gender;
            if (!genderCounts.ContainsKey(gender))
            {
                genderCounts[gender] = 0;
                genderOrder.Add(gender);
            }

            genderCounts[gender]++;
        }

        foreach (string gender in genderOrder)
        {
            genders[gender] = genderCounts[gender];
        }

        int total = customers.Count;

        Dictionary<string, object?> d = new Dictionary<string, object?>();
        d["total_customers"] = total;
        d["completed_customers"] = completed;
        d["reneged_customers"] = reneged;
        d["completion_rate"] = total > 0 ? (double)completed / total * 100 : 0.0;
        d["reneging_rate"] = total > 0 ? (double)reneged / total * 100 : 0.0;
        d["arrival"] = Block(arrivalTimes, includeAverage: false);
        d["outside_wait"] = Block(outsideWait, includeAverage: true);
        d["main_wait"] = Block(mainWait, includeAverage: true);
        d["service_time"] = Block(serviceTimes, includeAverage: true);
        d["total_time"] = Block(totalTime, includeAverage: true);
        d["gender_distribution"] = genders;
        return d;
    }
}

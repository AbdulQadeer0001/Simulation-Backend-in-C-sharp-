using QueueLab.Core.Common;
using QueueLab.Core.Models;

namespace QueueLab.Core.Statistics;

/// <summary>Time-weighted queue statistics derived from customer arrival / service-start / departure events.</summary>
public sealed class TimeBasedStatistics
{
    public double ObservationStart { get; init; }

    public double ObservationEnd { get; init; }

    public double ObservationTime { get; init; }

    public double AverageNumberInQueue { get; init; }

    public double AverageNumberInSystem { get; init; }

    public double Utilization { get; init; }

    public Dictionary<string, double> ServerUtilization { get; init; } = new Dictionary<string, double>();

    /// <summary>
    /// Calculates the statistics. The observation period runs from the first arrival to the last departure.
    /// </summary>
    public static TimeBasedStatistics Calculate(IReadOnlyList<CustomerSimulationResult> customerResults, int serverCount)
    {
        if (serverCount < 1)
        {
            throw new ValueError("Server count must be at least 1.");
        }

        if (customerResults.Count == 0)
        {
            throw new ValueError("Customer results cannot be empty.");
        }

        double firstArrival = customerResults[0].ArrivalTime;
        double lastDeparture = customerResults[0].ServiceEnd;
        foreach (CustomerSimulationResult customer in customerResults)
        {
            if (customer.ArrivalTime < firstArrival)
            {
                firstArrival = customer.ArrivalTime;
            }

            if (customer.ServiceEnd > lastDeparture)
            {
                lastDeparture = customer.ServiceEnd;
            }
        }

        double observationTime = lastDeparture - firstArrival;
        if (observationTime <= 0)
        {
            throw new ValueError("Observation time must be greater than zero.");
        }

        List<(double Time, int Delta)> queueEvents = new List<(double Time, int Delta)>(customerResults.Count * 2);
        List<(double Time, int Delta)> systemEvents = new List<(double Time, int Delta)>(customerResults.Count * 2);
        double[] busyTimeByServer = new double[serverCount + 1]; // index = server id (1-based)

        foreach (CustomerSimulationResult customer in customerResults)
        {
            double arrival = customer.ArrivalTime;
            double serviceStart = customer.ServiceStart;
            double serviceEnd = customer.ServiceEnd;

            if (!(arrival <= serviceStart && serviceStart <= serviceEnd))
            {
                throw new ValueError("Invalid event times for customer " + customer.CustomerId + ".");
            }

            // Queue: +1 when arriving, -1 when service begins.
            queueEvents.Add((arrival, 1));
            queueEvents.Add((serviceStart, -1));

            // System: +1 on arrival, -1 on departure.
            systemEvents.Add((arrival, 1));
            systemEvents.Add((serviceEnd, -1));

            busyTimeByServer[customer.ServerId] += serviceEnd - serviceStart;
        }

        double averageQueueLength = TimeWeightedAverage(queueEvents, firstArrival, lastDeparture, observationTime);
        double averageSystemPopulation = TimeWeightedAverage(systemEvents, firstArrival, lastDeparture, observationTime);

        Dictionary<string, double> serverUtilization = new Dictionary<string, double>();
        for (int serverId = 1; serverId <= serverCount; serverId++)
        {
            serverUtilization[serverId.ToString(System.Globalization.CultureInfo.InvariantCulture)] =
                PyMath.Round(busyTimeByServer[serverId] / observationTime, 6);
        }

        double[] busyValues = new double[serverCount];
        for (int serverId = 1; serverId <= serverCount; serverId++)
        {
            busyValues[serverId - 1] = busyTimeByServer[serverId];
        }

        double totalBusyTime = PyMath.Sum(busyValues);
        double overallUtilization = totalBusyTime / (serverCount * observationTime);

        return new TimeBasedStatistics
        {
            ObservationStart = PyMath.Round(firstArrival, 3),
            ObservationEnd = PyMath.Round(lastDeparture, 3),
            ObservationTime = PyMath.Round(observationTime, 6),
            AverageNumberInQueue = PyMath.Round(averageQueueLength, 6),
            AverageNumberInSystem = PyMath.Round(averageSystemPopulation, 6),
            Utilization = PyMath.Round(overallUtilization, 6),
            ServerUtilization = serverUtilization,
        };
    }

    private static double TimeWeightedAverage(
        List<(double Time, int Delta)> events,
        double firstArrival,
        double lastDeparture,
        double observationTime)
    {
        events.Sort((x, y) => x.Time.CompareTo(y.Time));

        int population = 0;
        double previousTime = firstArrival;
        double area = 0.0;
        int index = 0;

        while (index < events.Count)
        {
            double eventTime = events[index].Time;

            // Accumulate the population over the interval before applying the events at this timestamp.
            area += population * (eventTime - previousTime);

            // Apply all changes at the same timestamp together, avoiding temporary negative counts.
            int netChange = 0;
            while (index < events.Count && events[index].Time == eventTime)
            {
                netChange += events[index].Delta;
                index++;
            }

            population += netChange;

            if (population < 0)
            {
                throw new ValueError("Invalid event sequence: population became negative.");
            }

            previousTime = eventTime;
        }

        area += population * (lastDeparture - previousTime);

        if (population != 0)
        {
            throw new ValueError("Invalid event sequence: population did not return to zero.");
        }

        return area / observationTime;
    }
}

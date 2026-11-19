using System.Numerics;
using QueueLab.Core.Common;
using QueueLab.Core.Models;

namespace QueueLab.Core.Simulation;

/// <summary>Generates synthetic customers from exponential or deterministic arrival / service processes.</summary>
public static class DistributionGenerator
{
    /// <summary>Safety limit that did not exist in the Python version: stops absurd requests from exhausting memory.</summary>
    public const int MaxCustomers = 1_000_000;

    public const string Exponential = "exponential";

    public const string Deterministic = "deterministic";

    /// <summary>A generated customer (times rounded to 6 decimals).</summary>
    public sealed class GeneratedCustomer
    {
        public GeneratedCustomer(string customerId, double arrivalTime, double serviceTime)
        {
            CustomerId = customerId;
            ArrivalTime = arrivalTime;
            ServiceTime = serviceTime;
        }

        public string CustomerId { get; }

        public double ArrivalTime { get; }

        public double ServiceTime { get; }

        public SimulationCustomer ToSimulationCustomer()
        {
            return new SimulationCustomer(CustomerId, ArrivalTime, ServiceTime);
        }
    }

    private static void ValidateRate(object? rate, string name)
    {
        if (!PyTypes.IsNumber(rate))
        {
            throw new ValueError(name + " must be a finite positive number.");
        }

        double value = PyTypes.ToDouble(rate!);
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ValueError(name + " must be a finite positive number.");
        }
    }

    /// <summary>
    /// Generates customers. Arguments are loosely typed (as parsed from JSON) and validated like the original:
    /// integers must be real integers (not bools or decimals), rates must be finite positive numbers.
    /// </summary>
    public static List<GeneratedCustomer> GenerateCustomers(
        object? customerCount,
        object? arrivalRate,
        object? serviceRate,
        object? arrivalDistribution,
        object? serviceDistribution,
        object? seed)
    {
        if (!PyTypes.IsInt(customerCount) || PyTypes.ToBigInteger(customerCount!) < 1)
        {
            throw new ValueError("Customer count must be a positive integer.");
        }

        if (PyTypes.ToBigInteger(customerCount!) > MaxCustomers)
        {
            throw new ValueError("Customer count must not exceed " + MaxCustomers.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + ".");
        }

        ValidateRate(arrivalRate, "Arrival rate");
        ValidateRate(serviceRate, "Service rate");

        string? arrivalDist = arrivalDistribution as string;
        if (arrivalDist != Exponential && arrivalDist != Deterministic)
        {
            throw new ValueError("Unsupported arrival distribution.");
        }

        string? serviceDist = serviceDistribution as string;
        if (serviceDist != Exponential && serviceDist != Deterministic)
        {
            throw new ValueError("Unsupported service distribution.");
        }

        if (seed != null && !PyTypes.IsInt(seed))
        {
            throw new ValueError("Seed must be an integer or None.");
        }

        BigInteger? seedValue = seed == null ? (BigInteger?)null : PyTypes.ToBigInteger(seed);
        int count = (int)PyTypes.ToBigInteger(customerCount!);

        return Generate(
            count,
            PyTypes.ToDouble(arrivalRate!),
            PyTypes.ToDouble(serviceRate!),
            arrivalDist == Exponential,
            serviceDist == Exponential,
            seedValue);
    }

    /// <summary>Typed overload for callers that have already validated their input (e.g. the desktop app).</summary>
    public static List<GeneratedCustomer> Generate(
        int customerCount,
        double arrivalRate,
        double serviceRate,
        bool exponentialArrivals,
        bool exponentialService,
        BigInteger? seed)
    {
        PythonRandom rng = PythonRandom.FromSeed(seed);
        List<GeneratedCustomer> customers = new List<GeneratedCustomer>(customerCount);
        double arrivalTime = 0.0;

        for (int index = 0; index < customerCount; index++)
        {
            if (index > 0)
            {
                double interval = exponentialArrivals ? rng.ExpoVariate(arrivalRate) : 1.0 / arrivalRate;
                arrivalTime += interval;
            }

            double serviceTime = exponentialService ? rng.ExpoVariate(serviceRate) : 1.0 / serviceRate;

            customers.Add(new GeneratedCustomer(
                "C" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                PyMath.Round(arrivalTime, 6),
                PyMath.Round(serviceTime, 6)));
        }

        return customers;
    }
}

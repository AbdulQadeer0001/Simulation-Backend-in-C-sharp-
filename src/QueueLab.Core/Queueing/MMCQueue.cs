using QueueLab.Core.Common;

namespace QueueLab.Core.Queueing;

/// <summary>Analytical M/M/c queue: Poisson arrivals, c identical exponential servers, unlimited FCFS queue (Erlang C).</summary>
public sealed class MMCQueue
{
    private readonly double _arrivalRate;
    private readonly double _serviceRate;
    private readonly int _serverCount;

    public MMCQueue(double arrivalRate, double serviceRate, int serverCount)
    {
        _arrivalRate = arrivalRate;
        _serviceRate = serviceRate;
        _serverCount = serverCount;

        if (!double.IsFinite(_arrivalRate))
        {
            throw new ValueError("Arrival rate must be finite.");
        }

        if (!double.IsFinite(_serviceRate))
        {
            throw new ValueError("Service rate must be finite.");
        }

        if (_arrivalRate <= 0)
        {
            throw new ValueError("Arrival rate must be greater than zero.");
        }

        if (_serviceRate <= 0)
        {
            throw new ValueError("Service rate must be greater than zero.");
        }

        if (_serverCount < 1)
        {
            throw new ValueError("Server count must be at least 1.");
        }

        if (_arrivalRate >= _serverCount * _serviceRate)
        {
            throw new ValueError("M/M/c is unstable: arrival rate must be less than the total service capacity.");
        }
    }

    public QueueModelResult Calculate()
    {
        double lam = _arrivalRate;
        double mu = _serviceRate;
        int c = _serverCount;

        // Python raised OverflowError here (reported to the client as a 400); keep that behaviour.
        if (c > 170)
        {
            throw new ValueError("int too large to convert to float");
        }

        double a = lam / mu;
        double rho = lam / (c * mu);

        // First c terms of the steady-state probability series.
        double[] terms = new double[c];
        double factorial = 1.0;
        for (int n = 0; n < c; n++)
        {
            if (n > 0)
            {
                factorial *= n;
            }

            double power = Math.Pow(a, n);
            CheckOverflow(power);
            terms[n] = power / factorial;
        }

        double sumTerms = PyMath.Sum(terms);

        // Tail of the series for states n >= c.
        double factorialC = factorial * c;
        double powerC = Math.Pow(a, c);
        CheckOverflow(powerC);
        double tail = powerC / (factorialC * (1 - rho));

        double probabilityEmpty = 1 / (sumTerms + tail);

        // Erlang C: probability that an arriving customer has to wait.
        double probabilityWait = tail * probabilityEmpty;

        double averageNumberInQueue = probabilityWait * rho / (1 - rho);
        double averageNumberInSystem = averageNumberInQueue + a;
        double averageWaitingTime = averageNumberInQueue / lam;
        double averageSystemTime = averageWaitingTime + 1 / mu;

        QueueModelResult result = new QueueModelResult { Model = "M/M/c" };

        result.Assumptions["arrival_distribution"] = "Poisson";
        result.Assumptions["service_distribution"] = "Exponential";
        result.Assumptions["server_count"] = c;
        result.Assumptions["queue_capacity"] = "Unlimited";
        result.Assumptions["queue_discipline"] = "FCFS";
        result.Assumptions["steady_state"] = true;

        result.Input["arrival_rate"] = lam;
        result.Input["service_rate_per_server"] = mu;
        result.Input["server_count"] = c;
        result.Input["utilization"] = PyMath.Round(rho, 6);

        result.Metrics["probability_empty"] = PyMath.Round(probabilityEmpty, 6);
        result.Metrics["probability_wait"] = PyMath.Round(probabilityWait, 6);
        result.Metrics["average_number_in_queue"] = PyMath.Round(averageNumberInQueue, 6);
        result.Metrics["average_number_in_system"] = PyMath.Round(averageNumberInSystem, 6);
        result.Metrics["average_waiting_time"] = PyMath.Round(averageWaitingTime, 6);
        result.Metrics["average_system_time"] = PyMath.Round(averageSystemTime, 6);
        result.Metrics["throughput"] = lam;

        return result;
    }

    private static void CheckOverflow(double value)
    {
        if (double.IsInfinity(value))
        {
            throw new ValueError("(34, 'Numerical result out of range')");
        }
    }
}

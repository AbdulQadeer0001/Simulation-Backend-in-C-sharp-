using QueueLab.Core.Common;

namespace QueueLab.Core.Queueing;

/// <summary>Analytical M/M/1 queue: Poisson arrivals, one exponential server, unlimited FCFS queue.</summary>
public sealed class MM1Queue
{
    private readonly double _arrivalRate;
    private readonly double _serviceRate;

    public MM1Queue(double arrivalRate, double serviceRate)
    {
        _arrivalRate = arrivalRate;
        _serviceRate = serviceRate;

        if (!double.IsFinite(_arrivalRate))
        {
            throw new ValueError("Arrival rate must be a finite number.");
        }

        if (!double.IsFinite(_serviceRate))
        {
            throw new ValueError("Service rate must be a finite number.");
        }

        if (_arrivalRate <= 0)
        {
            throw new ValueError("Arrival rate must be greater than zero.");
        }

        if (_serviceRate <= 0)
        {
            throw new ValueError("Service rate must be greater than zero.");
        }

        if (_arrivalRate >= _serviceRate)
        {
            throw new ValueError("M/M/1 is unstable: arrival rate must be less than service rate.");
        }
    }

    public QueueModelResult Calculate()
    {
        double lam = _arrivalRate;
        double mu = _serviceRate;

        double rho = lam / mu;
        double probabilityEmpty = 1 - rho;
        double averageNumberInSystem = lam / (mu - lam);
        double averageNumberInQueue = Math.Pow(lam, 2) / (mu * (mu - lam));
        double averageTimeInSystem = 1 / (mu - lam);
        double averageWaitingTime = lam / (mu * (mu - lam));

        QueueModelResult result = new QueueModelResult { Model = "M/M/1" };

        result.Assumptions["arrival_distribution"] = "Poisson";
        result.Assumptions["service_distribution"] = "Exponential";
        result.Assumptions["server_count"] = 1;
        result.Assumptions["queue_capacity"] = "Unlimited";
        result.Assumptions["queue_discipline"] = "FCFS";
        result.Assumptions["steady_state"] = true;

        result.Input["arrival_rate"] = lam;
        result.Input["service_rate"] = mu;
        result.Input["utilization"] = PyMath.Round(rho, 6);

        result.Metrics["probability_empty"] = PyMath.Round(probabilityEmpty, 6);
        result.Metrics["average_number_in_queue"] = PyMath.Round(averageNumberInQueue, 6);
        result.Metrics["average_number_in_system"] = PyMath.Round(averageNumberInSystem, 6);
        result.Metrics["average_waiting_time"] = PyMath.Round(averageWaitingTime, 6);
        result.Metrics["average_system_time"] = PyMath.Round(averageTimeInSystem, 6);
        result.Metrics["throughput"] = lam;

        return result;
    }
}

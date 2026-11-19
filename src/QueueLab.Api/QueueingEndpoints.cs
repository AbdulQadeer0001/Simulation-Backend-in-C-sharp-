using QueueLab.Core.Common;
using QueueLab.Core.Queueing;

namespace QueueLab.Api;

/// <summary>POST /api/queueing/mm1 and /api/queueing/mmc (analytical models).</summary>
internal static class QueueingEndpoints
{
    public static async Task<IResult> Mm1(HttpRequest request)
    {
        Dictionary<string, object?>? data = await RequestParsing.ReadJsonBody(request) as Dictionary<string, object?>;
        if (data == null)
        {
            return Errors.StatusMessage(400, "Provide a JSON request body.");
        }

        try
        {
            object? arrivalRate = PyTypes.Get(data, "arrival_rate");
            object? serviceRate = PyTypes.Get(data, "service_rate");

            if (arrivalRate == null || serviceRate == null)
            {
                return Errors.StatusMessage(400, "arrival_rate and service_rate are required.");
            }

            MM1Queue model = new MM1Queue(PyTypes.ToFloat(arrivalRate), PyTypes.ToFloat(serviceRate));

            Dictionary<string, object?> body = new Dictionary<string, object?>();
            body["status"] = "success";
            body["result"] = model.Calculate().ToDict();
            return new JsonBodyResult(body);
        }
        catch (ValueError error)
        {
            return Errors.StatusMessage(400, error.Message);
        }
    }

    public static async Task<IResult> Mmc(HttpRequest request)
    {
        Dictionary<string, object?>? data = await RequestParsing.ReadJsonBody(request) as Dictionary<string, object?>;
        if (data == null)
        {
            return Errors.StatusMessage(400, "Provide a JSON request body.");
        }

        try
        {
            object? arrivalRate = PyTypes.Get(data, "arrival_rate");
            object? serviceRate = PyTypes.Get(data, "service_rate");
            object? serverCount = PyTypes.Get(data, "server_count");

            if (arrivalRate == null || serviceRate == null || serverCount == null)
            {
                return Errors.StatusMessage(400, "arrival_rate, service_rate, and server_count are required.");
            }

            // Reject fractional server counts instead of silently truncating.
            if (serverCount is bool)
            {
                throw new ValueError("Server count must be a positive integer.");
            }

            double numericServerCount = PyTypes.ToFloat(serverCount);
            if (!double.IsFinite(numericServerCount) || numericServerCount != Math.Floor(numericServerCount))
            {
                throw new ValueError("Server count must be a positive integer.");
            }

            // Anything above this is rejected by the model anyway (the series overflows at 171 servers).
            int servers = numericServerCount > 100000 ? 100000 : (numericServerCount < -100000 ? -100000 : (int)numericServerCount);

            MMCQueue model = new MMCQueue(PyTypes.ToFloat(arrivalRate), PyTypes.ToFloat(serviceRate), servers);

            Dictionary<string, object?> body = new Dictionary<string, object?>();
            body["status"] = "success";
            body["result"] = model.Calculate().ToDict();
            return new JsonBodyResult(body);
        }
        catch (ValueError error)
        {
            return Errors.StatusMessage(400, error.Message);
        }
    }
}

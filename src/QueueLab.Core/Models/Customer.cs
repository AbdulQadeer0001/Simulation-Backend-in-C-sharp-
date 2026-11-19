using QueueLab.Core.Common;

namespace QueueLab.Core.Models;

/// <summary>
/// One observed customer from the NADRA dataset. (The date/time text columns of the spreadsheet are not
/// exposed by any API endpoint, so they are not loaded.)
/// </summary>
public sealed class Customer : ISimCustomer
{
    public double? Day { get; init; }

    public double? SequenceNumber { get; init; }

    public string CustomerId { get; init; } = string.Empty;

    public string? Gender { get; init; }

    public double? ArrivalMin { get; init; }

    public double? AdmittedMin { get; init; }

    public double? TokenStartMin { get; init; }

    public double? TokenEndMin { get; init; }

    public double? MainWaitStartMin { get; init; }

    public double? ServiceStartMin { get; init; }

    public double? ServiceEndMin { get; init; }

    public bool Reneged { get; init; }

    public double? OutsideWaitMin { get; init; }

    public double? MainWaitMin { get; init; }

    public double? TotalTimeMin { get; init; }

    /// <summary>Observed service duration (service end - service start), rounded to 3 decimals.</summary>
    public double? ServiceTimeMin
    {
        get
        {
            if (ServiceStartMin.HasValue && ServiceEndMin.HasValue)
            {
                return PyMath.Round(ServiceEndMin.Value - ServiceStartMin.Value, 3);
            }

            return null;
        }
    }
}
